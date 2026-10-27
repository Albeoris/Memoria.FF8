[CmdletBinding()]
param([string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$product = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$temporary = Join-Path ([IO.Path]::GetTempPath()) ('memoria-packaging-tests-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $temporary | Out-Null
$checker = Join-Path $product 'tools/Memoria.FF8.Distribution.Check'
function Invoke-Checker([string[]]$Arguments, [switch]$ExpectFailure) {
    $output = @(& dotnet run --project $checker -c $Configuration -- @Arguments 2>&1)
    if (($LASTEXITCODE -ne 0) -ne [bool]$ExpectFailure) {
        throw "Unexpected distribution checker result for $($Arguments[0]): $($output -join [Environment]::NewLine)"
    }
}
try {
    $package = Join-Path $temporary 'package'
    foreach ($relative in @('Memoria.Launcher', 'Data/Memoria.FF8', 'Data/audio-host/Memoria.FF8.Music', 'Data/Game/vulkan/Memoria.FF8')) {
        $path = Join-Path $package $relative
        New-Item -ItemType Directory -Force -Path ([IO.Path]::GetDirectoryName($path)) | Out-Null
        $elf = [byte[]]::new(32)
        $elf[0] = 127; $elf[1] = 69; $elf[2] = 76; $elf[3] = 70; $elf[4] = 2; $elf[5] = 1; $elf[18] = 62
        [IO.File]::WriteAllBytes($path, $elf)
    }
    & (Join-Path $product 'tools/New-DistributionManifest.ps1') -PackageRoot $package -RuntimeIdentifier linux-x64
    $manifest = Get-Content -LiteralPath (Join-Path $package 'Data/distribution.json') -Raw | ConvertFrom-Json
    if ($manifest.SchemaVersion -ne 2 -or $manifest.RuntimeIdentifier -cne 'linux-x64' -or $manifest.Files.Count -ne 4 -or @($manifest.Files | Where-Object UnixMode -ne 493).Count -ne 0) {
        throw 'Linux descriptor identity, inventory or executable modes are incorrect.'
    }
    $archive = Join-Path $temporary 'package.tar.gz'
    Invoke-Checker @('--create-tar', $package, $archive)
    Invoke-Checker @('--validate-archive', $archive, 'linux-x64', (Join-Path $temporary 'extracted'))
    Invoke-Checker @('--set-version', $archive, 'v1.2.3', $manifest.BuildId)
    Invoke-Checker @('--validate-archive', $archive, 'linux-x64', (Join-Path $temporary 'versioned'))
    $versioned = Get-Content -LiteralPath (Join-Path $temporary 'versioned/Data/distribution.json') -Raw | ConvertFrom-Json
    if ($versioned.Version -cne 'v1.2.3' -or $versioned.BuildId -cne $manifest.BuildId) {
        throw 'Version mutation changed source identity or failed to set the version.'
    }
    # A fresh inventory does not make a foreign native architecture acceptable.
    $foreign = [IO.File]::ReadAllBytes((Join-Path $package 'Data/audio-host/Memoria.FF8.Music'))
    $foreign[18] = 183
    [IO.File]::WriteAllBytes((Join-Path $package 'Data/audio-host/Memoria.FF8.Music'), $foreign)
    & (Join-Path $product 'tools/New-DistributionManifest.ps1') -PackageRoot $package -RuntimeIdentifier linux-x64
    Invoke-Checker @('--create-tar', $package, $archive)
    Invoke-Checker @('--validate-archive', $archive, 'linux-x64', (Join-Path $temporary 'foreign')) -ExpectFailure
    Write-Output 'Packaging descriptor, tar modes, source-preserving version mutation and foreign-native rejection passed.'
}
finally {
    if ([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($temporary)) -ne [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar)) {
        throw 'Test cleanup escaped its dedicated temporary root.'
    }
    Remove-Item -LiteralPath $temporary -Recurse -Force
}
