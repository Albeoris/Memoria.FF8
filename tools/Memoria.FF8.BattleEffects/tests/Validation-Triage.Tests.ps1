[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '../Validation-Triage.ps1')
$script:assertionCount = 0

function Assert-Equal($Expected, $Actual, [string] $Message) {
    $script:assertionCount++
    if ($Expected -ne $Actual) {
        throw "$Message Expected '$Expected', got '$Actual'."
    }
}

function New-TestCast([double[]] $Durations) {
    return [pscustomobject]@{ effect = (Get-EffectFrameMetrics -IntervalMs $Durations) }
}

function New-TestCase([string] $Id, [int] $EffectId, [hashtable] $Overrides = @{}) {
    $fields = [ordered]@{
        case_id = $Id
        effect_id = $EffectId
        state = 'tested'
        retained_unresolved = $false
        errors = @()
        observed_executions = 2
        casts = @((New-TestCast @(16, 16, 16)), (New-TestCast @(16, 16, 16)))
        classification = 'healthy'
        problem_casts = 0
        overhead_ms = 0
    }
    foreach ($key in $Overrides.Keys) {
        $fields[$key] = $Overrides[$key]
    }
    return [pscustomobject]$fields
}

$healthyCast = New-TestCast @(16, 16, 16)
$jitterCast = New-TestCast @(34, 16, 34)
$sustainedCast = New-TestCast (@(50.0) * 12)
$stallCast = New-TestCast @(16, 250, 16)
$hundredCast = New-TestCast @(16, 100, 16)
$unavailableCast = New-TestCast @()

Assert-Equal 'healthy' (Get-ValidationCaseClassification @($healthyCast, $healthyCast)) 'Two healthy casts.'
Assert-Equal 'isolated-hitch' (Get-ValidationCaseClassification @($jitterCast, $jitterCast)) 'Tiny jitter across casts does not become repeated substantial hitches.'
Assert-Equal 'repeated-hitches' (Get-ValidationCaseClassification @($hundredCast, $hundredCast)) 'A 100 ms interval in each cast is a cross-cast repeated hitch.'
Assert-Equal 'isolated-hitch' (Get-ValidationCaseClassification @($hundredCast, $healthyCast)) 'A 100 ms interval in only one cast remains isolated.'
Assert-Equal 'sustained-low-fps' (Get-ValidationCaseClassification @($sustainedCast, $stallCast)) 'Sustained slowdown takes classification precedence.'
Assert-Equal 'unavailable' (Get-ValidationCaseClassification @($healthyCast, $unavailableCast)) 'Missing cast samples prevent a healthy classification.'
Assert-Equal 'unavailable' (Get-ValidationCaseClassification @($healthyCast)) 'One healthy cast is insufficient.'

$crash = New-TestCase 'crash' 10 @{ state = 'error'; errors = @('GRAPHICAL_HOST_FAULTED'); observed_executions = 0; casts = @() }
$sustainedBoth = New-TestCase 'sustained-both' 20 @{ classification = 'sustained-low-fps'; problem_casts = 2; casts = @($sustainedCast, $sustainedCast) }
$prestart = New-TestCase 'prestart' 30 @{ state = 'error'; errors = @('DISPATCH_UNAVAILABLE'); observed_executions = 0; casts = @() }
$isolated = New-TestCase 'isolated' 40 @{ classification = 'isolated-hitch'; problem_casts = 1; casts = @($stallCast, $healthyCast) }
$cases = @($prestart, $isolated, $sustainedBoth, $crash)
$problems = @(Get-ValidationProblems $cases)
Assert-Equal 4 $problems.Count 'Each distinct effect problem is retained.'
Assert-Equal '10,20,30,40' ($problems.effect_id -join ',') 'Crash, sustained, prestart, isolated priority ordering.'
Assert-Equal '0,1,3,4' ($problems.priority -join ',') 'Problem priorities.'
Assert-Equal '1,2,3,4' (($problems | ForEach-Object { $_.rank }) -join ',') 'Ranks are contiguous and one based.'
Assert-Equal 'functional-error' $problems[0].kinds[0] 'Crash is a functional error.'
Assert-Equal 'observed-in-both-casts' $problems[1].evidence 'Both-cast sustained evidence.'
Assert-Equal 'unverified-precondition' $problems[2].evidence 'Pre-dispatch failure remains unverified.'
Assert-Equal 'isolated-long-stall' $problems[3].kinds[0] 'A 250 ms stall is explicit.'

$nonproblems = @(
    (New-TestCase 'healthy' 50),
    (New-TestCase 'blocked' 51 @{ state = 'blocked'; errors = @('CRASH'); retained_unresolved = $true }),
    (New-TestCase 'pending' 52 @{ state = 'pending' }),
    (New-TestCase 'jitter' 53 @{ classification = 'isolated-hitch'; casts = @($jitterCast, $jitterCast) })
)
Assert-Equal 0 @(Get-ValidationProblems $nonproblems).Count 'Healthy, blocked, pending and tiny jitter produce no priority problems.'
$overhead = @(Get-ValidationProblems @((New-TestCase 'jitter-overhead' 53 @{ classification = 'isolated-hitch'; casts = @($jitterCast, $jitterCast); overhead_ms = 10001 })))
Assert-Equal 1 $overhead.Count 'Tiny jitter can still have a separate supervisor overhead problem.'
Assert-Equal 5 $overhead[0].priority 'Supervisor overhead priority.'
Assert-Equal 'supervisor-overhead' $overhead[0].kinds[0] 'Supervisor overhead kind.'
Assert-Equal 1 $overhead[0].impact_ms 'Overhead impact is only excess above its budget.'
Assert-Equal 0 @(Get-ValidationProblems @((New-TestCase 'budget-overhead' 54 @{ overhead_ms = 10000 }))).Count 'Exact overhead budget is not excess.'

$singleSustained = @(Get-ValidationProblems @((New-TestCase 'sustained-once' 60 @{ classification = 'sustained-low-fps'; problem_casts = 1; casts = @($sustainedCast, $healthyCast) })))
Assert-Equal 2 $singleSustained[0].priority 'One-cast sustained slowdown has lower confidence priority.'
$crossCast = @(Get-ValidationProblems @((New-TestCase 'hundred-both' 61 @{ classification = (Get-ValidationCaseClassification @($hundredCast, $hundredCast)); problem_casts = 2; casts = @($hundredCast, $hundredCast) })))
Assert-Equal 2 $crossCast[0].priority 'Repeated 100 ms hitch across casts is prioritized.'

$variant = New-TestCase 'sustained-variant' 20 @{ classification = 'isolated-hitch'; problem_casts = 1; casts = @($stallCast, $healthyCast) }
$grouped = @(Get-ValidationProblems @($variant, $sustainedBoth, $crash))
Assert-Equal 2 $grouped.Count 'Variants deduplicate by effect.'
Assert-Equal 'effect:20' $grouped[1].problem_id 'Effect grouping key is stable.'
Assert-Equal 2 $grouped[1].affected_cases 'Affected variants are counted once per supplied case.'
Assert-Equal 'sustained-both' $grouped[1].representative_case_id 'Best priority evidence represents a group.'
Assert-Equal 'isolated-long-stall,sustained-low-fps' (($grouped[1].kinds | Sort-Object) -join ',') 'Grouping preserves distinct problem kinds.'
Assert-Equal 'sustained-both,sustained-variant' (($grouped[1].case_ids | Sort-Object) -join ',') 'Grouping preserves variant identifiers.'

$largerCast = New-TestCast (@(60.0) * 12)
$larger = New-TestCase 'larger-impact' 70 @{ classification = 'sustained-low-fps'; problem_casts = 2; casts = @($largerCast, $largerCast) }
$tied = New-TestCase 'same-impact' 15 @{ classification = 'sustained-low-fps'; problem_casts = 2; casts = @($sustainedCast, $sustainedCast) }
$ordered = @(Get-ValidationProblems @($sustainedBoth, $larger, $tied))
$reordered = @(Get-ValidationProblems @($tied, $larger, $sustainedBoth))
Assert-Equal '70,15,20' ($ordered.effect_id -join ',') 'Priority ties use descending impact, then effect identifier.'
Assert-Equal ($ordered.effect_id -join ',') ($reordered.effect_id -join ',') 'Score ordering is independent of case enumeration order.'

$sameCrash = New-TestCase 'crash-variant' 11 @{ state = 'error'; functional_errors = @(' graphical_host_faulted '); observed_executions = 0; casts = @() }
$crashFamily = @(Get-ValidationProblems @($crash, $sameCrash))
Assert-Equal 1 $crashFamily.Count 'Identical normalized functional symptoms group across effects.'
Assert-Equal '10,11' ($crashFamily[0].effect_ids -join ',') 'Symptom family retains distinct effect identifiers.'
Assert-Equal 'crash,crash-variant' ($crashFamily[0].case_ids -join ',') 'Symptom family retains case identifiers.'
Assert-Equal 'symptom-family-not-proven-root-cause' $crashFamily[0].family_grouping 'Symptom grouping does not assert a root cause.'
Assert-Equal $true ($crashFamily[0].problem_id -match '^error:[0-9a-f]{16}$') 'Error identifier uses a compact stable hash.'
Assert-Equal $true ($crashFamily[0].error_signature -match 'GRAPHICAL_HOST_FAULTED') 'Readable signature preserves full normalized error.'
$otherCrash = New-TestCase 'other-crash' 12 @{ state = 'error'; functional_errors = @('CRASH', 'RESOURCE_MISSING'); observed_executions = 0; casts = @() }
Assert-Equal 2 @(Get-ValidationProblems @($crash, $otherCrash)).Count 'Distinct full error signatures produce separate families.'
$withExit = New-TestCase 'crash-exit' 13 @{ state = 'error'; functional_errors = @('GRAPHICAL_HOST_FAULTED'); observed_executions = 0; exit_code = 5; casts = @() }
Assert-Equal 2 @(Get-ValidationProblems @($crash, $withExit)).Count 'Available exit code distinguishes error families.'
$multiError1 = New-TestCase 'multi-error-a' 14 @{ state = 'error'; functional_errors = @('CRASH', '  RESOURCE   MISSING '); casts = @() }
$multiError2 = New-TestCase 'multi-error-b' 15 @{ state = 'error'; functional_errors = @('resource missing', 'crash'); casts = @() }
Assert-Equal 1 @(Get-ValidationProblems @($multiError1, $multiError2)).Count 'Full signature ignores error ordering, whitespace and casing.'
$executedError = New-TestCase 'executed-error' 16 @{ state = 'error'; functional_errors = @('EXECUTION_FAILED'); observed_executions = 1; casts = @() }
$executedProblem = @(Get-ValidationProblems @($executedError))
Assert-Equal 1 $executedProblem[0].priority 'Functional failure after dispatch has priority one.'
Assert-Equal 'failure-after-dispatch' $executedProblem[0].evidence 'Executed failure is distinguished from unverified setup.'

$bothIssues = New-TestCase 'slow-overhead' 20 @{ classification = 'sustained-low-fps'; problem_casts = 2; casts = @($sustainedCast, $sustainedCast); overhead_ms = 12000 }
$otherOverhead = New-TestCase 'other-overhead' 99 @{ overhead_ms = 14000 }
$multipleIssues = @(Get-ValidationProblems @($bothIssues, $otherOverhead))
Assert-Equal 2 $multipleIssues.Count 'Overhead remains separate from an effect performance problem.'
Assert-Equal 'effect:20,infrastructure:overhead' ($multipleIssues.problem_id -join ',') 'Overhead has one infrastructure identifier across effects.'
Assert-Equal '20,99' ($multipleIssues[1].effect_ids -join ',') 'Overhead includes all affected effects.'
Assert-Equal 4000 $multipleIssues[1].impact_ms 'Overhead ranks by maximum excess duration.'
Assert-Equal 'supervisor-overhead' ($multipleIssues[1].kinds -join ',') 'Overhead group has only infrastructure kind.'

$retryError = New-TestCase 'retry-error' 100 @{ state = 'pending'; retained_unresolved = $true; classification = 'error'; functional_errors = @('EXECUTION_FAILED'); observed_executions = 1; casts = @() }
$retrySlow = New-TestCase 'retry-slow' 101 @{ state = 'pending'; retained_unresolved = $true; classification = 'sustained-low-fps'; problem_casts = 2; casts = @($sustainedCast, $sustainedCast) }
$retryProblems = @(Get-ValidationProblems @($retryError, $retrySlow))
Assert-Equal 2 $retryProblems.Count 'Incomplete retries retain prior error and performance problems.'
Assert-Equal 'incomplete-retry,incomplete-retry' ($retryProblems.evidence -join ',') 'Incomplete retries do not claim fresh evidence or resolution.'
Assert-Equal 'functional-error,sustained-low-fps' (($retryProblems.kinds | Sort-Object) -join ',') 'Retained performance is not relabeled a functional error.'

$tiedVariant = New-TestCase 'a-tied-variant' 20 @{ classification = 'sustained-low-fps'; problem_casts = 2; casts = @($sustainedCast, $sustainedCast) }
$forward = @(Get-ValidationProblems @($sameCrash, $crash, $sustainedBoth, $tiedVariant, $bothIssues, $otherOverhead)) | ConvertTo-Json -Depth 10 -Compress
$reverse = @(Get-ValidationProblems @($otherOverhead, $bothIssues, $tiedVariant, $sustainedBoth, $crash, $sameCrash)) | ConvertTo-Json -Depth 10 -Compress
Assert-Equal $forward $reverse 'Complete serialized problem groups are independent of input ordering.'

$startupCrash = New-TestCase 'startup-crash' 110 @{ state = 'error'; functional_errors = @('CRASH'); error_phase = 'initialization'; exit_code = 1; observed_executions = 0; casts = @() }
$actionCrash = New-TestCase 'action-crash' 111 @{ state = 'error'; functional_errors = @('CRASH'); error_phase = 'action'; exit_code = 1; observed_executions = 1; casts = @() }
$abnormalCrash = New-TestCase 'abnormal-crash' 112 @{ state = 'error'; functional_errors = @('CRASH'); error_phase = 'initialization'; exit_code = -1; observed_executions = 0; casts = @() }
$renderFault = New-TestCase 'startup-render-fault' 113 @{ state = 'error'; functional_errors = @('GRAPHICAL_HOST_FAULTED'); error_phase = 'initialization'; exit_code = 1; observed_executions = 0; casts = @() }
$startupProblem = @(Get-ValidationProblems @($startupCrash))
Assert-Equal 3 $startupProblem[0].priority 'Initialization exit one with generic CRASH does not prove a gameplay crash.'
Assert-Equal 'unverified-startup-failure' $startupProblem[0].evidence 'Generic startup failure has explicit uncertainty.'
Assert-Equal 0 @(Get-ValidationProblems @($actionCrash))[0].priority 'Action-phase crash remains priority zero.'
Assert-Equal 0 @(Get-ValidationProblems @($abnormalCrash))[0].priority 'Abnormal initialization exit remains priority zero.'
Assert-Equal 0 @(Get-ValidationProblems @($renderFault))[0].priority 'Explicit render fault remains priority zero during initialization.'
Assert-Equal 3 @(Get-ValidationProblems @($startupCrash, $actionCrash, $abnormalCrash)).Count 'Error phase and exit code distinguish full signatures.'
Assert-Equal $true ($startupProblem[0].error_signature -match 'phase=initialization; exit_code=1') 'Readable signature includes phase and exit code.'

$severeRepeatedCast = New-TestCast @(16, 150, 16, 150, 16)
$severeRepeated = New-TestCase 'severe-repeated-both' 120 @{ classification = 'repeated-hitches'; problem_casts = 2; casts = @($severeRepeatedCast, $severeRepeatedCast) }
$mildSingle = New-TestCase 'mild-sustained-single' 121 @{ classification = 'sustained-low-fps'; problem_casts = 1; casts = @($sustainedCast, $healthyCast) }
$severityOrder = @(Get-ValidationProblems @($mildSingle, $severeRepeated))
Assert-Equal '120,121' ($severityOrder.effect_id -join ',') 'Severe repeated slowdown in both casts precedes mild single-cast sustained slowdown.'
Assert-Equal '1,2' ($severityOrder.priority -join ',') 'Both-cast severe effective FPS promotes repeated hitches.'
$mixed = New-TestCase 'mixed-sustained-and-isolated' 122 @{ classification = 'sustained-low-fps'; problem_casts = 2; casts = @($sustainedCast, $hundredCast) }
Assert-Equal 2 @(Get-ValidationProblems @($mixed))[0].priority 'One sustained cast plus an isolated hitch does not establish two sustained casts.'
$oneSevere = New-TestCase 'one-severe-repeated' 123 @{ classification = 'repeated-hitches'; problem_casts = 2; casts = @($severeRepeatedCast, $hundredCast) }
Assert-Equal 2 @(Get-ValidationProblems @($oneSevere))[0].priority 'Severe wall-time slowdown must occur in both casts.'
$fpsBoundaryCast = [pscustomobject]@{ effect = [pscustomobject]@{ Classification = 'repeated-hitches'; SampleAvailability = 'available'; EffectiveFps = 20.0; SlowTimeShare = 1.0; ExcessFrameTimeMs = 100; MaximumFrameMs = 150 } }
$fpsBoundary = New-TestCase 'severe-fps-boundary' 124 @{ classification = 'repeated-hitches'; problem_casts = 2; casts = @($fpsBoundaryCast, $fpsBoundaryCast) }
Assert-Equal 2 @(Get-ValidationProblems @($fpsBoundary))[0].priority 'Exactly 20 effective FPS does not meet the severe heuristic.'
$smallShareCast = [pscustomobject]@{ effect = [pscustomobject]@{ Classification = 'repeated-hitches'; SampleAvailability = 'available'; EffectiveFps = 19.0; SlowTimeShare = 0.24; ExcessFrameTimeMs = 100; MaximumFrameMs = 150 } }
$smallShareCase = New-TestCase 'severe-share-boundary' 125 @{ classification = 'repeated-hitches'; problem_casts = 2; casts = @($smallShareCast, $smallShareCast) }
Assert-Equal 2 @(Get-ValidationProblems @($smallShareCase))[0].priority 'Low effective FPS alone without sufficient slow wall-time share is not promoted.'
$legacyFixture = New-TestCase 'legacy-metrics-absent' 126 @{ classification = 'sustained-low-fps'; problem_casts = 2; casts = @() }
Assert-Equal 1 @(Get-ValidationProblems @($legacyFixture))[0].priority 'Fixtures without casts retain the legacy both-cast count fallback.'
$missingCastCase = New-TestCase 'metrics-present-unavailable' 127 @{ classification = 'sustained-low-fps'; problem_casts = 2; casts = @($sustainedCast, $unavailableCast) }
Assert-Equal 2 @(Get-ValidationProblems @($missingCastCase))[0].priority 'Present but unavailable casts do not use the legacy fallback.'

$stackMessageA = "System.InvalidOperationException: Example resource unavailable.`n   at Memoria.Example.Load() in /build/one/Example.cs:line 12"
$stackMessageB = "System.InvalidOperationException: Example resource unavailable.`n   at Memoria.Example.Load() in /build/two/Example.cs:line 987"
$stackCaseA = New-TestCase 'stack-path-a' 130 @{ state = 'error'; functional_errors = @($stackMessageA); error_phase = 'action'; exit_code = 1; casts = @() }
$stackCaseB = New-TestCase 'stack-path-b' 131 @{ state = 'error'; functional_errors = @($stackMessageB); error_phase = 'action'; exit_code = 1; casts = @() }
$stackGroup = @(Get-ValidationProblems @($stackCaseA, $stackCaseB))
Assert-Equal 1 $stackGroup.Count 'Same primary exception groups across changed stack paths and source coordinates.'
Assert-Equal $false ($stackGroup[0].error_signature -match 'BUILD/|EXAMPLE.CS|MEMORIA.EXAMPLE.LOAD') 'Displayed family signature excludes stack provenance.'
Assert-Equal $true ($stackGroup[0].error_signature -match 'EXAMPLE RESOURCE UNAVAILABLE') 'Displayed family signature retains the primary exception message.'
$differentMain = New-TestCase 'different-main-message' 132 @{ state = 'error'; functional_errors = @("System.InvalidOperationException: Different resource unavailable.`n   at Memoria.Example.Load() in /build/two/Example.cs:line 987"); error_phase = 'action'; exit_code = 1; casts = @() }
Assert-Equal 2 @(Get-ValidationProblems @($stackCaseA, $differentMain)).Count 'Different primary exception messages remain distinct families.'

$queueCases = @()
for ($id = 1; $id -le 4; $id++) {
    $cast = [pscustomobject]@{effect=[pscustomobject]@{Classification='repeated-hitches';SampleAvailability='available';EffectiveFps=10;SlowTimeShare=0.8;ExcessFrameTimeMs=(5000-$id*100);MaximumFrameMs=150}}
    $queueCases += New-TestCase "queue-$id" $id @{classification='repeated-hitches';problem_casts=2;casts=@($cast,$cast)}
}
$beforeQueue = @(Get-ValidationProblems $queueCases | Select-Object -First 3)
Assert-Equal 'effect:1,effect:2,effect:3' ($beforeQueue.problem_id -join ',') 'The initial top three are deterministic.'
$fixedQueue = @((New-TestCase 'queue-1' 1), $queueCases[1], $queueCases[2], $queueCases[3])
$afterQueue = @(Get-ValidationProblems $fixedQueue | Select-Object -First 3)
Assert-Equal 'effect:2,effect:3,effect:4' ($afterQueue.problem_id -join ',') 'Fresh healthy evidence removes the resolved effect and promotes the next problem.'
$partialQueue = @($fixedQueue) + @(New-TestCase 'queue-1-other-variant' 1 @{classification='repeated-hitches';casts=$queueCases[0].casts;problem_casts=2})
Assert-Equal $true (@(Get-ValidationProblems $partialQueue).problem_id -contains 'effect:1') 'Fixing one variant does not silently close another affected variant.'
$pendingPrior = New-TestCase 'queue-1' 1 @{state='pending';retained_unresolved=$true;classification='repeated-hitches';casts=$queueCases[0].casts;problem_casts=2}
Assert-Equal 'incomplete-retry' @(Get-ValidationProblems @($pendingPrior))[0].evidence 'Interrupted retry evidence preserves the unresolved queue item.'

Write-Output "Passed $script:assertionCount synthetic validation triage assertions."
