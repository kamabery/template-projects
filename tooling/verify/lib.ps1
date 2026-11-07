# The decisions live.ps1 makes, kept free of the network so tests/lib.Tests.ps1 can run them.

# Sets each variable in a tfvars file's text: replaced where it is, added where it is not.
function Set-TfvarLines([string] $text, $values) {
    foreach ($name in $values.Keys) {
        $line = "$name = $($values[$name])"
        $pattern = "(?m)^\s*$([regex]::Escape($name))\s*=.*$"
        $text = if ($text -match $pattern) { [regex]::Replace($text, $pattern, $line) } else { $text.TrimEnd() + "`n$line`n" }
    }
    $text
}

function ConvertTo-Redacted([string] $text, [string[]] $secrets) {
    foreach ($s in $secrets) { if ($s) { $text = $text.Replace($s, '***') } }
    $text
}

function Get-ConnectionPassword([string] $connection) {
    $m = [regex]::Match($connection, '(?:^|;)Password=([^;]*)')
    if (-not $m.Success) { throw 'the connection string has no password' }
    $m.Groups[1].Value
}

# Everything in $after that was not in $before, as "category: item", except what $accepted matches.
function Compare-Inventory($before, $after, [string[]] $accepted = @()) {
    $left = foreach ($category in $after.Keys) {
        foreach ($item in $after[$category]) {
            if ($item -cnotin @($before[$category])) { "${category}: $item" }
        }
    }
    @($left | Where-Object { $line = $_; -not ($accepted | Where-Object { $line -match $_ }) } | Sort-Object)
}

# Two snapshots of name -> version. Anything different is activity this harness did not cause.
function Get-ForeignChanges($before, $after) {
    $changes = @()
    foreach ($k in $before.Keys) {
        if (-not $after.ContainsKey($k)) { $changes += "gone: $k" }
        elseif ($after[$k] -cne $before[$k]) { $changes += "changed: $k" }
    }
    foreach ($k in $after.Keys) { if (-not $before.ContainsKey($k)) { $changes += "new: $k" } }
    @($changes | Sort-Object)
}

function Get-LiveForeignPreviews([string[]] $workspaces) {
    @($workspaces | Where-Object { $_ -match '-j\d+$' -and $_ -notmatch '^verify-' })
}

# Which path a deploy.yml run took, from what GitHub says ran rather than from any log line.
function Get-DeployPath($run) {
    $step = @($run.jobs.steps | Where-Object name -ceq 'terraform apply') | Select-Object -First 1
    if (-not $step) { throw 'the run has no terraform apply step' }
    switch ($step.conclusion) {
        'success' { 'full' }
        'skipped' { 'front-end-only' }
        default { throw "terraform apply concluded '$($step.conclusion)'" }
    }
}

# Only one uninterrupted run of every check at one commit, with nothing else going on, is a clean pass.
function Get-PassVerdict($Results, [string] $StartSha, [string] $EndSha, [bool] $EndClean, [int] $From, [string[]] $ForeignChanges) {
    if ($ForeignChanges) { return "void: other activity during the run: $($ForeignChanges -join ', ')" }
    if ($From -ne 1) { return "not clean: started at check $From" }
    if ($StartSha -cne $EndSha) { return "not clean: the commit moved from $StartSha to $EndSha" }
    if (-not $EndClean) { return 'not clean: uncommitted changes at the end' }
    foreach ($n in 1..9) {
        $r = @($Results | Where-Object Check -eq $n)
        if (-not $r) { return "not clean: check $n did not run" }
        if ($r | Where-Object Status -notin 'pass', 'accepted-risk') { return "not clean: check $n failed" }
    }
    'clean'
}

# Check 7's whole configuration: the real resource, cut from the real file, on an app it is handed.
function New-RetryProbeConfig([string] $authTf, [string] $workspace, [string] $organization, [string] $timeout) {
    $m = [regex]::Match($authTf.Replace("`r`n", "`n"), '(?ms)^resource "msgraph_resource_action" "user_flow_link" \{.*?^\}$')
    if (-not $m.Success) { throw 'no user_flow_link resource in that file' }
    $block = $m.Value.Replace('azuread_application.spa.client_id', 'var.app_id')
    $block = [regex]::Replace($block, '(?m)^\s*depends_on\s*=.*\n?', '')
    $block = [regex]::Replace($block, 'create = "[^"]*"', "create = `"$timeout`"")
    @"
terraform {
  cloud {
    organization = "$organization"
    workspaces {
      name = "$workspace"
    }
  }
  required_providers {
    msgraph = {
      source  = "microsoft/msgraph"
      version = "0.6.0"
    }
  }
}

variable "tfc_azure_dynamic_credentials" {
  type = object({
    default = object({ client_id_file_path = string, oidc_token_file_path = string })
    aliases = map(object({ client_id_file_path = string, oidc_token_file_path = string }))
  })
  default = null
}

variable "entra_external_tenant_id" {
  type = string
}

variable "entra_external_user_flow_id" {
  type = string
}

variable "app_id" {
  type = string
}

locals {
  external_credentials = try(var.tfc_azure_dynamic_credentials.aliases["EXTERNAL"], null)
}

provider "msgraph" {
  tenant_id            = var.entra_external_tenant_id
  use_oidc             = true
  client_id_file_path  = try(local.external_credentials.client_id_file_path, null)
  oidc_token_file_path = try(local.external_credentials.oidc_token_file_path, null)
}

$block
"@
}
