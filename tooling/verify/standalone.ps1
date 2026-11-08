# A real deploy of a scaffold with nothing from the factory: an HCP organization of its own, two Entra
# apps of its own, a private repository of its own. This is what proves the scaffold works for a
# stranger, which live.ps1 (on the factory's platform) cannot.
#
#   pwsh tooling/verify/standalone.ps1 -Mode Setup      pack this commit, scaffold, create apps, HCP, repo
#   pwsh tooling/verify/standalone.ps1 -Mode Deploy     run deploy.yml for dev, then smoke-test it
#   pwsh tooling/verify/standalone.ps1 -Mode Teardown   destroy through HCP; Azure's alert rule included
#   pwsh tooling/verify/standalone.ps1 -Mode Cleanup    remove the apps, repo, workspace, project, varset
#
# Configured by two user-scope environment variables, never by this file:
#   STANDALONE_HCP_ORG     the HCP organization to use. Nothing else may live in it.
#   STANDALONE_HCP_TOKEN   an owners team token for that organization, and no other
# and needs az (Owner or User Access Administrator on the subscription, plus Application Administrator)
# and gh (repo scope; delete_repo for Cleanup) signed in. README.md, "Testing the IaC for real".
param(
    [Parameter(Mandatory)][ValidateSet('Setup', 'Deploy', 'Teardown', 'Cleanup')][string] $Mode,
    [string] $RepoName = 'verify-standalone',
    [string] $Location = 'centralus'
)
$ErrorActionPreference = 'Stop'
$PSNativeCommandArgumentPassing = 'Standard'
. (Join-Path $PSScriptRoot 'lib.ps1')

$Repo = (git -C $PSScriptRoot rev-parse --show-toplevel).Trim()
$Org = [Environment]::GetEnvironmentVariable('STANDALONE_HCP_ORG', 'User')
$Token = [Environment]::GetEnvironmentVariable('STANDALONE_HCP_TOKEN', 'User')
if (-not $Org -or -not $Token) { throw 'set STANDALONE_HCP_ORG and STANDALONE_HCP_TOKEN at user scope first' }
$Environment = 'dev'
$Name = 'Solo'
$Rg = 'rg-solo-dev'
$Workspace = "$RepoName-$Environment"
$Work = Join-Path ([IO.Path]::GetTempPath()) "verify-$RepoName"
$App = Join-Path $Work 'app'

function Say([string] $text) { Write-Host "$((Get-Date).ToUniversalTime().ToString('HH:mm:ss'))  $text" }
function Native([string] $what) { if ($LASTEXITCODE) { throw "$what exited $LASTEXITCODE" } }
function Hcp([string] $method, [string] $path, $body = $null) {
    $p = @{ Method = $method; Uri = "https://app.terraform.io/api/v2$path"; Headers = @{ Authorization = "Bearer $Token" }; ContentType = 'application/vnd.api+json' }
    if ($null -ne $body) { $p.Body = $body | ConvertTo-Json -Depth 10 -Compress }
    Invoke-RestMethod @p
}
function AzJson([string[]] $a) { $o = az @a -o json --only-show-errors; Native "az $($a[0..2] -join ' ')"; $o | ConvertFrom-Json }

# The token must reach this organization and nothing else, so nothing here can touch another one.
$visible = @((Hcp GET '/organizations').data.id)
if ($visible.Count -ne 1 -or $visible[0] -ne $Org) { throw "STANDALONE_HCP_TOKEN must see only $Org; it sees: $($visible -join ', ')" }
$Owner = (gh api user --jq .login); Native 'gh api user'
$Full = "$Owner/$RepoName"
$Account = AzJson @('account', 'show')

function Get-Workspace { try { (Hcp GET "/organizations/$Org/workspaces/$Workspace").data } catch { $null } }
function Get-Project { @((Hcp GET "/organizations/$Org/projects").data | Where-Object { $_.attributes.name -eq $RepoName })[0] }
function Get-Varset { @((Hcp GET "/organizations/$Org/varsets").data | Where-Object { $_.attributes.name -eq "$RepoName-azure" })[0] }

function Get-EntraApp([string] $suffix, [switch] $Create) {
    $display = "$RepoName-$suffix"
    $id = az ad app list --display-name $display --query '[0].appId' -o tsv --only-show-errors
    if (-not $id) {
        if (-not $Create) { return $null }
        $id = az ad app create --display-name $display --sign-in-audience AzureADMyOrg --query appId -o tsv --only-show-errors; Native 'az ad app create'
    }
    $sp = az ad sp show --id $id --query id -o tsv --only-show-errors 2>$null
    if (-not $sp -and $Create) { $sp = az ad sp create --id $id --query id -o tsv --only-show-errors; Native 'az ad sp create' }
    [pscustomobject]@{ Name = $display; AppId = $id; SpId = $sp }
}

function Add-FederatedCredential($app, [string] $name, [string] $issuer, [string] $subject) {
    if (@(AzJson @('ad', 'app', 'federated-credential', 'list', '--id', $app.AppId) | Where-Object name -eq $name).Count) { return }
    $f = New-TemporaryFile
    try {
        @{ name = $name; issuer = $issuer; subject = $subject; audiences = @('api://AzureADTokenExchange') } | ConvertTo-Json -Compress | Set-Content $f -Encoding utf8NoBOM
        az ad app federated-credential create --id $app.AppId --parameters "@$f" -o none --only-show-errors; Native 'az ad app federated-credential create'
    } finally { Remove-Item $f -ErrorAction SilentlyContinue }
}

function Add-SubscriptionRole($app, [string] $role) {
    $scope = "/subscriptions/$($Account.id)"
    if (@(AzJson @('role', 'assignment', 'list', '--assignee', $app.SpId, '--scope', $scope, '--role', $role)).Count) { return }
    # A new service principal takes a little while to be visible to role assignment.
    foreach ($i in 1..12) {
        az role assignment create --assignee-object-id $app.SpId --assignee-principal-type ServicePrincipal --role $role --scope $scope -o none --only-show-errors
        if (-not $LASTEXITCODE) { return }
        Start-Sleep -Seconds 10
    }
    throw "could not grant $role to $($app.Name)"
}

function Invoke-Setup {
    if (Test-Path $Work) { throw "$Work exists: run Cleanup first" }
    New-Item -ItemType Directory $Work | Out-Null
    $sha = (git -C $Repo rev-parse --short HEAD).Trim()
    Say "packing $sha into a private hive"
    pwsh -NoProfile -File (Join-Path $Repo 'templating\stage.ps1') | Out-Null; Native 'stage.ps1'
    dotnet pack (Join-Path $Repo 'packaging\AspireStarter.Templates.csproj') -c Release -o "$Work\nupkg" --nologo -v q | Out-Null; Native 'pack templates'
    dotnet pack (Join-Path $Repo 'tooling\AspireStarter.Cli') -c Release -o "$Work\nupkg" --nologo -v q | Out-Null; Native 'pack cli'
    @"
<configuration><packageSources><clear /><add key="local" value="$Work\nupkg" /><add key="nuget.org" value="https://api.nuget.org/v3/index.json" /></packageSources>
<packageSourceMapping><packageSource key="local"><package pattern="AspireStarter.*" /></packageSource><packageSource key="nuget.org"><package pattern="*" /></packageSource></packageSourceMapping></configuration>
"@ | Set-Content "$Work\tool.nuget.config"
    # Private hive, tool path and package cache, so the global ones stay as they are.
    $env:DOTNET_CLI_HOME = "$Work\home"; $env:PATH = "$Work\tools;$env:PATH"; $env:NUGET_PACKAGES = "$Work\nuget"
    dotnet tool install AspireStarter.Addon --tool-path "$Work\tools" --configfile "$Work\tool.nuget.config" | Out-Null; Native 'tool install'
    dotnet new install "$Work\nupkg\AspireStarter.Templates.1.0.0.nupkg" | Out-Null; Native 'dotnet new install'

    Say "scaffolding ${Name}: postgres + React, no auth"
    New-Item -ItemType Directory $App | Out-Null
    Push-Location $App
    try {
        dotnet new aspire-base -n $Name -o . --dbProvider postgres --resource Item | Out-Null; Native 'aspire-base'
        aspire-addon add ui-react | Out-Null; Native 'add ui-react'
        aspire-addon infra check | Out-Null; Native 'infra check'
        Push-Location IaC
        try {
            terraform init -backend=false -no-color | Out-Null; Native 'terraform init'
            terraform test -no-color '-var-file=envs/ephemeral.tfvars' | Out-Null; Native 'terraform test'
        } finally { Pop-Location }
        $tfvars = Join-Path $App "IaC\envs\$Environment.tfvars"
        [IO.File]::WriteAllText($tfvars, (Set-TfvarLines ([IO.File]::ReadAllText($tfvars)) ([ordered]@{ location = "`"$Location`"" })))
        git init -q -b main; git add -A; git -c user.name=verify -c user.email=verify@localhost commit -q -m "scaffold from template-projects $sha"; Native 'git commit'
    } finally { Pop-Location }

    Say 'Entra: one app for HCP (Contributor, User Access Administrator), one for GitHub (Contributor)'
    $hcp = Get-EntraApp 'hcp' -Create
    foreach ($phase in 'plan', 'apply') {
        Add-FederatedCredential $hcp "hcp-$phase" 'https://app.terraform.io' "organization:${Org}:project:${RepoName}:workspace:${Workspace}:run_phase:$phase"
    }
    Add-SubscriptionRole $hcp 'Contributor'
    Add-SubscriptionRole $hcp 'User Access Administrator'
    $gha = Get-EntraApp 'gha' -Create
    Add-SubscriptionRole $gha 'Contributor'

    Say "HCP: project $RepoName, workspace $Workspace, variable set $RepoName-azure"
    $project = Get-Project
    if (-not $project) { $project = (Hcp POST "/organizations/$Org/projects" @{ data = @{ type = 'projects'; attributes = @{ name = $RepoName } } }).data }
    if (-not (Get-Workspace)) {
        Hcp POST "/organizations/$Org/workspaces" @{ data = @{ type = 'workspaces'; attributes = @{ name = $Workspace; 'execution-mode' = 'remote' }; relationships = @{ project = @{ data = @{ type = 'projects'; id = $project.id } } } } } | Out-Null
    }
    if (-not (Get-Varset)) {
        $vs = (Hcp POST "/organizations/$Org/varsets" @{ data = @{ type = 'varsets'; attributes = @{ name = "$RepoName-azure"; global = $false } } }).data
        foreach ($v in @(
                @{ key = 'TFC_AZURE_PROVIDER_AUTH'; value = 'true'; category = 'env' },
                @{ key = 'TFC_AZURE_RUN_CLIENT_ID'; value = $hcp.AppId; category = 'env' },
                @{ key = 'ARM_SUBSCRIPTION_ID'; value = $Account.id; category = 'env' },
                @{ key = 'ARM_TENANT_ID'; value = $Account.tenantId; category = 'env' },
                @{ key = 'deployer_object_id'; value = $gha.SpId; category = 'terraform' })) {
            Hcp POST "/varsets/$($vs.id)/relationships/vars" @{ data = @{ type = 'vars'; attributes = $v } } | Out-Null
        }
        Hcp POST "/varsets/$($vs.id)/relationships/projects" @{ data = @(@{ type = 'projects'; id = $project.id }) } | Out-Null
    }

    Say "GitHub: private $Full, environment $Environment"
    gh repo view $Full --json name 2>$null | Out-Null
    if ($LASTEXITCODE) { gh repo create $Full --private | Out-Null; Native 'gh repo create' }
    gh api -X PUT "repos/$Full/environments/$Environment" --silent; Native 'environment'
    foreach ($v in @(@('AZURE_CLIENT_ID', $gha.AppId), @('AZURE_TENANT_ID', $Account.tenantId), @('AZURE_SUBSCRIPTION_ID', $Account.id))) {
        gh variable set $v[0] --env $Environment --repo $Full --body $v[1]; Native "variable $($v[0])"
    }
    gh variable set TF_CLOUD_ORGANIZATION --repo $Full --body $Org; Native 'TF_CLOUD_ORGANIZATION'
    $Token | gh secret set TF_API_TOKEN --env $Environment --repo $Full; Native 'TF_API_TOKEN'
    # A new repository presents an immutable subject, which the federated credential has to match.
    $oidc = gh api "repos/$Full/actions/oidc/customization/sub" | ConvertFrom-Json
    $prefix = if ($oidc.use_immutable_subject -and $oidc.sub_claim_prefix) { $oidc.sub_claim_prefix } else { "repo:$Full" }
    Add-FederatedCredential $gha "gha-$Environment" 'https://token.actions.githubusercontent.com' "${prefix}:environment:$Environment"

    git -C $App remote add origin "git@github.com:$Full.git"
    git -C $App push -q origin main; Native 'git push'
    Say 'set up; next: -Mode Deploy'
}

function Invoke-Deploy {
    $last = @(gh run list -R $Full -w deploy.yml -L 1 --json databaseId | ConvertFrom-Json)
    $lastId = if ($last.Count) { [long]$last[0].databaseId } else { 0 }
    foreach ($i in 1..12) { gh workflow run deploy.yml -R $Full --ref main -f "environment=$Environment" | Out-Null; if (-not $LASTEXITCODE) { break }; Start-Sleep 15 }
    Native 'gh workflow run'
    do { Start-Sleep 10; $run = @(gh run list -R $Full -w deploy.yml -L 1 --json databaseId,url | ConvertFrom-Json)[0] } while ([long]$run.databaseId -le $lastId)
    Say "run $($run.url)"
    gh run watch $run.databaseId -R $Full --interval 30 *> $null
    $r = gh run view $run.databaseId -R $Full --json jobs,conclusion | ConvertFrom-Json
    $r.jobs[0].steps | Where-Object conclusion -notin 'skipped', 'success' | ForEach-Object { Say "  $($_.conclusion): $($_.name)" }
    if ($r.conclusion -ne 'success') {
        throw "deploy $($r.conclusion). Read it with: gh run view $($run.databaseId) -R $Full --log-failed. A provider download timeout or an Azure resource that reads back missing is not the scaffold: rerun with gh run rerun, after importing or deleting anything left out of state"
    }

    $ws = Get-Workspace
    $outputs = @{}; (Hcp GET "/workspaces/$($ws.id)/current-state-version-outputs").data | ForEach-Object { $outputs[$_.attributes.name] = $_.attributes.value }
    $react = $outputs['react_url']
    $failed = 0
    foreach ($path in '/', '/api/Items') {
        $code = 'timeout'
        foreach ($i in 1..12) {
            try { $code = (Invoke-WebRequest "$react$path" -TimeoutSec 60 -UseBasicParsing).StatusCode; break }
            catch { $code = $_.Exception.Response.StatusCode.value__; if ($code -and $code -lt 500) { break }; Start-Sleep 15 }
        }
        Say "GET $react$path -> $code"
        if ($code -ne 200) { $failed++ }
    }
    if ($failed) { throw 'smoke test failed' }
    Say 'deployed and answering; next: -Mode Teardown'
}

function Invoke-Teardown {
    $ws = Get-Workspace
    if (-not $ws) { Say 'no workspace; nothing to destroy'; return }
    $run = (Hcp POST '/runs' @{ data = @{ type = 'runs'; attributes = @{ 'is-destroy' = $true; 'auto-apply' = $true; message = 'standalone.ps1 teardown' }; relationships = @{ workspace = @{ data = @{ type = 'workspaces'; id = $ws.id } } } } }).data
    Say "destroy $($run.id)"
    # dev refuses to delete a resource group that still holds anything, and Azure can add its
    # Failure Anomalies rule well after Application Insights - so keep removing it until the end.
    $end = (Get-Date).AddMinutes(60)
    do {
        Start-Sleep 30
        if ((az group exists -n $Rg) -eq 'true') {
            foreach ($rule in @(AzJson @('resource', 'list', '-g', $Rg, '--resource-type', 'microsoft.alertsmanagement/smartDetectorAlertRules'))) {
                az resource delete --ids $rule.id --only-show-errors; Say "deleted Azure's $($rule.name)"
            }
        }
        $status = (Hcp GET "/runs/$($run.id)").data.attributes.status
    } while ($status -notin 'applied', 'errored', 'discarded', 'canceled', 'planned_and_finished' -and (Get-Date) -lt $end)
    $left = @((Hcp GET "/workspaces/$($ws.id)/resources?page%5Bsize%5D=100").data).Count
    Say "destroy $status; $left resources in state; $Rg exists: $(az group exists -n $Rg)"
    if ($status -notin 'applied', 'planned_and_finished' -or $left) { throw 'teardown incomplete' }
}

function Invoke-Cleanup {
    $ws = Get-Workspace
    if ($ws -and @((Hcp GET "/workspaces/$($ws.id)/resources?page%5Bsize%5D=100").data).Count) { throw "$Workspace still manages resources: run Teardown first" }
    foreach ($suffix in 'hcp', 'gha') {
        $app = Get-EntraApp $suffix
        if (-not $app) { continue }
        if ($app.SpId) {
            foreach ($a in @(AzJson @('role', 'assignment', 'list', '--assignee', $app.SpId, '--all'))) { az role assignment delete --ids $a.id --only-show-errors }
        }
        az ad app delete --id $app.AppId --only-show-errors; Native "delete $($app.Name)"
        Say "deleted $($app.Name), its roles and its federated credentials"
    }
    gh repo view $Full --json name 2>$null | Out-Null
    if (-not $LASTEXITCODE) { gh repo delete $Full --yes; Native 'gh repo delete (needs the delete_repo scope)'; Say "deleted $Full" }
    if ($ws) { Hcp DELETE "/workspaces/$($ws.id)" | Out-Null; Say "deleted workspace $Workspace" }
    $vs = Get-Varset; if ($vs) { Hcp DELETE "/varsets/$($vs.id)" | Out-Null; Say "deleted variable set $RepoName-azure" }
    $project = Get-Project; if ($project) { Hcp DELETE "/projects/$($project.id)" | Out-Null; Say "deleted project $RepoName" }
    Remove-Item $Work -Recurse -Force -ErrorAction SilentlyContinue
    Say "clean; $Org itself is kept"
}

switch ($Mode) {
    'Setup' { Invoke-Setup }
    'Deploy' { Invoke-Deploy }
    'Teardown' { Invoke-Teardown }
    'Cleanup' { Invoke-Cleanup }
}
