[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$InputRecipe,
    [Parameter(Mandatory)][string]$OutputRecipe,
    [Parameter(Mandatory)][string]$RuntimeAssembly
)
$ErrorActionPreference = 'Stop'
$inputPath = [IO.Path]::GetFullPath($InputRecipe)
$outputPath = [IO.Path]::GetFullPath($OutputRecipe)
$receiptPath = $outputPath + '.migration.json'
if ($inputPath.Equals($outputPath, [StringComparison]::OrdinalIgnoreCase) -or (Test-Path -LiteralPath $outputPath) -or (Test-Path -LiteralPath $receiptPath)) {
    throw 'Choose a distinct new recipe path; original recipes and migration receipts are preserved.'
}
$sourceStream = [IO.File]::Open($inputPath, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
try {
    if ($sourceStream.Length -gt 4MB) {
        throw 'The recipe exceeds the supported size.'
    }
    $source = [byte[]]::new([int]$sourceStream.Length)
    $sourceStream.ReadExactly($source)
} finally {
    $sourceStream.Dispose()
}
[Reflection.Assembly]::LoadFrom([IO.Path]::GetFullPath($RuntimeAssembly)) | Out-Null
$recipe = [Text.Encoding]::UTF8.GetString($source).TrimStart([char]0xFEFF) | ConvertFrom-Json
$changes = [Collections.Generic.List[object]]::new()
foreach ($repair in @($recipe.driver.repairs | Where-Object { $null -ne $_ })) {
    $request = [Memoria.FF8.Runtime.BattleDiagnosticCombatRepair]::new([string]$repair.actor_id, [int]$repair.hit_points, [bool]$repair.revive, [ushort]$repair.remove_statuses0, [uint]$repair.remove_statuses1)
    $request.Validate()
    $saved = [int]$request.RemoveStatuses0 -bor [int][Memoria.FF8.Runtime.BattleDiagnosticCombatRepair]::AllSavedStatuses
    $battle = [uint]$request.RemoveStatuses1 -bor [uint][Memoria.FF8.Runtime.BattleDiagnosticCombatRepair]::AllBattleStatuses
    $updated = [Memoria.FF8.Runtime.BattleDiagnosticCombatRepair]::new($request.ActorId, $request.HitPoints, $request.Revive, [ushort]$saved, [uint]$battle)
    $updated.Validate()
    if (-not $updated.ClearsAllStatuses) {
        throw 'Select a runtime supporting explicit full ordinary status cleanup.'
    }
    $changes.Add([ordered]@{actor_id=$request.ActorId;before_saved=$request.RemoveStatuses0;after_saved=$saved;before_battle=$request.RemoveStatuses1;after_battle=$battle})
    $repair | Add-Member -NotePropertyName remove_statuses0 -NotePropertyValue $saved -Force
    $repair | Add-Member -NotePropertyName remove_statuses1 -NotePropertyValue $battle -Force
}
function WriteNew([string]$Path, [byte[]]$Bytes) {
    $stream = [IO.File]::Open($Path, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
    try {
        $stream.Write($Bytes)
        $stream.Flush($true)
    } finally {
        $stream.Dispose()
    }
}
$bytes = [Text.Encoding]::UTF8.GetBytes(($recipe | ConvertTo-Json -Depth 100))
WriteNew $outputPath $bytes
$receipt = [ordered]@{
    schema_version=1
    original_recipe=$inputPath
    original_sha256=[Convert]::ToHexStringLower([Security.Cryptography.SHA256]::HashData($source))
    migrated_recipe=$outputPath
    migrated_sha256=[Convert]::ToHexStringLower([Security.Cryptography.SHA256]::HashData($bytes))
    changes=$changes.ToArray()
    policy='Only existing declared repair masks are extended. Recipients, HP, revival, actions, resources, seeds and terminal holds are retained. Missing repairs are not invented.'
}
WriteNew $receiptPath ([Text.Encoding]::UTF8.GetBytes(($receipt | ConvertTo-Json -Depth 10)))
$receipt | ConvertTo-Json -Depth 10
