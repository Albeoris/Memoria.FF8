[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PackageRoot
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
$manifestPath = Join-Path $root 'Data/distribution.json'
$files = @(
    Get-Item -LiteralPath (Join-Path $root 'Memoria.Launcher.exe')
    Get-ChildItem -LiteralPath (Join-Path $root 'Data') -File -Recurse | Where-Object FullName -ne $manifestPath
)
$inventory = @($files | Sort-Object FullName | ForEach-Object {
    [ordered]@{
        Path = [IO.Path]::GetRelativePath($root, $_.FullName).Replace('\', '/')
        Size = $_.Length
        Sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    }
})
$manifest = [ordered]@{
    SchemaVersion = 1
    BuildId = $buildId
    Version = $version
    BuiltAt = [DateTimeOffset]::UtcNow.ToString('o')
    Files = $inventory
}
$manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $manifestPath -Encoding utf8NoBOM
