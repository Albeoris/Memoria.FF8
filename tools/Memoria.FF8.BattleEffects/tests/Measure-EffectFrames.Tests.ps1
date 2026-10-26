[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '../Measure-EffectFrames.ps1')
$script:assertionCount = 0

function Assert-Equal($Expected, $Actual, [string] $Message) {
    $script:assertionCount++
    if ($Expected -ne $Actual) {
        throw "$Message Expected '$Expected', got '$Actual'."
    }
}

function Assert-Near([double] $Expected, [double] $Actual, [string] $Message) {
    $script:assertionCount++
    if (-not [double]::IsFinite($Actual) -or [Math]::Abs($Expected - $Actual) -gt 0.000001) {
        throw "$Message Expected $Expected, got $Actual."
    }
}

function Assert-Throws([scriptblock] $Action, [string] $Message) {
    $script:assertionCount++
    $threw = $false
    try {
        & $Action | Out-Null
    } catch {
        $threw = $true
    }
    if (-not $threw) {
        throw "$Message Expected an exception."
    }
}

$healthy = Get-EffectFrameMetrics -IntervalMs @(10, 20, 30)
Assert-Equal 'available' $healthy.SampleAvailability 'Healthy sample availability.'
Assert-Equal 3 $healthy.SampleCount 'Sample count.'
Assert-Equal 'healthy' $healthy.Classification 'Healthy classification.'
Assert-Near 60 $healthy.TotalElapsedMs 'Elapsed wall time.'
Assert-Near 50 $healthy.MedianFps 'Odd median FPS.'
Assert-Near 50 $healthy.EffectiveFps 'Effective FPS.'
Assert-Near 0 $healthy.SlowTimeShare 'Healthy slow time share.'
Assert-Near 0 $healthy.WorstSlowRunMs 'Healthy slow run.'

$even = Get-EffectFrameMetrics -IntervalMs @(10, 20)
Assert-Near 75 $even.MedianFps 'Even median averages FPS, not frame durations.'

# One bad frame biases the median very differently from elapsed-time throughput.
$isolated = Get-EffectFrameMetrics -IntervalMs (@(16.0) * 99 + @(2000.0))
Assert-Equal 'isolated-hitch' $isolated.Classification 'A single arbitrarily long frame is not sustained low FPS.'
Assert-Near 62.5 $isolated.MedianFps 'Median remains fast with one stall.'
Assert-Near (100000.0 / 3584.0) $isolated.EffectiveFps 'Effective FPS counts stall wall time.'
Assert-Near (2000.0 / 3584.0) $isolated.SlowTimeShare 'Slow time share is weighted by duration.'
Assert-Near $isolated.SlowTimeShare $isolated.Sub10FpsTimeShare 'Sub-10 FPS wall time.'
Assert-Near (2000.0 - 1000.0 / 30.0) $isolated.ExcessFrameTimeMs 'Only excess above budget is counted.'
Assert-Near 2000 $isolated.WorstSlowRunMs 'Trailing slow run is included.'
Assert-Equal 1 $isolated.LongStallCount 'One long stall.'

$sustained = Get-EffectFrameMetrics -IntervalMs (@(50.0) * 12)
Assert-Equal 'sustained-low-fps' $sustained.Classification 'Steady 20 FPS is sustained.'
Assert-Near 20 $sustained.MedianFps 'Steady median.'
Assert-Near 20 $sustained.EffectiveFps 'Steady effective FPS.'
Assert-Near 1 $sustained.SlowTimeShare 'All elapsed time is slow.'
Assert-Near 0 $sustained.Sub10FpsTimeShare '20 FPS does not count as sub-10.'
Assert-Near 600 $sustained.WorstSlowRunMs 'Contiguous slow wall time.'
Assert-Equal 0 $sustained.LongStallCount 'No long stalls at 20 FPS.'

$repeated = Get-EffectFrameMetrics -IntervalMs @(16, 150, 16, 150, 16)
Assert-Equal 'repeated-hitches' $repeated.Classification 'Separated stalls are repeated hitches.'
Assert-Equal 2 $repeated.SlowRunCount 'Fast intervals split runs.'
Assert-Equal 2 $repeated.LongStallCount 'Long stall count.'
Assert-Near 150 $repeated.WorstSlowRunMs 'Run duration does not span fast intervals.'
Assert-Equal 'repeated-hitches' (Get-EffectFrameMetrics -IntervalMs @(150, 150)).Classification 'Two adjacent stalls below sustained duration are repeated.'
Assert-Equal 'isolated-hitch' (Get-EffectFrameMetrics -IntervalMs @(34, 16, 34)).Classification 'Separated tiny slow intervals do not establish repeated substantial hitches.'
Assert-Equal 'repeated-hitches' (Get-EffectFrameMetrics -IntervalMs @(120, 16, 120)).Classification 'Two separated 120 ms stalls are repeated.'
$substantialRuns = Get-EffectFrameMetrics -IntervalMs @(40, 40, 40, 16, 40, 40, 40)
Assert-Equal 'repeated-hitches' $substantialRuns.Classification 'Two substantial slow runs need no long stalls.'
Assert-Equal 2 $substantialRuns.SignificantSlowRunCount 'Significant runs count once each.'
Assert-Equal 6 $substantialRuns.SlowIntervalCount 'All slow intervals are counted.'
Assert-Equal 0 $substantialRuns.Sub10IntervalCount 'Moderately slow frames are not sub-10.'
Assert-Equal 'isolated-hitch' (Get-EffectFrameMetrics -IntervalMs @(40, 40, 40, 16, 40, 40, 40) -SignificantSlowRunMs 121).Classification 'Significant run threshold is configurable.'
Assert-Equal 2 (Get-EffectFrameMetrics -IntervalMs @(50, 50, 16, 50, 50)).SignificantSlowRunCount 'Significant duration includes exact threshold equality.'
Assert-Equal 'isolated-hitch' (Get-EffectFrameMetrics -IntervalMs @(500)).Classification 'A single 500 ms freeze remains isolated.'
Assert-Near 2000 $isolated.MaximumFrameMs 'Maximum retains arbitrarily large isolated stall duration.'
Assert-Equal 1 $isolated.SlowIntervalCount 'Isolated slow interval count.'
Assert-Equal 1 $isolated.Sub10IntervalCount 'Isolated sub-10 interval count.'
Assert-Equal 1 $isolated.SignificantSlowRunCount 'Trailing significant run is counted.'
Assert-Near 16 $isolated.P95FrameMs 'Nearest rank P95 excludes the single final stall among 100 samples.'
Assert-Near 16 $isolated.P99FrameMs 'Nearest rank P99 excludes the single final stall among 100 samples.'
$percentiles = Get-EffectFrameMetrics -IntervalMs (100..1)
Assert-Near 95 $percentiles.P95FrameMs 'P95 uses sorted nearest rank.'
Assert-Near 99 $percentiles.P99FrameMs 'P99 uses sorted nearest rank.'
Assert-Near 100 $percentiles.MaximumFrameMs 'Maximum uses sorted intervals.'
Assert-Near 100 (Get-EffectFrameMetrics -IntervalMs @(100)).P95FrameMs 'Single-sample percentile is that sample.'

$boundary = Get-EffectFrameMetrics -IntervalMs @(1000.0 / 30.0)
Assert-Equal 'healthy' $boundary.Classification 'Exactly 30 FPS is not slow.'
$stallBoundary = Get-EffectFrameMetrics -IntervalMs @(100)
Assert-Equal 0 $stallBoundary.LongStallCount 'Exactly 10 FPS is not a long stall.'
Assert-Near 0 $stallBoundary.Sub10FpsTimeShare 'Exactly 10 FPS is not sub-10 FPS.'
Assert-Equal 'sustained-low-fps' (Get-EffectFrameMetrics -IntervalMs @(200, 200, 100)).Classification 'Sustained duration uses inclusive threshold.'
Assert-Equal 'repeated-hitches' (Get-EffectFrameMetrics -IntervalMs @(250, 250)).Classification 'Sustained runs require enough samples, not duration alone.'

$smallShare = Get-EffectFrameMetrics -IntervalMs (@(50.0) * 10 + @(16.0) * 100)
Assert-Equal 'isolated-hitch' $smallShare.Classification 'A long run below minimum wall-time share is not sustained.'
Assert-Equal 'sustained-low-fps' (Get-EffectFrameMetrics -IntervalMs (@(50.0) * 10 + @(16.0) * 100) -SustainedSlowTimeShare 0.2).Classification 'Wall-time share threshold is configurable.'
Assert-Equal 'healthy' (Get-EffectFrameMetrics -IntervalMs @(40, 40) -SlowFrameMs 50).Classification 'Slow-frame threshold is configurable.'
Assert-Equal 'sustained-low-fps' (Get-EffectFrameMetrics -IntervalMs @(60, 60, 60) -SustainedRunMs 180).Classification 'Sustained run duration is configurable.'
Assert-Equal 'repeated-hitches' (Get-EffectFrameMetrics -IntervalMs @(200, 200, 100) -SustainedRunMinimumSamples 4).Classification 'Sustained minimum sample count is configurable.'
$customStall = Get-EffectFrameMetrics -IntervalMs @(150) -LongStallMs 200
Assert-Equal 0 $customStall.LongStallCount 'Stall threshold is configurable.'
Assert-Near 1 $customStall.Sub10FpsTimeShare 'Sub-10 FPS always means over 100 ms, independently of stall heuristic.'

foreach ($missing in @(@{ Samples = $null }, @{ Samples = @() })) {
    $unavailable = Get-EffectFrameMetrics -IntervalMs $missing.Samples
    Assert-Equal 'unavailable' $unavailable.SampleAvailability 'Missing sample availability.'
    Assert-Equal 'unavailable' $unavailable.Classification 'Missing sample classification.'
    Assert-Equal 0 $unavailable.SampleCount 'Missing sample count.'
    Assert-Equal $null $unavailable.EffectiveFps 'Missing samples do not become zero FPS.'
}
foreach ($invalid in @(0.0, -1.0, [double]::NaN, [double]::PositiveInfinity, [double]::NegativeInfinity, [double]::Epsilon)) {
    $unavailable = Get-EffectFrameMetrics -IntervalMs @(16, $invalid, 16)
    Assert-Equal 'unavailable' $unavailable.SampleAvailability 'Invalid samples remain unavailable.'
    Assert-Equal 3 $unavailable.SampleCount 'Invalid samples are not silently dropped.'
    Assert-Equal $null $unavailable.TotalElapsedMs 'Invalid samples have no computed total.'
}
Assert-Equal 'unavailable' (Get-EffectFrameMetrics -IntervalMs @([double]::MaxValue, [double]::MaxValue)).Classification 'Overflowed totals are unavailable.'
Assert-Throws { Get-EffectFrameMetrics -IntervalMs @(16) -SlowFrameMs 0 } 'Zero slow threshold.'
Assert-Throws { Get-EffectFrameMetrics -IntervalMs @(16) -LongStallMs 10 } 'Stall threshold below slow threshold.'
Assert-Throws { Get-EffectFrameMetrics -IntervalMs @(16) -SustainedRunMs ([double]::NaN) } 'Nonfinite sustained duration.'
Assert-Throws { Get-EffectFrameMetrics -IntervalMs @(16) -SustainedRunMinimumSamples 1 } 'Single-sample sustained threshold.'
Assert-Throws { Get-EffectFrameMetrics -IntervalMs @(16) -SustainedSlowTimeShare 0 } 'Zero sustained share.'
Assert-Throws { Get-EffectFrameMetrics -IntervalMs @(16) -SustainedSlowTimeShare 1.1 } 'Sustained share above one.'
Assert-Throws { Get-EffectFrameMetrics -IntervalMs @(16) -SignificantSlowRunMs 0 } 'Zero significant run threshold.'

Write-Output "Passed $script:assertionCount synthetic frame metric assertions."
