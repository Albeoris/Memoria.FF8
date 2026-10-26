[CmdletBinding()]
param([switch]$Check)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
[xml]$portable = Get-Content -LiteralPath (Join-Path $root 'Memoria.FF8.Portable.slnx') -Raw
$covered = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($project in $portable.Solution.Project) {
    [void]$covered.Add([string]$project.Path)
}
$exceptions = @{
    'Memoria.FF8.App' = 'Windows DX11 host; Native hosts start at M5.'
    'Memoria.FF8.App.Tests' = 'Windows DX11 host integration; portable presentation tests run separately.'
    'Memoria.FF8.Acceptance' = 'External original-game debugging tools; explicit local inputs required.'
    'Memoria.FF8.BattleEffects.Tests' = 'Effect tests require an explicitly authorized affected-effect selection.'
}
$inventory = foreach ($area in @('src', 'tests', 'tools', 'vendor')) {
    foreach ($project in Get-ChildItem -LiteralPath (Join-Path $root $area) -Filter '*.csproj' -Recurse -File | Sort-Object FullName) {
        $relative = [IO.Path]::GetRelativePath($root, $project.FullName).Replace('\', '/')
        [xml]$xml = Get-Content -LiteralPath $project.FullName -Raw
        $frameworks = @($xml.SelectNodes('//TargetFramework|//TargetFrameworks') | ForEach-Object InnerText) -join ';'
        $status = if ($area -eq 'vendor') { 'Portable vendored dependency closure' } elseif ($covered.Contains($relative)) { 'Portable graph (net10.0)' } elseif ($exceptions.ContainsKey($project.BaseName)) { $exceptions[$project.BaseName] } else { 'UNACCOUNTED' }
        if ($Check -and $status -eq 'UNACCOUNTED') {
            throw "Project coverage missing: $relative"
        }
        [pscustomobject]@{ Project = $relative; Frameworks = $frameworks; Coverage = $status }
    }
}
if ($Check) {
    foreach ($project in $covered) {
        if (!(Test-Path -LiteralPath (Join-Path $root $project) -PathType Leaf)) {
            throw "Portable graph references a missing project: $project"
        }
    }
    Write-Host "Project coverage checked: $(@($inventory).Count) projects; $($covered.Count) portable roots plus their vendored closure."
}
else {
    $inventory | ConvertTo-Json -Depth 4
}
