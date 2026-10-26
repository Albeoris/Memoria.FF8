. (Join-Path $PSScriptRoot 'Measure-EffectFrames.ps1')
. (Join-Path $PSScriptRoot 'Validation-Retry.ps1')

function Get-ValidationCaseClassification($Casts) {
    $classes = @($Casts | ForEach-Object { $_.effect.Classification })
    if ($classes -contains 'sustained-low-fps') { return 'sustained-low-fps' }
    if ($classes -contains 'repeated-hitches') { return 'repeated-hitches' }
    if (@($Casts | Where-Object { $_.effect.MaximumFrameMs -ge 100 }).Count -eq 2) { return 'repeated-hitches' }
    if ($classes -contains 'isolated-hitch') { return 'isolated-hitch' }
    if ($classes.Count -ne 2 -or $classes -contains 'unavailable') { return 'unavailable' }
    return 'healthy'
}

function Get-ValidationProblems($Cases) {
    # Prioritization heuristics: severe wall-time slowdown requires both available
    # casts below 20 effective FPS with at least 25 percent slow-frame wall time.
    $severeEffectiveFps = 20.0
    $severeSlowTimeShare = 0.25
    $groups = @{}
    foreach ($case in $Cases) {
        if ($case.state -eq 'blocked') { continue }
        $candidates = [Collections.Generic.List[object]]::new()
        $priority = 99
        $kind = $null
        $impact = 0.0
        $confidence = 'unverified-precondition'
        $key = 'effect:' + $case.effect_id
        $signature = $null
        $familyGrouping = $null
        $retainedPerformance = $case.retained_unresolved -and $case.classification -in @('sustained-low-fps', 'repeated-hitches', 'isolated-hitch')
        if ($case.state -eq 'error' -or ($case.retained_unresolved -and -not $retainedPerformance)) {
            $kind = 'functional-error'
            $priority = 3
            $functionalErrors = @($case.functional_errors)
            if ($functionalErrors.Count -eq 0 -or $null -eq $functionalErrors[0]) { $functionalErrors = @($case.errors) }
            $normalizedErrors = [string[]]@($functionalErrors | ForEach-Object { ConvertTo-ValidationErrorIdentity ([string]$_) } | Where-Object { $_.Length -gt 0 } | Select-Object -Unique)
            [Array]::Sort($normalizedErrors, [StringComparer]::Ordinal)
            $exitCode = 'unavailable'
            if ($null -ne $case.exit_code) { $exitCode = [string]$case.exit_code }
            $errorPhase = 'unavailable'
            if (-not [string]::IsNullOrWhiteSpace($case.error_phase)) { $errorPhase = ([string]$case.error_phase).Trim().ToLowerInvariant() }
            $signature = 'errors=' + (ConvertTo-Json -InputObject $normalizedErrors -Compress) + '; phase=' + $errorPhase + '; exit_code=' + $exitCode
            $hash = [Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($signature))
            $key = 'error:' + [Convert]::ToHexString($hash).Substring(0, 16).ToLowerInvariant()
            $familyGrouping = 'symptom-family-not-proven-root-cause'
            if ($case.observed_executions -gt 0) { $confidence = 'failure-after-dispatch' }
            $errorText = $normalizedErrors -join ' '
            if ($errorText -match 'STACK_OVERFLOW|STACKOVERFLOWEXCEPTION|GRAPHICAL_HOST_FAULTED') { $priority = 0 }
            elseif ($errorText -match 'CRASH' -and $errorPhase -eq 'initialization' -and $exitCode -eq '1') { $priority = 3; $confidence = 'unverified-startup-failure' }
            elseif ($errorText -match 'CRASH') { $priority = 0 }
            elseif ($case.observed_executions -gt 0) { $priority = 1 }
        } elseif ($case.state -eq 'tested' -or $retainedPerformance) {
            $metrics = @($case.casts | ForEach-Object { $_.effect })
            foreach ($m in $metrics) { $impact = [Math]::Max($impact, [double]$m.ExcessFrameTimeMs) }
            $bothSustained = $metrics.Count -eq 2 -and @($metrics | Where-Object { $_.Classification -eq 'sustained-low-fps' }).Count -eq 2
            $bothSevere = $metrics.Count -eq 2 -and @($metrics | Where-Object { $_.SampleAvailability -eq 'available' -and $null -ne $_.EffectiveFps -and $_.EffectiveFps -lt $severeEffectiveFps -and $_.SlowTimeShare -ge $severeSlowTimeShare }).Count -eq 2
            if ($metrics.Count -eq 0) { $bothSustained = $case.problem_casts -eq 2 }
            if ($case.classification -eq 'sustained-low-fps') {
                $kind = 'sustained-low-fps'
                $priority = $(if ($bothSustained -or $bothSevere) { 1 } else { 2 })
            } elseif ($case.classification -eq 'repeated-hitches') {
                $kind = 'repeated-hitches'
                $priority = $(if ($bothSevere) { 1 } else { 2 })
            } elseif (@($metrics | Where-Object { $_.MaximumFrameMs -ge 250 }).Count -gt 0) {
                $kind = 'isolated-long-stall'
                $priority = 4
            }
            $confidence = $(if ($case.problem_casts -eq 2) { 'observed-in-both-casts' } else { 'single-cast-or-infrastructure' })
        }
        if ($case.retained_unresolved) { $confidence = 'incomplete-retry' }
        if ($null -ne $kind) {
            $candidates.Add([pscustomobject]@{ key = $key; kind = $kind; priority = $priority; impact = $impact; confidence = $confidence; signature = $signature; family_grouping = $familyGrouping })
        }
        if ($case.state -eq 'tested' -and $case.overhead_ms -gt 10000) {
            $candidates.Add([pscustomobject]@{ key = 'infrastructure:overhead'; kind = 'supervisor-overhead'; priority = 5; impact = ($case.overhead_ms - 10000); confidence = 'observed-supervisor-overhead'; signature = $null; family_grouping = $null })
        }
        foreach ($candidate in $candidates) {
            $key = $candidate.key
            if (-not $groups.ContainsKey($key)) {
                $groups[$key] = [pscustomobject]@{ problem_id = $key; effect_id = $case.effect_id; effect_ids = [Collections.Generic.List[int]]::new(); priority = $candidate.priority; impact_ms = $candidate.impact; evidence = $candidate.confidence; kinds = [Collections.Generic.List[string]]::new(); case_ids = [Collections.Generic.List[string]]::new(); representative_case_id = $case.case_id; error_signature = $candidate.signature; family_grouping = $candidate.family_grouping }
            }
            $group = $groups[$key]
            $betterScore = $candidate.priority -lt $group.priority -or ($candidate.priority -eq $group.priority -and $candidate.impact -gt $group.impact_ms)
            $tiedScore = $candidate.priority -eq $group.priority -and $candidate.impact -eq $group.impact_ms
            if ($betterScore -or ($tiedScore -and [string]::CompareOrdinal($case.case_id, $group.representative_case_id) -lt 0)) {
                $group.priority = $candidate.priority
                $group.impact_ms = $candidate.impact
                $group.evidence = $candidate.confidence
                $group.representative_case_id = $case.case_id
                $group.effect_id = $case.effect_id
            }
            if (-not $group.kinds.Contains($candidate.kind)) { $group.kinds.Add($candidate.kind) }
            if (-not $group.case_ids.Contains($case.case_id)) { $group.case_ids.Add($case.case_id) }
            if (-not $group.effect_ids.Contains($case.effect_id)) { $group.effect_ids.Add($case.effect_id) }
        }
    }
    $rank = 0
    foreach ($group in @($groups.Values | Sort-Object priority, @{Expression='impact_ms';Descending=$true}, effect_id, problem_id)) {
        $rank++
        $group.kinds.Sort([StringComparer]::Ordinal)
        $group.case_ids.Sort([StringComparer]::Ordinal)
        $group.effect_ids.Sort()
        [pscustomobject]@{ rank = $rank; problem_id = $group.problem_id; effect_id = $group.effect_id; effect_ids = $group.effect_ids.ToArray(); priority = $group.priority; impact_ms = $group.impact_ms; evidence = $group.evidence; kinds = $group.kinds.ToArray(); case_ids = $group.case_ids.ToArray(); representative_case_id = $group.representative_case_id; affected_cases = $group.case_ids.Count; error_signature = $group.error_signature; family_grouping = $group.family_grouping }
    }
}

function New-ValidationTriageReport {
    param([string] $ManifestPath, [string] $HistoryRoot, $PreviousReport = $null, [double] $BelowFps = 30)
    $requested = [Memoria.FF8.BattleEffects.RecordJson]::Read[Memoria.FF8.BattleEffects.PilotManifest]($ManifestPath)
    $plan = [Memoria.FF8.BattleEffects.PilotResume]::Plan($requested, $HistoryRoot)
    $sources = @{}
    foreach ($source in $plan.ReusedCases) { $sources[$source.Case.Id] = $source }
    $prior = @{}
    if ($PreviousReport) { foreach ($case in $PreviousReport.cases) { $prior[$case.case_id] = $case } }
    $cases = [Collections.Generic.List[object]]::new()
    $diagnostics = [Collections.Generic.List[object]]::new()
    foreach ($diagnostic in $plan.Diagnostics) { $diagnostics.Add([pscustomobject]@{path=$diagnostic.Path;message=$diagnostic.Message}) }
    $slowMs = 1000.0 / $BelowFps
    foreach ($item in $requested.Cases) {
        $source = $sources[$item.Id]
        $entry = [ordered]@{case_id=$item.Id;effect_id=$item.EffectId;state='pending';classification='unavailable';assessment_available=$false;errors=@();functional_errors=@();error_phase=$null;exit_code=$null;original_verdict=$null;observed_executions=0;build_fingerprint=$null;result_path=$null;telemetry_path=$null;recipe=$item.Recipe;recipe_sha256=$item.RecipeSha256;source_manifest_path=$null;overhead_ms=$null;casts=@();problem_casts=0;retained_unresolved=$false}
        if ($null -eq $source) {
            if ($prior.ContainsKey($item.Id) -and ($prior[$item.Id].state -eq 'error' -or $prior[$item.Id].classification -in @('sustained-low-fps','repeated-hitches'))) {
                $entry.retained_unresolved = $true
                $entry.errors = @($prior[$item.Id].errors)
                $entry.functional_errors = @($prior[$item.Id].functional_errors)
                $entry.classification = $prior[$item.Id].classification
                $entry.casts = @($prior[$item.Id].casts)
                $entry.problem_casts = $prior[$item.Id].problem_casts
                $entry.overhead_ms = $prior[$item.Id].overhead_ms
                $entry.error_phase = $prior[$item.Id].error_phase
                $entry.exit_code = $prior[$item.Id].exit_code
            }
            $cases.Add([pscustomobject]$entry)
            continue
        }
        $entry.state = $(if ($source.Tested) {'tested'} elseif ($item.Status -eq 'RUNNABLE') {'error'} else {'blocked'})
        $entry.original_verdict = $source.PreviousResult.Verdict
        $entry.observed_executions = $source.PreviousResult.Executions
        $entry.errors = @($source.PreviousResult.Errors)
        $entry.build_fingerprint = $source.PreviousBuildFingerprint
        $entry.result_path = $source.ResultPath
        $entry.telemetry_path = $source.TelemetryPath
        $entry.source_manifest_path = $source.ManifestPath
        $entry.overhead_ms = $source.PreviousResult.OverheadMs
        if ($entry.state -eq 'error') {
            $entry.classification = 'error'
            $entry.functional_errors = @($entry.errors | Where-Object { $_ -ne 'OVERHEAD_EXCEEDS_10000_MS' -and $_ -ne 'PRESENTATION_BELOW_30_FPS' })
            $errorPath = Join-Path ([IO.Path]::GetDirectoryName($source.ResultPath)) 'output/result.json'
            if ([IO.File]::Exists($errorPath) -and ([IO.FileInfo]::new($errorPath)).Length -lt 4194304) {
                try {
                    $errorRecord = Get-Content -LiteralPath $errorPath -Raw | ConvertFrom-Json
                    $entry.error_phase = $errorRecord.last_stage
                    $entry.exit_code = $errorRecord.exit_code
                } catch { $diagnostics.Add([pscustomobject]@{path=$errorPath;message='Optional error phase/exit detail could not be read.'}) }
            }
        }
        if ($source.Tested) {
            try {
                $validation = [Memoria.FF8.BattleEffects.LiveEvidenceValidation]::ValidateFile($source.TelemetryPath, $null)
                if ($validation.EffectExecutions.Count -ne 2 -or $validation.Verdict -notin @('PASS','FAIL')) { throw 'Two authenticated completed casts are required.' }
                $casts = [Collections.Generic.List[object]]::new()
                for ($index = 0; $index -lt 2; $index++) {
                    $scope = $validation.EffectExecutions[$index]
                    $durations = [double[]]@($scope.Frames.Intervals | ForEach-Object { $_.DurationMs })
                    $metrics = Get-EffectFrameMetrics -IntervalMs $durations -SlowFrameMs $slowMs -LongStallMs ([Math]::Max(100,$slowMs))
                    if (-not $scope.Available) { $metrics = Get-EffectFrameMetrics -IntervalMs @() -SlowFrameMs $slowMs -LongStallMs ([Math]::Max(100,$slowMs)) }
                    $boundary = [double[]]@($scope.BoundaryIntervals | ForEach-Object { $_.DurationMs })
                    $boundaryMetrics = Get-EffectFrameMetrics -IntervalMs $boundary -SlowFrameMs $slowMs -LongStallMs ([Math]::Max(100,$slowMs))
                    $casts.Add([pscustomobject]@{index=($index+1);effect=$metrics;invocation_median_fps=$validation.Executions[$index].MedianFps;effect_wall_ms=$validation.EffectDurationsMs[$index];boundary=$boundaryMetrics;boundary_interval_count=$scope.BoundaryIntervalCount;whole_interval_coverage=($scope.Frames.DurationMs / $validation.EffectDurationsMs[$index])})
                }
                $entry.casts = $casts.ToArray()
                $entry.classification = Get-ValidationCaseClassification $entry.casts
                $entry.problem_casts = @($entry.casts | Where-Object { $_.effect.Classification -in @('sustained-low-fps','repeated-hitches') -or $_.effect.MaximumFrameMs -ge 100 }).Count
                $entry.assessment_available = @($entry.casts | Where-Object { $_.effect.SampleAvailability -eq 'available' -and $_.effect.SampleCount -ge 3 -and $_.effect.TotalElapsedMs -ge 100 }).Count -eq 2
            } catch {
                $entry.state = 'pending'
                $entry.errors = @('TRIAGE_EVIDENCE_INVALID: ' + $_.Exception.Message)
                $diagnostics.Add([pscustomobject]@{path=$source.ResultPath;message=$_.Exception.Message})
            }
        }
        $cases.Add([pscustomobject]$entry)
    }
    $problems = @(Get-ValidationProblems $cases.ToArray())
    [pscustomobject]@{
        schema_version=1; generated_utc=[DateTimeOffset]::UtcNow; fps_scope='effect'; below_fps=$BelowFps
        policy=[pscustomobject]@{version=1;boundary_policy=[Memoria.FF8.BattleEffects.TelemetryEffectFrames]::Policy;ranking='Action crashes/render faults first; sustained in both casts, or both casts below20 effective FPS with >=25% slow wall time, or failures after execution next; other sustained/repeated slowdowns; unverified startup/pre-dispatch errors; isolated >=250ms stalls; supervisor overhead. Within priority use worst cast excess milliseconds. Error families are symptoms, not proved shared causes. Variants count is not gameplay frequency.';assessment='At least three whole intervals and 100ms of samples in each cast; no causal GPU/CPU diagnosis or gameplay-frequency claim.';fixed='Fresh retry evidence and issue-specific comparison required; historical strict verdict is preserved.'}
        requested=$requested.Cases.Count;tested=@($cases | Where-Object state -eq 'tested').Count;errors=@($cases | Where-Object state -eq 'error').Count;blocked=@($cases | Where-Object state -eq 'blocked').Count;pending=@($cases | Where-Object state -eq 'pending').Count
        cases=$cases.ToArray();problems=$problems;diagnostics=$diagnostics.ToArray()
    }
}
