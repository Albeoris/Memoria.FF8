function Get-ValidationRetryProperty {
    param([AllowNull()] $Value, [string] $Name, $Default = $null)

    if ($null -eq $Value) {
        return $Default
    }
    if ($Value -is [System.Collections.IDictionary]) {
        if ($Value.Contains($Name)) {
            return $Value[$Name]
        }
    } elseif ($null -ne $Value.PSObject.Properties[$Name]) {
        return $Value.$Name
    }
    return $Default
}

function Select-ValidationRetryCases {
    <# Selectors are unioned, then emitted in report order. Only tested/error cases are runnable evidence. #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] $Report,
        [string[]] $CaseId = @(),
        [int[]] $EffectId = @(),
        [string[]] $ErrorCode = @(),
        [switch] $Errors,
        [switch] $LowFps,
        [string[]] $ProblemId = @(),
        [ValidateRange(0, [int]::MaxValue)] [int] $Top = 0
    )

    if ($CaseId.Count -eq 0 -and $EffectId.Count -eq 0 -and $ErrorCode.Count -eq 0 -and -not $Errors -and -not $LowFps -and $ProblemId.Count -eq 0 -and $Top -eq 0) {
        throw 'Specify at least one retry selector.'
    }
    foreach ($code in $ErrorCode) {
        if ([string]::IsNullOrWhiteSpace($code)) {
            throw 'Error-code selectors must not be empty.'
        }
    }
    $groupCases = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $problems = @(Get-ValidationRetryProperty $Report 'problems' @())
    for ($index = 0; $index -lt $problems.Count; $index++) {
        $problem = $problems[$index]
        if ($index -lt $Top -or (Get-ValidationRetryProperty $problem 'problem_id') -in $ProblemId) {
            foreach ($id in @(Get-ValidationRetryProperty $problem 'case_ids' @())) {
                [void] $groupCases.Add([string] $id)
            }
        }
    }
    $emitted = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($case in @(Get-ValidationRetryProperty $Report 'cases' @())) {
        if ((Get-ValidationRetryProperty $case 'state') -notin @('tested', 'error')) {
            continue
        }
        $id = [string] (Get-ValidationRetryProperty $case 'case_id')
        if ([string]::IsNullOrWhiteSpace($id)) {
            continue
        }
        $selected = $id -in $CaseId -or (Get-ValidationRetryProperty $case 'effect_id') -in $EffectId -or $groupCases.Contains($id)
        if ($Errors -and (Get-ValidationRetryProperty $case 'state') -eq 'error') {
            $selected = $true
        }
        if ($LowFps -and (Get-ValidationRetryProperty $case 'classification') -in @('sustained-low-fps', 'repeated-hitches')) {
            $selected = $true
        }
        foreach ($errorText in @(Get-ValidationRetryProperty $case 'errors' @())) {
            foreach ($code in $ErrorCode) {
                if ([string] $errorText -and ([string] $errorText).IndexOf($code, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
                    $selected = $true
                }
            }
        }
        if ($selected -and $emitted.Add($id)) {
            $id
        }
    }
}

function Get-ValidationRetryFunctionalErrors {
    param($Case)

    $hasExplicit = $null -ne $Case -and (($Case -is [System.Collections.IDictionary] -and $Case.Contains('functional_errors')) -or $null -ne $Case.PSObject.Properties['functional_errors'])
    if ($hasExplicit) {
        return @(Get-ValidationRetryProperty $Case 'functional_errors' @() | Where-Object { -not [string]::IsNullOrWhiteSpace([string] $_) })
    }
    if ((Get-ValidationRetryProperty $Case 'state') -eq 'error') {
        return @(Get-ValidationRetryProperty $Case 'errors' @() | Where-Object { -not [string]::IsNullOrWhiteSpace([string] $_) })
    }
}

function ConvertTo-ValidationErrorIdentity {
    <# Build paths, source lines and managed callers in a stack trace are evidence, not error identity. #>
    [CmdletBinding()]
    param([Parameter(Mandatory)] [AllowEmptyString()] [string] $ErrorText)

    $stackStart = [regex]::Match($ErrorText, '(?:\r?\n)[\t ]*at[\t ]+|[\t ]+at[\t ]+Memoria\.', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
    if ($stackStart.Success) {
        $ErrorText = $ErrorText.Substring(0, $stackStart.Index)
    }
    return [regex]::Replace($ErrorText, '\s+', ' ').Trim().ToUpperInvariant()
}

function Get-ValidationRetryIssues {
    <# Issue identities describe symptoms, not causes. The report retains the original functional error text. #>
    param($Case)

    $seen = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($errorText in @(Get-ValidationRetryFunctionalErrors $Case)) {
        $identity = ConvertTo-ValidationErrorIdentity -ErrorText $errorText
        $issue = "functional-error:$identity"
        if ($seen.Add($issue)) {
            $issue
        }
    }
    $classification = Get-ValidationRetryProperty $Case 'classification'
    if ($classification -in @('sustained-low-fps', 'repeated-hitches')) {
        'performance'
    }
    if ($classification -eq 'isolated-hitch') {
        $hasLongStall = $false
        foreach ($cast in @(Get-ValidationRetryProperty $Case 'casts' @())) {
            $effect = Get-ValidationRetryProperty $cast 'effect'
            $maximum = Get-ValidationRetryProperty $effect 'MaximumFrameMs'
            if ($null -ne $maximum -and $maximum -ge 250) {
                $hasLongStall = $true
            }
        }
        if ($hasLongStall) {
            'isolated-long-stall'
        }
    }
    $overhead = Get-ValidationRetryProperty $Case 'overhead_ms'
    if ($null -ne $overhead -and $overhead -gt 10000) {
        'supervisor-overhead'
    }
}

function Compare-ValidationRetry {
    <# Fresh evidence and a complete assessment are required before declaring resolution. #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] $Before,
        [Parameter(Mandatory)] $After,
        [Parameter(Mandatory)] [string[]] $CaseId
    )

    $seen = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($id in $CaseId) {
        if (-not $seen.Add($id)) {
            continue
        }
        $prior = @(Get-ValidationRetryProperty $Before 'cases' @() | Where-Object { (Get-ValidationRetryProperty $_ 'case_id') -eq $id }) | Select-Object -First 1
        $latest = @(Get-ValidationRetryProperty $After 'cases' @() | Where-Object { (Get-ValidationRetryProperty $_ 'case_id') -eq $id }) | Select-Object -First 1
        $priorState = Get-ValidationRetryProperty $prior 'state'
        $latestState = Get-ValidationRetryProperty $latest 'state'
        $priorClass = Get-ValidationRetryProperty $prior 'classification'
        $latestClass = Get-ValidationRetryProperty $latest 'classification'
        $priorIssues = @(Get-ValidationRetryIssues $prior)
        $latestIssues = @(Get-ValidationRetryIssues $latest)
        $priorPath = [string] (Get-ValidationRetryProperty $prior 'result_path')
        $latestPath = [string] (Get-ValidationRetryProperty $latest 'result_path')
        $status = 'inconclusive'
        $resolvedIssues = @()
        $remainingIssues = @()
        $newIssues = @()
        if ($null -ne $prior -and $priorState -in @('tested', 'error') -and $latestState -in @('tested', 'error') -and -not [string]::IsNullOrWhiteSpace($latestPath) -and $latestPath -ne $priorPath) {
            $newIssues = @($latestIssues | Where-Object { $_ -notin $priorIssues })
            $remainingIssues = @($priorIssues | Where-Object { $_ -in $latestIssues })
            $assessed = $latestState -eq 'tested' -and (Get-ValidationRetryProperty $latest 'assessment_available' $true) -and $latestClass -in @('healthy', 'isolated-hitch', 'sustained-low-fps', 'repeated-hitches')
            $latestErrors = @(Get-ValidationRetryFunctionalErrors $latest)
            foreach ($issue in $priorIssues) {
                if ($issue -in $latestIssues) {
                    continue
                }
                if ($issue.StartsWith('functional-error:', [StringComparison]::OrdinalIgnoreCase)) {
                    if ($latestState -eq 'tested' -and $latestErrors.Count -eq 0) {
                        $resolvedIssues += $issue
                    }
                } elseif ($issue -eq 'supervisor-overhead') {
                    $overhead = Get-ValidationRetryProperty $latest 'overhead_ms'
                    if ($null -ne $overhead -and $overhead -le 10000) {
                        $resolvedIssues += $issue
                    }
                } elseif ($issue -eq 'isolated-long-stall') {
                    $knownCastMaximums = @(foreach ($cast in @(Get-ValidationRetryProperty $latest 'casts' @())) {
                        $maximum = Get-ValidationRetryProperty (Get-ValidationRetryProperty $cast 'effect') 'MaximumFrameMs'
                        if ($null -ne $maximum) {
                            $maximum
                        }
                    })
                    if ($assessed -and (Get-ValidationRetryProperty $prior 'assessment_available' $true) -and $knownCastMaximums.Count -eq 2 -and @($knownCastMaximums | Where-Object { $_ -ge 250 }).Count -eq 0) {
                        $resolvedIssues += $issue
                    }
                } elseif ($assessed -and (Get-ValidationRetryProperty $prior 'assessment_available' $true)) {
                    $resolvedIssues += $issue
                }
            }
            if ($newIssues.Count -gt 0) {
                $status = 'changed-failure'
            } elseif ($remainingIssues.Count -gt 0) {
                $status = 'reproduced'
            } elseif ($assessed) {
                if ($priorIssues.Count -gt 0 -and $resolvedIssues.Count -eq $priorIssues.Count) {
                    $status = 'resolved'
                } elseif ($priorIssues.Count -eq 0 -and $priorState -eq 'tested' -and $priorClass -in @('healthy', 'isolated-hitch')) {
                    $status = 'verified'
                }
            }
        }
        [pscustomobject] @{
            case_id = $id
            effect_id = Get-ValidationRetryProperty $prior 'effect_id' (Get-ValidationRetryProperty $latest 'effect_id')
            before_state = $priorState
            before_classification = $priorClass
            before_build_fingerprint = Get-ValidationRetryProperty $prior 'build_fingerprint' (Get-ValidationRetryProperty $Before 'build_fingerprint')
            after_state = $latestState
            after_classification = $latestClass
            after_build_fingerprint = Get-ValidationRetryProperty $latest 'build_fingerprint' (Get-ValidationRetryProperty $After 'build_fingerprint')
            status = $status
            resolved_issues = $resolvedIssues
            remaining_issues = $remainingIssues
            new_issues = $newIssues
        }
    }
}
