[CmdletBinding()]
param(
    [string] $PinsFile = (Join-Path $PSScriptRoot 'launch-pins.json'),
    [switch] $AllScenarios,
    [switch] $PrepareOnly,
    [switch] $Recalculate,
    [double] $BelowFps = 30,
    [switch] $Triage,
    [switch] $Retry,
    [int[]] $EffectId = @(),
    [string[]] $CaseId = @(),
    [string[]] $ErrorCode = @(),
    [switch] $Errors,
    [switch] $LowFps,
    [string[]] $ProblemId = @(),
    [int] $Top = 3,
    [int] $RetryTop = 0,
    [string] $HostExecutable
)

$ErrorActionPreference = 'Stop'
$pinsPath = [IO.Path]::GetFullPath($PinsFile)
$validationRoot = [IO.Path]::GetDirectoryName($pinsPath)
if (@($Recalculate, $Triage, $Retry | Where-Object { $_ }).Count -gt 1) {
    throw 'Choose only one of Recalculate, Triage or Retry.'
}
if (($Recalculate -or $Triage) -and $PrepareOnly) {
    throw 'Choose either Recalculate or PrepareOnly.'
}
if (($Recalculate -or $Triage -or $Retry) -and (-not [double]::IsFinite($BelowFps) -or $BelowFps -le 0)) {
    throw 'BelowFps must be finite and positive.'
}
if ($Top -lt 1 -or $RetryTop -lt 0) { throw 'Top must be positive; RetryTop must be nonnegative.' }
if (-not $Retry -and ($EffectId.Count -or $CaseId.Count -or $ErrorCode.Count -or $ProblemId.Count -or $RetryTop -or $HostExecutable)) {
    throw 'Retry selectors and HostExecutable require -Retry.'
}
if (-not ($Retry -or $Triage) -and ($Errors -or $LowFps)) { throw 'Errors and LowFps require Triage or Retry.' }
if ($Retry -and -not ($EffectId.Count -or $CaseId.Count -or $ErrorCode.Count -or $Errors -or $LowFps -or $ProblemId.Count -or $RetryTop)) { throw 'Retry requires an explicit selector.' }

function HashFile([string] $path) {
    $stream = [IO.File]::OpenRead($path)
    try {
        [Convert]::ToHexStringLower([Security.Cryptography.SHA256]::HashData($stream))
    } finally {
        $stream.Dispose()
    }
}

function WriteNewJson([string] $path, [object] $value) {
    $stream = [IO.FileStream]::new($path, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
    try {
        $bytes = [Text.Encoding]::UTF8.GetBytes(($value | ConvertTo-Json -Depth 100))
        $stream.Write($bytes)
        $stream.Flush($true)
    } finally {
        $stream.Dispose()
    }
}

function ResolvePinnedPath([string] $path) {
    if ([string]::IsNullOrWhiteSpace($path)) {
        throw 'A required pinned path is empty.'
    }
    return [IO.Path]::GetFullPath($path, $validationRoot)
}

function WriteLatestJson([string] $path, [object] $value) {
    $temporary = $path + '.' + [Guid]::NewGuid().ToString('N') + '.tmp'
    try {
        WriteNewJson $temporary $value
        if ([IO.File]::Exists($path)) {
            [IO.File]::Replace($temporary, $path, $path + '.' + [Guid]::NewGuid().ToString('N') + '.previous.json')
        } else {
            [IO.File]::Move($temporary, $path)
        }
    } finally {
        if ([IO.File]::Exists($temporary)) {
            [IO.File]::Delete($temporary)
        }
    }
}

function Invoke-PinnedRun([string] $InputManifest) {
    $previousProfile = [Environment]::GetEnvironmentVariable('MEMORIA_FF8_EXECUTION_STACK_PROFILE', 'Process')
    try {
        if ($pins.execution_stack_profile) {
            $profile = ResolvePinnedPath $pins.execution_stack_profile
            if ((HashFile $profile) -ne $pins.execution_stack_profile_sha256) { throw 'Pinned execution-stack profile changed.' }
            [Environment]::SetEnvironmentVariable('MEMORIA_FF8_EXECUTION_STACK_PROFILE', $profile, 'Process')
        }
        & $toolExe run --input $InputManifest
        $script:validationRunExit = $LASTEXITCODE
    } finally {
        [Environment]::SetEnvironmentVariable('MEMORIA_FF8_EXECUTION_STACK_PROFILE', $previousProfile, 'Process')
    }
}

function Publish-Triage($Report, [string] $Suffix) {
    $path = Join-Path $validationRoot ("triage-$Suffix.json")
    WriteNewJson $path $Report
    WriteLatestJson (Join-Path $validationRoot 'latest-triage.json') ([ordered]@{
        schema_version=1; generated_utc=[DateTimeOffset]::UtcNow; report=$path; report_sha256=(HashFile $path)
        manifest=$templatePath; manifest_sha256=$templateSha; pins_sha256=(HashFile $pinsPath); below_fps=$BelowFps
    })
    return $path
}

$pins = Get-Content -LiteralPath $pinsPath -Raw | ConvertFrom-Json
$toolRoot = ResolvePinnedPath $pins.tool_directory
$hostRoot = ResolvePinnedPath $pins.host_directory
if ((HashFile (Join-Path $toolRoot 'Memoria.FF8.BattleEffects.dll')) -ne $pins.tool_assembly_sha256) {
    throw 'Pinned tool assembly changed.'
}
foreach ($name in @('Memoria.FF8.Runtime', 'Memoria.FF8.Resources', 'Memoria.FF8.Assets', 'Memoria.FF8.BattleEffects')) {
    [Reflection.Assembly]::LoadFrom((Join-Path $toolRoot ($name + '.dll'))) | Out-Null
}
$directories = @(@{ Path = $toolRoot; Expected = $pins.tool_fingerprint })
if (-not ($Recalculate -or $Triage)) {
    $directories += @{ Path = $hostRoot; Expected = $pins.host_fingerprint }
}
foreach ($directory in $directories) {
    $files = [Memoria.FF8.BattleEffects.EvidenceStore]::DescribeFiles($directory.Path)
    $actual = [Memoria.FF8.BattleEffects.EvidenceStore]::HashRecord[Memoria.FF8.BattleEffects.BuildFile[]]($files)
    if ($actual -ne $directory.Expected) {
        throw "Pinned directory changed: $($directory.Path)"
    }
}
if ((HashFile (Join-Path (ResolvePinnedPath $pins.plan_root) 'plan-seal.json')) -ne $pins.plan_seal_sha256) {
    throw 'Discovery plan seal changed.'
}
$templatePath = ResolvePinnedPath $pins.primary_manifest
$templateSha = $pins.primary_manifest_sha256
$prefix = 'effects-attempt'
if ($AllScenarios) {
    $templatePath = ResolvePinnedPath $pins.full_manifest
    $templateSha = $pins.full_manifest_sha256
    $prefix = 'scenarios-attempt'
}
if ((HashFile $templatePath) -ne $templateSha) {
    throw 'Frozen manifest template changed.'
}
$manifest = Get-Content -LiteralPath $templatePath -Raw | ConvertFrom-Json
$token = [DateTimeOffset]::UtcNow.ToString('yyyyMMddTHHmmssfffZ') + '-' + [Guid]::NewGuid().ToString('N')
$toolExe = Join-Path $toolRoot 'Memoria.FF8.BattleEffects.exe'
if ($Triage -or $Retry) {
    . (Join-Path $PSScriptRoot 'Validation-Triage.ps1')
    . (Join-Path $PSScriptRoot 'Validation-Retry.ps1')
    $previous = $null
    $indexPath = Join-Path $validationRoot 'latest-triage.json'
    if (Test-Path -LiteralPath $indexPath) {
        $index = Get-Content -LiteralPath $indexPath -Raw | ConvertFrom-Json
        if ((HashFile $index.report) -ne $index.report_sha256) { throw 'Latest triage report changed.' }
        if ($index.manifest_sha256 -eq $templateSha) { $previous = Get-Content -LiteralPath $index.report -Raw | ConvertFrom-Json }
    }
    $measurement = [Memoria.FF8.BattleEffects.PilotRunner]::AcquireMeasurementSlot([NullString]::Value)
    try {
        $before = New-ValidationTriageReport -ManifestPath $templatePath -HistoryRoot $validationRoot -PreviousReport $previous -BelowFps $BelowFps
        $beforePath = Publish-Triage $before "$token-before"
    } finally { $measurement.Dispose() }
    if ($Triage) {
        $problems = @($before.problems)
        if ($Errors -or $LowFps) { $problems = @($problems | Where-Object { ($Errors -and $_.kinds -contains 'functional-error') -or ($LowFps -and (@($_.kinds | Where-Object { $_ -in @('sustained-low-fps','repeated-hitches') }).Count -gt 0)) }) }
        [ordered]@{report=$beforePath;requested=$before.requested;tested=$before.tested;errors=$before.errors;blocked=$before.blocked;pending=$before.pending;top_problems=@($problems | Select-Object -First $Top rank,problem_id,effect_id,effect_ids,error_signature,priority,impact_ms,evidence,kinds,affected_cases,representative_case_id);launched=$false} | ConvertTo-Json -Depth 20
        return
    }
    $selectedIds = @(Select-ValidationRetryCases -Report $before -EffectId $EffectId -CaseId $CaseId -ErrorCode $ErrorCode -Errors:$Errors -LowFps:$LowFps -ProblemId $ProblemId -Top $RetryTop)
    $selection = [Collections.Generic.HashSet[string]]::new([string[]]$selectedIds, [StringComparer]::Ordinal)
    $manifest.cases = @($manifest.cases | Where-Object { $selection.Contains($_.id) })
    if ($manifest.cases.Count -ne $selectedIds.Count) { throw 'Retry selection does not match the frozen manifest.' }
    $retryOutput = Join-Path $validationRoot ("retry-$token")
    $retryManifest = $retryOutput + '.manifest.json'
    $retryReceipt = $retryOutput + '.comparison.json'
    if ($HostExecutable) {
        $retryExe = [IO.Path]::GetFullPath($HostExecutable)
        if (-not [IO.File]::Exists($retryExe)) { throw 'Retry host executable does not exist.' }
        $retryHost = [IO.Path]::GetDirectoryName($retryExe)
        $hostFiles = [Memoria.FF8.BattleEffects.EvidenceStore]::DescribeFiles($retryHost)
        $retryFingerprint = [Memoria.FF8.BattleEffects.EvidenceStore]::HashRecord[Memoria.FF8.BattleEffects.BuildFile[]]($hostFiles)
    } else {
        $retryExe = Join-Path $hostRoot 'Memoria.FF8.exe'
        $retryHost = $hostRoot
        $retryFingerprint = $pins.host_fingerprint
    }
    $manifest.output_root = $retryOutput
    $manifest.executable = $retryExe
    $manifest.working_directory = $retryHost
    $manifest.build_fingerprint = $retryFingerprint
    WriteNewJson ($retryOutput + '.selection.json') ([ordered]@{schema_version=1;before_report=$beforePath;before_report_sha256=(HashFile $beforePath);case_ids=$selectedIds;host_executable=$retryExe;build_fingerprint=$retryFingerprint;prepared=[bool]$PrepareOnly;no_launch=($PrepareOnly -or $selectedIds.Count -eq 0);selection_policy='Explicit selectors are a union; this retry bypasses normal incremental skipping only for selected cases.'})
    if ($selectedIds.Count -eq 0) {
        [ordered]@{selected=0;launched=$false;report=$beforePath;message='No matching measured or errored cases.'} | ConvertTo-Json
        return
    }
    WriteNewJson $retryManifest $manifest
    & $toolExe validate-pilot --input $retryManifest | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Retry manifest validation failed.' }
    if ($PrepareOnly) {
        [ordered]@{selected=$selectedIds.Count;manifest=$retryManifest;selection=($retryOutput + '.selection.json');before_report=$beforePath;launched=$false} | ConvertTo-Json
        return
    }
    $launchError = $null
    $runExit = 4
    try {
        Invoke-PinnedRun $retryManifest
        $runExit = $script:validationRunExit
    } catch { $launchError = $_.Exception.Message }
    $after = $null
    $afterPath = $null
    $reportError = $null
    try {
        $measurement = [Memoria.FF8.BattleEffects.PilotRunner]::AcquireMeasurementSlot([NullString]::Value)
        try {
            $after = New-ValidationTriageReport -ManifestPath $templatePath -HistoryRoot $validationRoot -PreviousReport $before -BelowFps $BelowFps
            $afterPath = Publish-Triage $after "$token-after"
        } finally { $measurement.Dispose() }
    } catch { $reportError = $_.Exception.Message }
    # Only evidence written by this exact retry can confirm its outcome.
    $fenced = [Collections.Generic.List[object]]::new()
    $outputPrefix = [IO.Path]::GetFullPath($retryOutput).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if ($after) {
        foreach ($case in $after.cases) {
            if ($selection.Contains($case.case_id) -and $case.result_path -and [IO.Path]::GetFullPath($case.result_path).StartsWith($outputPrefix, [StringComparison]::OrdinalIgnoreCase)) { $fenced.Add($case) }
        }
    }
    $comparison = @(Compare-ValidationRetry -Before $before -After ([pscustomobject]@{cases=$fenced.ToArray()}) -CaseId $selectedIds)
    $exitCode = 0
    if (@($comparison | Where-Object status -in @('reproduced','changed-failure')).Count) { $exitCode = 2 }
    if ($launchError -or $reportError -or @($comparison | Where-Object status -eq 'inconclusive').Count) { $exitCode = 4 }
    WriteNewJson $retryReceipt ([ordered]@{schema_version=1;before_report=$beforePath;after_report=$afterPath;manifest=$retryManifest;output_root=$retryOutput;host_executable=$retryExe;build_fingerprint=$retryFingerprint;runtime_exit_code=$runExit;exit_code=$exitCode;launch_error=$launchError;report_error=$reportError;cases=$comparison;top_problems=@($after.problems | Select-Object -First $Top)})
    WriteLatestJson (Join-Path $validationRoot 'latest-retry.json') ([ordered]@{comparison=$retryReceipt;comparison_sha256=(HashFile $retryReceipt);report=$afterPath;exit_code=$exitCode})
    [ordered]@{comparison=$retryReceipt;report=$afterPath;exit_code=$exitCode;outcomes=@($comparison | Group-Object status | Select-Object Name,Count)} | ConvertTo-Json -Depth 10
    exit $exitCode
}
if ($Recalculate) {
    $measurement = [Memoria.FF8.BattleEffects.PilotRunner]::AcquireMeasurementSlot([NullString]::Value)
    try {
        $reportPath = Join-Path $validationRoot ("fps-recalculation-$token.json")
        $slowPath = Join-Path $validationRoot ("slow-effects-recalculated-$token.json")
        $toolExe = Join-Path $toolRoot 'Memoria.FF8.BattleEffects.exe'
        $threshold = $BelowFps.ToString('G', [Globalization.CultureInfo]::InvariantCulture)
        & $toolExe recalculate-fps --input $templatePath --history $validationRoot --below-fps $threshold --output $reportPath --slow-output $slowPath
        if ($LASTEXITCODE -ne 0) {
            throw 'FPS recalculation failed; previous reports and latest pointer remain unchanged.'
        }
        WriteLatestJson (Join-Path $validationRoot 'latest-fps-recalculation.json') ([ordered]@{
            schema_version = 1
            generated_utc = [DateTimeOffset]::UtcNow
            fps_scope = 'effect'
            below_fps = $BelowFps
            all_scenarios = [bool]$AllScenarios
            report = $reportPath
            report_sha256 = (HashFile $reportPath)
            slow_effects = $slowPath
            slow_effects_sha256 = (HashFile $slowPath)
            pins_sha256 = (HashFile $pinsPath)
            tool_fingerprint = $pins.tool_fingerprint
            raw_reports_preserved = $true
            historical_verdicts_preserved = $true
            launched = $false
        })
        Write-Output "Recalculated FPS: $reportPath"
        Write-Output "Slow effects: $slowPath"
        Write-Output 'Latest report index: latest-fps-recalculation.json; no host launched.'
        return
    } finally {
        $measurement.Dispose()
    }
}
$attempt = Join-Path $validationRoot ($prefix + '-' + $token)
$outputRoot = $attempt
$requestedPath = $attempt + '.requested.manifest.json'
$selectedPath = $attempt + '.selected.manifest.json'
$resumePath = $attempt + '.resume.json'
$receiptPath = $attempt + '.launch.json'
$completionPath = $attempt + '.completion.json'
if (Test-Path -LiteralPath $outputRoot) {
    throw 'Fresh output directory unexpectedly exists.'
}
$manifest.output_root = $outputRoot
$manifest.executable = Join-Path $hostRoot 'Memoria.FF8.exe'
$manifest.working_directory = $hostRoot
$manifest.build_fingerprint = $pins.host_fingerprint
WriteNewJson $requestedPath $manifest
$toolExe = Join-Path $toolRoot 'Memoria.FF8.BattleEffects.exe'
$resumeOutput = & $toolExe resume-plan --input $requestedPath --history $validationRoot --output $selectedPath --report $resumePath 2>&1
$resumeExit = $LASTEXITCODE
if ($resumeExit -ne 0) {
    throw "Pinned CLI could not select pending cases: $resumeOutput"
}
$resume = Get-Content -LiteralPath $resumePath -Raw | ConvertFrom-Json
$pendingCount = [int]$resume.pending_cases
$reused = @($resume.reused_cases)
$tested = @($reused | Where-Object { $_.tested }).Count
$errored = @($reused | Where-Object { -not $_.tested -and $_.case.status -eq 'RUNNABLE' -and $_.previous_result.verdict -eq 'ERROR' }).Count
$blocked = @($reused | Where-Object { -not $_.tested -and -not ($_.case.status -eq 'RUNNABLE' -and $_.previous_result.verdict -eq 'ERROR') }).Count
$passed = @($reused | Where-Object { $_.tested -and $_.previous_result.verdict -eq 'PASS' }).Count
$failed = @($reused | Where-Object { $_.tested -and $_.previous_result.verdict -eq 'FAIL' }).Count
if ($pendingCount -lt 0 -or [int]$resume.requested_cases -ne $manifest.cases.Count -or $pendingCount + $reused.Count -ne $manifest.cases.Count) {
    throw 'Resume receipt has an inconsistent requested/pending/reused case inventory.'
}
$schemaOutput = @()
$schemaExit = $null
$runtimeManifest = $null
if ($pendingCount -gt 0) {
    $runtimeManifest = $selectedPath
    $schemaOutput = & $toolExe validate-pilot --input $selectedPath 2>&1
    $schemaExit = $LASTEXITCODE
} elseif (Test-Path -LiteralPath $selectedPath) {
    throw 'A zero-pending resume must not emit an empty runtime manifest.'
}
WriteNewJson $receiptPath ([ordered]@{
    schema_version = 1
    created_utc = [DateTimeOffset]::UtcNow
    requested_manifest = $requestedPath
    requested_manifest_sha256 = (HashFile $requestedPath)
    manifest = $runtimeManifest
    manifest_sha256 = $(if ($runtimeManifest) { HashFile $runtimeManifest } else { $null })
    output_root = $outputRoot
    source_commit = $pins.source_commit
    plan_sha256 = $pins.plan_sha256
    host_fingerprint = $pins.host_fingerprint
    tool_fingerprint = $pins.tool_fingerprint
    pins_sha256 = (HashFile $pinsPath)
    resume_report = $resumePath
    resume_report_sha256 = (HashFile $resumePath)
    previous_lineage = $reused
    all_scenarios = [bool]$AllScenarios
    prepare_only = [bool]$PrepareOnly
    requested_cases = [int]$resume.requested_cases
    pending_cases = $pendingCount
    reused_tested = $tested
    reused_passed = $passed
    reused_failed = $failed
    reused_errored = $errored
    reused_blocked = $blocked
    schema_exit_code = $schemaExit
    schema_output = ($schemaOutput -join [Environment]::NewLine)
    campaign_acceptance = $false
    counts_are_execution_passes = $false
})
if ($null -ne $schemaExit -and $schemaExit -ne 0) {
    throw "Pinned CLI rejected selected runtime manifest: $schemaOutput"
}
Write-Output "Requested manifest: $requestedPath"
Write-Output "Resume lineage: $resumePath"
Write-Output "Requested cases: $($resume.requested_cases); selected pending: $pendingCount."
Write-Output "Reused tested: $tested (previous PASS: $passed; previous FAIL: $failed); reused ERROR: $errored; reused blocked/untestable: $blocked."
Write-Output 'Historical results retain their previous build lineage and do not certify this build or campaign acceptance.'
if ($pendingCount -eq 0) {
    WriteNewJson $completionPath ([ordered]@{
        schema_version = 1
        requested_manifest = $requestedPath
        manifest = $null
        resume_report = $resumePath
        completed_utc = [DateTimeOffset]::UtcNow
        pending_cases = 0
        reused_tested = $tested
        reused_passed = $passed
        reused_failed = $failed
        reused_errored = $errored
        reused_blocked = $blocked
        no_launch = $true
        exit_code = 0
        campaign_acceptance = $false
    })
    Write-Output 'No pending cases. Completion receipt saved; no host launched.'
    return
}
Write-Output "Selected manifest: $selectedPath"
Write-Output "Fresh output: $outputRoot"
if ($PrepareOnly) {
    Write-Output 'Prepared; schema and runtime path policy validated. No cases launched.'
    return
}
$started = [DateTimeOffset]::UtcNow
Invoke-PinnedRun $selectedPath
$runExit = $script:validationRunExit
WriteNewJson $completionPath ([ordered]@{
    schema_version = 1
    requested_manifest = $requestedPath
    manifest = $selectedPath
    output_root = $outputRoot
    resume_report = $resumePath
    started_utc = $started
    completed_utc = [DateTimeOffset]::UtcNow
    exit_code = $runExit
    campaign_acceptance = $false
})
exit $runExit
