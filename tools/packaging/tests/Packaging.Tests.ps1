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
    foreach ($relative in @('Memoria.Launcher', 'Data/Memoria.FF8', 'Data/audio-host/Memoria.FF8.Music', 'Data/Game/vulkan/Memoria.FF8', 'Data/Game/vulkan/Memoria.PsfDecoder')) {
        $path = Join-Path $package $relative
        New-Item -ItemType Directory -Force -Path ([IO.Path]::GetDirectoryName($path)) | Out-Null
        $elf = [byte[]]::new(32)
        $elf[0] = 127; $elf[1] = 69; $elf[2] = 76; $elf[3] = 70; $elf[4] = 2; $elf[5] = 1; $elf[18] = 62
        [IO.File]::WriteAllBytes($path, $elf)
    }
    & (Join-Path $product 'tools/New-DistributionManifest.ps1') -PackageRoot $package -RuntimeIdentifier linux-x64
    $manifest = Get-Content -LiteralPath (Join-Path $package 'Data/distribution.json') -Raw | ConvertFrom-Json
    if ($manifest.SchemaVersion -ne 2 -or $manifest.RuntimeIdentifier -cne 'linux-x64' -or $manifest.Files.Count -ne 5 -or @($manifest.Files | Where-Object UnixMode -ne 493).Count -ne 0) {
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
    # Reusing a nightly Mac payload for a tag must stamp bundle metadata and its
    # inventory entry, while retaining every non-metadata payload byte and mode.
    $macParent = Join-Path $temporary 'mac'
    $macRoot = Join-Path $macParent 'Memoria.FF8.app'
    New-Item -ItemType Directory -Force -Path (Join-Path $macRoot 'Contents/MacOS/Data') | Out-Null
    Copy-Item -LiteralPath (Join-Path $product 'tools/packaging/Info.plist') -Destination (Join-Path $macRoot 'Contents/Info.plist')
    Set-Content -LiteralPath (Join-Path $macRoot 'Contents/MacOS/Data/engine.dll') -Value 'synthetic tested payload' -Encoding utf8NoBOM
    $previousEnvironment = @{}
    foreach ($name in @('GITHUB_ACTIONS', 'GITHUB_SHA', 'GITHUB_REF_TYPE', 'GITHUB_REF_NAME')) {
        $previousEnvironment[$name] = [Environment]::GetEnvironmentVariable($name)
    }
    try {
        $env:GITHUB_ACTIONS = 'true'
        $env:GITHUB_SHA = 'a' * 40
        $env:GITHUB_REF_TYPE = 'branch'
        $env:GITHUB_REF_NAME = 'main'
        & (Join-Path $product 'tools/New-DistributionManifest.ps1') -PackageRoot $macRoot -RuntimeIdentifier osx-x64
    }
    finally {
        foreach ($name in $previousEnvironment.Keys) {
            [Environment]::SetEnvironmentVariable($name, $previousEnvironment[$name])
        }
    }
    $before = Get-Content -LiteralPath (Join-Path $macRoot 'Contents/MacOS/Data/distribution.json') -Raw | ConvertFrom-Json
    [xml]$nightlyInfo = Get-Content -LiteralPath (Join-Path $macRoot 'Contents/Info.plist') -Raw
    if ($before.Version -cne 'nightly' -or $nightlyInfo.SelectSingleNode('/plist/dict/key[text()="CFBundleShortVersionString"]/following-sibling::string[1]').InnerText -cne '0.0.0') {
        throw 'Nightly bundle version generation is inconsistent.'
    }
    $macArchive = Join-Path $temporary 'mac.tar.gz'
    Invoke-Checker @('--validate-mac-bundle-version', (Join-Path $macRoot 'Contents/Info.plist'), 'v1.2.3') -ExpectFailure
    Invoke-Checker @('--create-tar', $macParent, $macArchive)
    foreach ($tag in @('v1.2.3', 'v1.2.3.4')) {
        Invoke-Checker @('--set-version', $macArchive, $tag, $before.BuildId)
        $extracted = Join-Path $temporary $tag
        New-Item -ItemType Directory -Path $extracted | Out-Null
        & tar -xf $macArchive -C $extracted
        if ($LASTEXITCODE -ne 0) {
            throw 'Could not inspect the synthetic versioned Mac archive.'
        }
        $bundle = Join-Path $extracted 'Memoria.FF8.app'
        Invoke-Checker @('--validate-mac-bundle-version', (Join-Path $bundle 'Contents/Info.plist'), $tag)
        $after = Get-Content -LiteralPath (Join-Path $bundle 'Contents/MacOS/Data/distribution.json') -Raw | ConvertFrom-Json
        [xml]$info = Get-Content -LiteralPath (Join-Path $bundle 'Contents/Info.plist') -Raw
        foreach ($key in @('CFBundleShortVersionString', 'CFBundleVersion')) {
            if ($info.SelectSingleNode("/plist/dict/key[text()='$key']/following-sibling::string[1]").InnerText -cne '1.2.3') {
                throw "Incorrect tagged bundle metadata: $key."
            }
        }
        if ($info.SelectSingleNode('/plist/dict/key[text()="MemoriaReleaseVersion"]/following-sibling::string[1]').InnerText -cne $tag -or $after.Version -cne $tag -or $after.BuildId -cne $before.BuildId) {
            throw 'The tagged Mac bundle and distribution identity disagree.'
        }
        foreach ($file in $after.Files) {
            $actual = Get-Item -LiteralPath (Join-Path $bundle $file.Path)
            if ($file.Size -ne $actual.Length -or $file.Sha256 -cne (Get-FileHash -LiteralPath $actual.FullName -Algorithm SHA256).Hash.ToLowerInvariant()) {
                throw 'The tagged Mac inventory hash or size is stale.'
            }
            $original = @($before.Files | Where-Object Path -ceq $file.Path)
            if ($original.Count -ne 1 -or $file.UnixMode -ne $original[0].UnixMode -or ($file.Path -cne 'Contents/Info.plist' -and $file.Sha256 -cne $original[0].Sha256)) {
                throw 'Tag stamping changed a non-metadata Mac payload entry or mode.'
            }
        }
    }
    Write-Output 'Packaging descriptor, tar modes, Linux/Mac source-preserving tag metadata and foreign-native rejection passed.'
}
finally {
    if ([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($temporary)) -ne [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar)) {
        throw 'Test cleanup escaped its dedicated temporary root.'
    }
    Remove-Item -LiteralPath $temporary -Recurse -Force
}
