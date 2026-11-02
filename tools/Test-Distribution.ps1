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
$previousArchive = $env:MEMORIA_FF8_DISTRIBUTION_PATH
$previousRuntime = $env:MEMORIA_FF8_DISTRIBUTION_RID
try {
    $env:MEMORIA_FF8_DISTRIBUTION_PATH = [IO.Path]::GetFullPath($ArchivePath)
    $env:MEMORIA_FF8_DISTRIBUTION_RID = $RuntimeIdentifier
    $updaterArguments = @('run', '--project', (Join-Path $root 'tests/Memoria.Launcher.Tests'), '-c', $Configuration)
    if ($BuildArguments.Count -gt 0) {
        $updaterArguments += @('--no-build') + $BuildArguments
    }
    & dotnet @updaterArguments -- --filter-class '*DistributionArchiveTests'
    if ($LASTEXITCODE -ne 0) {
        throw "The $RuntimeIdentifier archive cannot be staged by the launcher updater."
    }
}
finally {
    $env:MEMORIA_FF8_DISTRIBUTION_PATH = $previousArchive
    $env:MEMORIA_FF8_DISTRIBUTION_RID = $previousRuntime
}
Write-Host "Extracted package ABI, inventory, containment and executable modes verified: $extracted"
