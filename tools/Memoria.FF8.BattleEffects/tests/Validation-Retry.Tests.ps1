[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot '../Validation-Retry.ps1')
$script:assertionCount = 0

function Assert-Equal($Expected, $Actual, [string] $Message) {
    $script:assertionCount++
    if ($Expected -cne $Actual) {
        throw "$Message Expected '$Expected', got '$Actual'."
    }
}

function New-Case([string] $Id, [int] $Effect, [string] $State, [string] $Classification, [string[]] $Errors = @(), [string] $Path = 'before.json', [bool] $Available = $true) {
    return [pscustomobject] @{
        case_id = $Id
        effect_id = $Effect
        state = $State
        classification = $Classification
        errors = $Errors
        result_path = $Path
        assessment_available = $Available
        build_fingerprint = 'synthetic-build'
    }
}

function Assert-Status($Prior, $Latest, [string] $Expected, [string] $Message) {
    $before = @{ cases = @($Prior) }
    $after = @{ cases = @($Latest) }
    $comparison = @(Compare-ValidationRetry -Before $before -After $after -CaseId 'a')
    Assert-Equal 1 $comparison.Count "$Message Count."
    Assert-Equal $Expected $comparison[0].status $Message
    Assert-Equal 'synthetic-build' $comparison[0].before_build_fingerprint "$Message Build retained."
}

$report = @{
    cases = @(
        (New-Case 'a' 1 'tested' 'healthy'),
        (New-Case 'b' 2 'error' 'error' @('ERROR_[X]*: detail')),
        (New-Case 'c' 3 'tested' 'sustained-low-fps'),
        (New-Case 'd' 3 'tested' 'isolated-hitch'),
        (New-Case 'e' 3 'tested' 'repeated-hitches'),
        (New-Case 'f' 3 'pending' 'unavailable'),
        (New-Case 'g' 3 'blocked' 'unavailable'),
        (New-Case 'b' 2 'error' 'error' @('ERROR_[X]*: detail'))
    )
    problems = @(
        @{ problem_id = 'first'; case_ids = @('d', 'f', 'g') },
        @{ problem_id = 'second'; case_ids = @('b') }
    )
}
Assert-Equal 'a,b,c,e' (@(Select-ValidationRetryCases -Report $report -EffectId 1 -ErrorCode 'error_[x]*' -LowFps) -join ',') 'Union preserves order and uniqueness.'
Assert-Equal 'b' (@(Select-ValidationRetryCases -Report $report -ErrorCode '[X]*') -join ',') 'Literal special characters match.'
Assert-Equal 'b' (@(Select-ValidationRetryCases -Report $report -Errors) -join ',') 'All errors is a valid selector.'
Assert-Equal 'b,c,e' (@(Select-ValidationRetryCases -Report $report -Errors -LowFps) -join ',') 'All errors unions with other selectors.'
Assert-Equal 0 @(Select-ValidationRetryCases -Report $report -ErrorCode 'ERROR_?').Count 'Wildcard syntax is literal.'
Assert-Equal 'c,d,e' (@(Select-ValidationRetryCases -Report $report -EffectId 3) -join ',') 'Effect verification includes healthy and isolated runnable cases.'
Assert-Equal 'd' (@(Select-ValidationRetryCases -Report $report -Top 1) -join ',') 'Top problem excludes unexecuted cases.'
Assert-Equal 'b,d' (@(Select-ValidationRetryCases -Report $report -Top 1 -ProblemId 'second') -join ',') 'Problem filters union in report order.'
Assert-Equal 'd' (@(Select-ValidationRetryCases -Report $report -CaseId @('d', 'f', 'g')) -join ',') 'Explicit case IDs exclude blocked and pending cases.'
Assert-Equal 'a,d' (@(Select-ValidationRetryCases -Report $report -CaseId 'd' -EffectId 1) -join ',') 'Explicit case IDs union in report order.'
$literalCase = New-Case 'case_[x]*' 4 'tested' 'healthy'
Assert-Equal 'case_[x]*' (@(Select-ValidationRetryCases -Report @{ cases = @($literalCase) } -CaseId 'case_[x]*') -join ',') 'Case IDs match literally.'
Assert-Equal 0 @(Select-ValidationRetryCases -Report @{ cases = @($literalCase) } -CaseId 'case_*').Count 'Case IDs do not expand wildcard patterns.'
$threw = $false
try {
    Select-ValidationRetryCases -Report $report | Out-Null
} catch {
    $threw = $true
}
Assert-Equal $true $threw 'No selector throws.'

$slow = New-Case 'a' 1 'tested' 'sustained-low-fps' @('PRESENTATION_BELOW_THRESHOLD')
$healthy = New-Case 'a' 1 'tested' 'healthy' @() 'after.json'
$repeated = New-Case 'a' 1 'tested' 'repeated-hitches' @() 'after.json'
$failure = New-Case 'a' 1 'error' 'error' @('RESOURCE_MISSING')
$sameFailure = New-Case 'a' 1 'error' 'error' @('RESOURCE_MISSING') 'after.json' $false
Assert-Status $slow $healthy 'resolved' 'Slow case resolves only with fresh healthy evidence.'
Assert-Status $slow $repeated 'reproduced' 'Different FPS classification remains slow.'
Assert-Status $failure $healthy 'resolved' 'Functional failure resolves.'
Assert-Status $slow $sameFailure 'changed-failure' 'New functional failure after a performance issue is changed failure.'
Assert-Status $failure $sameFailure 'reproduced' 'Same functional failure reproduces without an FPS assessment.'
Assert-Status $failure (New-Case 'a' 1 'error' 'error' @('OTHER_FAILURE') 'after.json') 'changed-failure' 'Different failure is not fixed.'
Assert-Status $healthy (New-Case 'a' 1 'tested' 'healthy' @() 'third.json') 'verified' 'Healthy verification remains verified.'
Assert-Status $slow (New-Case 'a' 1 'tested' 'healthy') 'inconclusive' 'Stale evidence cannot resolve.'
Assert-Status $slow (New-Case 'a' 1 'tested' 'healthy' @() '' ) 'inconclusive' 'Missing evidence path cannot resolve.'
Assert-Status $slow (New-Case 'a' 1 'pending' 'unavailable' @() 'after.json') 'inconclusive' 'Pending cases cannot resolve.'
Assert-Status $slow (New-Case 'a' 1 'tested' 'healthy' @() 'after.json' $false) 'inconclusive' 'Insufficient assessment cannot resolve.'
Assert-Status $slow (New-Case 'a' 1 'tested' 'unavailable' @() 'after.json') 'inconclusive' 'Unavailable samples cannot resolve.'
Assert-Status (New-Case 'a' 1 'tested' 'sustained-low-fps' @() 'before.json' $false) $healthy 'inconclusive' 'An incomplete original slow assessment cannot establish resolution.'
Assert-Status $failure $repeated 'changed-failure' 'Functional repair with a new performance failure is changed failure.'
$both = New-Case 'a' 1 'error' 'sustained-low-fps' @('RESOURCE_MISSING')
Assert-Status $both $repeated 'reproduced' 'Functional repair with existing slow playback remains unresolved.'
$presentationOnly = New-Case 'a' 1 'error' 'sustained-low-fps' @('PRESENTATION_BELOW_THRESHOLD')
$presentationOnly | Add-Member -NotePropertyName functional_errors -NotePropertyValue @()
Assert-Status $presentationOnly $repeated 'reproduced' 'Explicit empty functional errors overrides legacy errors.'
$comparison = @(Compare-ValidationRetry -Before @{ cases = @($slow) } -After @{ cases = @() } -CaseId @('a', 'a'))
Assert-Equal 1 $comparison.Count 'Comparison IDs are unique.'
Assert-Equal 'inconclusive' $comparison[0].status 'Missing rerun is inconclusive.'

function New-IssueCase([string] $Classification, [string] $Path, [double] $Overhead = 0, [double] $Maximum = 20) {
    $case = New-Case 'a' 1 'tested' $Classification @() $Path
    $case | Add-Member -NotePropertyName overhead_ms -NotePropertyValue $Overhead
    $case | Add-Member -NotePropertyName casts -NotePropertyValue @(
        @{ effect = @{ MaximumFrameMs = $Maximum } },
        @{ effect = @{ MaximumFrameMs = 20 } }
    )
    return $case
}

$overheadBefore = New-IssueCase 'healthy' 'before.json' 10001
$overheadAfter = New-IssueCase 'healthy' 'after.json' 10001
$assessedHealthy = New-IssueCase 'healthy' 'after.json' 10000
Assert-Status $overheadBefore $overheadAfter 'reproduced' 'Supervisor overhead remains an issue despite healthy frame timing.'
Assert-Status $overheadBefore $assessedHealthy 'resolved' 'Supervisor overhead resolves at the exact budget.'
Assert-Status (New-IssueCase 'healthy' 'before.json') $overheadAfter 'changed-failure' 'New supervisor overhead is a changed failure.'
Assert-Status $overheadBefore $healthy 'inconclusive' 'Missing overhead measurement cannot resolve overhead.'
$longBefore = New-IssueCase 'isolated-hitch' 'before.json' 0 250
$longAfter = New-IssueCase 'isolated-hitch' 'after.json' 0 500
Assert-Status $longBefore $longAfter 'reproduced' 'An isolated long stall remains a reportable issue.'
Assert-Status $longBefore $assessedHealthy 'resolved' 'Fresh measured casts without long stalls resolve the original stall.'
Assert-Status $longBefore (New-IssueCase 'isolated-hitch' 'after.json' 0 249) 'resolved' 'An isolated stall below the reportable boundary resolves the original issue.'
Assert-Status $longBefore $healthy 'inconclusive' 'Missing cast maximums cannot resolve an original long stall.'
Assert-Status (New-IssueCase 'healthy' 'before.json') $longAfter 'changed-failure' 'A new isolated long stall is a changed failure.'
$partialBefore = New-IssueCase 'sustained-low-fps' 'before.json' 12000
$partial = @(Compare-ValidationRetry -Before @{ cases = @($partialBefore) } -After @{ cases = @($overheadAfter) } -CaseId 'a')[0]
Assert-Equal 'reproduced' $partial.status 'Fixing performance does not resolve remaining overhead.'
Assert-Equal 'performance' ($partial.resolved_issues -join ',') 'Partial comparison identifies resolved performance.'
Assert-Equal 'supervisor-overhead' ($partial.remaining_issues -join ',') 'Partial comparison identifies remaining overhead.'
Assert-Equal 0 $partial.new_issues.Count 'Partial comparison has no newly introduced issue.'
$changed = @(Compare-ValidationRetry -Before @{ cases = @($overheadBefore) } -After @{ cases = @($longAfter) } -CaseId 'a')[0]
Assert-Equal 'changed-failure' $changed.status 'Replacing overhead with a long stall is a changed failure.'
Assert-Equal 'supervisor-overhead' ($changed.resolved_issues -join ',') 'Changed failure retains the resolved issue details.'
Assert-Equal 'isolated-long-stall' ($changed.new_issues -join ',') 'Changed failure identifies its new issue.'

$stackBefore = New-Case 'a' 1 'error' 'error' @("System.InvalidOperationException: Unsupported native method 0x1234 at address 0xABC.`n   at Memoria.FF8.Before.Call() in example-build-one/Renderer.cs:line 123")
$stackAfter = New-Case 'a' 1 'error' 'error' @("system.invalidoperationexception:  Unsupported native method 0x1234 at address 0xABC.`r`n    at Memoria.FF8.After.OtherCall() in example-build-two/Renderer.cs:line 456") 'after.json' $false
Assert-Status $stackBefore $stackAfter 'reproduced' 'Managed stack paths, line numbers and callers do not change an error identity.'
Assert-Equal 'SYSTEM.INVALIDOPERATIONEXCEPTION: UNSUPPORTED NATIVE METHOD 0X1234 AT ADDRESS 0XABC.' (ConvertTo-ValidationErrorIdentity $stackBefore.errors[0]) 'Native method and address remain in the main error identity.'
Assert-Equal 'FAILURE DETAILS' (ConvertTo-ValidationErrorIdentity "  Failure`t details at Memoria.FF8.Renderer.Draw() in example/Renderer.cs:line 10") 'Inline managed stack frames are removed and whitespace is canonicalized.'
Assert-Equal 'NATIVE FAILURE AT ADDRESS 0X1234' (ConvertTo-ValidationErrorIdentity 'Native failure at address 0x1234') 'Main message address phrasing remains intact.'
$differentMain = New-Case 'a' 1 'error' 'error' @("System.InvalidOperationException: Unsupported native method 0x5678 at address 0xABC.`n   at Memoria.FF8.After.OtherCall()") 'after.json' $false
Assert-Status $stackBefore $differentMain 'changed-failure' 'Distinct main messages remain distinct despite equivalent stack shapes.'
Assert-Equal $stackBefore.errors[0] @(Get-ValidationRetryFunctionalErrors $stackBefore)[0] 'Raw functional error evidence is retained.'
Write-Output "Validation retry tests passed ($script:assertionCount assertions)."
