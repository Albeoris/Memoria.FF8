[CmdletBinding()]
param(
    [string] $PinsFile = (Join-Path $PSScriptRoot 'launch-pins.json'),
    [switch] $AllScenarios,
    [switch] $PrepareOnly
)

$ErrorActionPreference = 'Stop'
$pinsPath = [IO.Path]::GetFullPath($PinsFile)
$validationRoot = [IO.Path]::GetDirectoryName($pinsPath)

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

$pins = Get-Content -LiteralPath $pinsPath -Raw | ConvertFrom-Json
$toolRoot = ResolvePinnedPath $pins.tool_directory
$hostRoot = ResolvePinnedPath $pins.host_directory
if ((HashFile (Join-Path $toolRoot 'Memoria.FF8.BattleEffects.dll')) -ne $pins.tool_assembly_sha256) {
    throw 'Pinned tool assembly changed.'
}
foreach ($name in @('Memoria.FF8.Runtime', 'Memoria.FF8.Resources', 'Memoria.FF8.Assets', 'Memoria.FF8.BattleEffects')) {
    [Reflection.Assembly]::LoadFrom((Join-Path $toolRoot ($name + '.dll'))) | Out-Null
}
foreach ($directory in @(@{ Path = $toolRoot; Expected = $pins.tool_fingerprint }, @{ Path = $hostRoot; Expected = $pins.host_fingerprint })) {
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
& $toolExe run --input $selectedPath
$runExit = $LASTEXITCODE
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
