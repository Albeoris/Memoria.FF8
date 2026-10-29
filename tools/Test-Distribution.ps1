[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ArchivePath,
    [Parameter(Mandatory)][ValidateSet('win-x64', 'linux-x64', 'osx-x64', 'osx-arm64')][string]$RuntimeIdentifier,
    [string]$Configuration = 'Release',
    [string[]]$BuildArguments = @()
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$extracted = Join-Path $root ('artifacts/validated/' + $RuntimeIdentifier + '-' + [Guid]::NewGuid().ToString('N'))
$arguments = @('run', '--project', (Join-Path $root 'tools/Memoria.FF8.Distribution.Check'), '-c', $Configuration)
if ($BuildArguments.Count -gt 0) {
    $arguments += @('--no-build') + $BuildArguments
}
$arguments += @('--', '--validate-archive', [IO.Path]::GetFullPath($ArchivePath), $RuntimeIdentifier, $extracted)
& dotnet @arguments
if ($LASTEXITCODE -ne 0) {
    throw "Extracted $RuntimeIdentifier package validation failed; retained at $extracted."
}
Write-Host "Extracted package ABI, inventory, containment and executable modes verified: $extracted"
