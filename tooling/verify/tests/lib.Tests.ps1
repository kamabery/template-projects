# The harness's own logic, which decides what counts as proof. No network.
#
#   Invoke-Pester tooling/verify/tests

BeforeAll { . (Join-Path $PSScriptRoot '..\lib.ps1') }

Describe 'Set-TfvarLines' {
    It 'replaces a variable that is there and adds one that is not' {
        $out = Set-TfvarLines "a = 1`nb = 2`n" ([ordered]@{ b = '"x"'; c = 'false' })
        $out | Should -Match '(?m)^a = 1\r?$'
        $out | Should -Match '(?m)^b = "x"\r?$'
        $out | Should -Match '(?m)^c = false\r?$'
        $out | Should -Not -Match 'b = 2'
    }

    It 'does not touch a variable whose name only starts the same' {
        Set-TfvarLines "use_platform_x = 1`n" ([ordered]@{ use_platform = 'false' }) | Should -Match 'use_platform_x = 1'
    }
}

Describe 'ConvertTo-Redacted' {
    It 'removes every secret, however often it appears' {
        ConvertTo-Redacted 'a s3 b s3 c p4' @('s3', 'p4') | Should -Be 'a *** b *** c ***'
    }

    It 'takes a secret with regex characters literally' {
        ConvertTo-Redacted 'x a.b* y axbb' @('a.b*') | Should -Be 'x *** y axbb'
    }

    It 'ignores an empty secret rather than matching everything' {
        ConvertTo-Redacted 'text' @('', $null) | Should -Be 'text'
    }
}

Describe 'Get-ConnectionPassword' {
    It 'reads the password out of the connection string the workflow reads it from' {
        Get-ConnectionPassword 'Host=h;Port=5432;Username=u;Password=p-_1;SSL Mode=Require;Database=d' | Should -Be 'p-_1'
    }

    It 'refuses a string with no password' { { Get-ConnectionPassword 'Host=h' } | Should -Throw }
}

Describe 'Compare-Inventory' {
    It 'reports what is there now and was not before' {
        Compare-Inventory @{ roles = @('old') } @{ roles = @('old', 'new'); apps = @('x') } | Should -Be @('apps: x', 'roles: new')
    }

    It 'lets an accepted residual through, and only that' {
        Compare-Inventory @{} @{ roles = @('preview_a', 'other') } @('^roles: preview_') | Should -Be @('roles: other')
    }

    It 'finds nothing when nothing is left' { @(Compare-Inventory @{ a = @('x') } @{ a = @('x') }).Count | Should -Be 0 }
}

Describe 'Get-ForeignChanges' {
    It 'finds a workspace that changed, appeared or went' {
        $changes = Get-ForeignChanges @{ a = '1'; gone = '1'; same = '1' } @{ a = '2'; new = '1'; same = '1' }
        $changes | Should -Be @('changed: a', 'gone: gone', 'new: new')
    }
}

Describe 'Get-LiveForeignPreviews' {
    It 'names another job''s preview, and neither its own nor a long-lived workspace' {
        Get-LiveForeignPreviews @('factory-canary-j000', 'verify-a-j901', 'todoapp-two-dev', 'factory-state') | Should -Be @('factory-canary-j000')
    }
}

Describe 'Get-DeployPath' {
    BeforeAll {
        function Run([string] $apply) {
            [pscustomobject]@{ jobs = @([pscustomobject]@{ steps = @(
                [pscustomobject]@{ name = 'terraform apply (registry and database)'; conclusion = $apply }
                [pscustomobject]@{ name = 'terraform apply'; conclusion = $apply }
                [pscustomobject]@{ name = 'publish the React front end'; conclusion = 'success' }) }) }
        }
    }

    It 'is full when the applies ran' { Get-DeployPath (Run 'success') | Should -Be 'full' }
    It 'is front-end-only when they were skipped' { Get-DeployPath (Run 'skipped') | Should -Be 'front-end-only' }

    It 'will not guess when the step is missing' {
        { Get-DeployPath ([pscustomobject]@{ jobs = @([pscustomobject]@{ steps = @() }) }) } | Should -Throw
    }
}

Describe 'Get-PassVerdict' {
    BeforeAll {
        $good = @(1..9 | ForEach-Object { [pscustomobject]@{ Check = $_; Status = 'pass' } })
        $args0 = @{ StartSha = 'abc'; EndSha = 'abc'; EndClean = $true; From = 1; ForeignChanges = @() }
    }

    It 'is a clean pass only when every check passed in one run at one commit' {
        Get-PassVerdict -Results $good @args0 | Should -Be 'clean'
    }

    It 'does not count an accepted risk as a failure' {
        $results = @($good | ForEach-Object { if ($_.Check -eq 4) { [pscustomobject]@{ Check = 4; Status = 'accepted-risk' } } else { $_ } })
        Get-PassVerdict -Results $results @args0 | Should -Be 'clean'
    }

    It 'is not clean with a failed check' {
        $results = @($good | ForEach-Object { if ($_.Check -eq 8) { [pscustomobject]@{ Check = 8; Status = 'fail' } } else { $_ } })
        Get-PassVerdict -Results $results @args0 | Should -Match '^not clean: check 8'
    }

    It 'is not clean with a check missing' {
        Get-PassVerdict -Results @($good | Where-Object Check -ne 3) @args0 | Should -Match '^not clean: check 3'
    }

    It 'is not clean when it started part way' {
        $a = $args0.Clone(); $a.From = 5
        Get-PassVerdict -Results $good @a | Should -Match '^not clean: started at check 5'
    }

    It 'is not clean when the commit moved' {
        $a = $args0.Clone(); $a.EndSha = 'def'
        Get-PassVerdict -Results $good @a | Should -Match '^not clean: .*abc.*def'
    }

    It 'is not clean with uncommitted changes' {
        $a = $args0.Clone(); $a.EndClean = $false
        Get-PassVerdict -Results $good @a | Should -Match '^not clean: uncommitted'
    }

    It 'is void with activity from anything else' {
        $a = $args0.Clone(); $a.ForeignChanges = @('changed: factory-canary-j000')
        Get-PassVerdict -Results $good @a | Should -Match '^void: .*factory-canary-j000'
    }
}

Describe 'New-RetryProbeConfig' {
    BeforeAll {
        $tf = [IO.File]::ReadAllText((Join-Path $PSScriptRoot '..\..\..\IaC\auth.entra-external.tf'))
        $config = New-RetryProbeConfig $tf 'verify-retry' 'kurt-mabery' '3m'
    }

    It 'uses the real retry from the real resource' {
        $config | Should -Match 'error_message_regex = \["The application id \.\* is invalid"\]'
        $config | Should -Match 'resource "msgraph_resource_action" "user_flow_link"'
    }

    It 'links the app it is given, with the timeout it is given' {
        $config | Should -Match 'appId\s+= var\.app_id'
        $config | Should -Match 'create = "3m"'
        $config | Should -Not -Match '20m'
    }

    It 'has no reference to a resource it does not declare' {
        $config | Should -Not -Match 'azuread_'
        $config | Should -Not -Match 'depends_on'
    }

    It 'pins the provider the scaffold is locked to' { $config | Should -Match 'version = "0\.6\.0"' }

    It 'refuses a file without the resource' { { New-RetryProbeConfig 'nothing' 'w' 'o' '3m' } | Should -Throw }
}
