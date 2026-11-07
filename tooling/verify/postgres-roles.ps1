# Runs the role and sweep SQL from deploy.yml on a local postgres:16, with forced interleaving. Exits 1
# if a check fails. Needs Docker and nothing else; the SQL is read from the workflow, so it cannot drift.
#
#   pwsh tooling/verify/postgres-roles.ps1
#
# An approximation of the shared server: as on Azure, the admin is not a superuser and the public schema
# belongs to another role. It proves the SQL's logic, not Azure's behaviour.
$ErrorActionPreference = 'Stop'

$workflow = Join-Path $PSScriptRoot '..\..\base-project\.github\workflows\deploy.yml'
$text = [IO.File]::ReadAllText($workflow).Replace("`r`n", "`n")
$from = $text.IndexOf("- name: create the preview's database login")
if ($from -lt 0) { throw 'deploy.yml has no step that creates the database login' }
$step = $text.Substring($from, $text.IndexOf('# Migrations are Development-only', $from) - $from)
$blocks = [regex]::Matches($step, "(?s)<<'SQL'\n(.*?)\n\s*SQL\n") | ForEach-Object { $_.Groups[1].Value }

function Block([string] $marker) {
    $found = @($blocks | Where-Object { $_ -match $marker })
    if ($found.Count -ne 1) { throw "expected one SQL block in deploy.yml containing '$marker', found $($found.Count)" }
    $found[0]
}
$CreateRole = Block 'CREATE ROLE'
$GrantSchema = Block 'GRANT ALL ON SCHEMA'
$Sweep = Block 'DROP ROLE'

$container = "verify-pg-$([guid]::NewGuid().ToString('N').Substring(0, 8))"
$admin = 'admin-pw'
$script:failed = 0

function Psql-Args([string] $user = 'pgadmin', [string] $pw = $admin, [string] $db = 'postgres', [hashtable] $vars = @{}) {
    $a = @('exec', '-i', '-e', "PGPASSWORD=$pw", $container, 'psql', '-h', '127.0.0.1', '-U', $user, '-d', $db, '-X', '-q', '-t', '-A', '-v', 'ON_ERROR_STOP=1')
    foreach ($k in $vars.Keys) { $a += @('-v', "$k=$($vars[$k])") }
    $a
}

# Runs the SQL in the background, as the admin, for the other side of a forced interleaving.
function Start-Psql([string] $sql, [hashtable] $vars = @{}) {
    Start-ThreadJob -ScriptBlock {
        param($s, $a)
        $out = $s | docker @a 2>&1
        [pscustomobject]@{ Ok = $LASTEXITCODE -eq 0; Out = (($out | ForEach-Object { "$_" }) -join ' ').Trim() }
    } -ArgumentList @($sql, (Psql-Args -vars $vars))
}

function Stop-Psql($job) { $r = Receive-Job $job -Wait; Remove-Job $job; $r }

# The SQL with a pause inserted before $before, so another session can land in the gap.
function With-Pause([string] $sql, [string] $before) {
    if (-not $sql.Contains($before)) { throw "no '$before' to pause before" }
    $sql.Replace($before, "SELECT pg_sleep(4);`n$before")
}

function Psql([string] $sql, [string] $user = 'pgadmin', [string] $pw = $admin, [string] $db = 'postgres', [hashtable] $vars = @{}) {
    $a = Psql-Args $user $pw $db $vars
    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try { $out = $sql | docker @a 2>&1 } finally { $ErrorActionPreference = $previous }
    [pscustomobject]@{ Ok = $LASTEXITCODE -eq 0; Out = (($out | ForEach-Object { "$_" }) -join ' ').Trim() }
}

function Check([string] $name, [bool] $pass, [string] $detail = '') {
    if (-not $pass) { $script:failed++ }
    Write-Host ("{0}  {1}{2}" -f $(if ($pass) { 'pass' } else { 'FAIL' }), $name, $(if ($detail) { " -- $detail" }))
}

function New-Database([string] $db) { $r = Psql "CREATE DATABASE $db;"; if (-not $r.Ok) { throw $r.Out } }

# The workflow's step for one preview, without the sweep.
function Deploy-Role([string] $role, [string] $pw, [string[]] $dbs) {
    $r = Psql $CreateRole -vars @{ role = $role; pw = $pw; dbs = $dbs -join ' ' }
    if (-not $r.Ok) { return $r }
    foreach ($db in $dbs) {
        $r = Psql $GrantSchema -db $db -vars @{ role = $role }
        if (-not $r.Ok) { return $r }
    }
    $r
}

function Role-Exists([string] $role) { (Psql "SELECT count(*) FROM pg_roles WHERE rolname = '$role';").Out -eq '1' }

# The image trusts 127.0.0.1 by default, which would make every password check pass.
docker run -d --name $container -e POSTGRES_PASSWORD=$admin -e POSTGRES_INITDB_ARGS=--auth-host=scram-sha-256 postgres:16 | Out-Null
try {
    $ready = $false
    foreach ($i in 1..60) {
        $r = Psql 'SELECT 1;' -user postgres
        if ($r.Ok) { $ready = $true; break }
        Start-Sleep -Seconds 1
    }
    if (-not $ready) { throw "postgres never became ready: $($r.Out)" }
    $setup = @"
CREATE ROLE azure_pg_admin;
CREATE ROLE pgadmin LOGIN CREATEDB CREATEROLE PASSWORD '$admin';
GRANT azure_pg_admin TO pgadmin;
"@
    $r = Psql $setup -user postgres; if (-not $r.Ok) { throw $r.Out }
    $r = Psql 'ALTER SCHEMA public OWNER TO azure_pg_admin;' -user postgres -db template1; if (-not $r.Ok) { throw $r.Out }

    Write-Host "`n# Data access between previews"
    New-Database 'a_appdb'; New-Database 'b_appdb'
    $r = Deploy-Role 'preview_a' 'a-pw' @('a_appdb')
    Check 'A''s login is set up' $r.Ok $r.Out
    $r = Psql 'SELECT 1;' -user preview_a -pw 'a-pw' -db 'b_appdb'
    Check 'control: before B''s REVOKE, A can connect to B''s new database' $r.Ok $r.Out
    $r = Deploy-Role 'preview_b' 'b-pw' @('b_appdb')
    Check 'B''s login is set up' $r.Ok $r.Out
    $r = Psql 'CREATE TABLE t (v text); INSERT INTO t VALUES (''a''); SELECT v FROM t;' -user preview_a -pw 'a-pw' -db 'a_appdb'
    Check 'A can create tables and write in its own database' ($r.Ok -and $r.Out -eq 'a') $r.Out
    $r = Psql 'SELECT 1;' -user preview_a -pw 'a-pw' -db 'b_appdb'
    Check 'A cannot connect to B''s database' (-not $r.Ok -and $r.Out -match 'permission denied') $r.Out
    $r = Psql 'SELECT 1;' -user preview_b -pw 'b-pw' -db 'a_appdb'
    Check 'B cannot connect to A''s database' (-not $r.Ok -and $r.Out -match 'permission denied') $r.Out
    $r = Psql 'DROP DATABASE b_appdb;' -user preview_a -pw 'a-pw'
    Check 'A cannot drop B''s database' (-not $r.Ok) $r.Out
    $r = Psql 'SELECT 1;' -user preview_a -pw 'wrong' -db 'a_appdb'
    Check 'control: a wrong password is refused' (-not $r.Ok) $r.Out
    $r = Psql "SELECT string_agg(datname, ',' ORDER BY datname) FROM pg_database WHERE datname LIKE '%appdb';" -user preview_a -pw 'a-pw'
    Write-Host "note  A can still reach the postgres database and list every preview's database name: $($r.Out)"

    Write-Host "`n# Redeploying is harmless"
    $r = Deploy-Role 'preview_a' 'a-pw-2' @('a_appdb')
    Check 'a redeploy of A succeeds' $r.Ok $r.Out
    $r = Psql 'SELECT v FROM t;' -user preview_a -pw 'a-pw-2' -db 'a_appdb'
    Check 'A''s rows survive its redeploy' ($r.Ok -and $r.Out -eq 'a') $r.Out

    Write-Host "`n# The sweep leaves live previews alone"
    $r = Psql $Sweep
    Check 'the sweep runs' $r.Ok $r.Out
    Check 'A''s role survives a sweep' (Role-Exists 'preview_a')
    Check 'B''s role survives a sweep' (Role-Exists 'preview_b')
    Check 'the admin survives a sweep' (Role-Exists 'pgadmin')

    Write-Host "`n# Destroy, then the next deploy"
    $r = Psql 'DROP DATABASE b_appdb;'; if (-not $r.Ok) { throw $r.Out }
    $r = Psql 'SELECT 1;' -user preview_b -pw 'b-pw'
    Check 'a destroyed preview''s login stops working' (-not $r.Ok) 'it can still log in to the postgres database until the next deploy''s sweep'
    $r = Psql $Sweep
    Check 'the next deploy''s sweep runs' $r.Ok $r.Out
    Check 'that sweep removes the destroyed preview''s role' (-not (Role-Exists 'preview_b'))

    Write-Host "`n# Race: a sweep lands between another preview's CREATE ROLE and its ALTER DATABASE OWNER"
    New-Database 'c_appdb'
    $job = Start-Psql (With-Pause $CreateRole "SELECT format('ALTER DATABASE") @{ role = 'preview_c'; pw = 'c-pw'; dbs = 'c_appdb' }
    Start-Sleep -Seconds 2
    $r = Psql $Sweep
    Check 'the other deploy''s sweep runs' $r.Ok $r.Out
    $c = Stop-Psql $job
    Check 'C''s deploy carries on' $c.Ok $c.Out
    Check 'C''s new role survives that sweep' (Role-Exists 'preview_c')
    $owner = (Psql "SELECT pg_get_userbyid(datdba) FROM pg_database WHERE datname = 'c_appdb';").Out
    Check 'C''s role owns its database' ($owner -eq 'preview_c') $owner

    Write-Host "`n# Race: two sweeps go for the same leftover role"
    $r = Psql "CREATE ROLE preview_stale LOGIN;"; if (-not $r.Ok) { throw $r.Out }
    $job = Start-Psql (With-Pause $Sweep 'COMMIT;')
    Start-Sleep -Seconds 2
    $r = Psql $Sweep
    $first = Stop-Psql $job
    Check 'forced: the first sweep succeeds' $first.Ok $first.Out
    Check 'forced: a second sweep waiting on the first does not fail its deploy' $r.Ok $r.Out
    Check 'forced: the leftover role is gone' (-not (Role-Exists 'preview_stale'))

    # The same thing unforced, for how likely it is: nothing but two real sweeps at once.
    $rounds = 20; $failures = 0
    foreach ($round in 1..$rounds) {
        $r = Psql ((1..5 | ForEach-Object { "CREATE ROLE preview_s$_ LOGIN;" }) -join "`n"); if (-not $r.Ok) { throw $r.Out }
        $codes = 1..2 | ForEach-Object -Parallel {
            $using:Sweep | docker exec -i -e "PGPASSWORD=$using:admin" $using:container psql -h 127.0.0.1 -U pgadmin -d postgres -X -q -t -A -v ON_ERROR_STOP=1 2>&1 | Out-Null
            $LASTEXITCODE
        } -ThrottleLimit 2
        if (@($codes | Where-Object { $_ -ne 0 }).Count) { $failures++ }
        Psql $Sweep | Out-Null
    }
    Check 'unforced: two simultaneous sweeps never fail a deploy' ($failures -eq 0) "$failures of $rounds rounds failed"
} finally {
    docker rm -f $container | Out-Null
}
Write-Host "`n$script:failed failed"
if ($script:failed) { exit 1 }
