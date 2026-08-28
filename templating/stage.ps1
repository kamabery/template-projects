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
$SkipDirs = @('bin', 'obj', 'node_modules', 'dist', 'dev-dist', '.tanstack', '.vite', '.git', 'artifacts')

# Local run artifacts that are not source. The sqlite branch writes its store beside the built API,
# and a developer who ran the API before packing would otherwise ship their data in the template.
$SkipFilePatterns = @('weather.db', 'weather.db-shm', 'weather.db-wal', '*.user', '*.suo')

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

    # The wiring instructions an add-on template hands to the scaffolding skill.
    $wiring = Join-Path $PSScriptRoot "wiring/$Name.wiring.md"
    if (Test-Path -LiteralPath $wiring) {
        $scaffoldDir = Join-Path $Root '.scaffold'
        New-Item -ItemType Directory -Path $scaffoldDir -Force | Out-Null
        Copy-Item -LiteralPath $wiring -Destination (Join-Path $scaffoldDir "$Name.wiring.md") -Force
    }
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
$n = Copy-Tree -From (Join-Path $RepoRoot '.aspire') -To (Join-Path $base '.aspire')
$n += Copy-Tree -From $baseProject -To $base -ExcludePrefixes @(
    'src/ui',
    'tests/Template.Blazor.Maui.Tests',
    # The workspace README documents every option this repo carries, most of which a given scaffold
    # does not have. The scaffolding skill writes a README describing what was actually chosen; the
    # solution's <File Path="README.md" /> entry points at that one.
    'README.md'
)

# The two path rewrites the flattening implies.
Edit-StagedFile -Path (Join-Path $base 'template.slnx') -Pairs @(
    'base-project/', ''
)
Edit-StagedFile -Path (Join-Path $base '.aspire/apphost.cs') -Pairs @(
    '../base-project/src/', '../src/'
)

# React, Blazor and events are separate templates; their regions can never be true here.
$falseForBase = @('includeReact', 'includeBlazorWeb', 'includeMaui', 'includeEvents')
foreach ($rel in @('template.slnx', '.aspire/apphost.cs', '.aspire/appsettings.json')) {
    Remove-FalseRegions -Path (Join-Path $base $rel) -FalseSymbols $falseForBase
}

Add-TemplateConfig -Name 'aspire-base' -Root $base
Write-Host "aspire-base       $n files"

# --- aspire-ui-react ------------------------------------------------------------------------------
$react = Join-Path $OutputRoot 'aspire-ui-react'
$n = Copy-Tree -From (Join-Path $baseProject 'src/ui/react') -To (Join-Path $react 'src/ui/react')
Add-TemplateConfig -Name 'aspire-ui-react' -Root $react
Write-Host "aspire-ui-react   $n files"

# --- aspire-ui-blazor -----------------------------------------------------------------------------
# The Pages RCL plus whichever hosts were asked for, and the bUnit suite that covers them.
$blazor = Join-Path $OutputRoot 'aspire-ui-blazor'
$n = Copy-Tree -From (Join-Path $baseProject 'src/ui/blazor') -To (Join-Path $blazor 'src/ui/blazor')
$n += Copy-Tree -From (Join-Path $baseProject 'tests/Template.Blazor.Maui.Tests') `
                -To (Join-Path $blazor 'tests/Template.Blazor.Maui.Tests')
Add-TemplateConfig -Name 'aspire-ui-blazor' -Root $blazor
Write-Host "aspire-ui-blazor  $n files"

# --- aspire-events --------------------------------------------------------------------------------
# Folded into src/ and tests/ rather than kept as a sibling directory. That is what retires the
# cross-boundary ProjectReference: servicedefaults becomes a normal sibling one level up.
# event-project's own Directory.Build.props and Directory.Packages.props are deliberately not copied
# - a scaffold has one of each at its root, and the wiring step adds the Wolverine/Marten versions.
$events = Join-Path $OutputRoot 'aspire-events'
$n = Copy-Tree -From (Join-Path $eventProject 'src/template.events') -To (Join-Path $events 'src/template.events')
$n += Copy-Tree -From (Join-Path $eventProject 'tests/template.events.tests') `
                -To (Join-Path $events 'tests/template.events.tests')

Edit-StagedFile -Path (Join-Path $events 'src/template.events/template.events.csproj') -Pairs @(
    '..\..\..\base-project\src\template.servicedefaults\', '..\template.servicedefaults\'
)
Add-TemplateConfig -Name 'aspire-events' -Root $events
Write-Host "aspire-events     $n files"

Write-Host ""
Write-Host "Staged to $OutputRoot"
