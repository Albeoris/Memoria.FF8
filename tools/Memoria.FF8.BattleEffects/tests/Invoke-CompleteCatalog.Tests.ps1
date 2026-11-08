#requires -Version 7.0
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$tokens = $null
$parseErrors = $null
$scriptPath = Join-Path $PSScriptRoot '../Invoke-CompleteCatalog.ps1'
$ast = [Management.Automation.Language.Parser]::ParseFile($scriptPath, [ref]$tokens, [ref]$parseErrors)
if ($parseErrors.Count) { throw 'Complete catalog script failed parsing.' }
foreach ($name in @('Get-GpuProcessContext', 'Assert-GpuProcessContext', 'Get-OrdinaryPerformanceEntries', 'Get-ComponentCompletionKind')) {
    $function = @($ast.FindAll({ param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq $name }, $false))
    if ($function.Count -ne 1) { throw "Expected one helper $name." }
    . ([scriptblock]::Create($function[0].Extent.Text))
}
function Assert-True([bool]$Value, [string]$Message) {
    if (-not $Value) { throw $Message }
}
function Assert-Throws([scriptblock]$Action, [string]$Message) {
    $thrown = $false
    try { & $Action } catch { $thrown = $true }
    Assert-True $thrown $Message
}
$selection = @($ast.FindAll({ param($node) $node -is [Management.Automation.Language.AssignmentStatementAst] -and $node.Left.Extent.Text -eq '$selected' }, $false))
Assert-True ($selection.Count -eq 1) 'Expected one catalog selection assignment.'
$catalogIds = @(1, 2, 301)
foreach ($EffectId in @(@(301), @(301, 301, 2), @())) {
    . ([scriptblock]::Create($selection[0].Extent.Text))
    Assert-True ($selected -is [array]) 'A single selected effect must remain an array under StrictMode.'
    $expected = if ($EffectId.Count) { @($EffectId | Sort-Object -Unique) } else { $catalogIds }
    Assert-True (($selected -join ',') -eq ($expected -join ',')) 'Selection must preserve all requested unique bindings.'
}
$component = [pscustomobject]@{ scope='installed-source-controlled-component-gpu-presentation'; effect=101; source_sha256='source'; tail_presented_frames=2; drained=$true }
$completion = [pscustomobject]@{ state='completed'; pid=123; expected_effect=101; native_identity=[pscustomobject]@{ effect=101; drained=$true } }
Assert-True ((Get-ComponentCompletionKind $component $completion 101 'source' 123) -eq 'drained') 'Legacy truthful full drain must remain supported.'
$component | Add-Member completion_kind 'terminal-texture-quiescence'
$completion.native_identity | Add-Member completion_kind 'terminal-texture-quiescence'
$component.drained = $false
$completion.native_identity.drained = $false
Assert-Throws { Get-ComponentCompletionKind $component $completion 101 'source' 123 } 'Quiescence cannot be admitted by booleans without its explicit certificate.'
$component | Add-Member terminal_texture_certificate ([pscustomobject]@{ completed=$true })
$completion.native_identity | Add-Member terminal_texture_certificate ([pscustomobject]@{ completed=$true })
Assert-True ((Get-ComponentCompletionKind $component $completion 101 'source' 123) -eq 'terminal-texture-quiescence') 'Quiescent kind must route through the independent typed certificate CLI gate.'
$component.drained = $true
Assert-Throws { Get-ComponentCompletionKind $component $completion 101 'source' 123 } 'A resident completion must never claim raw full drain.'
$component.drained = $false
$completion.pid = 124
Assert-Throws { Get-ComponentCompletionKind $component $completion 101 'source' 123 } 'Completion must identify the actual owned host.'
$completion.pid = 123
$component.completion_kind = 'held-native-screen-blend'
Assert-Throws { Get-ComponentCompletionKind $component $completion 101 'source' 123 } 'Unproved distinct resident kinds cannot enter the terminal texture gate.'
$component.completion_kind = 'held-native-screen-blend'
$completion.native_identity.completion_kind = 'held-native-screen-blend'
$component.terminal_texture_certificate = $null
$completion.native_identity.terminal_texture_certificate = $null
foreach ($outer in @($component,$completion.native_identity)) {
    foreach ($flag in @('completed','resident_tasks_remain','held_render_work_remains')) { $outer | Add-Member $flag $true }
}
$component | Add-Member held_screen_blend_certificate ([pscustomobject]@{ completed=$true; held_render_work_remains=$true })
$completion.native_identity | Add-Member held_screen_blend_certificate ([pscustomobject]@{ completed=$true; held_render_work_remains=$true })
Assert-True ((Get-ComponentCompletionKind $component $completion 101 'source' 123) -eq 'held-native-screen-blend') 'Held native work must route only to its distinct independent typed CLI validation.'
$component.held_render_work_remains = $false
Assert-Throws { Get-ComponentCompletionKind $component $completion 101 'source' 123 } 'Held work cannot be hidden in the outer completion context.'
$component.held_render_work_remains = $true
$component.terminal_texture_certificate = [pscustomobject]@{ completed=$true }
Assert-Throws { Get-ComponentCompletionKind $component $completion 101 'source' 123 } 'Texture certificate cannot substitute or combine with a held screen-blend certificate.'
$script:MockProcesses = @()
$script:MockQueryFails = $false
function Get-CimInstance {
    param($ClassName, $Filter, $Property, $ErrorAction)
    if ($script:MockQueryFails) { throw 'Synthetic metadata access failure.' }
    if ($Filter -like 'Name LIKE*') { return $script:MockProcesses }
    if ($Filter -eq 'ProcessId=101') { return [pscustomobject]@{ ParentProcessId=100 } }
    return $null
}
$performanceRequested = $true
$script:MockProcesses = @([pscustomobject]@{ ProcessId=102; ParentProcessId=101; Name='Memoria.FF8.exe'; ExecutablePath='host.exe'; CreationDate=[DateTime]::UtcNow })
$owned = Get-GpuProcessContext 'owned' 100
Assert-True ($owned.status -eq 'observed' -and $owned.foreign_processes.Count -eq 0) 'Owned descendant must not reject its own GPU measurement.'
$foreign = Get-GpuProcessContext 'foreign'
Assert-True ($foreign.foreign_processes.Count -eq 1 -and $foreign.foreign_processes[0].pid -eq 102) 'Foreign process metadata must be retained.'
Assert-Throws { Assert-GpuProcessContext $foreign } 'Default performance must refuse a competing host.'
$performanceRequested = $false
Assert-GpuProcessContext $foreign
$performanceRequested = $true
$script:MockQueryFails = $true
$unknown = Get-GpuProcessContext 'unknown'
Assert-True ($unknown.status -eq 'unknown') 'Unavailable metadata must not become a clean observation.'
Assert-Throws { Assert-GpuProcessContext $unknown } 'Default performance must refuse unverifiable process context.'
$temporary = Join-Path ([IO.Path]::GetTempPath()) ('catalog-context-' + [Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($temporary) | Out-Null
try {
    $reportPath = Join-Path $temporary 'report.json'
    '{"environment":{"active":false}}' | Set-Content -LiteralPath $reportPath -Encoding utf8
    $hash = (Get-FileHash -LiteralPath $reportPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $fps = [pscustomobject]@{ cases=@([pscustomobject]@{ telemetry_path=$reportPath; telemetry_sha256=$hash; casts=@([pscustomobject]@{ index=1; source=$null },[pscustomobject]@{ index=2; source=$null }) }) }
    $entries = @(Get-OrdinaryPerformanceEntries $fps)
    Assert-True ($entries.Count -eq 2 -and $entries[0].active -eq $false -and $entries[1].report_sha256 -eq $hash) 'Both ordinary casts must preserve authenticated inactive metadata.'
    '{}' | Set-Content -LiteralPath $reportPath -Encoding utf8
    Assert-Throws { Get-OrdinaryPerformanceEntries $fps } 'Changed report must not supply authenticated activity.'
    $fps.cases[0].telemetry_sha256 = (Get-FileHash -LiteralPath $reportPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $entries = @(Get-OrdinaryPerformanceEntries $fps)
    Assert-True ($null -eq $entries[0].active) 'Missing activity must remain unknown.'
    $fps.cases[0].casts = @([pscustomobject]@{ index=1; source=[pscustomobject]@{ report=[pscustomobject]@{ path=$reportPath; sha256=$fps.cases[0].telemetry_sha256 } } })
    $entries = @(Get-OrdinaryPerformanceEntries $fps)
    Assert-True ($entries[0].report_sha256 -eq $fps.cases[0].telemetry_sha256) 'Fresh-entry source pin must retain its own report identity.'
}
finally {
    $resolvedTemporary = [IO.Path]::GetFullPath($temporary)
    $temporaryRoot = [IO.Path]::TrimEndingDirectorySeparator([IO.Path]::GetFullPath([IO.Path]::GetTempPath())) + [IO.Path]::DirectorySeparatorChar
    if (-not $resolvedTemporary.StartsWith($temporaryRoot, [StringComparison]::OrdinalIgnoreCase) -or -not [IO.Path]::GetFileName($resolvedTemporary).StartsWith('catalog-context-', [StringComparison]::Ordinal)) { throw 'Test cleanup escaped its freshly created temporary directory.' }
    Remove-Item -LiteralPath $resolvedTemporary -Recurse
}
Write-Host 'Complete catalog context helper checks passed.'
