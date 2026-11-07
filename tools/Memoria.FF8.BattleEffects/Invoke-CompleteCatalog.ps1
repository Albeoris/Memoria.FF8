#requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$GameDirectory,
    [Parameter(Mandatory)][string]$HostExecutable,
    [Parameter(Mandatory)][string]$ToolAssembly,
    [Parameter(Mandatory)][string]$TestExecutable,
    [Parameter(Mandatory)][string]$DiscoveryDirectory,
    [Parameter(Mandatory)][string]$OutputRoot,
    [string]$ExecutionStackProfile,
    [int[]]$EffectId = @(),
    [switch]$PrepareSourcesOnly,
    [ValidateRange(1, 1000)][double]$BelowFps = 50,
    [ValidateRange(1, 16384)][int]$MaximumRetainedMiB = 1024
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$measurementSlotPath = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'Memoria.FF8/Validation/measurement.lock'

function Write-Json([string]$Path, [object]$Value) {
    $Value | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $Path -Encoding utf8
}

function Invoke-OwnedProcess([string]$Executable, [string[]]$Arguments, [string]$Directory, [int]$TimeoutSeconds = 660) {
    [IO.Directory]::CreateDirectory($Directory) | Out-Null
    $start = [Diagnostics.ProcessStartInfo]::new($Executable)
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    foreach ($argument in $Arguments) {
        $start.ArgumentList.Add($argument)
    }
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $start
    $started = $false
    try {
        if (-not $process.Start()) { throw 'The owned child could not start.' }
        $started = $true
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
            $failure = [InvalidOperationException]::new("Owned child exceeded its $TimeoutSeconds second deadline; stop the catalog run.")
            $failure.Data['StopCatalog'] = $true
            throw $failure
        }
        $process.WaitForExit()
        [IO.File]::WriteAllText((Join-Path $Directory 'stdout.txt'), $stdout.GetAwaiter().GetResult())
        [IO.File]::WriteAllText((Join-Path $Directory 'stderr.txt'), $stderr.GetAwaiter().GetResult())
        $script:LastOwnedProcessId = $process.Id
        return $process.ExitCode
    }
    finally {
        try {
            if ($started -and -not $process.HasExited) {
                try {
                    $process.Kill($true)
                    if (-not $process.WaitForExit(10000)) { throw 'Owned child exit is unconfirmed.' }
                }
                catch {
                    [IO.Directory]::CreateDirectory((Split-Path -Parent $measurementSlotPath)) | Out-Null
                    Write-Json ($measurementSlotPath + '.blocked.json') @{ process_id=$process.Id; output_root=$OutputRoot; reason='Catalog child termination could not be confirmed.' }
                    $failure = [InvalidOperationException]::new('Owned child termination could not be confirmed; stop the catalog run.', $_.Exception)
                    $failure.Data['StopCatalog'] = $true
                    throw $failure
                }
            }
        }
        finally {
            $process.Dispose()
        }
    }
}

function Invoke-Tool([string[]]$Arguments, [string]$LogDirectory) {
    $exitCode = Invoke-OwnedProcess 'dotnet' (@($ToolAssembly) + $Arguments) $LogDirectory
    if ($exitCode -ne 0) { throw "Tool command $($Arguments[0]) failed with exit $exitCode. See $LogDirectory" }
}

function Remove-OwnedDirectory([string]$Directory) {
    $resolved = [IO.Path]::GetFullPath($Directory)
    if (-not $resolved.StartsWith($OutputRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Disposable output escaped this run directory.'
    }
    if (Test-Path -LiteralPath $resolved) { Remove-Item -LiteralPath $resolved -Recurse }
}

foreach ($name in @('GameDirectory', 'HostExecutable', 'ToolAssembly', 'TestExecutable', 'DiscoveryDirectory')) {
    Set-Variable -Name $name -Value (Resolve-Path -LiteralPath (Get-Variable -Name $name -ValueOnly)).Path
}
$profileIdentity = $null
if ($ExecutionStackProfile) {
    $ExecutionStackProfile = (Resolve-Path -LiteralPath $ExecutionStackProfile).Path
    if (-not (Test-Path -LiteralPath $ExecutionStackProfile -PathType Leaf)) { throw 'The execution stack profile must be a file.' }
    $profileIdentity = @{ path=$ExecutionStackProfile; sha256=(Get-FileHash -LiteralPath $ExecutionStackProfile -Algorithm SHA256).Hash.ToLowerInvariant() }
}
$OutputRoot = [IO.Path]::TrimEndingDirectorySeparator([IO.Path]::GetFullPath($OutputRoot))
if (Test-Path -LiteralPath $OutputRoot) { throw 'Select a new catalog execution directory.' }
foreach ($inputDirectory in @($GameDirectory, $DiscoveryDirectory, (Split-Path -Parent $HostExecutable), (Split-Path -Parent $ToolAssembly), (Split-Path -Parent $TestExecutable))) {
    if ($OutputRoot.Equals($inputDirectory, [StringComparison]::OrdinalIgnoreCase) -or $OutputRoot.StartsWith($inputDirectory.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Catalog execution output must be outside source resources and binaries.'
    }
}
$catalogPath = Join-Path $DiscoveryDirectory 'installed-inventory.json'
$catalog = Get-Content -LiteralPath $catalogPath -Raw | ConvertFrom-Json -Depth 100
$catalogIds = @($catalog.bindings | Where-Object { $_.entry -ne 0 -or $_.initializer -ne 0 } | ForEach-Object { [int]$_.effect_id } | Sort-Object -Unique)
$selected = if ($EffectId.Count) { @($EffectId | Sort-Object -Unique) } else { $catalogIds }
if (@($selected | Where-Object { $_ -notin $catalogIds }).Count) { throw 'Requested IDs include empty or absent native bindings.' }
$cases = @(Get-Content -LiteralPath (Join-Path $DiscoveryDirectory 'cases-manifest.json') -Raw | ConvertFrom-Json -Depth 100)
$results = [Collections.Generic.List[object]]::new()
[IO.Directory]::CreateDirectory($OutputRoot) | Out-Null
$binaryHashes = @{}
foreach ($file in Get-ChildItem -LiteralPath (Split-Path -Parent $HostExecutable) -File | Where-Object { $_.Extension -in @('.dll', '.exe', '.so', '.dylib') }) {
    $binaryHashes[$file.Name] = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
}
if ($selected.Count -eq 0) { throw 'The selected catalog contains no native effects.' }
Write-Json (Join-Path $OutputRoot 'inputs.json') @{ prepare_sources_only=[bool]$PrepareSourcesOnly; execution_stack_profile=$profileIdentity; catalog_effect_ids=$catalogIds; selected_effect_ids=$selected; executable_sha256=$catalog.executable_sha256; kernel_sha256=$catalog.kernel_sha256; binaries=$binaryHashes; tool_sha256=(Get-FileHash -LiteralPath $ToolAssembly -Algorithm SHA256).Hash.ToLowerInvariant(); source_test_sha256=(Get-FileHash -LiteralPath $TestExecutable -Algorithm SHA256).Hash.ToLowerInvariant(); pacing='normal-real-time'; presentation_v_sync=$false; resolution='1920x1080'; target_fps=60; below_fps=$BelowFps }
$oldEnvironment = @{}
foreach ($name in @('MEMORIA_FF8_GAME_DIR', 'MEMORIA_FF8_COMPONENT_EFFECT_ID', 'MEMORIA_FF8_COMPONENT_SOURCE_OUTPUT', 'MEMORIA_FF8_COMPONENT_SOURCE_ONLY', 'MEMORIA_FF8_EXECUTION_STACK_PROFILE')) {
    $oldEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
}
try {
    $env:MEMORIA_FF8_GAME_DIR = $GameDirectory
    if ($ExecutionStackProfile) { $env:MEMORIA_FF8_EXECUTION_STACK_PROFILE = $ExecutionStackProfile }
    foreach ($effect in $selected) {
        $caseRoot = Join-Path $OutputRoot ('effect-{0:D3}' -f $effect)
        [IO.Directory]::CreateDirectory($caseRoot) | Out-Null
        $result = [ordered]@{ effect_id=$effect; route=$null; prepared=$false; completed=$false; executions=0; minimum_cast_fps=$null; below_threshold=$null; error=$null }
        $componentLease = $null
        try {
            $ordinary = @($cases | Where-Object { $_.status -eq 'RUNNABLE' -and $_.effect_id -eq $effect } | Sort-Object id | Select-Object -First 1)
            if ($ordinary.Count) {
                $result.route = 'ordinary-native-invocation'
                if ($PrepareSourcesOnly) {
                    $recipePath = $ordinary[0].recipe
                    if (-not (Test-Path -LiteralPath $recipePath -PathType Leaf) -or (Get-FileHash -LiteralPath $recipePath -Algorithm SHA256).Hash -ne $ordinary[0].recipe_sha256) { throw 'The selected ordinary recipe is missing or differs from its discovery hash.' }
                    $result.preparation = @{ phase='ordinary_recipe_ready'; case_id=$ordinary[0].id; recipe_sha256=$ordinary[0].recipe_sha256; ordinary_admission_certified=$false; functional_completion_certified=$false }
                    $result.prepared = $true
                }
                else {
                    $manifestPath = Join-Path $caseRoot 'manifest.json'
                    $manifest = @{ schema_version=1; executable=$HostExecutable; working_directory=(Split-Path -Parent $HostExecutable); game_dir=$GameDirectory; output_root=(Join-Path $caseRoot 'results'); mute_output=$true; presentation_v_sync=$false; budgets=@{ initialization_ms=180000; action_one_ms=180000; cleanup_rearm_ms=60000; action_two_ms=180000; teardown_ms=15000; total_ms=600000; no_progress_ms=60000 }; cases=$ordinary }
                    if ($ExecutionStackProfile) { $manifest.execution_stack_profile = $ExecutionStackProfile }
                    Write-Json $manifestPath $manifest
                    $runExit = Invoke-OwnedProcess 'dotnet' @($ToolAssembly, 'run', '--input', $manifestPath) (Join-Path $caseRoot 'run-log')
                    Invoke-Tool @('reconcile-coverage','--input',$manifestPath,'--catalog',$catalogPath,'--history',$caseRoot,'--output',(Join-Path $caseRoot 'coverage.json')) (Join-Path $caseRoot 'coverage-log')
                    $coverage = (Get-Content -LiteralPath (Join-Path $caseRoot 'coverage.json') -Raw | ConvertFrom-Json -Depth 100).coverage
                    if ($effect -notin $coverage.completed.effect_ids) { throw "No authenticated two-use completion; runner exit=$runExit." }
                    Invoke-Tool @('recalculate-fps','--input',$manifestPath,'--history',$caseRoot,'--below-fps',$BelowFps.ToString('R',[Globalization.CultureInfo]::InvariantCulture),'--output',(Join-Path $caseRoot 'fps.json'),'--slow-output',(Join-Path $caseRoot 'slow-fps.json')) (Join-Path $caseRoot 'fps-log')
                    $fps = Get-Content -LiteralPath (Join-Path $caseRoot 'fps.json') -Raw | ConvertFrom-Json -Depth 100
                    if ($fps.requested -ne 1 -or $fps.completed_cases -ne 1 -or $fps.pending_cases -ne 0 -or $fps.unavailable_effect_casts -ne 0 -or $fps.cases.Count -ne 1 -or $fps.cases[0].case.effect_id -ne $effect -or $fps.cases[0].casts.Count -ne 2) { throw 'Authenticated FPS extraction did not retain exactly two measured casts for the selected effect.' }
                    $result.fps = $fps
                    $values = @($fps.cases[0].casts | Where-Object { $_.effect.available -and $_.effect.duration_ms -gt 0 } | ForEach-Object { 1000.0 * $_.effect.frame_count / $_.effect.duration_ms })
                    if ($values.Count -ne 2 -or @($values | Where-Object { -not [double]::IsFinite($_) -or $_ -le 0 }).Count) { throw 'Both ordinary casts require finite positive whole-interval FPS measurements.' }
                    $result.minimum_cast_fps = ($values | Measure-Object -Minimum).Minimum
                    $result.executions = 2
                    $result.completed = $true
                    $result.prepared = $true
                    Remove-OwnedDirectory (Join-Path $caseRoot 'results')
                }
            }
            else {
                $result.route = 'controlled-original-component'
                try {
                    [IO.Directory]::CreateDirectory((Split-Path -Parent $measurementSlotPath)) | Out-Null
                    $componentLease = [IO.FileStream]::new($measurementSlotPath, [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
                    if (Test-Path -LiteralPath ($measurementSlotPath + '.blocked.json')) { throw 'A previous measurement has unresolved owned-process quarantine.' }
                }
                catch {
                    $failure = [InvalidOperationException]::new('Exclusive component measurement ownership is unavailable; stop the catalog run.', $_.Exception)
                    $failure.Data['StopCatalog'] = $true
                    throw $failure
                }
                $sourceRoot = Join-Path $caseRoot 'source'
                $sourcePath = Join-Path $sourceRoot 'queued.battle.json'
                $env:MEMORIA_FF8_COMPONENT_EFFECT_ID = $effect.ToString([Globalization.CultureInfo]::InvariantCulture)
                $env:MEMORIA_FF8_COMPONENT_SOURCE_OUTPUT = $sourcePath
                $env:MEMORIA_FF8_COMPONENT_SOURCE_ONLY = '1'
                $prepareExit = Invoke-OwnedProcess $TestExecutable @('--filter-class','*NativeBattleComponentSourceTests','--show-test-results','failed,skipped') (Join-Path $caseRoot 'source-log') 300
                if ($prepareExit -ne 0 -or -not (Test-Path -LiteralPath $sourcePath)) { throw "Exact existing component source preparation failed ($prepareExit)." }
                $receipt = Get-Content -LiteralPath ($sourcePath+'.preparation.json') -Raw | ConvertFrom-Json -Depth 100
                $sourceHash = (Get-FileHash -LiteralPath $sourcePath -Algorithm SHA256).Hash.ToLowerInvariant()
                if ($receipt.Phase -ne 'prepared' -or $receipt.Scope -ne 'native-effect-component' -or $receipt.Boundary -notin @('queued-action104', 'admitted-before-entry', 'module-loaded-before-entry') -or -not $receipt.SourceOnly -or $receipt.SourceFile -ne [IO.Path]::GetFileName($sourcePath) -or $receipt.Effect -ne $effect -or $receipt.Sha256 -ne $sourceHash -or $receipt.OrdinaryAdmissionCertified -or $receipt.FunctionalCompletionCertified) { throw 'Component preparation receipt is inconsistent.' }
                $result.source = $receipt
                $result.prepared = $true
                if (-not $PrepareSourcesOnly) {
                    $entries = [Collections.Generic.List[object]]::new()
                    foreach ($entry in 1..2) {
                        $entryRoot = Join-Path $caseRoot "entry-$entry"
                        [IO.Directory]::CreateDirectory((Join-Path $entryRoot 'settings')) | Out-Null
                        Write-Json (Join-Path $entryRoot 'settings/ui-settings.json') @{ MusicVolume=0; SoundVolume=0; VoiceVolume=0; VSync=$false }
                        $requestPath = Join-Path $entryRoot 'request.json'
                        Write-Json $requestPath @{ enemy_id=0; ally_id=0; effect_id=$effect; source_snapshot=$sourcePath; source_sha256=$sourceHash; duration_seconds=300 }
                        $control = Join-Path $entryRoot 'control'
                        $entryExit = Invoke-OwnedProcess $HostExecutable @('--game-dir',$GameDirectory,'--effect-preview',$requestPath,'--resolution','1920x1080','--settings-dir',(Join-Path $entryRoot 'settings'),'--control-dir',$control,'--save-state',(Join-Path $entryRoot 'saves/checkpoint.json')) (Join-Path $entryRoot 'host-log') 360
                        $measurementPath = Join-Path $control 'component-measurements.json'
                        if ($entryExit -ne 0 -or -not (Test-Path -LiteralPath (Join-Path $control 'completed.json')) -or (Test-Path -LiteralPath (Join-Path $control 'blocked.json')) -or -not (Test-Path -LiteralPath $measurementPath)) { throw "Controlled component entry $entry failed ($entryExit)." }
                        $completedReceipt = Get-Content -LiteralPath (Join-Path $control 'completed.json') -Raw | ConvertFrom-Json -Depth 100
                        if ($completedReceipt.state -ne 'completed' -or $completedReceipt.pid -ne $script:LastOwnedProcessId -or $completedReceipt.expected_effect -ne $effect -or -not $completedReceipt.native_identity.drained -or $completedReceipt.native_identity.effect -ne $effect) { throw 'Component completion does not identify the exited owned host and selected drained effect.' }
                        $measurement = Get-Content -LiteralPath $measurementPath -Raw | ConvertFrom-Json -Depth 100
                        $identityMatches = $false
                        if ('native_dispatch' -in $measurement.PSObject.Properties.Name -and $null -ne $measurement.native_dispatch) {
                            $identityMatches = $measurement.native_dispatch.requested_effect -eq $effect -and $measurement.native_dispatch.executed_effect -eq $effect
                        }
                        elseif ('native_admission' -in $measurement.PSObject.Properties.Name -and $null -ne $measurement.native_admission) {
                            $identityMatches = $measurement.native_admission.effect -eq $effect
                        }
                        if ($measurement.scope -ne 'installed-source-controlled-component-gpu-presentation' -or $measurement.effect -ne $effect -or $measurement.source_sha256 -ne $sourceHash -or -not $measurement.drained -or $measurement.tail_presented_frames -lt 2 -or -not $identityMatches) { throw 'Component execution identity, cleanup or tail evidence is inconsistent.' }
                        if ($measurement.host.vsync -ne $false -or $measurement.host.presentation_interval -ne 'Immediate' -or $measurement.host.frame_limit -ne 60 -or $measurement.host.width -ne 1920 -or $measurement.host.height -ne 1080) { throw 'The actual component host differs from the declared real-time 60-FPS VSync-off presentation profile.' }
                        $metrics = $measurement.measurements
                        if ($metrics.scope -ne $measurement.scope -or $metrics.interval_count -lt 1 -or -not [double]::IsFinite($metrics.interval_milliseconds) -or $metrics.interval_milliseconds -le 0 -or -not [double]::IsFinite($metrics.effective_fps) -or $metrics.effective_fps -le 0 -or [Math]::Abs($metrics.effective_fps - 1000.0 * $metrics.interval_count / $metrics.interval_milliseconds) -gt 0.000001 -or $metrics.target_fps -ne 60) { throw 'Component FPS lacks consistent positive whole successful-present intervals at the declared target.' }
                        $entries.Add($measurement)
                        $result.executions++
                    }
                    $result.entries = $entries.ToArray()
                    $result.completed = $true
                    $result.minimum_cast_fps = ($entries | ForEach-Object { [double]$_.measurements.effective_fps } | Measure-Object -Minimum).Minimum
                }
                Remove-OwnedDirectory $sourceRoot
            }
            if ($null -ne $result.minimum_cast_fps) { $result.below_threshold = $result.minimum_cast_fps -lt $BelowFps }
        }
        catch {
            $result.error = $_.Exception.Message
            if ($_.Exception.Data['StopCatalog']) {
                Write-Json (Join-Path $caseRoot 'result.json') $result
                throw
            }
        }
        finally {
            if ($null -ne $componentLease) { $componentLease.Dispose() }
        }
        $results.Add([pscustomobject]$result)
        Write-Json (Join-Path $caseRoot 'result.json') $result
        $completed = @($results | Where-Object completed | ForEach-Object effect_id)
        $prepared = @($results | Where-Object { $_.prepared -and $null -eq $_.error } | ForEach-Object effect_id)
        $unprepared = @($selected | Where-Object { $_ -notin $prepared })
        $missing = @($selected | Where-Object { $_ -notin $completed })
        $summary = @{ schema_version=1; catalog_effect_ids=$catalogIds; selected_effect_ids=$selected; completed_effect_ids=$completed; missing_effect_ids=$missing; full_catalog_requested=($EffectId.Count -eq 0); full_execution_complete=($EffectId.Count -eq 0 -and $missing.Count -eq 0); fps_available_count=@($results | Where-Object { $null -ne $_.minimum_cast_fps }).Count; below_threshold_effect_ids=@($results | Where-Object below_threshold | ForEach-Object effect_id); ordinary_admission_count=@($results | Where-Object { $_.completed -and $_.route -eq 'ordinary-native-invocation' }).Count; controlled_component_count=@($results | Where-Object { $_.completed -and $_.route -eq 'controlled-original-component' }).Count; raw_retirement='Closed ordinary telemetry and successful component preparation snapshots are retired after authentication and compact measurement extraction; inputs, hashes and measurements remain.'; results=$results.ToArray() }
        $summary.prepared_effect_ids = $prepared
        $summary.unprepared_effect_ids = $unprepared
        $summary.prepare_sources_only = [bool]$PrepareSourcesOnly
        $summary.full_preparation_complete = $selected.Count -eq $catalogIds.Count -and $unprepared.Count -eq 0
        $summary.selected_preparation_complete = $unprepared.Count -eq 0
        $summary.selected_execution_complete = $missing.Count -eq 0
        Write-Json (Join-Path $OutputRoot 'summary.json') $summary
        Write-Host ("Effect {0}: prepared={1}, completed={2}, route={3}, minimum cast FPS={4}, error={5}" -f $effect,$result.prepared,$result.completed,$result.route,$result.minimum_cast_fps,$result.error)
        $retainedBytes = (Get-ChildItem -LiteralPath $OutputRoot -File -Recurse | Measure-Object Length -Sum).Sum
        if ($retainedBytes -gt $MaximumRetainedMiB * 1MB) { throw "Retained data exceeded $MaximumRetainedMiB MiB; catalog capture stopped after confirmed child exit." }
    }
}
finally {
    foreach ($name in $oldEnvironment.Keys) { [Environment]::SetEnvironmentVariable($name,$oldEnvironment[$name],'Process') }
}
if ($PrepareSourcesOnly) {
    if (@($results | Where-Object { -not $_.prepared -or $null -ne $_.error }).Count) { throw 'Catalog source preparation has missing or failed effects; see summary.json.' }
}
elseif (@($results | Where-Object { -not $_.completed }).Count) { throw 'Catalog execution has missing or failed effects; see summary.json.' }
