# Breaks each load-bearing line of the deploy path in a throwaway worktree and says whether an offline
# suite notices. A mutation that leaves everything green is a test gap. Exits 1 if any survives.
#
#   pwsh tooling/verify/mutations.ps1
#
# Works from HEAD, so commit first. Needs dotnet, terraform and Git's bash; no Azure and no Docker.
$ErrorActionPreference = 'Stop'

$workflow = 'base-project/.github/workflows/deploy.yml'
$script = 'base-project/.github/scripts/front-end-only.sh'
$entra = 'IaC/auth.entra-external.tf'
$postgres = 'IaC/modules/database-postgres/main.tf'

$mutations = @(
    @{ Name = 'retry block removed'; File = $entra
       Find = '(?s)  retry = \{.*?\r?\n  \}\r?\n'; With = '' }
    @{ Name = 'retry regex no longer matches the real error'; File = $entra
       Find = 'The application id \.\* is invalid'; With = 'The application id .* is not valid' }
    @{ Name = 'FRONT_END_ONLY guard removed from terraform apply'; File = $workflow
       Find = "(- name: terraform apply\r?\n)\s+if: env\.FRONT_END_ONLY != 'true'\r?\n"; With = '$1' }
    @{ Name = 'front-end-only no longer checks the environment was applied'; File = $workflow
       Find = 'elif \[ -z "\$\(terraform output -raw "\$key" 2>/dev/null \|\| true\)" \]; then'; With = 'elif false; then' }
    @{ Name = '--no-renames removed'; File = $script
       Find = 'diff --name-only --no-renames'; With = 'diff --name-only' }
    @{ Name = 'REVOKE removed'; File = $workflow
       Find = "(?m)^.*REVOKE ALL ON DATABASE.*\r?\n"; With = '' }
    @{ Name = 'role name no longer carries the preview id'; File = $postgres
       Find = '"preview_\$\{trimsuffix\(local\.database_prefix, "_"\)\}"'; With = '"preview_shared"' }
    @{ Name = 'sweep matches every role, not just preview_*'; File = $workflow
       Find = [regex]::Escape("r.rolname LIKE 'preview\_%' AND "); With = '' }
    @{ Name = 'lock removed from the role step'; File = $workflow
       Find = "SELECT pg_advisory_xact_lock\(\d+\);\r?\n(\s*SELECT format\('CREATE ROLE)"; With = '$1' }
    @{ Name = 'lock removed from the sweep'; File = $workflow
       Find = "SELECT pg_advisory_xact_lock\(\d+\);\r?\n(\s*SELECT format\('DROP ROLE)"; With = '$1' }
    @{ Name = 'role and ownership no longer one transaction'; File = $workflow
       Find = "(?m)^\s*BEGIN;\r?\n(\s*SELECT pg_advisory_xact_lock\(\d+\);\r?\n\s*SELECT format\('CREATE ROLE)"; With = '$1' }
)

$repo = (git rev-parse --show-toplevel).Trim()
$work = Join-Path ([IO.Path]::GetTempPath()) "verify-mutations-$([guid]::NewGuid().ToString('N').Substring(0, 8))"

function Test-Suites {
    # Returns the suites that went red. --no-build: the tests read these files at run time.
    $red = @()
    Push-Location $work
    try {
        dotnet test tooling/tooling.slnx --nologo -v q --no-build *> $null
        if ($LASTEXITCODE -ne 0) { $red += 'CLI' }
        Push-Location IaC
        try {
            terraform test -no-color '-var-file=envs/ephemeral.tfvars' *> $null
            if ($LASTEXITCODE -ne 0) { $red += 'Terraform' }
        } finally { Pop-Location }
    } finally { Pop-Location }
    , $red
}

# LF, whatever this machine's core.autocrlf says: nine CLI tests fail on a CRLF checkout, because their
# raw string literals take the source file's line endings and the editors under test insert LF.
git -C $repo -c core.autocrlf=false worktree add -q --detach $work HEAD
if ($LASTEXITCODE -ne 0) { throw 'git worktree add failed' }
try {
    Write-Host "HEAD $(git -C $work rev-parse --short HEAD), worktree $work"
    Push-Location $work
    try {
        dotnet build tooling/tooling.slnx --nologo -v q *> $null
        if ($LASTEXITCODE -ne 0) { throw 'baseline: dotnet build failed' }
        Push-Location IaC
        try {
            terraform init -backend=false -no-color *> $null
            if ($LASTEXITCODE -ne 0) { throw 'baseline: terraform init failed' }
        } finally { Pop-Location }
    } finally { Pop-Location }

    $baseline = Test-Suites
    if ($baseline.Count) { throw "baseline is not green before any mutation: $($baseline -join ', ')" }
    Write-Host 'baseline green'

    $survivors = @()
    foreach ($m in $mutations) {
        $path = Join-Path $work $m.File
        $before = [IO.File]::ReadAllText($path)
        $after = [regex]::new($m.Find).Replace($before, $m.With, 1)
        if ($after -ceq $before) { throw "mutation did not apply, so it would prove nothing: $($m.Name)" }
        [IO.File]::WriteAllText($path, $after)
        try { $red = Test-Suites } finally { [IO.File]::WriteAllText($path, $before) }
        if ($red.Count) { Write-Host ("caught    {0} ({1})" -f $m.Name, ($red -join ', ')) }
        else { Write-Host ("SURVIVED  {0}" -f $m.Name); $survivors += $m.Name }
    }
    Write-Host "$($mutations.Count - $survivors.Count) of $($mutations.Count) caught"
    if ($survivors.Count) { exit 1 }
} finally {
    # A test host that is still exiting can hold the folder open for a moment.
    foreach ($try in 1..6) {
        git -C $repo worktree remove --force $work 2>$null
        if ($LASTEXITCODE -eq 0) { break }
        Start-Sleep -Seconds 5
    }
    if ($LASTEXITCODE -ne 0) { Write-Warning "could not remove the worktree: $work" }
}
