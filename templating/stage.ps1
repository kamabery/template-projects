#Requires -Version 7.0
<#
.SYNOPSIS
    Assembles the dotnet new template content trees under artifacts/templates/.

.DESCRIPTION
    The live workspace is not laid out the way a scaffold should be: the solution, aspire.config.json
    and .aspire/ sit at the workspace root while the code sits under base-project/, and the events
    slice is a sibling directory that reaches back into base-project/ for servicedefaults. A scaffold
    wants none of that shape.

    Rather than express the difference as `replaces` symbols in template.json - which would mean
    rewriting path prefixes with blind string substitution across every packed file - this script
    stages each template into the layout a scaffold actually wants. Every path rewrite happens here,
    once, where it can be read and tested.

    It also strips the conditional regions whose symbols are statically false for a given template.
    aspire-base ships no React, no Blazor and no events source, so the //#if (includeReact) regions
    in apphost.cs and template.slnx can never be true there; removing them at staging time keeps
    those symbols out of template.json entirely, so nobody can pass a flag that produces a scaffold
    referencing files that were never packed. The dbProvider regions are deliberately left intact -
    those are resolved by dotnet new at instantiation time.

.EXAMPLE
    pwsh templating/stage.ps1
#>
[CmdletBinding()]
param(
    [string] $OutputRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if (-not $OutputRoot) { $OutputRoot = Join-Path $RepoRoot 'artifacts/templates' }

# Build output and package caches must never reach a template. node_modules in particular would add
# tens of thousands of files to the nupkg.
$SkipDirs = @('bin', 'obj', 'node_modules', 'dist', 'dev-dist', '.tanstack', '.vite', '.git', 'artifacts', '.terraform')

# Local run artifacts that are not source. The sqlite branch writes its store beside the built API,
# and a developer who ran the API before packing would otherwise ship their data in the template.
# Terraform state is added for a second reason on top of "not source": it holds the database
# password Terraform generated.
$SkipFilePatterns = @('app.db', 'app.db-shm', 'app.db-wal', '*.user', '*.suo',
                      '*.tfstate', '*.tfstate.*', '*.tfplan')

function Get-StageableFiles {
    param([Parameter(Mandatory)][string] $Root)

    if (-not (Test-Path -LiteralPath $Root)) { throw "Source root not found: $Root" }

    Get-ChildItem -LiteralPath $Root -Recurse -File -Force | Where-Object {
        $rel = [IO.Path]::GetRelativePath($Root, $_.FullName)
        $parts = $rel -split '[\\/]'
        # Compare all but the file name against the skip list.
        $dirParts = $parts[0..([Math]::Max(0, $parts.Length - 2))]
        if ($parts.Length -eq 1) { $dirParts = @() }
        if ($dirParts | Where-Object { $SkipDirs -contains $_ }) { return $false }

        foreach ($pattern in $SkipFilePatterns) {
            if ($_.Name -like $pattern) { return $false }
        }
        return $true
    }
}

function Copy-Tree {
    param(
        [Parameter(Mandatory)][string] $From,
        [Parameter(Mandatory)][string] $To,
        [string[]] $ExcludePrefixes = @()
    )

    $count = 0
    foreach ($file in Get-StageableFiles -Root $From) {
        $rel = [IO.Path]::GetRelativePath($From, $file.FullName).Replace('\', '/')

        $skip = $false
        foreach ($prefix in $ExcludePrefixes) {
            if ($rel -eq $prefix -or $rel.StartsWith($prefix.TrimEnd('/') + '/')) { $skip = $true; break }
        }
        if ($skip) { continue }

        $dest = Join-Path $To $rel
        $destDir = Split-Path -Parent $dest
        if (-not (Test-Path -LiteralPath $destDir)) { New-Item -ItemType Directory -Path $destDir -Force | Out-Null }
        Copy-Item -LiteralPath $file.FullName -Destination $dest -Force
        $count++
    }
    return $count
}

function Edit-StagedFile {
    <#
        Literal, ordered string replacements against one staged file. Pairs are applied in order;
        every pair must match at least once, so a rename in the live workspace fails the build here
        rather than producing a silently wrong scaffold.
    #>
    param(
        [Parameter(Mandatory)][string] $Path,
        # AllowEmptyString: the flattening rewrite replaces the "base-project/" prefix with nothing,
        # and PowerShell rejects empty elements in a mandatory string[] without it.
        [Parameter(Mandatory)][AllowEmptyString()][string[]] $Pairs
    )

    if (-not (Test-Path -LiteralPath $Path)) { throw "Cannot rewrite missing file: $Path" }
    $text = [IO.File]::ReadAllText($Path)

    for ($i = 0; $i -lt $Pairs.Length; $i += 2) {
        $find = $Pairs[$i]
        $replace = $Pairs[$i + 1]
        if (-not $text.Contains($find)) {
            throw "Rewrite target not found in $Path`n  looking for: $find"
        }
        $text = $text.Replace($find, $replace)
    }

    [IO.File]::WriteAllText($Path, $text)
}

function Remove-FalseRegions {
    <#
        Resolves //#if / <!--#if --> regions whose symbols are statically false for this template.

        A condition mentioning only false symbols has its whole region removed. A condition that
        also mentions dbProvider is simplified rather than removed - "(dbProvider == 'postgres' ||
        includeEvents)" becomes "(dbProvider == 'postgres')" - because dbProvider is still a real
        parameter that dotnet new resolves later.
    #>
    param(
        [Parameter(Mandatory)][string] $Path,
        [Parameter(Mandatory)][string[]] $FalseSymbols
    )

    $lines = [IO.File]::ReadAllLines($Path)
    $out = [System.Collections.Generic.List[string]]::new()

    # Depth of regions we are currently discarding; 0 means we are emitting.
    $suppressDepth = 0
    $nesting = [System.Collections.Generic.Stack[bool]]::new()   # true = this region was kept

    foreach ($line in $lines) {
        $trimmed = $line.Trim()

        $ifMatch = [regex]::Match($trimmed, '^(?://#if\s*\((?<cond>.*)\)|<!--#if\s*\((?<cond>.*)\)\s*-->)$')
        $endMatch = $trimmed -match '^(?://#endif|<!--#endif\s*-->)$'

        if ($ifMatch.Success) {
            $cond = $ifMatch.Groups['cond'].Value

            $simplified = $cond
            foreach ($sym in $FalseSymbols) {
                $simplified = [regex]::Replace($simplified, "\b$([regex]::Escape($sym))\b", 'false')
            }
            # Collapse the disjunctions the false symbols left behind.
            $simplified = $simplified -replace '\s*\|\|\s*false\b', ''
            $simplified = $simplified -replace '\bfalse\s*\|\|\s*', ''
            $simplified = $simplified.Trim()

            $isFalse = ($simplified -eq 'false')

            if ($suppressDepth -gt 0 -or $isFalse) {
                $suppressDepth++
                $nesting.Push($false)
            }
            else {
                $nesting.Push($true)
                if ($simplified -ne $cond) {
                    # Condition changed but is still live: re-emit it in the original comment style.
                    if ($trimmed.StartsWith('<!--')) { $out.Add("<!--#if ($simplified) -->") }
                    else { $out.Add("//#if ($simplified)") }
                }
                else {
                    $out.Add($line)
                }
            }
            continue
        }

        if ($endMatch) {
            if ($nesting.Count -eq 0) { throw "Unbalanced #endif in $Path" }
            $wasKept = $nesting.Pop()
            if (-not $wasKept) { $suppressDepth-- } else { $out.Add($line) }
            continue
        }

        if ($suppressDepth -eq 0) { $out.Add($line) }
    }

    if ($nesting.Count -ne 0) { throw "Unclosed #if region in $Path" }

    # A removed region leaves the blank lines that separated it behind. Collapse any run of three
    # or more into one so the staged file reads like something a person wrote.
    $collapsed = [System.Collections.Generic.List[string]]::new()
    $blankRun = 0
    foreach ($line in $out) {
        if ([string]::IsNullOrWhiteSpace($line)) {
            $blankRun++
            if ($blankRun -ge 2) { continue }
        }
        else { $blankRun = 0 }
        $collapsed.Add($line)
    }

    [IO.File]::WriteAllLines($Path, $collapsed)
}

function Copy-IacFiles {
    <#
        The Terraform configuration is the one part of this workspace an add-on contributes to
        without a merge step: Terraform loads every *.tf in a directory, so an add-on simply drops
        its own file in beside the base's. That is why there is no <<addon->> marker for any of
        this, and why the wiring files have almost nothing to say about it.

        IaC/ lives at the workspace root rather than under base-project/ because it spans both
        base-project/ and event-project/. It is staged explicitly here, the same way template.slnx
        and .aspire/ are.
    #>
    param(
        [Parameter(Mandatory)][string] $Root,
        [Parameter(Mandatory)][string[]] $Files
    )

    $source = Join-Path $RepoRoot 'IaC'
    foreach ($rel in $Files) {
        $from = Join-Path $source $rel
        if (-not (Test-Path -LiteralPath $from)) { throw "Missing IaC file: $from" }

        $to = Join-Path $Root (Join-Path 'IaC' $rel)
        $toDir = Split-Path -Parent $to
        if (-not (Test-Path -LiteralPath $toDir)) { New-Item -ItemType Directory -Path $toDir -Force | Out-Null }
        Copy-Item -LiteralPath $from -Destination $to -Force
    }
}

function Assert-TerraformSubstitutionSafe {
    <#
        sourceName is the bare word this workspace prefixes its projects with, and dotnet new
        rewrites it in file *contents* as well as file names. azurerm_container_app declares a block
        whose name is exactly that word, so any .tf file containing one must be listed as copyOnly in
        its template.json - substitution would rename the block and the configuration would stop
        parsing.

        Every other .tf file must therefore not contain the token at all. Checking it here means a
        stray mention in a comment fails the build rather than producing a scaffold that does not
        plan.
    #>
    param(
        [Parameter(Mandatory)][string] $Root,
        [string[]] $CopyOnly = @()
    )

    $iac = Join-Path $Root 'IaC'
    if (-not (Test-Path -LiteralPath $iac)) { return }

    $token = 'temp' + 'late'   # split so this check does not trip over its own source
    foreach ($file in Get-ChildItem -LiteralPath $iac -Recurse -File -Filter *.tf) {
        $rel = [IO.Path]::GetRelativePath($Root, $file.FullName).Replace('\', '/')
        if ($CopyOnly -contains $rel) { continue }

        $pattern = '\b' + $token + '\b'
        if ([IO.File]::ReadAllText($file.FullName) -match $pattern) {
            throw @"
$rel contains the bare sourceName token and is not listed as copyOnly.
  Either reword it, or add it to the copyOnly list in this template's template.json AND to the
  list passed to Assert-TerraformSubstitutionSafe in stage.ps1.
"@
        }
    }
}

function Assert-ResourceTokenSafe {
    <#
        The resource parameter substitutes the bare words Widget and widget in file contents and
        file names, exactly as sourceName does for the project prefix. That is only safe where the
        token means the sample resource and nothing else.

        Two kinds of file are dangerous. A copyOnly file is not substituted at all, so a Widget in
        one would ship verbatim into a scaffold that asked for Product. And package-lock.json,
        already copyOnly for the @babel/template reason, must not grow a dependency whose name
        contains the token either.

        Checking it here means a stray mention fails the build rather than producing a scaffold
        that half-renames.
    #>
    param(
        [Parameter(Mandatory)][string] $Root,
        [string[]] $CopyOnly = @()
    )

    if ($CopyOnly.Count -eq 0) { return }

    $token = 'Wid' + 'get'   # split so this check does not trip over its own source
    foreach ($rel in $CopyOnly) {
        $path = Join-Path $Root $rel
        if (-not (Test-Path -LiteralPath $path)) { continue }

        if ([IO.File]::ReadAllText($path) -match ('(?i)' + $token + '')) {
            throw @"
$rel is copyOnly and contains the resource token, which is never substituted there.
  Reword it, or drop the file from the copyOnly list in this template's template.json.
"@
        }
    }
}

function Assert-ProseSubstitutionSafe {
    <#
        sourceName substitution is a plain substring rewrite rather than a word-boundary one, and it
        runs over prose as readily as over code. Every occurrence of the token in a staged file is
        rewritten, including the ones meant as English rather than as a project name: "templating",
        the HCL function whose name ends in "file()", and "a starter template" all reach a scaffold
        with the project's own name spliced into the middle of the word.

        The mirror image is just as quiet. The three lowercase-prefixed templates do not substitute a
        PascalCase occurrence at all, so one that names a project ships verbatim and points at an
        assembly the scaffold never had. That is the gap the nameBlazorProject symbol closes, and
        this is what stops the next one being written.

        The token is legitimate only immediately before a name this workspace gives a project, a
        solution or the React app. Anywhere else it is one of those two bugs. .tf files are left to
        Assert-TerraformSubstitutionSafe, which holds them to the stricter rule of no token at all,
        and .tfvars is skipped because the bare token there IS the value being substituted.
    #>
    param(
        [Parameter(Mandatory)][string] $Root,
        [string[]] $CopyOnly = @()
    )

    $token      = 'temp' + 'late'   # split so this check does not trip over its own source
    $allowed    = "^$token\.(api|data|servicedefaults|events|slnx|ui|blazor)"
    $configDir  = ".$token.config/"
    $extensions = @(
        '.md', '.cs', '.razor', '.ts', '.tsx', '.css', '.html',
        '.json', '.props', '.csproj', '.slnx', '.yml', '.http'
    )

    $findings = @()

    foreach ($file in Get-ChildItem -LiteralPath $Root -Recurse -File) {
        if ($extensions -notcontains $file.Extension) { continue }

        $rel = [IO.Path]::GetRelativePath($Root, $file.FullName).Replace('\', '/')
        if ($CopyOnly -contains $rel) { continue }
        if ($rel.StartsWith($configDir)) { continue }

        $number = 0
        foreach ($line in [IO.File]::ReadAllLines($file.FullName)) {
            $number++
            foreach ($hit in [regex]::Matches($line, $token, 'IgnoreCase')) {
                if ($line.Substring($hit.Index) -match $allowed) { continue }

                $from    = [Math]::Max(0, $hit.Index - 30)
                $length  = [Math]::Min($line.Length - $from, 70)
                $findings += "  ${rel}:${number}  ...$($line.Substring($from, $length).Trim())..."
            }
        }
    }

    if ($findings.Count -gt 0) {
        throw @"
$($findings.Count) occurrence(s) of the bare sourceName token outside a project name:
$($findings -join "`n")
  Substitution rewrites each of these, so the word reaches a scaffold with the project's name
  spliced into it. Reword so the token does not appear - refer to the thing indirectly if you
  have to, the way IaC/README.md now describes the HCL function it must not name.
"@
    }
}

function Add-TemplateConfig {
    param(
        [Parameter(Mandatory)][string] $Name,
        [Parameter(Mandatory)][string] $Root
    )

    $src = Join-Path $PSScriptRoot "$Name/template.json"
    if (-not (Test-Path -LiteralPath $src)) { throw "Missing template.json for $Name at $src" }

    $dir = Join-Path $Root '.template.config'
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
    Copy-Item -LiteralPath $src -Destination (Join-Path $dir 'template.json') -Force

    # The wiring instructions an add-on template hands to the scaffolding skill. A template may ship
    # more than one - aspire-events has a variant per event store - in which case its template.json
    # excludes the ones that do not apply, so exactly one reaches a scaffold's .scaffold/.
    $wiringFiles = @(Get-ChildItem -Path (Join-Path $PSScriptRoot 'wiring') -Filter "$Name*.wiring.md" -File -ErrorAction SilentlyContinue)
    if ($wiringFiles.Count -gt 0) {
        $scaffoldDir = Join-Path $Root '.scaffold'
        New-Item -ItemType Directory -Path $scaffoldDir -Force | Out-Null
        foreach ($file in $wiringFiles) {
            Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $scaffoldDir $file.Name) -Force
        }
    }
}

function Add-DesignDoc {
    # docs/DESIGN.md owns the design rules for this workspace AND for every scaffold made from it, so
    # a UI template that did not carry it would hand someone a style guide with no statement of what
    # it is for. It lands at the scaffold's own docs/DESIGN.md, where CLAUDE.md's links resolve.
    # Unlike the wiring files this is not optional: throw rather than stage a UI with no doctrine.
    param([Parameter(Mandatory)][string] $Root)

    $src = Join-Path $RepoRoot 'docs/DESIGN.md'
    if (-not (Test-Path -LiteralPath $src)) { throw "Missing docs/DESIGN.md at $src" }

    $dir = Join-Path $Root 'docs'
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
    Copy-Item -LiteralPath $src -Destination (Join-Path $dir 'DESIGN.md') -Force
}

# --------------------------------------------------------------------------------------------------

if (Test-Path -LiteralPath $OutputRoot) { Remove-Item -LiteralPath $OutputRoot -Recurse -Force }
New-Item -ItemType Directory -Path $OutputRoot -Force | Out-Null

$baseProject = Join-Path $RepoRoot 'base-project'
$eventProject = Join-Path $RepoRoot 'event-project'

# --- aspire-base ----------------------------------------------------------------------------------
# The solution, the AppHost, the API, the data project, servicedefaults, every migrations project and
# the API test suite. base-project/ is flattened onto the root, which is what makes the scaffold a
# normal-looking repository rather than one with a directory called "base-project" in it.
$base = Join-Path $OutputRoot 'aspire-base'
New-Item -ItemType Directory -Path $base -Force | Out-Null

foreach ($f in @('template.slnx', 'aspire.config.json', 'nuget.config')) {
    Copy-Item -LiteralPath (Join-Path $RepoRoot $f) -Destination (Join-Path $base $f) -Force
}
# keycloak/ is the auth add-on's realm import, staged with that add-on, never with the base.
$n = Copy-Tree -From (Join-Path $RepoRoot '.aspire') -To (Join-Path $base '.aspire') -ExcludePrefixes @('keycloak')
$n += Copy-Tree -From $baseProject -To $base -ExcludePrefixes @(
    'src/ui',
    'tests/Template.Blazor.Maui.Tests',
    # The workspace README documents every option this repo carries, most of which a given scaffold
    # does not have. The scaffolding skill writes a README describing what was actually chosen.
    'README.md'
)

# The solution's README entry goes with it: a hand-driven `dotnet new aspire-base` never writes a
# README, and a solution item naming a missing file is a dangling entry. The skill adds it back
# when it writes one. Line-based so it works whatever line endings the checkout has.
$slnx = Join-Path $base 'template.slnx'
$slnxText = [IO.File]::ReadAllText($slnx)
$newline = if ($slnxText.Contains("`r`n")) { "`r`n" } else { "`n" }
$slnxLines = $slnxText -split "`r?`n"
$kept = @($slnxLines | Where-Object { $_.Trim() -ne '<File Path="base-project/README.md" />' })
if ($kept.Count -ne $slnxLines.Count - 1) { throw "Expected exactly one README solution item in $slnx" }
[IO.File]::WriteAllText($slnx, $kept -join $newline)

# The two path rewrites the flattening implies.
Edit-StagedFile -Path $slnx -Pairs @(
    'base-project/', ''
)
Edit-StagedFile -Path (Join-Path $base '.aspire/apphost.cs') -Pairs @(
    '../base-project/src/', '../src/'
)

# React, Blazor and events are separate templates; their regions can never be true here.
$falseForBase = @('includeReact', 'includeBlazorWeb', 'includeMaui', 'includeEventsMarten', 'includeEventsPolecat', 'includeEventsFisher', 'includeAuth')
foreach ($rel in @('template.slnx', '.aspire/apphost.cs', '.aspire/appsettings.json')) {
    Remove-FalseRegions -Path (Join-Path $base $rel) -FalseSymbols $falseForBase
}

# The Terraform configuration, minus the five files the add-ons own. Nothing rewrites paths here:
# every path inside IaC/ is already relative to the scaffold root.
$n += Copy-Tree -From (Join-Path $RepoRoot 'IaC') -To (Join-Path $base 'IaC') -ExcludePrefixes @(
    'frontend.react.tf',
    'frontend.blazor-web.tf',
    'containerapp.events.tf',
    'servicebus.tf',
    'functions.tf',
    'tests/events.tftest.hcl',
    'auth.entra-external.tf',
    'auth.redirects.react.tf',
    'auth.redirects.blazor-web.tf',
    'tests/auth.tftest.hcl'
)
Assert-TerraformSubstitutionSafe -Root $base -CopyOnly @('IaC/containerapp.api.tf')
Assert-ResourceTokenSafe -Root $base -CopyOnly @('IaC/containerapp.api.tf')

Add-TemplateConfig -Name 'aspire-base' -Root $base

# The scaffold's provenance: what it was built with, read and updated by the aspire-addon CLI so a
# late add-on gets the same -n, --resource and data store the base did. Written here rather than
# kept in base-project/, which would leave the live workspace carrying a file that claims it is a
# scaffold. Each value is a token template.json substitutes: the name verbatim (contents only ever
# see the lowercase form, and the Blazor add-on needs the original casing), the resource, and the
# data store. The pack version comes from the one place it is declared.
$packVersion = ([xml](Get-Content -Raw (Join-Path $RepoRoot 'packaging/AspireStarter.Templates.csproj'))).Project.PropertyGroup[0].Version
[IO.File]::WriteAllText((Join-Path $base '.aspirestarter.json'), @"
{
  "name": "AspireStarterProjectName",
  "resource": "Widget",
  "dbProvider": "postgres",
  "packVersion": "$packVersion",
  "addOns": []
}

"@)

Assert-ProseSubstitutionSafe -Root $base -CopyOnly @('IaC/containerapp.api.tf')
Write-Host "aspire-base       $n files"

# --- aspire-ui-react ------------------------------------------------------------------------------
$react = Join-Path $OutputRoot 'aspire-ui-react'
$n = Copy-Tree -From (Join-Path $baseProject 'src/ui/react') -To (Join-Path $react 'src/ui/react')
Copy-IacFiles -Root $react -Files @('frontend.react.tf')
Assert-TerraformSubstitutionSafe -Root $react
Assert-ResourceTokenSafe -Root $react -CopyOnly @('src/ui/react/package-lock.json')
Add-TemplateConfig -Name 'aspire-ui-react' -Root $react
Add-DesignDoc -Root $react
Assert-ProseSubstitutionSafe -Root $react -CopyOnly @('src/ui/react/package-lock.json')
Write-Host "aspire-ui-react   $n files"

# --- aspire-ui-blazor -----------------------------------------------------------------------------
# The Pages RCL plus whichever hosts were asked for, and the bUnit suite that covers them.
$blazor = Join-Path $OutputRoot 'aspire-ui-blazor'
$n = Copy-Tree -From (Join-Path $baseProject 'src/ui/blazor') -To (Join-Path $blazor 'src/ui/blazor')
$n += Copy-Tree -From (Join-Path $baseProject 'tests/Template.Blazor.Maui.Tests') `
                -To (Join-Path $blazor 'tests/Template.Blazor.Maui.Tests')
# Only the WebAssembly host is a deployable web front end; template.json drops this file when
# --web false. The MAUI host provisions nothing at all - it is a client, not a workload.
Copy-IacFiles -Root $blazor -Files @('frontend.blazor-web.tf')
$blazorCopyOnly = @('src/ui/blazor/Template.Blazor.Maui.Pages/wwwroot/app.css')
Assert-TerraformSubstitutionSafe -Root $blazor
Assert-ResourceTokenSafe -Root $blazor -CopyOnly $blazorCopyOnly
Add-TemplateConfig -Name 'aspire-ui-blazor' -Root $blazor
Add-DesignDoc -Root $blazor
Assert-ProseSubstitutionSafe -Root $blazor -CopyOnly $blazorCopyOnly
Write-Host "aspire-ui-blazor  $n files"

# --- aspire-events --------------------------------------------------------------------------------
# Folded into src/ and tests/ rather than kept as a sibling directory. That is what retires the
# cross-boundary ProjectReference: servicedefaults becomes a normal sibling one level up.
# event-project's own Directory.Build.props and Directory.Packages.props are deliberately not copied
# - a scaffold has one of each at its root, and the wiring step adds the event store's versions.
#
# All three hosts are staged and template.json prunes two of them; src/shared/ always survives and
# is LINKED into whichever host remains rather than being a project of its own. They cannot be one
# project with conditional regions, because a project may reference exactly one event store -
# JasperFx.Events ships a source generator that emits a type per projection, so two stores in one
# project fail to compile. And the shared half cannot be its own assembly either, because that same
# generator has to see the aggregate and the store in one compilation; see src/shared/README.md.
$events = Join-Path $OutputRoot 'aspire-events'
# template.events.functions is never pruned: it is the store-agnostic widget-completed consumer.
$eventHosts = @('shared', 'template.events', 'template.events.polecat', 'template.events.fisher',
                'template.events.functions')

$n = 0
foreach ($project in $eventHosts) {
    $n += Copy-Tree -From (Join-Path $eventProject "src/$project") -To (Join-Path $events "src/$project")
}
$n += Copy-Tree -From (Join-Path $eventProject 'tests/template.events.tests') `
                -To (Join-Path $events 'tests/template.events.tests')

# The one path the flattening invalidates, in each host that carries it. The shared domain project
# has no reference to base-project/, so it is deliberately absent from this list.
foreach ($project in @('template.events', 'template.events.polecat', 'template.events.fisher',
                       'template.events.functions')) {
    Edit-StagedFile -Path (Join-Path $events "src/$project/$project.csproj") -Pairs @(
        '..\..\..\base-project\src\template.servicedefaults\', '..\template.servicedefaults\'
    )
}

# All three ship on every event store: every host publishes to Service Bus once deployed, and the
# Function that consumes it is store-agnostic.
Copy-IacFiles -Root $events -Files @('containerapp.events.tf', 'servicebus.tf', 'functions.tf', 'tests/events.tftest.hcl')
Assert-TerraformSubstitutionSafe -Root $events -CopyOnly @('IaC/containerapp.events.tf')
Assert-ResourceTokenSafe -Root $events -CopyOnly @('IaC/containerapp.events.tf')

Add-TemplateConfig -Name 'aspire-events' -Root $events
Assert-ProseSubstitutionSafe -Root $events -CopyOnly @('IaC/containerapp.events.tf')
Write-Host "aspire-events     $n files"

# --- aspire-auth ----------------------------------------------------------------------------------
# The identity provider only: the API and both front ends already speak OIDC once Auth:* is set, so
# this is Keycloak's realm for the AppHost and Entra External ID for Azure. Both redirect files are
# staged and template.json keeps the one for the scaffold's front end.
$auth = Join-Path $OutputRoot 'aspire-auth'
$n = Copy-Tree -From (Join-Path $RepoRoot '.aspire/keycloak') -To (Join-Path $auth '.aspire/keycloak')
$authIac = @('auth.entra-external.tf', 'auth.redirects.react.tf', 'auth.redirects.blazor-web.tf', 'tests/auth.tftest.hcl')
Copy-IacFiles -Root $auth -Files $authIac
$n += $authIac.Count
Assert-TerraformSubstitutionSafe -Root $auth
Add-TemplateConfig -Name 'aspire-auth' -Root $auth
Assert-ProseSubstitutionSafe -Root $auth
Write-Host "aspire-auth       $n files"

Write-Host ""
Write-Host "Staged to $OutputRoot"
