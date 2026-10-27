[CmdletBinding()]
param([switch]$Check)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))

function Get-GraphClosure([string]$solutionName) {
    [xml]$solution = Get-Content -LiteralPath (Join-Path $root $solutionName) -Raw
    $covered = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $pending = [Collections.Generic.Queue[string]]::new()
    foreach ($project in $solution.SelectNodes('//Project')) {
        $pending.Enqueue([string]$project.Path)
    }
    while ($pending.Count -gt 0) {
        $relative = $pending.Dequeue().Replace('\', '/')
        if (!$covered.Add($relative)) {
            continue
        }
        $path = Join-Path $root $relative
        if (!(Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Graph $solutionName references a missing project: $relative"
        }
        [xml]$project = Get-Content -LiteralPath $path -Raw
        foreach ($reference in $project.SelectNodes('//ProjectReference[@Include]')) {
            $referencePath = [string]$reference.Include
            foreach ($property in $project.SelectNodes('//PropertyGroup/*')) {
                $referencePath = $referencePath.Replace('$(' + $property.Name + ')', [string]$property.InnerText)
            }
            $referencePath = $referencePath.Replace('$(MSBuildThisFileDirectory)', [IO.Path]::GetDirectoryName($path) + [IO.Path]::DirectorySeparatorChar)
            if ($referencePath.Contains('$(')) {
                throw "Unresolved project reference in ${relative}: $referencePath"
            }
            $referencePath = $referencePath.Replace('\', [string][IO.Path]::DirectorySeparatorChar)
            $dependency = if ([IO.Path]::IsPathRooted($referencePath)) { [IO.Path]::GetFullPath($referencePath) } else { [IO.Path]::GetFullPath((Join-Path ([IO.Path]::GetDirectoryName($path)) $referencePath)) }
            $dependencyRelative = [IO.Path]::GetRelativePath($root, $dependency).Replace('\', '/')
            if ($dependencyRelative -eq '..' -or $dependencyRelative.StartsWith('../', [StringComparison]::Ordinal) -or [IO.Path]::IsPathRooted($dependencyRelative)) {
                throw "Project reference leaves the product source tree: $relative -> $dependencyRelative"
            }
            $pending.Enqueue($dependencyRelative)
        }
    }
    return ,$covered
}

$portable = Get-GraphClosure 'Memoria.FF8.Portable.slnx'
$native = Get-GraphClosure 'Memoria.FF8.Native.slnx'
$windows = Get-GraphClosure 'Memoria.FF8.slnx'
$inventory = foreach ($area in @('src', 'tests', 'tools', 'vendor')) {
    foreach ($project in Get-ChildItem -LiteralPath (Join-Path $root $area) -Filter '*.csproj' -Recurse -File | Sort-Object FullName) {
        $relative = [IO.Path]::GetRelativePath($root, $project.FullName).Replace('\', '/')
        [xml]$xml = Get-Content -LiteralPath $project.FullName -Raw
        $frameworks = @($xml.SelectNodes('//TargetFramework|//TargetFrameworks') | ForEach-Object InnerText) -join ';'
        $status = if ($native.Contains($relative) -and $windows.Contains($relative)) { 'Complete Windows and Native graphs (compile only; tests require explicit selection)' } else { 'UNACCOUNTED' }
        if ($Check -and $status -eq 'UNACCOUNTED') {
            throw "Complete project coverage missing: $relative"
        }
        [pscustomobject]@{ Project = $relative; Frameworks = $frameworks; PortableCore = $portable.Contains($relative); Coverage = $status }
    }
}
if ($Check) {
    Write-Host "Project coverage checked: $(@($inventory).Count) projects; complete Windows and Native graph closures. Portable core is an additional focused graph."
}
else {
    $inventory | ConvertTo-Json -Depth 4
}
