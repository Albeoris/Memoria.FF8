param(
    [Parameter(Mandatory = $true)][string] $Manifest,
    [Parameter(Mandatory = $true)][string] $GameDirectory,
    [Parameter(Mandatory = $true)][string] $TestExecutable,
    [Parameter(Mandatory = $true)][string] $OutputDirectory
)

$ErrorActionPreference = 'Stop'
$manifestPath = (Resolve-Path -LiteralPath $Manifest).Path
$testPath = (Resolve-Path -LiteralPath $TestExecutable).Path
$gamePath = (Resolve-Path -LiteralPath $GameDirectory).Path
$outputPath = [System.IO.Path]::GetFullPath($OutputDirectory)
[System.IO.Directory]::CreateDirectory($outputPath) | Out-Null
$cases = (Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json).Cases
if ($null -eq $cases -or $cases.Count -eq 0) {
    throw 'The external manifest contains no cases.'
}
if (@($cases.Id | Select-Object -Unique).Count -ne $cases.Count) {
    throw 'Case identities must be unique.'
}

$environmentNames = @('MEMORIA_FF8_GAME_DIR', 'MEMORIA_FF8_EFFECT_FAMILY_MANIFEST', 'MEMORIA_FF8_EFFECT_FAMILY_CASE', 'MEMORIA_FF8_EFFECT_FAMILY_PHASE', 'MEMORIA_FF8_EFFECT_FAMILY_RESULT', 'MEMORIA_FF8_EFFECT_FAMILY_FIXTURE_OUTPUT')
$priorEnvironment = @{}
foreach ($name in $environmentNames) {
    $priorEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
}
$results = [System.Collections.Generic.List[object]]::new()
$binaryIdentity = Get-FileHash -LiteralPath $testPath -Algorithm SHA256
try {
    $env:MEMORIA_FF8_GAME_DIR = $gamePath
    $env:MEMORIA_FF8_EFFECT_FAMILY_MANIFEST = $manifestPath
    $index = 0
    foreach ($case in $cases) {
        $index++
        if ($null -eq $case.ProcessTimeoutSeconds -or $case.ProcessTimeoutSeconds -le 0 -or $case.ProcessTimeoutSeconds -gt 2147483) {
            throw 'Each case requires an explicit positive ProcessTimeoutSeconds within the process wait range.'
        }
        foreach ($phase in @('run', 'cold')) {
            $env:MEMORIA_FF8_EFFECT_FAMILY_CASE = $case.Id
            $env:MEMORIA_FF8_EFFECT_FAMILY_PHASE = $phase
            $resultPath = Join-Path $outputPath ('{0:D4}-{1}.json' -f $index, $phase)
            if (Test-Path -LiteralPath $resultPath) {
                throw "A prior result already exists: $resultPath. Select a distinct output directory."
            }
            $env:MEMORIA_FF8_EFFECT_FAMILY_RESULT = $resultPath
            $env:MEMORIA_FF8_EFFECT_FAMILY_FIXTURE_OUTPUT = Join-Path $outputPath ('{0:D4}-{1}-fixtures' -f $index, $phase)
            # A fresh test process for each phase/case; binaries are supplied and never rebuilt here.
            $processClock = [System.Diagnostics.Stopwatch]::StartNew()
            $stdoutPath = Join-Path $outputPath ('{0:D4}-{1}-stdout.txt' -f $index, $phase)
            $stderrPath = Join-Path $outputPath ('{0:D4}-{1}-stderr.txt' -f $index, $phase)
            $process = Start-Process -FilePath $testPath -ArgumentList @('--filter-class', '*NativeBattleEffectFamilyRunnerTests') -WindowStyle Hidden -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath -PassThru
            $timedOut = -not $process.WaitForExit([int]($case.ProcessTimeoutSeconds * 1000))
            if ($timedOut) {
                # Kill only this owned process and its descendants; preserve all results and redirected output.
                & taskkill.exe /PID $process.Id /T /F | Out-Null
                if (-not $process.WaitForExit(10000)) {
                    throw 'The timed-out owned process did not terminate; its binaries must remain untouched.'
                }
                $processExit = -1
            }
            else {
                $process.WaitForExit()
                $processExit = $process.ExitCode
            }
            $processClock.Stop()
            $processMetrics = [pscustomobject]@{ WallSeconds = $processClock.Elapsed.TotalSeconds; Command = @($testPath, '--filter-class', '*NativeBattleEffectFamilyRunnerTests'); ExecutableSha256 = $binaryIdentity.Hash; TimeoutSeconds = $case.ProcessTimeoutSeconds; TimedOut = $timedOut; StandardOutput = $stdoutPath; StandardError = $stderrPath }
            if (Test-Path -LiteralPath $resultPath) {
                $result = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
                if ($timedOut) {
                    $result = [pscustomobject]@{ Status = 'blocked'; FirstBlocker = 'process-timeout: The owned test process exceeded its explicit case limit.'; ReportedResult = $result }
                }
                $results.Add([pscustomobject]@{ Id = $case.Id; Phase = $phase; ExitCode = $processExit; Process = $processMetrics; Result = $result })
            }
            else {
                $blocker = if ($timedOut) { 'process-timeout: The owned test process exceeded its explicit case limit.' } else { 'The isolated test process produced no result.' }
                $results.Add([pscustomobject]@{ Id = $case.Id; Phase = $phase; ExitCode = $processExit; Process = $processMetrics; Result = @{ Status = 'blocked'; FirstBlocker = $blocker } })
            }
        }
    }
    $results | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $outputPath 'summary.json') -Encoding utf8
}
finally {
    foreach ($name in $environmentNames) {
        [Environment]::SetEnvironmentVariable($name, $priorEnvironment[$name], 'Process')
    }
}
if (@($results | Where-Object { $_.ExitCode -ne 0 -or $_.Result.Status -ne 'checked' }).Count -gt 0) {
    exit 1
}
