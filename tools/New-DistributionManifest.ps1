[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PackageRoot,
    [ValidateSet("win-x64", "linux-x64", "osx-x64")][string]$RuntimeIdentifier = "win-x64"
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = [IO.Path]::GetFullPath($PackageRoot)
$buildId = [Guid]::NewGuid().ToString('N')
$version = 'local'
if ($env:GITHUB_ACTIONS -eq 'true') {
    if ($env:GITHUB_SHA -notmatch '^[0-9a-f]{40,64}$') {
        throw 'The CI source identity is missing or invalid.'
    }
    $buildId = $env:GITHUB_SHA
    $version = 'nightly'
    if ($env:GITHUB_REF_TYPE -eq 'tag' -and $env:GITHUB_REF_NAME -match '^v[0-9]+\.[0-9]+\.[0-9]+(?:\.[0-9]+)?$') {
        $version = $env:GITHUB_REF_NAME
    }
}
$prefix = if ($RuntimeIdentifier -eq 'osx-x64') { 'Contents/MacOS/' } else { '' }
if ($RuntimeIdentifier -eq 'osx-x64') {
    & dotnet run --project (Join-Path $PSScriptRoot 'Memoria.FF8.Distribution.Check') -c Release -- --set-mac-bundle-version (Join-Path $root 'Contents/Info.plist') $version
    if ($LASTEXITCODE -ne 0) {
        throw 'The macOS bundle version could not be stamped before inventory generation.'
    }
}
$manifestPath = Join-Path $root ($prefix + 'Data/distribution.json')
$files = @(Get-ChildItem -LiteralPath $root -File -Recurse | Where-Object FullName -ne $manifestPath)
$inventory = @($files | Sort-Object FullName | ForEach-Object {
    $relative = [IO.Path]::GetRelativePath($root, $_.FullName).Replace('\', '/')
    $mode = if ($RuntimeIdentifier -eq 'win-x64') { 0 } elseif ([IO.Path]::GetFileName($_.FullName) -in @('Memoria.Launcher', 'Memoria.FF8', 'Memoria.FF8.Music', 'Memoria.PsfDecoder')) { 493 } else { 420 }
    [ordered]@{
        UnixMode = $mode
        Path = [IO.Path]::GetRelativePath($root, $_.FullName).Replace('\', '/')
        Size = $_.Length
        Sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    }
})
$manifest = [ordered]@{
    SchemaVersion = $(if ($RuntimeIdentifier -eq "win-x64") { 1 } else { 2 })
    RuntimeIdentifier = $RuntimeIdentifier
    LayoutVersion = 1
    EntryPoints = @{ Launcher = $prefix + "Memoria.Launcher" + $(if ($RuntimeIdentifier -eq "win-x64") { ".exe" } else { "" }); Game = $prefix + "Data/Memoria.FF8" + $(if ($RuntimeIdentifier -eq "win-x64") { ".exe" } else { "" }) }
    BuildId = $buildId
    Version = $version
    BuiltAt = [DateTimeOffset]::UtcNow.ToString('o')
    Files = $inventory
}
$manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $manifestPath -Encoding utf8NoBOM
