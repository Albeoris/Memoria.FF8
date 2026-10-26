function Get-EffectFrameMetrics {
    <#
    .SYNOPSIS
    Summarizes a single already selected effect-only sequence of frame intervals.
    .DESCRIPTION
    Dot-source this script, then pass ordered, whole frame intervals in milliseconds.
    The caller owns boundary selection, completeness checks and aggregation across casts.
    No files are read or changed. Missing, empty or invalid samples are unavailable;
    invalid intervals are never discarded to produce a seemingly healthy result.

    MedianFps is the median of individual 1000 / interval values (frame weighted).
    EffectiveFps is SampleCount * 1000 / TotalElapsedMs (wall time weighted).
    Time shares are fractions from zero to one, counting the entire duration of
    each interval strictly greater than its threshold. ExcessFrameTimeMs sums only
    the portion above SlowFrameMs. Slow runs contain adjacent slow intervals.
    P95FrameMs and P99FrameMs use the nearest-rank percentile of frame intervals
    (sorted ascending, rank ceiling(percentile * SampleCount), one based).

    Classification is a configurable prioritization heuristic, not a correctness
    verdict, causal diagnosis or claim about the game's intended frame frequency.
    In precedence order: sustained-low-fps requires a contiguous slow run lasting
    SustainedRunMs with at least SustainedRunMinimumSamples intervals, plus at least
    SustainedSlowTimeShare of total wall time in slow frames; repeated-hitches
    requires at least two slow runs each lasting SignificantSlowRunMs or two long
    stalls; isolated-hitch requires any
    slow interval; otherwise healthy. A single arbitrarily long stall cannot alone
    qualify as sustained. Thresholds and comparisons are included in the result.
    #>
    [CmdletBinding()]
    param(
        [AllowNull()]
        [AllowEmptyCollection()]
        [double[]] $IntervalMs,
        [double] $SlowFrameMs = (1000.0 / 30.0),
        [double] $LongStallMs = 100.0,
        [double] $SignificantSlowRunMs = 100.0,
        [double] $SustainedRunMs = 500.0,
        [int] $SustainedRunMinimumSamples = 3,
        [double] $SustainedSlowTimeShare = 0.25
    )

    foreach ($threshold in @($SlowFrameMs, $LongStallMs, $SignificantSlowRunMs, $SustainedRunMs)) {
        if (-not [double]::IsFinite($threshold) -or $threshold -le 0) {
            throw 'Frame and run thresholds must be finite and positive.'
        }
    }
    if ($LongStallMs -lt $SlowFrameMs) {
        throw 'LongStallMs must be at least SlowFrameMs.'
    }
    if ($SustainedRunMinimumSamples -lt 2 -or -not [double]::IsFinite($SustainedSlowTimeShare) -or $SustainedSlowTimeShare -le 0 -or $SustainedSlowTimeShare -gt 1) {
        throw 'Sustained classification requires at least two samples and a time share in (0, 1].'
    }

    $sampleCount = 0
    if ($null -ne $IntervalMs) {
        $sampleCount = $IntervalMs.Count
    }
    $result = [ordered]@{
        SampleAvailability = 'unavailable'
        SampleCount = $sampleCount
        TotalElapsedMs = $null
        MedianFps = $null
        EffectiveFps = $null
        MaximumFrameMs = $null
        P95FrameMs = $null
        P99FrameMs = $null
        SlowTimeShare = $null
        Sub10FpsTimeShare = $null
        ExcessFrameTimeMs = $null
        WorstSlowRunMs = $null
        SlowRunCount = $null
        SignificantSlowRunCount = $null
        SlowIntervalCount = $null
        Sub10IntervalCount = $null
        LongStallCount = $null
        Classification = 'unavailable'
        ClassificationReason = 'No frame intervals were supplied.'
        Heuristic = [pscustomobject]@{
            Version = 2
            SlowFrameMs = $SlowFrameMs
            LongStallMs = $LongStallMs
            SignificantSlowRunMs = $SignificantSlowRunMs
            SustainedRunMs = $SustainedRunMs
            SustainedRunMinimumSamples = $SustainedRunMinimumSamples
            SustainedSlowTimeShare = $SustainedSlowTimeShare
            ThresholdComparison = 'strictly-greater-than for frame intervals; at-least for run and sustained thresholds'
            FramePercentileMethod = 'nearest-rank'
        }
    }
    if ($sampleCount -eq 0) {
        return [pscustomobject]$result
    }

    $total = 0.0
    $slowTime = 0.0
    $sub10Time = 0.0
    $excess = 0.0
    $runTime = 0.0
    $runCount = 0
    $slowRuns = 0
    $significantSlowRuns = 0
    $slowIntervals = 0
    $sub10Intervals = 0
    $runIsSignificant = $false
    $worstRun = 0.0
    $longStalls = 0
    $hasSustainedRun = $false
    for ($index = 0; $index -lt $sampleCount; $index++) {
        $interval = $IntervalMs[$index]
        if (-not [double]::IsFinite($interval) -or $interval -le 0 -or -not [double]::IsFinite(1000.0 / $interval)) {
            $result.ClassificationReason = "Invalid frame interval at index $index; all intervals must be finite and positive with finite FPS."
            return [pscustomobject]$result
        }
        $total += $interval
        if ($interval -gt 100.0) {
            $sub10Time += $interval
            $sub10Intervals++
        }
        if ($interval -gt $LongStallMs) {
            $longStalls++
        }
        if ($interval -gt $SlowFrameMs) {
            if ($runCount -eq 0) {
                $slowRuns++
            }
            $slowTime += $interval
            $slowIntervals++
            $excess += $interval - $SlowFrameMs
            $runTime += $interval
            $runCount++
            if (-not $runIsSignificant -and $runTime -ge $SignificantSlowRunMs) {
                $significantSlowRuns++
                $runIsSignificant = $true
            }
            $worstRun = [Math]::Max($worstRun, $runTime)
            if ($runTime -ge $SustainedRunMs -and $runCount -ge $SustainedRunMinimumSamples) {
                $hasSustainedRun = $true
            }
        } else {
            $runTime = 0.0
            $runCount = 0
            $runIsSignificant = $false
        }
    }
    if (-not [double]::IsFinite($total)) {
        $result.ClassificationReason = 'Total frame duration overflowed.'
        return [pscustomobject]$result
    }

    $sortedIntervals = [double[]]$IntervalMs.Clone()
    [Array]::Sort($sortedIntervals)
    $middle = [int][Math]::Floor($sampleCount / 2.0)
    $median = 1000.0 / $sortedIntervals[$middle]
    if ($sampleCount % 2 -eq 0) {
        $median = (1000.0 / $sortedIntervals[$middle - 1]) / 2.0 + (1000.0 / $sortedIntervals[$middle]) / 2.0
    }
    $result.SampleAvailability = 'available'
    $result.TotalElapsedMs = $total
    $result.MedianFps = $median
    $result.EffectiveFps = ($sampleCount / $total) * 1000.0
    $result.MaximumFrameMs = $sortedIntervals[$sampleCount - 1]
    $result.P95FrameMs = $sortedIntervals[[int][Math]::Ceiling(0.95 * $sampleCount) - 1]
    $result.P99FrameMs = $sortedIntervals[[int][Math]::Ceiling(0.99 * $sampleCount) - 1]
    $result.SlowTimeShare = $slowTime / $total
    $result.Sub10FpsTimeShare = $sub10Time / $total
    $result.ExcessFrameTimeMs = $excess
    $result.WorstSlowRunMs = $worstRun
    $result.SlowRunCount = $slowRuns
    $result.SignificantSlowRunCount = $significantSlowRuns
    $result.SlowIntervalCount = $slowIntervals
    $result.Sub10IntervalCount = $sub10Intervals
    $result.LongStallCount = $longStalls
    if ($hasSustainedRun -and $result.SlowTimeShare -ge $SustainedSlowTimeShare) {
        $result.Classification = 'sustained-low-fps'
        $result.ClassificationReason = 'A sufficiently long multi-interval slow run and slow wall-time share meet the configured thresholds.'
    } elseif ($significantSlowRuns -ge 2 -or $longStalls -ge 2) {
        $result.Classification = 'repeated-hitches'
        $result.ClassificationReason = 'At least two separated slow runs meet the significant-run duration threshold, or at least two long stalls were observed.'
    } elseif ($slowRuns -gt 0) {
        $result.Classification = 'isolated-hitch'
        $result.ClassificationReason = 'Slow intervals were observed without meeting the sustained or repeated thresholds.'
    } else {
        $result.Classification = 'healthy'
        $result.ClassificationReason = 'No interval exceeded the configured slow-frame threshold.'
    }
    return [pscustomobject]$result
}
