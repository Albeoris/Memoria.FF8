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
    [switch]$FunctionalOnly,
    [switch]$DisableVSync,
    [ValidateRange(1, 1000)][double]$BelowFps = 50,
    [ValidateRange(1, 16384)][int]$MaximumRetainedMiB = 1024
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if ($PrepareSourcesOnly -and $FunctionalOnly) { throw 'Select source preparation or functional execution, not both.' }
$performanceRequested = -not $PrepareSourcesOnly -and -not $FunctionalOnly
$presentationVSync = -not [bool]$DisableVSync
$expectedPresentationInterval = if ($presentationVSync) { 'One' } else { 'Immediate' }
$measurementSlotPath = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'Memoria.FF8/Validation/measurement.lock'

function Write-Json([string]$Path, [object]$Value) {
    $Value | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $Path -Encoding utf8
}

function Get-GpuProcessContext([string]$Stage, [int]$OwnedRoot = 0) {
    $observation = [ordered]@{ utc=[DateTimeOffset]::UtcNow.ToString('O'); stage=$Stage; status='observed'; foreign_processes=@(); error=$null }
    try {
        $candidates = @(Get-CimInstance Win32_Process -Filter "Name LIKE 'Memoria.FF8%'" -Property ProcessId,ParentProcessId,Name,ExecutablePath,CreationDate -ErrorAction Stop)
        foreach ($candidate in $candidates) {
            $owned = $false
            $ancestor = [int]$candidate.ProcessId
            $visited = [Collections.Generic.HashSet[int]]::new()
            while ($OwnedRoot -gt 0 -and $ancestor -gt 0 -and $visited.Add($ancestor)) {
                if ($ancestor -eq $OwnedRoot) { $owned = $true; break }
                if ($ancestor -eq [int]$candidate.ProcessId) { $ancestor = [int]$candidate.ParentProcessId }
                else {
                    $parent = Get-CimInstance Win32_Process -Filter "ProcessId=$ancestor" -Property ParentProcessId -ErrorAction Stop
                    if ($null -eq $parent) { break }
                    $ancestor = [int]$parent.ParentProcessId
                }
            }
            if (-not $owned) {
                $observation.foreign_processes += @{ pid=[int]$candidate.ProcessId; name=$candidate.Name; executable=$candidate.ExecutablePath; created_utc=$(if ($null -eq $candidate.CreationDate) { $null } else { $candidate.CreationDate.ToUniversalTime().ToString('O') }) }
            }
        }
    }
    catch {
        $observation.status = 'unknown'
        $observation.error = $_.Exception.Message
    }
    return [pscustomobject]$observation
}

function Assert-GpuProcessContext([object]$Observation) {
    if ($performanceRequested -and ($Observation.status -ne 'observed' -or $Observation.foreign_processes.Count -ne 0)) {
        throw 'GPU performance requires a verified exclusive Memoria.FF8 process context. Other processes were not stopped; defer performance or explicitly select -FunctionalOnly.'
    }
}

function Get-OrdinaryPerformanceEntries([object]$Fps) {
    $case = $Fps.cases[0]
    foreach ($cast in $case.casts) {
        $reportPath = $null
        $reportHash = $null
        if ($null -ne $cast.PSObject.Properties['source'] -and $null -ne $cast.source -and $null -ne $cast.source.PSObject.Properties['report'] -and $null -ne $cast.source.report -and $null -ne $cast.source.report.PSObject.Properties['path'] -and $null -ne $cast.source.report.PSObject.Properties['sha256']) {
            $reportPath = $cast.source.report.path
            $reportHash = $cast.source.report.sha256
        }
        elseif ($null -ne $case.PSObject.Properties['telemetry_path'] -and $null -ne $case.PSObject.Properties['telemetry_sha256']) {
            $reportPath = $case.telemetry_path
            $reportHash = $case.telemetry_sha256
        }
        $active = $null
        if ($reportPath -and $reportHash -and (Test-Path -LiteralPath $reportPath -PathType Leaf)) {
            if ((Get-FileHash -LiteralPath $reportPath -Algorithm SHA256).Hash -ne $reportHash) { throw 'Ordinary activity metadata differs from the authenticated telemetry hash.' }
            $report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json -Depth 100
            if ($null -ne $report -and $null -ne $report.PSObject.Properties['environment'] -and $null -ne $report.environment -and $null -ne $report.environment.PSObject.Properties['active'] -and $report.environment.active -is [bool]) { $active = $report.environment.active }
        }
        [pscustomobject]@{ cast=[int]$cast.index; active=$active; report_sha256=$reportHash }
    }
}

function Get-ComponentCompletionKind([object]$Measurement, [object]$Receipt, [int]$Effect, [string]$SourceHash, [int]$OwnedPid) {
    $identity = $Receipt.native_identity
    if ($Receipt.state -ne 'completed' -or $Receipt.pid -ne $OwnedPid -or $Receipt.expected_effect -ne $Effect -or $identity.effect -ne $Effect -or $Measurement.scope -ne 'installed-source-controlled-component-gpu-presentation' -or $Measurement.effect -ne $Effect -or $Measurement.source_sha256 -ne $SourceHash -or $Measurement.tail_presented_frames -lt 2 -or $Measurement.drained -isnot [bool] -or $identity.drained -isnot [bool]) { throw 'Component completion does not identify its exited owned host, selected source and truthful completion boundary.' }
    $kind = if ($null -ne $Measurement.PSObject.Properties['completion_kind']) { $Measurement.completion_kind } elseif ($Measurement.drained) { 'drained' } else { $null }
    $receiptKind = if ($null -ne $identity.PSObject.Properties['completion_kind']) { $identity.completion_kind } elseif ($identity.drained) { 'drained' } else { $null }
    if ($kind -ne $receiptKind -or $kind -notin @('drained','terminal-texture-quiescence','held-native-screen-blend')) { throw 'Component completion kind is absent, unsupported or inconsistent.' }
    $certificateName = if ($kind -eq 'held-native-screen-blend') { 'held_screen_blend_certificate' } else { 'terminal_texture_certificate' }
    $otherCertificateName = if ($kind -eq 'held-native-screen-blend') { 'terminal_texture_certificate' } else { 'held_screen_blend_certificate' }
    $measurementCertificate = if ($null -ne $Measurement.PSObject.Properties[$certificateName]) { $Measurement.PSObject.Properties[$certificateName].Value } else { $null }
    $receiptCertificate = if ($null -ne $identity.PSObject.Properties[$certificateName]) { $identity.PSObject.Properties[$certificateName].Value } else { $null }
    $otherMeasurementCertificate = if ($null -ne $Measurement.PSObject.Properties[$otherCertificateName]) { $Measurement.PSObject.Properties[$otherCertificateName].Value } else { $null }
    $otherReceiptCertificate = if ($null -ne $identity.PSObject.Properties[$otherCertificateName]) { $identity.PSObject.Properties[$otherCertificateName].Value } else { $null }
    if ($null -ne $otherMeasurementCertificate -or $null -ne $otherReceiptCertificate) { throw 'Distinct resident completion contracts cannot substitute or combine their certificates.' }
    if ($kind -eq 'drained') {
        if (-not $Measurement.drained -or -not $identity.drained -or $null -ne $measurementCertificate -or $null -ne $receiptCertificate) { throw 'Full drain cannot carry a resident certificate or a non-drained owner.' }
    }
    elseif ($Measurement.drained -or $identity.drained -or $null -eq $measurementCertificate -or $null -eq $receiptCertificate) { throw 'Resident completion requires an explicit non-drained certificate of its exact declared kind in both receipts.' }
    if ($kind -eq 'held-native-screen-blend') {
        foreach ($outer in @($Measurement,$identity)) {
            foreach ($flag in @('completed','resident_tasks_remain','held_render_work_remains')) {
                $property = $outer.PSObject.Properties[$flag]
                if ($null -eq $property -or $property.Value -isnot [bool] -or -not $property.Value) { throw 'Held native screen blend completion must explicitly disclose completed execution with resident rendering work in both outer receipts.' }
            }
        }
    }
    return $kind
}

function Invoke-OwnedProcess([string]$Executable, [string[]]$Arguments, [string]$Directory, [int]$TimeoutSeconds = 660, [switch]$GpuWork) {
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
        if ($GpuWork -and $performanceRequested) {
            $invocationContext = Get-GpuProcessContext 'before-gpu-invocation'
            $script:CurrentPerformanceContext.observations.Add($invocationContext)
            Assert-GpuProcessContext $invocationContext
        }
        if (-not $process.Start()) { throw 'The owned child could not start.' }
        $started = $true
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        $deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
        while (-not $process.WaitForExit(5000)) {
            if ($GpuWork -and $performanceRequested) {
                $script:CurrentPerformanceContext.poll_count++
                $observation = Get-GpuProcessContext 'during-gpu-invocation' $process.Id
                if ($observation.status -ne 'observed' -or $observation.foreign_processes.Count -ne 0) {
                    if ($script:CurrentPerformanceContext.observations.Count -lt 16) { $script:CurrentPerformanceContext.observations.Add($observation) }
                    $script:CurrentPerformanceContext.status = if ($observation.foreign_processes.Count) { 'invalid' } elseif ($script:CurrentPerformanceContext.status -ne 'invalid') { 'unknown' } else { 'invalid' }
                }
            }
            if ([DateTimeOffset]::UtcNow -ge $deadline) { break }
        }
        if (-not $process.HasExited) {
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
$selected = @(if ($EffectId.Count) { $EffectId | Sort-Object -Unique } else { $catalogIds })
if (@($selected | Where-Object { $_ -notin $catalogIds }).Count) { throw 'Requested IDs include empty or absent native bindings.' }
$cases = @(Get-Content -LiteralPath (Join-Path $DiscoveryDirectory 'cases-manifest.json') -Raw | ConvertFrom-Json -Depth 100)
$results = [Collections.Generic.List[object]]::new()
[IO.Directory]::CreateDirectory($OutputRoot) | Out-Null
$binaryHashes = @{}
foreach ($file in Get-ChildItem -LiteralPath (Split-Path -Parent $HostExecutable) -File | Where-Object { $_.Extension -in @('.dll', '.exe', '.so', '.dylib') }) {
    $binaryHashes[$file.Name] = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
}
if ($selected.Count -eq 0) { throw 'The selected catalog contains no native effects.' }
$preflightContext = Get-GpuProcessContext 'preflight'
Write-Json (Join-Path $OutputRoot 'inputs.json') @{ performance_requested=$performanceRequested; functional_only=[bool]$FunctionalOnly; process_context=$preflightContext; process_context_policy='Metadata-only pre/post case and five-second owned-GPU wait sampling; no foreign process control; gaps remain unsampled.'; prepare_sources_only=[bool]$PrepareSourcesOnly; disable_v_sync=[bool]$DisableVSync; execution_stack_profile=$profileIdentity; catalog_effect_ids=$catalogIds; selected_effect_ids=$selected; executable_sha256=$catalog.executable_sha256; kernel_sha256=$catalog.kernel_sha256; binaries=$binaryHashes; tool_sha256=(Get-FileHash -LiteralPath $ToolAssembly -Algorithm SHA256).Hash.ToLowerInvariant(); source_test_sha256=(Get-FileHash -LiteralPath $TestExecutable -Algorithm SHA256).Hash.ToLowerInvariant(); pacing='normal-real-time'; presentation_v_sync=$presentationVSync; resolution='1920x1080'; target_fps=60; below_fps=$BelowFps }
Assert-GpuProcessContext $preflightContext
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
        $script:CurrentPerformanceContext = @{ status=$(if ($performanceRequested) { 'valid' } elseif ($FunctionalOnly) { 'deferred' } else { 'not_requested' }); observations=[Collections.Generic.List[object]]::new(); poll_count=0; entries=@(); sampling='pre/post case and five-second waits; not continuous process history' }
        $result.performance_requested = $performanceRequested
        $result.performance_context = $script:CurrentPerformanceContext
        $beforeContext = Get-GpuProcessContext 'before-case'
        $script:CurrentPerformanceContext.observations.Add($beforeContext)
        Assert-GpuProcessContext $beforeContext
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
                    $manifest = @{ schema_version=1; executable=$HostExecutable; working_directory=(Split-Path -Parent $HostExecutable); game_dir=$GameDirectory; output_root=(Join-Path $caseRoot 'results'); mute_output=$true; presentation_v_sync=$presentationVSync; budgets=@{ initialization_ms=180000; action_one_ms=180000; cleanup_rearm_ms=60000; action_two_ms=180000; teardown_ms=15000; total_ms=600000; no_progress_ms=60000 }; cases=$ordinary }
                    if ($ExecutionStackProfile) { $manifest.execution_stack_profile = $ExecutionStackProfile }
                    Write-Json $manifestPath $manifest
                    $runExit = Invoke-OwnedProcess 'dotnet' @($ToolAssembly, 'run', '--input', $manifestPath) (Join-Path $caseRoot 'run-log') -GpuWork
                    Invoke-Tool @('reconcile-coverage','--input',$manifestPath,'--catalog',$catalogPath,'--history',$caseRoot,'--output',(Join-Path $caseRoot 'coverage.json')) (Join-Path $caseRoot 'coverage-log')
                    $coverage = (Get-Content -LiteralPath (Join-Path $caseRoot 'coverage.json') -Raw | ConvertFrom-Json -Depth 100).coverage
                    if ($effect -notin $coverage.completed.effect_ids) { throw "No authenticated two-use completion; runner exit=$runExit." }
                    if ($performanceRequested) {
                        Invoke-Tool @('recalculate-fps','--input',$manifestPath,'--history',$caseRoot,'--below-fps',$BelowFps.ToString('R',[Globalization.CultureInfo]::InvariantCulture),'--output',(Join-Path $caseRoot 'fps.json'),'--slow-output',(Join-Path $caseRoot 'slow-fps.json')) (Join-Path $caseRoot 'fps-log')
                        $fps = Get-Content -LiteralPath (Join-Path $caseRoot 'fps.json') -Raw | ConvertFrom-Json -Depth 100
                        if ($fps.requested -ne 1 -or $fps.completed_cases -ne 1 -or $fps.pending_cases -ne 0 -or $fps.unavailable_effect_casts -ne 0 -or $fps.cases.Count -ne 1 -or $fps.cases[0].case.effect_id -ne $effect -or $fps.cases[0].casts.Count -ne 2) { throw 'Authenticated FPS extraction did not retain exactly two measured casts for the selected effect.' }
                        $result.fps = $fps
                        $values = @($fps.cases[0].casts | Where-Object { $_.effect.available -and $_.effect.duration_ms -gt 0 } | ForEach-Object { 1000.0 * $_.effect.frame_count / $_.effect.duration_ms })
                        if ($values.Count -ne 2 -or @($values | Where-Object { -not [double]::IsFinite($_) -or $_ -le 0 }).Count) { throw 'Both ordinary casts require finite positive whole-interval FPS measurements.' }
                        $result.minimum_cast_fps = ($values | Measure-Object -Minimum).Minimum
                        $script:CurrentPerformanceContext.entries = @(Get-OrdinaryPerformanceEntries $fps)
                    }
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
                        Write-Json (Join-Path $entryRoot 'settings/ui-settings.json') @{ MusicVolume=0; SoundVolume=0; VoiceVolume=0; VSync=$presentationVSync }
                        $requestPath = Join-Path $entryRoot 'request.json'
                        Write-Json $requestPath @{ enemy_id=0; ally_id=0; effect_id=$effect; source_snapshot=$sourcePath; source_sha256=$sourceHash; duration_seconds=300 }
                        $control = Join-Path $entryRoot 'control'
                        $entryExit = Invoke-OwnedProcess $HostExecutable @('--game-dir',$GameDirectory,'--effect-preview',$requestPath,'--resolution','1920x1080','--settings-dir',(Join-Path $entryRoot 'settings'),'--control-dir',$control,'--save-state',(Join-Path $entryRoot 'saves/checkpoint.json')) (Join-Path $entryRoot 'host-log') 360 -GpuWork
                        $measurementPath = Join-Path $control 'component-measurements.json'
                        if ($entryExit -ne 0 -or -not (Test-Path -LiteralPath (Join-Path $control 'completed.json')) -or (Test-Path -LiteralPath (Join-Path $control 'blocked.json')) -or -not (Test-Path -LiteralPath $measurementPath)) { throw "Controlled component entry $entry failed ($entryExit)." }
                        $completedReceipt = Get-Content -LiteralPath (Join-Path $control 'completed.json') -Raw | ConvertFrom-Json -Depth 100
                        $measurement = Get-Content -LiteralPath $measurementPath -Raw | ConvertFrom-Json -Depth 100
                        $identityMatches = $false
                        if ('native_dispatch' -in $measurement.PSObject.Properties.Name -and $null -ne $measurement.native_dispatch) {
                            $identityMatches = $measurement.native_dispatch.requested_effect -eq $effect -and $measurement.native_dispatch.executed_effect -eq $effect
                        }
                        elseif ('native_admission' -in $measurement.PSObject.Properties.Name -and $null -ne $measurement.native_admission) {
                            $identityMatches = $measurement.native_admission.effect -eq $effect
                        }
                        if ($measurement.scope -ne 'installed-source-controlled-component-gpu-presentation' -or $measurement.effect -ne $effect -or $measurement.source_sha256 -ne $sourceHash -or $measurement.tail_presented_frames -lt 2 -or -not $identityMatches) { throw 'Component execution identity, cleanup or tail evidence is inconsistent.' }
                        $completionKind = Get-ComponentCompletionKind $measurement $completedReceipt $effect $sourceHash $script:LastOwnedProcessId
                        if ($completionKind -in @('terminal-texture-quiescence','held-native-screen-blend')) {
                            Invoke-Tool @('validate-component-completion','--input',$measurementPath,'--receipt',(Join-Path $control 'completed.json'),'--effect',$effect.ToString([Globalization.CultureInfo]::InvariantCulture),'--source-sha256',$sourceHash,'--pid',$script:LastOwnedProcessId.ToString([Globalization.CultureInfo]::InvariantCulture)) (Join-Path $entryRoot 'completion-validation-log')
                        }
                        if ($measurement.host.vsync -ne $presentationVSync -or $measurement.host.presentation_interval -ne $expectedPresentationInterval -or $measurement.host.frame_limit -ne 60 -or $measurement.host.width -ne 1920 -or $measurement.host.height -ne 1080) { throw 'The actual component host differs from the declared real-time 60-FPS selected VSync presentation profile.' }
                        $metrics = $measurement.measurements
                        if ($metrics.scope -ne $measurement.scope -or $metrics.interval_count -lt 1 -or -not [double]::IsFinite($metrics.interval_milliseconds) -or $metrics.interval_milliseconds -le 0 -or -not [double]::IsFinite($metrics.effective_fps) -or $metrics.effective_fps -le 0 -or [Math]::Abs($metrics.effective_fps - 1000.0 * $metrics.interval_count / $metrics.interval_milliseconds) -gt 0.000001 -or $metrics.target_fps -ne 60) { throw 'Component FPS lacks consistent positive whole successful-present intervals at the declared target.' }
                        $entries.Add($measurement)
                        $result.executions++
                    }
                    $result.entries = $entries.ToArray()
                    $result.completed = $true
                    if ($performanceRequested) {
                        $result.minimum_cast_fps = ($entries | ForEach-Object { [double]$_.measurements.effective_fps } | Measure-Object -Minimum).Minimum
                        $script:CurrentPerformanceContext.entries = @(for ($index = 0; $index -lt $entries.Count; $index++) {
                            $inactive = if ('inactive_presented_frames' -in $entries[$index].host.PSObject.Properties.Name) { $entries[$index].host.inactive_presented_frames } else { $null }
                            [pscustomobject]@{ cast=$index + 1; inactive_presented_frames=$inactive; active=$(if ($null -eq $inactive) { $null } else { $inactive -eq 0 }) }
                        })
                    }
                }
                Remove-OwnedDirectory $sourceRoot
            }
            if ($null -ne $result.minimum_cast_fps) { $result.below_threshold = $result.minimum_cast_fps -lt $BelowFps }
        }
        catch {
            $result.completed = $false
            $result.error = $_.Exception.Message
            if ($_.Exception.Data['StopCatalog']) {
                Write-Json (Join-Path $caseRoot 'result.json') $result
                throw
            }
        }
        finally {
            if ($null -ne $componentLease) { $componentLease.Dispose() }
        }
        $afterContext = Get-GpuProcessContext 'after-case'
        $script:CurrentPerformanceContext.observations.Add($afterContext)
        if ($performanceRequested) {
            $foreign = @($script:CurrentPerformanceContext.observations | Where-Object { $_.foreign_processes.Count -gt 0 })
            $unknown = @($script:CurrentPerformanceContext.observations | Where-Object { $_.status -ne 'observed' })
            $inactive = @($script:CurrentPerformanceContext.entries | Where-Object { $null -ne $_.active -and -not $_.active })
            $missingActivity = @($script:CurrentPerformanceContext.entries | Where-Object { $null -eq $_.active })
            if ($foreign.Count -or $inactive.Count -or $script:CurrentPerformanceContext.status -eq 'invalid') { $script:CurrentPerformanceContext.status = 'invalid' }
            elseif ($unknown.Count -or $missingActivity.Count -or $script:CurrentPerformanceContext.entries.Count -ne 2 -or $script:CurrentPerformanceContext.status -eq 'unknown') { $script:CurrentPerformanceContext.status = 'unknown' }
            $script:CurrentPerformanceContext.reasons = @($(if ($foreign.Count) { 'competing_memoria_process' }); $(if ($unknown.Count) { 'process_metadata_unavailable' }); $(if ($inactive.Count) { 'inactive_presentations' }); $(if ($missingActivity.Count -or $script:CurrentPerformanceContext.entries.Count -ne 2) { 'activity_metadata_unavailable' }))
        }
        $result.performance_status = $script:CurrentPerformanceContext.status
        $results.Add([pscustomobject]$result)
        Write-Json (Join-Path $caseRoot 'result.json') $result
        $completed = @($results | Where-Object { $_.completed -and $null -eq $_.error } | ForEach-Object effect_id)
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
        $summary.error_effect_ids = @($results | Where-Object { $null -ne $_.error } | ForEach-Object effect_id)
        $summary.observed_below_threshold_effect_ids = $summary.below_threshold_effect_ids
        $summary.performance_requested = $performanceRequested
        $summary.performance_status = if ($FunctionalOnly) { 'deferred' } elseif ($PrepareSourcesOnly) { 'not_requested' } elseif (@($results | Where-Object { $_.performance_context.status -eq 'invalid' }).Count) { 'invalid' } elseif (@($results | Where-Object { $_.performance_context.status -ne 'valid' }).Count) { 'unknown' } else { 'measured' }
        $summary.performance_invalid_effect_ids = @($results | Where-Object { $_.performance_context.status -eq 'invalid' } | ForEach-Object effect_id)
        $summary.performance_unknown_effect_ids = @($results | Where-Object { $_.performance_context.status -eq 'unknown' } | ForEach-Object effect_id)
        $summary.accepted_below_threshold_effect_ids = @($results | Where-Object { $_.completed -and $null -eq $_.error -and $_.below_threshold -and $_.performance_context.status -eq 'valid' } | ForEach-Object effect_id)
        $summary.selected_performance_passed = $performanceRequested -and $summary.performance_status -eq 'measured' -and $summary.selected_execution_complete -and $summary.fps_available_count -eq $selected.Count -and $summary.below_threshold_effect_ids.Count -eq 0
        $summary.full_catalog_performance_passed = $summary.full_execution_complete -and $summary.selected_performance_passed
        Write-Json (Join-Path $OutputRoot 'summary.json') $summary
        Write-Host ("Effect {0}: prepared={1}, completed={2}, route={3}, minimum cast FPS={4}, error={5}" -f $effect,$result.prepared,$result.completed,$result.route,$result.minimum_cast_fps,$result.error)
        $retainedBytes = (Get-ChildItem -LiteralPath $OutputRoot -File -Recurse | Measure-Object Length -Sum).Sum
        if ($performanceRequested -and @($script:CurrentPerformanceContext.observations | Where-Object { $_.foreign_processes.Count -gt 0 -or $_.status -ne 'observed' }).Count -gt 0) { throw 'GPU performance context became contaminated or unverifiable; functional case evidence was retained and no foreign process was stopped. Defer performance or select -FunctionalOnly.' }
        if ($retainedBytes -gt $MaximumRetainedMiB * 1MB) { throw "Retained data exceeded $MaximumRetainedMiB MiB; catalog capture stopped after confirmed child exit." }
    }
}
finally {
    foreach ($name in $oldEnvironment.Keys) { [Environment]::SetEnvironmentVariable($name,$oldEnvironment[$name],'Process') }
}
if ($PrepareSourcesOnly) {
    if (@($results | Where-Object { -not $_.prepared -or $null -ne $_.error }).Count) { throw 'Catalog source preparation has missing or failed effects; see summary.json.' }
}
elseif (@($results | Where-Object { -not $_.completed -or $null -ne $_.error }).Count) { throw 'Catalog execution has missing or failed effects; see summary.json.' }
