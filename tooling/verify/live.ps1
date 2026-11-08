# The live half of verifying the deploy path, independent of the factory: no factory script drives or
# judges anything. It does run on the factory's platform and identities, because that is what is claimed.
#
#   pwsh tooling/verify/live.ps1 -Mode Preflight          read-only; says what would stop a run
#   pwsh tooling/verify/live.ps1 -Mode Prepare            local only: pack, scaffolds, offline checks
#   pwsh tooling/verify/live.ps1 -Mode Run                every check, then cleanup and the report
#   pwsh tooling/verify/live.ps1 -Mode Run -From 5 -Through 5   for diagnosis; never a clean pass
#   pwsh tooling/verify/live.ps1 -Mode Cleanup            removes everything named verify-* and ver*-j90*
#
# Only a run of every check, in one go, at one commit, with nothing else on the platform, is a clean pass.
# Needs VERIFY_HCP_TOKEN, and gh, az, docker, node and terraform signed in where they need to be.
#
# Postgres, as approved: admin sessions are read-only, the password stays in memory and out of logs, and
# firewall rules are named verify-* and removed even when a step fails.
param(
    [Parameter(Mandatory)][ValidateSet('Preflight', 'Prepare', 'Run', 'Cleanup')][string] $Mode,
    [ValidateRange(1, 9)][int] $From = 1,
    [ValidateRange(1, 9)][int] $Through = 9
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'lib.ps1')

$Repo = (git -C $PSScriptRoot rev-parse --show-toplevel).Trim()
$Sha = (git -C $Repo rev-parse HEAD).Trim()
$Short = $Sha.Substring(0, 7)
$Work = Join-Path ([IO.Path]::GetTempPath()) "verify-live-$Short"
$LogDir = Join-Path $PSScriptRoot "reports\$Short"
$StatePath = Join-Path $Work 'state.json'

$Owner = 'kamabery'
$Org = 'kurt-mabery'
$ExternalTenant = 'b9423072-52e3-464e-bacf-fc71b75defbc'
$ExternalSubdomain = 'kamfactorypv'
$UserFlowName = 'preview-signin'
$Varset = 'varset-hencmcQWrTRHqzAv'
$GitHubAppName = 'factory-github-actions'
$HcpToken = [Environment]::GetEnvironmentVariable('VERIFY_HCP_TOKEN', 'User')

$Previews = [ordered]@{
    A = @{ Key = 'A'; RepoName = 'verify-a'; Name = 'vera'; Ui = 'react'; Env = 'j901'; Platform = $true }
    B = @{ Key = 'B'; RepoName = 'verify-b'; Name = 'verb'; Ui = 'blazor'; Env = 'j902'; Platform = $true }
    Full = @{ Key = 'Full'; RepoName = 'verify-full'; Name = 'verf'; Ui = 'react'; Env = 'j903'; Platform = $false }
}
$RetryWorkspace = 'verify-retry'
$OursName = '^ver[abf]-j90\d'
# Decision of 2026-11-07: a destroy cannot drop a preview's role, so it lasts until the next deploy's sweep.
$AcceptedResidual = @('^roles: preview_ver[abf]_j90')

$script:Secrets = [Collections.Generic.List[string]]::new()
$script:Tokens = @{}
$script:Results = [Collections.Generic.List[object]]::new()

function Secret([string] $value) { if ($value) { $script:Secrets.Add($value) }; $value }
function Clean([string] $text) { ConvertTo-Redacted $text $script:Secrets }

function Say([string] $text) {
    $line = "$((Get-Date).ToUniversalTime().ToString('HH:mm:ss'))  $(Clean $text)"
    Write-Host $line
    Add-Content (Join-Path $LogDir 'run.log') $line
}

function Save-Log([string] $name, [string] $text) { [IO.File]::WriteAllText((Join-Path $LogDir $name), (Clean $text)) }

function Exec([string] $exe, [string[]] $a, [string] $stdin = '') {
    $err = New-TemporaryFile
    try {
        $out = if ($stdin) { $stdin | & $exe @a 2>$err } else { & $exe @a 2>$err }
        $code = $LASTEXITCODE
        if ($code -ne 0) { throw (Clean "$exe $($a -join ' ') exited $code`: $(Get-Content $err -Raw)") }
        ($out | ForEach-Object { "$_" }) -join "`n"
    } finally { Remove-Item $err -ErrorAction SilentlyContinue }
}

function AzJson([string[]] $a) { Exec az ($a + @('-o', 'json', '--only-show-errors')) | ConvertFrom-Json }
function GhJson([string[]] $a) { Exec gh $a | ConvertFrom-Json }

function Hcp([string] $method, [string] $path, $body = $null, [switch] $AllowNotFound) {
    $p = @{ Method = $method; Uri = "https://app.terraform.io/api/v2$path"; SkipHttpErrorCheck = $true; StatusCodeVariable = 'status'
        Headers = @{ Authorization = "Bearer $HcpToken" }; ContentType = 'application/vnd.api+json' }
    if ($null -ne $body) { $p.Body = $body | ConvertTo-Json -Depth 10 -Compress }
    $res = Invoke-RestMethod @p
    if ($AllowNotFound -and $status -eq 404) { return $null }
    if ($status -ge 300) { throw "HCP $status on $method $path`: $($res | ConvertTo-Json -Depth 5 -Compress)" }
    $res
}

function Graph([string] $method, [string] $path, $body = $null, [switch] $Beta) {
    $token = Exec az @('account', 'get-access-token', '--tenant', $ExternalTenant, '--resource-type', 'ms-graph', '--query', 'accessToken', '-o', 'tsv')
    $p = @{ Method = $method; Uri = "https://graph.microsoft.com/$(if ($Beta) { 'beta' } else { 'v1.0' })$path"; Headers = @{ Authorization = "Bearer $token" } }
    if ($null -ne $body) { $p.Body = $body | ConvertTo-Json -Depth 10 -Compress; $p.ContentType = 'application/json' }
    Invoke-RestMethod @p
}

function Get-Platform {
    if ($script:Platform) { return $script:Platform }
    $var = (Hcp GET "/varsets/$Varset/relationships/vars").data | Where-Object { $_.attributes.key -eq 'platform' }
    $values = @{}
    foreach ($m in [regex]::Matches($var.attributes.value, '(?m)^\s*(\w+)\s*=\s*"([^"]*)"')) { $values[$m.Groups[1].Value] = $m.Groups[2].Value }
    $script:Platform = $values
    $values
}

function Get-UserFlowId { ((Graph GET '/identity/authenticationEventsFlows' -Beta).value | Where-Object displayName -eq $UserFlowName).id }

function Get-GitHubAppId { (AzJson @('ad', 'app', 'list', '--display-name', $GitHubAppName, '--query', '[0].appId')) }

# ---------- state ----------

function Load-State { if (Test-Path $StatePath) { Get-Content $StatePath -Raw | ConvertFrom-Json -AsHashtable } else { @{} } }
function Save-State { $script:State | ConvertTo-Json -Depth 8 | Set-Content $StatePath }
function P([string] $key) {
    if (-not $script:State[$key]) { $script:State[$key] = @{} }
    $script:State[$key]
}

function Add-Result([int] $check, [string] $status, [string] $evidence) {
    $script:Results.Add([pscustomobject]@{ Check = $check; Status = $status; Evidence = (Clean $evidence) })
    Say "check $check $status`: $evidence"
}

# ---------- what is on the platform and in the tenant ----------

function Get-ForeignSnapshot {
    $snapshot = @{}
    foreach ($w in (Hcp GET "/organizations/$Org/workspaces?page%5Bsize%5D=100").data) {
        if ($w.attributes.name -notmatch '^verify-') { $snapshot["workspace $($w.attributes.name)"] = "$($w.attributes.'latest-change-at')" }
    }
    # A canary that came and went leaves no workspace, but it does leave a run and a soft-deleted app.
    $canary = @(GhJson @('run', 'list', '-R', "$Owner/factory-canary", '-L', '1', '--json', 'databaseId'))
    $snapshot['factory-canary latest run'] = "$($canary[0].databaseId)"
    $deleted = (Graph GET '/directory/deletedItems/microsoft.graph.application?$select=id,displayName').value | Where-Object displayName -notmatch $OursName
    $snapshot['soft-deleted apps not ours'] = "$(@($deleted).Count)"
    $snapshot
}

# Always in try/finally at the caller. Not a helper taking a script block: a block runs in a child scope,
# so a check's "$problems += ..." inside one would be lost, and the check would pass.
function Open-Firewall {
    $platform = Get-Platform
    $ip = (Invoke-RestMethod 'https://api.ipify.org').Trim()
    Exec az @('postgres', 'flexible-server', 'firewall-rule', 'create', '--resource-group', $platform.postgres_resource_group, '--server-name', $platform.postgres_server_name,
        '--name', "verify-$PID", '--start-ip-address', $ip, '--end-ip-address', $ip, '--only-show-errors', '-o', 'none') | Out-Null
}

function Close-Firewall {
    $platform = Get-Platform
    Exec az @('postgres', 'flexible-server', 'firewall-rule', 'delete', '--yes', '--resource-group', $platform.postgres_resource_group, '--server-name', $platform.postgres_server_name,
        '--name', "verify-$PID", '--only-show-errors', '-o', 'none') | Out-Null
}

# Through the container's psql; the password reaches it from this process's environment, never argv.
function Pg([string] $sql, [string] $user, [string] $password, [string] $db = 'postgres', [switch] $ReadOnly) {
    $fqdn = (Get-Platform).postgres_server_fqdn
    $env:PGPASSWORD = $password
    $a = @('run', '--rm', '-i', '-e', 'PGPASSWORD', '-e', 'PGSSLMODE=require')
    if ($ReadOnly) { $a += @('-e', 'PGOPTIONS=-c default_transaction_read_only=on') }
    $a += @('postgres:16', 'psql', '-h', $fqdn, '-U', $user, '-d', $db, '-X', '-q', '-t', '-A', '-v', 'ON_ERROR_STOP=1')
    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try { $out = $sql | docker @a 2>&1; $code = $LASTEXITCODE }
    finally { $ErrorActionPreference = $previous; Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue }
    [pscustomobject]@{ Ok = $code -eq 0; Out = Clean ((($out | ForEach-Object { "$_" }) -join ' ').Trim()) }
}

function Get-AdminPassword {
    if (-not $script:AdminPassword) {
        $script:AdminPassword = Secret (Exec az @('keyvault', 'secret', 'show', '--vault-name', (Get-Platform).key_vault_name, '--name', 'postgres-admin-password', '--query', 'value', '-o', 'tsv', '--only-show-errors'))
    }
    $script:AdminPassword
}

function Admin([string] $sql) {
    $r = Pg $sql 'pgadmin' (Get-AdminPassword) -ReadOnly
    if (-not $r.Ok) { throw "admin query failed: $($r.Out)" }
    $r.Out
}

function Get-Inventory {
    $platform = Get-Platform
    $inv = @{}
    $inv.resourceGroups = @((AzJson @('group', 'list', '--query', '[].name')) | Where-Object { $_ -match '^rg-ver[abf]-j90' })
    $inv.containerApps = @((AzJson @('resource', 'list', '--resource-type', 'Microsoft.App/containerApps', '--query', '[].name')) | Where-Object { $_ -match '^ca-ver[abf]' })
    $inv.secrets = @((AzJson @('keyvault', 'secret', 'list', '--vault-name', $platform.key_vault_name, '--query', '[].name')) | Where-Object { $_ -match $OursName })
    $inv.deletedSecrets = @((AzJson @('keyvault', 'secret', 'list-deleted', '--vault-name', $platform.key_vault_name, '--query', '[].name')) | Where-Object { $_ -match $OursName })
    $inv.firewallRules = @((AzJson @('postgres', 'flexible-server', 'firewall-rule', 'list', '--resource-group', $platform.postgres_resource_group, '--name', $platform.postgres_server_name, '--query', '[].name')) |
        Where-Object { $_ -match '^(gha-j90|verify-)' -and $_ -ne "verify-$PID" })
    Open-Firewall
    try {
        $inv.databases = @((Admin "SELECT datname FROM pg_database WHERE datname ~ '^ver[abf]_j90';") -split '\s+' | Where-Object { $_ })
        $inv.roles = @((Admin "SELECT rolname FROM pg_roles WHERE rolname ~ '^preview_ver[abf]_j90';") -split '\s+' | Where-Object { $_ })
    } finally { Close-Firewall }
    $apps = (Graph GET '/applications?$select=appId,displayName&$top=200').value
    $deletedApps = (Graph GET '/directory/deletedItems/microsoft.graph.application?$select=appId,displayName&$top=200').value
    $inv.entraApps = @($apps | Where-Object { $_.displayName -match $OursName -or $_.displayName -eq $RetryWorkspace } | ForEach-Object displayName)
    $inv.entraDeletedApps = @($deletedApps | Where-Object { $_.displayName -match $OursName -or $_.displayName -eq $RetryWorkspace } | ForEach-Object { "$($_.displayName) $($_.appId)" })
    $inv.entraUsers = @((Graph GET '/users?$select=displayName&$top=200').value | Where-Object displayName -match '^QA j90' | ForEach-Object displayName)
    $inv.entraDeletedUsers = @((Graph GET '/directory/deletedItems/microsoft.graph.user?$select=displayName,id&$top=200').value | Where-Object displayName -match '^QA j90' | ForEach-Object { "$($_.displayName) $($_.id)" })
    $ours = @($apps + $deletedApps | Where-Object { $_.displayName -match $OursName -or $_.displayName -eq $RetryWorkspace })
    $links = (Graph GET "/identity/authenticationEventsFlows/$(Get-UserFlowId)/conditions/applications/includeApplications" -Beta).value.appId
    $inv.userFlowLinks = @($links | Where-Object { $_ -in $ours.appId })
    $inv.workspaces = @((Hcp GET "/organizations/$Org/workspaces?page%5Bsize%5D=100").data.attributes.name | Where-Object { $_ -match '^verify-' })
    $inv
}

# ---------- build and scaffold, local only ----------

function Build-Pack {
    Say "packing $Short into $Work"
    Remove-Item "$Work\home", "$Work\tools", "$Work\nupkg", "$Work\nuget" -Recurse -Force -ErrorAction SilentlyContinue
    Exec pwsh @('-NoProfile', '-File', (Join-Path $Repo 'templating\stage.ps1')) | Out-Null
    # To the work folder, not artifacts/nupkg: that is what the factory scaffolds from.
    Exec dotnet @('pack', (Join-Path $Repo 'packaging\AspireStarter.Templates.csproj'), '-c', 'Release', '-o', "$Work\nupkg", '--nologo', '-v', 'q') | Out-Null
    Exec dotnet @('pack', (Join-Path $Repo 'tooling\AspireStarter.Cli'), '-c', 'Release', '-o', "$Work\nupkg", '--nologo', '-v', 'q') | Out-Null
    @"
<configuration><packageSources><clear /><add key="local" value="$Work\nupkg" /><add key="nuget.org" value="https://api.nuget.org/v3/index.json" /></packageSources>
<packageSourceMapping><packageSource key="local"><package pattern="AspireStarter.*" /></packageSource><packageSource key="nuget.org"><package pattern="*" /></packageSource></packageSourceMapping></configuration>
"@ | Set-Content "$Work\tool.nuget.config"
    # A private template hive, tool path and package cache: the global ones are the factory's, and a
    # same-version pack in the cache would otherwise be used instead of this commit's.
    $env:DOTNET_CLI_HOME = "$Work\home"
    $env:PATH = "$Work\tools;$env:PATH"
    $packages = $env:NUGET_PACKAGES
    $env:NUGET_PACKAGES = "$Work\nuget"
    try { Exec dotnet @('tool', 'install', 'AspireStarter.Addon', '--tool-path', "$Work\tools", '--configfile', "$Work\tool.nuget.config") | Out-Null }
    finally { $env:NUGET_PACKAGES = $packages }
    Exec dotnet @('new', 'install', "$Work\nupkg\AspireStarter.Templates.1.0.0.nupkg") | Out-Null
}

function Use-PrivatePack {
    $env:DOTNET_CLI_HOME = "$Work\home"
    if ($env:PATH -notlike "$Work\tools*") { $env:PATH = "$Work\tools;$env:PATH" }
}

function New-Scaffold($p) {
    $dir = Join-Path $Work $p.Name
    Remove-Item $dir -Recurse -Force -ErrorAction SilentlyContinue
    New-Item -ItemType Directory $dir | Out-Null
    Say "scaffolding $($p.Name): $($p.Ui), auth, postgres"
    Push-Location $dir
    try {
        Exec dotnet @('new', 'aspire-base', '-n', $p.Name, '-o', '.', '--dbProvider', 'postgres', '--resource', 'Item') | Out-Null
        if ($p.Ui -eq 'react') { Exec aspire-addon @('add', 'ui-react') | Out-Null }
        else { Exec aspire-addon @('add', 'ui-blazor', '--web', 'true', '--maui', 'false') | Out-Null }
        Exec aspire-addon @('add', 'auth') | Out-Null

        # Offline, so a broken scaffold costs seconds rather than a deploy.
        Exec aspire-addon @('infra', 'check') | Out-Null
        Exec dotnet @('build', "$($p.Name).slnx", '--nologo', '-v', 'q') | Out-Null
        Exec dotnet @('test', "$($p.Name).slnx", '--nologo', '-v', 'q', '--no-build') | Out-Null
        Push-Location IaC
        try {
            Exec terraform @('init', '-backend=false', '-no-color') | Out-Null
            Exec terraform @('validate', '-no-color') | Out-Null
            Exec terraform @('test', '-no-color', '-var-file=envs/ephemeral.tfvars') | Out-Null
        } finally { Pop-Location }

        # After the tests: their "uses the platform" runs would fail with use_platform = false.
        $values = [ordered]@{ entra_external_tenant_id = "`"$ExternalTenant`""; entra_external_subdomain = "`"$ExternalSubdomain`""; entra_external_user_flow_id = "`"$(Get-UserFlowId)`"" }
        if (-not $p.Platform) { $values.use_platform = 'false' }
        $tfvars = Join-Path $dir 'IaC\envs\ephemeral.tfvars'
        [IO.File]::WriteAllText($tfvars, (Set-TfvarLines ([IO.File]::ReadAllText($tfvars)) $values))

        Exec git @('init', '-q', '-b', 'main') | Out-Null
        Exec git @('add', '-A') | Out-Null
        Exec git @('-c', 'user.name=verify', '-c', 'user.email=verify@localhost', 'commit', '-q', '-m', "scaffold from template-projects $Short") | Out-Null
        (P $p.Key).sha = (Exec git @('rev-parse', 'HEAD')).Trim()
        Say "$($p.Name): build, tests, infra check and offline Terraform all pass"
    } finally { Pop-Location }
}

function Invoke-Prepare {
    Build-Pack
    foreach ($p in $Previews.Values) { New-Scaffold $p }
    Save-State
}

# ---------- publish and deploy ----------

function Publish-Preview($p) {
    $full = "$Owner/$($p.RepoName)"
    Say "creating $full, its preview environment, federated credential and HCP project"
    Exec gh @('repo', 'create', $full, '--private') | Out-Null
    Exec gh @('api', '-X', 'PUT', "repos/$full/environments/preview", '--silent') | Out-Null
    $account = AzJson @('account', 'show')
    $appId = Get-GitHubAppId
    foreach ($v in @(@('AZURE_CLIENT_ID', $appId), @('AZURE_TENANT_ID', $account.tenantId), @('AZURE_SUBSCRIPTION_ID', $account.id))) {
        Exec gh @('variable', 'set', $v[0], '--env', 'preview', '--repo', $full, '--body', $v[1]) | Out-Null
    }
    Exec gh @('variable', 'set', 'TF_CLOUD_ORGANIZATION', '--repo', $full, '--body', $Org) | Out-Null
    Exec gh @('secret', 'set', 'TF_API_TOKEN', '--env', 'preview', '--repo', $full) $HcpToken | Out-Null

    $oidc = GhJson @('api', "repos/$full/actions/oidc/customization/sub")
    $prefix = if ($oidc.use_immutable_subject -and $oidc.sub_claim_prefix) { $oidc.sub_claim_prefix } else { "repo:$full" }
    $file = New-TemporaryFile
    @{ name = "verify-preview-$($p.RepoName)"; issuer = 'https://token.actions.githubusercontent.com'; subject = "${prefix}:environment:preview"; audiences = @('api://AzureADTokenExchange') } |
        ConvertTo-Json -Compress | Set-Content $file -Encoding utf8NoBOM
    try { Exec az @('ad', 'app', 'federated-credential', 'create', '--id', $appId, '--parameters', "@$file", '--only-show-errors', '-o', 'none') | Out-Null }
    finally { Remove-Item $file -ErrorAction SilentlyContinue }

    $project = (Hcp POST "/organizations/$Org/projects" @{ data = @{ type = 'projects'; attributes = @{ name = $p.RepoName } } }).data.id
    Hcp POST "/varsets/$Varset/relationships/projects" @{ data = @(@{ type = 'projects'; id = $project }) } | Out-Null
    # The belt to Cleanup's braces: if this process dies, HCP destroys the preview tomorrow regardless.
    $attrs = @{ name = "$($p.RepoName)-$($p.Env)"; 'execution-mode' = 'remote'; 'auto-destroy-at' = (Get-Date).ToUniversalTime().AddHours(24).ToString('yyyy-MM-ddTHH:mm:ssZ') }
    $ws = (Hcp POST "/organizations/$Org/workspaces" @{ data = @{ type = 'workspaces'; attributes = $attrs; relationships = @{ project = @{ data = @{ type = 'projects'; id = $project } } } } }).data.id
    (P $p.Key).workspaceId = $ws
    (P $p.Key).project = $project

    $ssh = (GhJson @('repo', 'view', $full, '--json', 'sshUrl')).sshUrl
    Exec git @('-C', (Join-Path $Work $p.Name), 'remote', 'add', 'origin', $ssh) | Out-Null
    Exec git @('-C', (Join-Path $Work $p.Name), 'push', '-q', 'origin', 'main') | Out-Null
    Save-State
}

# Dispatches deploy.yml and returns the new run's id.
function Start-Deploy($p, [string] $since = '') {
    $full = "$Owner/$($p.RepoName)"
    $last = try { @(GhJson @('run', 'list', '-R', $full, '-w', 'deploy.yml', '-L', '1', '--json', 'databaseId')) } catch { @() }
    $lastId = if ($last.Count) { [long]$last[0].databaseId } else { 0 }
    $a = @('workflow', 'run', 'deploy.yml', '-R', $full, '--ref', 'main', '-f', "environment=$($p.Env)")
    if ($since) { $a += @('-f', "since=$since") }
    # A workflow in a repo created a moment ago takes a little while to register.
    foreach ($try in 1..12) {
        try { Exec gh $a | Out-Null; break }
        catch { if ($try -eq 12) { throw }; Start-Sleep -Seconds 15 }
    }
    foreach ($try in 1..40) {
        $runs = @(GhJson @('run', 'list', '-R', $full, '-w', 'deploy.yml', '-L', '5', '--json', 'databaseId'))
        $new = $runs | Where-Object { [long]$_.databaseId -gt $lastId } | Select-Object -First 1
        if ($new) { Say "$full`: run $($new.databaseId)$(if ($since) { " since $since" })"; return [long]$new.databaseId }
        Start-Sleep -Seconds 5
    }
    throw "no new run of deploy.yml appeared in $full"
}

function Get-Run($p, [long] $id) {
    GhJson @('run', 'view', "$id", '-R', "$Owner/$($p.RepoName)", '--json', 'status,conclusion,headSha,createdAt,updatedAt,url,jobs')
}

# Waits for every (preview, run id) pair; $tick runs on each pass. Returns the finished runs by key.
function Wait-Runs([hashtable] $runs, [scriptblock] $tick = $null) {
    $deadline = (Get-Date).AddMinutes(75)
    $done = @{}
    while ($done.Count -lt $runs.Count) {
        if ((Get-Date) -gt $deadline) { throw 'a deploy did not finish in 75 minutes' }
        foreach ($key in $runs.Keys) {
            if ($done[$key]) { continue }
            $run = Get-Run $Previews[$key] $runs[$key]
            if ($run.status -eq 'completed') {
                $done[$key] = $run
                Save-Log "run-$key-$($runs[$key]).log" (Exec gh @('run', 'view', "$($runs[$key])", '-R', "$Owner/$($Previews[$key].RepoName)", '--log'))
            }
        }
        if ($tick) { & $tick }
        if ($done.Count -lt $runs.Count) { Start-Sleep -Seconds 20 }
    }
    $done
}

function Minutes($run) { '{0:n1}' -f ([datetime]$run.updatedAt - [datetime]$run.createdAt).TotalMinutes }

function Get-Outputs($p) {
    $ws = (P $p.Key).workspaceId
    $out = @{}
    foreach ($o in (Hcp GET "/workspaces/$ws/current-state-version-outputs").data) {
        $out[$o.attributes.name] = if ($o.attributes.sensitive) {
            if ($o.attributes.name -eq 'qa_password') { Secret (Hcp GET "/state-version-outputs/$($o.id)").data.attributes.value }
        } else { $o.attributes.value }
    }
    $s = P $p.Key
    $s.frontend = if ($p.Ui -eq 'react') { $out.react_url } else { $out.blazor_web_url }
    $base = if ($p.Ui -eq 'react') { "$($out.react_url.TrimEnd('/'))/api" } else { $out.api_url.TrimEnd('/') }
    $s.me = "$base/me"
    $s.health = "$base/health"
    $s.resourceGroup = $out.resource_group_name
    $s.secret = $out.appdb_connection_secret_name
    $s.role = $out.database_role
    $s.database = $out.database_names
    Save-State
    $out
}

# A real sign-in. Entra can still be replicating a new app's client type (AADSTS9002326, about 90 s).
function Invoke-SignIn($p) {
    $outs = Get-Outputs $p
    $s = P $p.Key
    $tokenFile = Join-Path $Work "token-$($p.Key)"
    $lines = @()
    try {
        foreach ($try in 1..5) {
            Push-Location $PSScriptRoot
            try {
                $lines = @($outs.qa_password | & node signin.mjs --url $s.frontend --me $s.me --user $outs.qa_user --token-file $tokenFile 2>&1 | ForEach-Object { "$_" })
                $code = $LASTEXITCODE
            } finally { Pop-Location }
            if ($code -eq 0) { break }
            if (($lines -join ' ') -notmatch 'AADSTS9002326' -or $try -eq 5) { return @{ Ok = $false; Lines = $lines } }
            Say "$($p.Name): Entra is still replicating the app; retrying in 30 s"
            Start-Sleep -Seconds 30
        }
        $script:Tokens[$p.Key] = Secret (Get-Content $tokenFile -Raw).Trim()
        @{ Ok = $true; Lines = $lines }
    } finally {
        Remove-Item $tokenFile -ErrorAction SilentlyContinue
    }
}

function Test-SignIn($p) {
    $r = Invoke-SignIn $p
    Save-Log "signin-$($p.Key)-$(Get-Date -Format HHmmss).log" ($r.Lines -join "`n")
    $r
}

function Get-MeStatus([string] $url, [string] $token) {
    $h = if ($token) { @{ Authorization = "Bearer $token" } } else { @{} }
    [int](Invoke-WebRequest $url -Headers $h -SkipHttpErrorCheck -TimeoutSec 60).StatusCode
}

# The API's revision and image, the HCP run count's newest id: what a front-end-only deploy must not move.
function Get-Backend($p) {
    $s = P $p.Key
    $app = AzJson @('containerapp', 'show', '--resource-group', $s.resourceGroup, '--name', "ca-$($p.Name)-$($p.Env)-api")
    $run = (Hcp GET "/workspaces/$($s.workspaceId)/runs?page%5Bsize%5D=1").data | Select-Object -First 1
    "revision=$($app.properties.latestRevisionName) image=$($app.properties.template.containers[0].image) hcp=$($run.id)"
}

function Save-HcpLogs($p) {
    $ws = (P $p.Key).workspaceId
    foreach ($run in (Hcp GET "/workspaces/$ws/runs?page%5Bsize%5D=50").data) {
        foreach ($part in @(@('plans', $run.relationships.plan.data.id), @('applies', $run.relationships.apply.data.id))) {
            if (-not $part[1]) { continue }
            $url = (Hcp GET "/$($part[0])/$($part[1])" -AllowNotFound).data.attributes.'log-read-url'
            if ($url) { Save-Log "hcp-$($p.Key)-$($run.id)-$($part[0]).log" "$(Invoke-RestMethod $url -SkipHttpErrorCheck)" }
        }
    }
}

# Queues the destroy, waits, and deletes the workspace. HCP logs go with it, so they are saved first.
function Remove-Preview($p, [scriptblock] $tick = $null) {
    $ws = (P $p.Key).workspaceId
    Save-HcpLogs $p
    $run = (Hcp POST '/runs' @{ data = @{ type = 'runs'; attributes = @{ 'is-destroy' = $true; 'auto-apply' = $true; message = 'verify: tear down' }
        relationships = @{ workspace = @{ data = @{ type = 'workspaces'; id = $ws } } } } }).data
    $started = Get-Date
    while ($run.attributes.status -notin 'applied', 'errored', 'canceled', 'discarded', 'force_canceled', 'planned_and_finished') {
        if ((Get-Date) -gt $started.AddMinutes(60)) { throw "destroy of $($p.Name) did not finish in 60 minutes" }
        if ($tick) { & $tick }
        Start-Sleep -Seconds 20
        $run = (Hcp GET "/runs/$($run.id)").data
    }
    Save-HcpLogs $p
    $minutes = '{0:n1}' -f ((Get-Date) - $started).TotalMinutes
    if ($run.attributes.status -notin 'applied', 'planned_and_finished') { throw "destroy of $($p.Name) ended $($run.attributes.status)" }
    foreach ($try in 1..20) {
        try { Hcp POST "/workspaces/$ws/actions/safe-delete" | Out-Null; break }
        catch { if ($_.Exception.Message -notmatch 'HCP 409' -or $try -eq 20) { throw }; Start-Sleep -Seconds 15 }
    }
    $minutes
}

# ---------- the checks ----------

function Check-1 {
    $a, $b = $Previews.A, $Previews.B
    foreach ($p in $a, $b) { Publish-Preview $p }
    Start-Sleep -Seconds 20
    $runs = @{ A = Start-Deploy $a; B = Start-Deploy $b }
    $done = Wait-Runs $runs
    foreach ($p in $a, $b) {
        $run = $done[$p.Key]
        if ($run.conclusion -ne 'success') { Add-Result 1 'fail' "$($p.Name) deploy: $($run.conclusion), $($run.url)"; return }
        $signin = Test-SignIn $p
        if (-not $signin.Ok) { Add-Result 1 'fail' "$($p.Name) sign-in: $($signin.Lines -join ' | ')"; return }
        Add-Result 1 'pass' "$($p.Name): deployed in $(Minutes $run) min, $($run.url); sign-in passed, /me 200 with a token and 401 without"
    }
}

function Check-2 {
    $a, $b = $Previews.A, $Previews.B
    foreach ($p in $a, $b) {
        if (-not $script:Tokens[$p.Key]) {
            $signin = Test-SignIn $p
            if (-not $signin.Ok) { Add-Result 2 'fail' "$($p.Name) sign-in: $($signin.Lines -join ' | ')"; return }
        }
    }
    $sa, $sb = (P 'A'), (P 'B')
    $problems = @()

    $code = Get-MeStatus $sb.me $script:Tokens.A
    if ($code -ne 401) { $problems += "B's API answered $code to A's token" }
    $code = Get-MeStatus $sa.me $script:Tokens.B
    if ($code -ne 401) { $problems += "A's API answered $code to B's token" }

    $vault = (Get-Platform).key_vault_name
    $conn = @{}
    foreach ($key in 'A', 'B') {
        $conn[$key] = Secret (Get-ConnectionPassword (Exec az @('keyvault', 'secret', 'show', '--vault-name', $vault, '--name', (P $key).secret, '--query', 'value', '-o', 'tsv', '--only-show-errors')))
    }
    $note = ''
    Open-Firewall
    try {
        $own = Pg 'SELECT 1;' $sa.role $conn.A $sa.database
        if (-not $own.Ok) { $problems += "A cannot reach its own database: $($own.Out)" }
        $own = Pg 'SELECT 1;' $sb.role $conn.B $sb.database
        if (-not $own.Ok) { $problems += "B cannot reach its own database: $($own.Out)" }
        $cross = Pg 'SELECT 1;' $sa.role $conn.A $sb.database
        if ($cross.Ok -or $cross.Out -notmatch 'permission denied') { $problems += "A reached B's database: $($cross.Out)" }
        $cross = Pg 'SELECT 1;' $sb.role $conn.B $sa.database
        if ($cross.Ok -or $cross.Out -notmatch 'permission denied') { $problems += "B reached A's database: $($cross.Out)" }
        $wrong = Pg 'SELECT 1;' $sa.role 'not-the-password' $sa.database
        if ($wrong.Ok) { $problems += 'control: a wrong password was accepted' }
        $note = (Pg "SELECT count(*) FROM pg_database;" $sa.role $conn.A).Out
    } finally { Close-Firewall }

    foreach ($pair in @(@('A', 'B'), @('B', 'A'))) {
        $mine, $theirs = (P $pair[0]), (P $pair[1])
        $principal = (AzJson @('identity', 'list', '--resource-group', $mine.resourceGroup, '--query', '[0].principalId'))
        $scopes = @((AzJson @('role', 'assignment', 'list', '--assignee', $principal, '--all', '--query', '[].scope')))
        if (-not ($scopes | Where-Object { $_ -like "*/secrets/$($mine.secret)" })) { $problems += "control: $($pair[0])'s identity has no grant on its own secret" }
        if ($scopes | Where-Object { $_ -like "*/secrets/$($theirs.secret)" }) { $problems += "$($pair[0])'s identity can read $($pair[1])'s secret" }
        if ($scopes | Where-Object { $_ -like "*/vaults/$vault" }) { $problems += "$($pair[0])'s identity can read the whole vault" }
    }

    if ($problems) { Add-Result 2 'fail' ($problems -join '; ') }
    else { Add-Result 2 'pass' "tokens and logins rejected in both directions, grants per secret. Noted: A's login can still connect to the postgres database and count $note databases" }
}

function Check-3 {
    $a, $b = $Previews.A, $Previews.B
    $script:PeakConnections = 0
    Open-Firewall
    try {
        $limit = [int](Admin 'SHOW max_connections;')
        $runs = @{ A = Start-Deploy $a; B = Start-Deploy $b }
        $done = Wait-Runs $runs {
            $count = [int](Admin 'SELECT count(*) FROM pg_stat_activity;')
            if ($count -gt $script:PeakConnections) { $script:PeakConnections = $count }
        }
    } finally { Close-Firewall }
    foreach ($p in $a, $b) {
        if ($done[$p.Key].conclusion -ne 'success') { Add-Result 3 'fail' "$($p.Name) redeploy alongside the other: $($done[$p.Key].conclusion), $($done[$p.Key].url)"; return }
    }
    if ($script:PeakConnections -ge $limit) { Add-Result 3 'fail' "connections reached $script:PeakConnections of $limit"; return }
    Add-Result 3 'pass' "both redeployed at once ($(Minutes $done.A) and $(Minutes $done.B) min); peak $script:PeakConnections of $limit connections, sampled every 20 s. A destroy alongside a deploy is proved in check 8"
}

function Check-4 {
    $a = $Previews.A
    $dir = Join-Path $Work $a.Name
    $full = "$Owner/$($a.RepoName)"
    $vault = (Get-Platform).key_vault_name
    $names = @('postgres-admin-password', (P 'B').secret)
    $yaml = (Get-Content (Join-Path $PSScriptRoot 'identity-probe.yml') -Raw).Replace('__VAULT__', $vault).Replace('__SECRETS__', ($names -join ' '))
    Exec git @('-C', $dir, 'checkout', '-q', '-b', 'verify-identity') | Out-Null
    try {
        [IO.File]::WriteAllText((Join-Path $dir '.github\workflows\verify-identity.yml'), $yaml)
        Exec git @('-C', $dir, 'add', '-A') | Out-Null
        Exec git @('-C', $dir, '-c', 'user.name=verify', '-c', 'user.email=verify@localhost', 'commit', '-q', '-m', 'identity probe') | Out-Null
        Exec git @('-C', $dir, 'push', '-q', 'origin', 'verify-identity') | Out-Null
    } finally { Exec git @('-C', $dir, 'checkout', '-q', 'main') | Out-Null }

    $run = $null
    foreach ($try in 1..80) {
        Start-Sleep -Seconds 15
        $found = @(GhJson @('run', 'list', '-R', $full, '--branch', 'verify-identity', '-L', '1', '--json', 'databaseId,status,conclusion,url'))
        if ($found.Count -and $found[0].status -eq 'completed') { $run = $found[0]; break }
    }
    if (-not $run) { Add-Result 4 'fail' 'the probe never finished'; return }
    $log = Exec gh @('run', 'view', "$($run.databaseId)", '-R', $full, '--log')
    Save-Log "run-identity-$($run.databaseId).log" $log
    $answers = foreach ($n in $names) { [regex]::Match($log, "probe $([regex]::Escape($n)): (readable|denied)").Groups[1].Value }
    if ($run.conclusion -ne 'success' -or @($answers | Where-Object { -not $_ }).Count) { Add-Result 4 'fail' "the probe gave no answer: $($run.url)"; return }
    if ($answers -contains 'readable') {
        Add-Result 4 'accepted-risk' "a job-branch workflow can read: $(@(0..($names.Count - 1) | ForEach-Object { "$($names[$_]) $($answers[$_])" }) -join '; '). $($run.url). Kurt signs this off; it goes in IaC/README.md"
    } else {
        Add-Result 4 'pass' "a job-branch workflow was denied both, contrary to what setup-platform.ps1 grants: $($run.url)"
    }
}

function Add-Marker($p, [string] $marker) {
    $dir = Join-Path $Work $p.Name
    $index = if ($p.Ui -eq 'react') { Join-Path $dir 'src\ui\react\index.html' }
             else { (Get-ChildItem (Join-Path $dir 'src\ui\blazor') -Recurse -Filter index.html | Where-Object FullName -match '\.Web\\wwwroot\\')[0].FullName }
    $text = [IO.File]::ReadAllText($index)
    if ($text -notmatch '</head>') { throw "no </head> in $index" }
    [IO.File]::WriteAllText($index, $text.Replace('</head>', "<meta name=`"verify-marker`" content=`"$marker`" /></head>"))
    Push-Commit $p "front end change $marker"
}

function Push-Commit($p, [string] $message) {
    $dir = Join-Path $Work $p.Name
    Exec git @('-C', $dir, 'add', '-A') | Out-Null
    Exec git @('-C', $dir, '-c', 'user.name=verify', '-c', 'user.email=verify@localhost', 'commit', '-q', '-m', $message) | Out-Null
    Exec git @('-C', $dir, 'push', '-q', 'origin', 'main') | Out-Null
    (Exec git @('-C', $dir, 'rev-parse', 'HEAD')).Trim()
}

function Check-5 {
    $a, $b = $Previews.A, $Previews.B
    $marker = [guid]::NewGuid().ToString('N')
    $since = @{}; $before = @{}; $heads = @{}
    foreach ($p in $a, $b) {
        Get-Outputs $p | Out-Null
        $since[$p.Key] = (P $p.Key).sha
        $before[$p.Key] = Get-Backend $p
        $heads[$p.Key] = $null
        Add-Marker $p $marker
        $heads[$p.Key] = (Exec git @('-C', (Join-Path $Work $p.Name), 'rev-parse', 'HEAD')).Trim()
    }
    $runs = @{ A = Start-Deploy $a $since.A; B = Start-Deploy $b $since.B }
    $done = Wait-Runs $runs
    foreach ($p in $a, $b) {
        $run = $done[$p.Key]; $s = P $p.Key
        if ($run.conclusion -ne 'success') { Add-Result 5 'fail' "$($p.Name): $($run.conclusion), $($run.url)"; return }
        $path = Get-DeployPath $run
        if ($path -ne 'front-end-only') { Add-Result 5 'fail' "$($p.Name) took the $path path: $($run.url)"; return }
        $firewall = ($run.jobs.steps | Where-Object name -eq 'open the database firewall for this runner').conclusion
        if ($firewall -ne 'skipped') { Add-Result 5 'fail' "$($p.Name) firewall step was $firewall"; return }
        $served = $false
        foreach ($try in 1..30) {
            $html = (Invoke-WebRequest $s.frontend -SkipHttpErrorCheck -Headers @{ 'Cache-Control' = 'no-cache' }).Content
            if ("$html" -match $marker) { $served = $true; break }
            Start-Sleep -Seconds 10
        }
        if (-not $served) { Add-Result 5 'fail' "$($p.Name) never served the change"; return }
        $after = Get-Backend $p
        if ($after -cne $before[$p.Key]) { Add-Result 5 'fail' "$($p.Name) backend moved: $($before[$p.Key]) -> $after"; return }
        $signin = Test-SignIn $p
        if (-not $signin.Ok) { Add-Result 5 'fail' "$($p.Name) sign-in afterwards: $($signin.Lines -join ' | ')"; return }
        $s.sha = $heads[$p.Key]
        Add-Result 5 'pass' "$($p.Name) ($($p.Ui)): front-end-only in $(Minutes $run) min, $($run.url); change served, $after unchanged, no firewall rule, sign-in passed"
    }
    Save-State
}

function Check-6 {
    $a = $Previews.A
    $s = P 'A'
    $dir = Join-Path $Work $a.Name
    $program = Join-Path $dir "src\$($a.Name).api\Program.cs"
    Add-Content $program "// verify $([guid]::NewGuid().ToString('N'))"
    $head = Push-Commit $a 'API change'
    $run = (Wait-Runs @{ A = Start-Deploy $a $s.sha }).A
    if ($run.conclusion -ne 'success') { Add-Result 6 'fail' "after an API change: $($run.conclusion), $($run.url)"; return }
    $path = Get-DeployPath $run
    if ($path -ne 'full') { Add-Result 6 'fail' "an API change took the $path path: $($run.url)"; return }
    Add-Result 6 'pass' "API change took the full path in $(Minutes $run) min, $($run.url)"
    $s.sha = $head

    $run = (Wait-Runs @{ A = Start-Deploy $a '0123456789012345678901234567890123456789' }).A
    if ($run.conclusion -ne 'success') { Add-Result 6 'fail' "with an unreachable since: $($run.conclusion), $($run.url)"; return }
    $path = Get-DeployPath $run
    if ($path -ne 'full') { Add-Result 6 'fail' "an unreachable since took the $path path: $($run.url)"; return }
    Add-Result 6 'pass' "unreachable since took the full path in $(Minutes $run) min, $($run.url)"
    Save-State
}

function Invoke-RetryApply([string] $dir, [string] $appId, [string] $flow) {
    $file = Join-Path $dir "apply-$(Get-Date -Format HHmmss).log"
    Start-ThreadJob -ScriptBlock {
        param($dir, $file, $tenant, $flow, $appId)
        Set-Location $dir
        terraform apply -auto-approve -no-color "-var=entra_external_tenant_id=$tenant" "-var=entra_external_user_flow_id=$flow" "-var=app_id=$appId" *> $file
        [pscustomobject]@{ Exit = $LASTEXITCODE; File = $file }
    } -ArgumentList $dir, $file, $ExternalTenant, $flow, $appId
}

function Check-7 {
    $flow = Get-UserFlowId
    $dir = Join-Path $Work 'retry'
    New-Item -ItemType Directory -Force $dir | Out-Null
    $auth = [IO.File]::ReadAllText((Join-Path $Repo 'IaC\auth.entra-external.tf'))
    $env:TF_TOKEN_app_terraform_io = $HcpToken

    $project = (Hcp POST "/organizations/$Org/projects" @{ data = @{ type = 'projects'; attributes = @{ name = $RetryWorkspace } } }).data.id
    Hcp POST "/varsets/$Varset/relationships/projects" @{ data = @(@{ type = 'projects'; id = $project }) } | Out-Null
    $ws = (Hcp POST "/organizations/$Org/workspaces" @{ data = @{ type = 'workspaces'; attributes = @{ name = $RetryWorkspace; 'execution-mode' = 'remote' }
        relationships = @{ project = @{ data = @{ type = 'projects'; id = $project } } } } }).data.id
    # Provider logs, so the attempts can be counted rather than inferred from the elapsed time.
    Hcp POST "/workspaces/$ws/vars" @{ data = @{ type = 'vars'; attributes = @{ key = 'TF_LOG_PROVIDER'; value = 'DEBUG'; category = 'env' } } } | Out-Null

    $app = Graph POST '/applications' @{ displayName = $RetryWorkspace; signInAudience = 'AzureADMyOrg' }
    $sp = Graph POST '/servicePrincipals' @{ appId = $app.appId }
    try {
        Graph DELETE "/applications/$($app.id)" | Out-Null
        Say "check 7: $RetryWorkspace app $($app.appId) soft-deleted"
        Start-Sleep -Seconds 60

        # No restore: this is also what proves a soft-deleted app gets the same error as a racing one.
        [IO.File]::WriteAllText((Join-Path $dir 'main.tf'), (New-RetryProbeConfig $auth $RetryWorkspace $Org '3m'))
        Exec terraform @("-chdir=$dir", 'init', '-no-color', '-input=false') | Out-Null
        $started = Get-Date
        $result = Receive-Job (Invoke-RetryApply $dir $app.appId $flow) -Wait -AutoRemoveJob
        $seconds = ((Get-Date) - $started).TotalSeconds
        $log = Get-Content $result.File -Raw
        Save-Log 'retry-no-restore.log' $log
        if ($result.Exit -eq 0) { Add-Result 7 'fail' 'with the app still deleted, the link succeeded, so this test proves nothing'; return }
        if ($log -notmatch 'The application id \S+ is invalid') {
            Add-Result 7 'fail' "STOP FOR KURT: a soft-deleted app does not give the same error, so the test design is invalid. See retry-no-restore.log"; return
        }
        $attempts = [regex]::Matches($log, 'AADB2C').Count
        if ($seconds -lt 150) { Add-Result 7 'fail' "it stopped after $([int]$seconds) s with a 3 min timeout, so it did not retry"; return }
        Add-Result 7 'pass' "with no restore: same AADB2C error, retried, and stopped at the 3 min timeout after $([int]$seconds) s ($attempts mentions of AADB2C)"

        # Restored part way: the retry has to carry on to success.
        [IO.File]::WriteAllText((Join-Path $dir 'main.tf'), (New-RetryProbeConfig $auth $RetryWorkspace $Org '15m'))
        $started = Get-Date
        $job = Invoke-RetryApply $dir $app.appId $flow
        Start-Sleep -Seconds 120
        Graph POST "/directory/deletedItems/$($app.id)/restore" | Out-Null
        try { Graph POST "/directory/deletedItems/$($sp.id)/restore" | Out-Null } catch { Say "the service principal did not need restoring: $($_.Exception.Message)" }
        Say 'check 7: app restored'
        $result = Receive-Job $job -Wait -AutoRemoveJob
        $seconds = ((Get-Date) - $started).TotalSeconds
        $log = Get-Content $result.File -Raw
        Save-Log 'retry-restored.log' $log
        $attempts = [regex]::Matches($log, 'AADB2C').Count
        if ($result.Exit -ne 0) { Add-Result 7 'fail' "after the restore the link never succeeded. See retry-restored.log"; return }
        if ($seconds -lt 120) { Add-Result 7 'fail' "it finished in $([int]$seconds) s, before the restore, so it proves nothing"; return }
        if ($attempts -lt 2) { Add-Result 7 'fail' "it succeeded, but the log shows $attempts failed attempts. See retry-restored.log"; return }
        Add-Result 7 'pass' "restored after 2 min: repeated failures ($attempts mentions of AADB2C), then success after $([int]$seconds) s"
    } finally {
        try { Graph DELETE "/identity/authenticationEventsFlows/$flow/conditions/applications/includeApplications/$($app.appId)/`$ref" -Beta | Out-Null } catch { Say "user-flow link: $($_.Exception.Message)" }
        try { Graph DELETE "/applications/$($app.id)" | Out-Null } catch { Say "app delete: $($_.Exception.Message)" }
        try { Graph DELETE "/directory/deletedItems/$($app.id)" | Out-Null } catch { Say "app purge: $($_.Exception.Message)" }
        try { Exec terraform @("-chdir=$dir", 'destroy', '-auto-approve', '-no-color', "-var=entra_external_tenant_id=$ExternalTenant", "-var=entra_external_user_flow_id=$flow", "-var=app_id=$($app.appId)") | Out-Null } catch { Say "retry workspace destroy: $($_.Exception.Message)" }
        Remove-Item Env:TF_TOKEN_app_terraform_io -ErrorAction SilentlyContinue
    }
}

function Check-8 {
    $a, $b = $Previews.A, $Previews.B
    $sb = P 'B'
    Get-Outputs $b | Out-Null
    $password = Secret (Get-ConnectionPassword (Exec az @('keyvault', 'secret', 'show', '--vault-name', (Get-Platform).key_vault_name, '--name', $sb.secret, '--query', 'value', '-o', 'tsv', '--only-show-errors')))

    # A comes down while B redeploys, so B's sweep runs with A's role in the middle of going.
    $script:Unhealthy = 0
    $script:Probes = 0
    $tick = {
        $script:Probes++
        $code = [int](Invoke-WebRequest $sb.health -SkipHttpErrorCheck -TimeoutSec 60).StatusCode
        if ($code -ne 200) { $script:Unhealthy++ ; Say "B's health answered $code" }
    }
    $bRun = Start-Deploy $b
    $minutes = Remove-Preview $a $tick
    $run = (Wait-Runs @{ B = $bRun } $tick).B
    if ($run.conclusion -ne 'success') { Add-Result 8 'fail' "B's redeploy during A's destroy: $($run.conclusion), $($run.url)"; return }
    if ($script:Unhealthy) { Add-Result 8 'fail' "B answered unhealthy $script:Unhealthy of $script:Probes times during A's destroy"; return }

    Open-Firewall
    try {
        $write = Pg "CREATE TABLE IF NOT EXISTS verify_probe (v text); INSERT INTO verify_probe VALUES ('after A went'); SELECT count(*) FROM verify_probe;" $sb.role $password $sb.database
    } finally { Close-Firewall }
    if (-not $write.Ok) { Add-Result 8 'fail' "B cannot write after A's destroy: $($write.Out)"; return }
    Add-Result 8 'pass' "A destroyed in $minutes min while B redeployed ($(Minutes $run) min, $($run.url)); B healthy on $script:Probes of $script:Probes probes, and writing afterwards"

    $minutes = Remove-Preview $b
    Say "B destroyed in $minutes min; taking the inventory"
    Compare-AndReport 8
}

function Compare-AndReport([int] $check) {
    $left = @(Compare-Inventory $script:State.baseline (Get-Inventory) $AcceptedResidual | Where-Object { $_ -notmatch '^workspaces: verify-' })
    if ($left) { Add-Result $check 'fail' "left behind: $($left -join '; ')" }
    else { Add-Result $check 'pass' 'inventory clean, apart from the accepted preview_* roles' }
}

function Check-9 {
    $p = $Previews.Full
    Publish-Preview $p
    Start-Sleep -Seconds 20
    $run = (Wait-Runs @{ Full = Start-Deploy $p }).Full
    if ($run.conclusion -ne 'success') { Add-Result 9 'fail' "from scratch: $($run.conclusion), $($run.url)"; return }
    $signin = Test-SignIn $p
    if (-not $signin.Ok) { Add-Result 9 'fail' "sign-in: $($signin.Lines -join ' | ')"; return }
    $minutes = Remove-Preview $p
    Add-Result 9 'pass' "from scratch with auth: deployed in $(Minutes $run) min, $($run.url); sign-in passed, /me 200 and 401; destroyed in $minutes min"
    Compare-AndReport 9
}

# ---------- cleanup, preflight, report ----------

function Invoke-Cleanup {
    Say 'cleanup: everything named verify-* and ver*-j90*'
    foreach ($w in (Hcp GET "/organizations/$Org/workspaces?page%5Bsize%5D=100").data | Where-Object { $_.attributes.name -match '^verify-' }) {
        if ($w.attributes.'resource-count' -gt 0) {
            Say "destroying $($w.attributes.name)"
            $run = (Hcp POST '/runs' @{ data = @{ type = 'runs'; attributes = @{ 'is-destroy' = $true; 'auto-apply' = $true; message = 'verify: cleanup' }
                relationships = @{ workspace = @{ data = @{ type = 'workspaces'; id = $w.id } } } } }).data
            while ($run.attributes.status -notin 'applied', 'errored', 'canceled', 'discarded', 'force_canceled', 'planned_and_finished') {
                Start-Sleep -Seconds 20
                $run = (Hcp GET "/runs/$($run.id)").data
            }
            if ($run.attributes.status -notin 'applied', 'planned_and_finished') { Say "destroy of $($w.attributes.name) ended $($run.attributes.status): left for a person"; continue }
        }
        foreach ($try in 1..20) {
            try { Hcp POST "/workspaces/$($w.id)/actions/safe-delete" | Out-Null; break }
            catch { if ($_.Exception.Message -notmatch 'HCP 409' -or $try -eq 20) { Say "$($w.attributes.name): $($_.Exception.Message)"; break }; Start-Sleep -Seconds 15 }
        }
    }
    foreach ($project in (Hcp GET "/organizations/$Org/projects?page%5Bsize%5D=100").data | Where-Object { $_.attributes.name -match '^verify-' }) {
        try { Hcp DELETE "/projects/$($project.id)" | Out-Null } catch { Say "project $($project.attributes.name): $($_.Exception.Message)" }
    }
    foreach ($repo in @(GhJson @('repo', 'list', $Owner, '--limit', '200', '--json', 'name')) | Where-Object { $_.name -match '^verify-(a|b|full)$' }) {
        Exec gh @('repo', 'delete', "$Owner/$($repo.name)", '--yes') | Out-Null
    }
    $appId = Get-GitHubAppId
    foreach ($f in @(AzJson @('ad', 'app', 'federated-credential', 'list', '--id', $appId)) | Where-Object { $_.name -match '^verify-preview-' }) {
        Exec az @('ad', 'app', 'federated-credential', 'delete', '--id', $appId, '--federated-credential-id', $f.id, '--only-show-errors') | Out-Null
    }
    # Its own soft-deleted apps, users and user-flow links: what the audit reported, gone once reported.
    $flow = Get-UserFlowId
    $links = (Graph GET "/identity/authenticationEventsFlows/$flow/conditions/applications/includeApplications" -Beta).value.appId
    foreach ($d in (Graph GET '/directory/deletedItems/microsoft.graph.application?$select=id,appId,displayName&$top=200').value | Where-Object { $_.displayName -match $OursName -or $_.displayName -eq $RetryWorkspace }) {
        if ($d.appId -in $links) {
            try { Graph DELETE "/identity/authenticationEventsFlows/$flow/conditions/applications/includeApplications/$($d.appId)/`$ref" -Beta | Out-Null } catch { Say "link $($d.appId): $($_.Exception.Message)" }
        }
        try { Graph DELETE "/directory/deletedItems/$($d.id)" | Out-Null } catch { Say "purge $($d.displayName): $($_.Exception.Message)" }
    }
    foreach ($d in (Graph GET '/directory/deletedItems/microsoft.graph.user?$select=id,displayName&$top=200').value | Where-Object displayName -match '^QA j90') {
        try { Graph DELETE "/directory/deletedItems/$($d.id)" | Out-Null } catch { Say "purge $($d.displayName): $($_.Exception.Message)" }
    }
    Say 'cleanup done. preview_ver*_j90* roles remain until the next deploy''s sweep, as accepted'
}

function Invoke-Preflight {
    $problems = [Collections.Generic.List[string]]::new()
    $note = { param($ok, $text) if ($ok) { Write-Host "ok    $text" } else { Write-Host "STOP  $text"; $problems.Add($text) } }

    & $note (-not (Exec git @('-C', $Repo, 'status', '--porcelain'))) 'the repo has no uncommitted changes'
    & $note ([bool]$HcpToken) 'VERIFY_HCP_TOKEN is set'
    foreach ($tool in 'gh', 'az', 'docker', 'node', 'terraform') { & $note ([bool](Get-Command $tool -ErrorAction SilentlyContinue)) "$tool is installed" }
    & $note (Test-Path (Join-Path $PSScriptRoot 'node_modules\playwright')) 'npm ci has been run in tooling/verify'
    & $note ([bool](docker version --format '{{.Server.Version}}' 2>$null)) 'Docker is running'

    $scopes = (gh auth status 2>&1 | Select-String 'Token scopes') -join ''
    & $note ($scopes -match 'delete_repo') 'gh can delete repos, for cleanup (gh auth refresh -s delete_repo)'
    & $note ($scopes -match "'repo'") 'gh can create repos'

    $workspaces = @((Hcp GET "/organizations/$Org/workspaces?page%5Bsize%5D=100").data.attributes.name)
    $foreign = Get-LiveForeignPreviews $workspaces
    & $note (-not $foreign) "no other preview is live$(if ($foreign) { ": $($foreign -join ', ')" })"
    $lock = Test-Path 'D:\code\claude-factory\.factory-runs\canary.lock'
    & $note (-not $lock) 'no canary is running'
    $apps = @((AzJson @('resource', 'list', '--resource-type', 'Microsoft.App/containerApps', '--query', '[].name')) | Where-Object { $_ -notmatch '^ca-ver[abf]' })
    & $note (-not $apps) "no other container apps are running$(if ($apps) { ": $($apps -join ', ')" })"

    $mine = @($workspaces | Where-Object { $_ -match '^verify-' })
    & $note (-not $mine) "no verify workspaces are left from an earlier run$(if ($mine) { ": $($mine -join ', ')" })"
    $repos = @(GhJson @('repo', 'list', $Owner, '--limit', '200', '--json', 'name') | Where-Object { $_.name -match '^verify-(a|b|full)$' } | ForEach-Object name)
    & $note (-not $repos) "no verify repos are left from an earlier run$(if ($repos) { ": $($repos -join ', ')" })"

    $credentials = @(AzJson @('ad', 'app', 'federated-credential', 'list', '--id', (Get-GitHubAppId)))
    & $note ($credentials.Count + 3 -le 20) "room for 3 federated credentials ($($credentials.Count) of 20 used)"
    & $note ([bool](Get-UserFlowId)) 'the External tenant answers, and the user flow is readable'
    $platform = Get-Platform
    & $note ([bool]$platform.postgres_server_fqdn) "the platform is on the variable set: $($platform.postgres_server_name)"
    $names = @(AzJson @('keyvault', 'secret', 'list', '--vault-name', $platform.key_vault_name, '--query', '[].name'))
    & $note ($names -contains 'postgres-admin-password') 'the shared vault is readable and holds the admin password'

    if ($problems.Count) { Write-Host "`n$($problems.Count) thing(s) stop a run"; exit 1 }
    Write-Host "`nClear to run at $Short"
}

function Write-Report([string] $verdict, [string[]] $foreign) {
    $lines = @("# Deploy path verification at $Short", '', "**Verdict: $verdict**", '', "Commit ``$Sha``, finished $((Get-Date).ToUniversalTime().ToString('yyyy-MM-dd HH:mm')) UTC. Logs are in ``reports/$Short/``, which is not committed.", '')
    $lines += '| Check | Result | Evidence |', '|---|---|---|'
    foreach ($r in $script:Results) { $lines += "| $($r.Check) | $($r.Status) | $($r.Evidence.Replace('|', '\|')) |" }
    if ($foreign) { $lines += '', "Other activity during the run: $($foreign -join ', ')" }
    [IO.File]::WriteAllText((Join-Path $PSScriptRoot "reports\$Short.md"), (Clean ($lines -join "`n")) + "`n")
    Say "report: tooling/verify/reports/$Short.md"
}

function Invoke-Run {
    if (Exec git @('-C', $Repo, 'status', '--porcelain')) { throw 'commit first: the report says which commit it proves' }
    if (-not (Test-Path (Join-Path $PSScriptRoot 'node_modules\playwright'))) { throw 'run npm ci in tooling/verify first' }
    Use-PrivatePack
    if (-not (Test-Path (Join-Path $Work 'home'))) { Invoke-Prepare }
    if ($From -eq 1) {
        $script:State.baseline = Get-Inventory
        $script:State.foreign = Get-ForeignSnapshot
        Save-State
    }
    $bodies = @{ 1 = ${function:Check-1}; 2 = ${function:Check-2}; 3 = ${function:Check-3}; 4 = ${function:Check-4}; 5 = ${function:Check-5}; 6 = ${function:Check-6}; 7 = ${function:Check-7}; 8 = ${function:Check-8}; 9 = ${function:Check-9} }
    foreach ($n in $From..$Through) {
        Say "--- check $n"
        $before = $script:Results.Count
        try { & $bodies[$n] }
        catch { Add-Result $n 'fail' "harness error: $($_.Exception.Message) at $($_.InvocationInfo.ScriptLineNumber)" }
        Save-State
        if ($script:Results.Count -eq $before) { Add-Result $n 'fail' 'the check recorded nothing' }
        $failed = @($script:Results | Select-Object -Skip $before | Where-Object Status -eq 'fail')
        # 8's inventory is a report, not a blocker: everything is already down by the time it runs.
        if ($failed -and $n -ne 8) {
            Say "stopping at check $n. Previews stay up for diagnosis; -Mode Cleanup removes them, and HCP does within 24 hours regardless."
            Write-Report "not clean: check $n failed" @()
            exit 1
        }
    }
    if ($Through -lt 9) { Write-Report "partial: checks $From to $Through only" @(); return }
    Invoke-Cleanup
    $foreign = if ($script:State.foreign) { Get-ForeignChanges $script:State.foreign (Get-ForeignSnapshot) } else { @() }
    $endSha = (git -C $Repo rev-parse HEAD).Trim()
    $endClean = -not (Exec git @('-C', $Repo, 'status', '--porcelain'))
    $verdict = Get-PassVerdict -Results $script:Results -StartSha $Sha -EndSha $endSha -EndClean $endClean -From $From -ForeignChanges $foreign
    Write-Report $verdict $foreign
    Say "verdict: $verdict"
    if ($verdict -ne 'clean') { exit 1 }
}

# ---------- main ----------

New-Item -ItemType Directory -Force $Work, $LogDir | Out-Null
if ($HcpToken) { Secret $HcpToken | Out-Null }
$script:State = Load-State
switch ($Mode) {
    'Preflight' { Invoke-Preflight }
    'Prepare' {
        if (Exec git @('-C', $Repo, 'status', '--porcelain')) { throw 'commit first: the scaffolds say which commit they came from' }
        Invoke-Prepare
    }
    'Run' { Invoke-Run }
    'Cleanup' { Invoke-Cleanup }
}
