[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$product = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$restore = Join-Path $product 'tools/Restore-NativeRuntimePackage.ps1'
$testRoot = Join-Path ([IO.Path]::GetTempPath()) ('memoria-native-packages-tests-' + [Guid]::NewGuid().ToString('N'))
$cache = Join-Path $testRoot 'cache'
$lockFile = Join-Path $testRoot 'lock.json'
$producerCommit = 'a' * 40
$version = '3.8.5.1-memoria.1'
$fakeSha256 = 'b' * 64
$matrix = @(
    @{ backend = 'vulkan'; runtimeIdentifier = 'win-x64'; sha256 = $fakeSha256 },
    @{ backend = 'vulkan'; runtimeIdentifier = 'linux-x64'; sha256 = $fakeSha256 },
    @{ backend = 'vulkan'; runtimeIdentifier = 'osx-x64'; sha256 = $fakeSha256 },
    @{ backend = 'vulkan'; runtimeIdentifier = 'osx-arm64'; sha256 = $fakeSha256 },
    @{ backend = 'dx12'; runtimeIdentifier = 'win-x64'; sha256 = $fakeSha256 }
)
$downloads = [Collections.Generic.List[string]]::new()
$assets = @{}
New-Item -ItemType Directory -Path $testRoot | Out-Null

function Invoke-WebRequest([string]$Uri, [string]$OutFile) {
    if (!$assets.ContainsKey($Uri)) {
        throw "Unexpected network request: $Uri"
    }
    $downloads.Add($Uri)
    Copy-Item -LiteralPath $assets[$Uri] -Destination $OutFile
}

function Get-Hash([string]$Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Write-Lock {
    @{ schemaVersion = 1; version = $version; producerCommit = $producerCommit; packages = $matrix } | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $lockFile
}

function Assert-Fails([scriptblock]$Command, [string]$Expected) {
    try {
        & $Command
    }
    catch {
        if ($_ -notmatch $Expected) {
            throw
        }
        return
    }
    throw "Expected package validation to fail with $Expected."
}

function New-Fixture($Pin) {
    $rid = $Pin.runtimeIdentifier
    $backend = $Pin.backend
    $vulkan = $backend -eq 'vulkan'
    $mac = $rid -like 'osx-*'
    $recipeName = if ($vulkan) { 'MonoGame.Native.Vulkan' } else { 'MonoGame.Native.DX12' }
    $recipe = Join-Path $product "vendor/$recipeName"
    $source = Get-Content -LiteralPath (Join-Path $recipe 'native-runtime-lock.json') -Raw | ConvertFrom-Json -AsHashtable
    $directory = Join-Path $testRoot "$backend-$rid-fixture"
    [IO.Directory]::CreateDirectory($directory) | Out-Null
    Copy-Item -LiteralPath (Join-Path $recipe 'licenses') -Destination $directory -Recurse
    foreach ($notice in Get-ChildItem -LiteralPath (Join-Path $directory 'licenses') -File -Recurse) {
        [IO.File]::WriteAllText($notice.FullName, [IO.File]::ReadAllText($notice.FullName).Replace("`r`n", "`n"))
    }
    $library = switch ($rid) {
        'win-x64' { 'mgruntime.dll' }
        'linux-x64' { 'libmgruntime.so' }
        default { 'libmgruntime.dylib' }
    }
    $libraryPath = Join-Path $directory $library
    # The distribution checker separately verifies real compiled images. This fixture checks package identity and integrity.
    Set-Content -LiteralPath $libraryPath "Synthetic $backend library for $rid"
    $shaderDirectory = if ($vulkan) { 'vulkan' } else { 'directx12' }
    $shaderSuffix = if ($vulkan) { 'vk' } else { 'dx12' }
    $receipt = @{
        schemaVersion = 1
        runtimeIdentifier = $rid
        backend = if ($vulkan) { 'Vulkan' } else { 'Direct3D12' }
        producer = @{ repository = 'https://github.com/Albeoris/MonoGame'; commit = $producerCommit }
        packageVersion = $version
        shaderCompiler = $source.shaderCompiler
        stockShaders = @('AlphaTestEffect', 'BasicEffect', 'DualTextureEffect', 'EnvironmentMapEffect', 'SkinnedEffect', 'SpriteEffect' | ForEach-Object {
            @{ path = "native/monogame/$shaderDirectory/$_.$shaderSuffix.mgfxo.h"; sha256 = $fakeSha256 }
        })
        exportedApiSha256 = $fakeSha256
    }
    if ($vulkan) {
        $referenceKey = if ($mac) { 'osx' } else { $rid }
        $receipt.upstreamCommit = $source.upstream.commit
        $receipt.referencePackageSha512 = $source.referencePackages[$referenceKey].sha512
        $receipt.dependencyCommits = $source.dependencies
        $receipt.patchSha256 = $source.patchSha256
        $receipt.desktopWindowPatchSha256 = $source.desktopWindowPatchSha256
        $receipt.faudioPatchSha256 = $source.faudioPatchSha256
        $receipt.recipeHashes = @{ 'vulkan/build.sh' = $fakeSha256; 'vulkan/FAudioCurveAlignment.patch' = $source.faudioPatchSha256 }
        $receipt.toolchain = @{ configuration = 'Release'; cppStandard = 'c++17' }
        $receipt.outputSha256 = Get-Hash $libraryPath
    }
    else {
        $receipt.upstream = $source.upstream
        $receipt.referencePackageSha512 = $source.referencePackageSha512
        $receipt.dependencies = $source.dependencies
        $receipt.package = $source.package
        $receipt.version = $source.version
        $receipt.configuration = 'Release'
        $receipt.patchSha256 = $source.patchSha256
        $receipt.recipeSha256 = $fakeSha256
        $receipt.buildScriptSha256 = $fakeSha256
        $receipt.exportedSymbols = 140
        $receipt.files = @(@{ path = $library; sha256 = Get-Hash $libraryPath })
    }
    if ($mac) {
        $receipt.moltenVK = @{ version = '1.4.2'; sha256 = 'f95765a6229cb7b915990a2890ce12ebe36a730b021545d3d52ae69ce4c4024e' }
        Set-Content -LiteralPath (Join-Path $directory 'licenses/MoltenVK.LICENSE') 'Synthetic MoltenVK notice'
    }
    $assetName = "Memoria-MonoGame-$version-$backend-$rid.zip"
    $uri = "https://github.com/Albeoris/MonoGame/releases/download/memoria-native-$version/$assetName"
    return @{
        pin = $Pin
        receipt = $receipt
        directory = $directory
        library = $library
        output = Join-Path $testRoot "$backend-$rid-output"
        asset = Join-Path $testRoot $assetName
        cachedAsset = Join-Path $cache "$version/$assetName"
        uri = $uri
    }
}

function Publish-Fixture($Fixture) {
    $Fixture.receipt | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $Fixture.directory 'native-runtime.json')
    [IO.File]::Delete($Fixture.asset)
    [IO.Compression.ZipFile]::CreateFromDirectory($Fixture.directory, $Fixture.asset)
    $assets[$Fixture.uri] = $Fixture.asset
    $Fixture.pin.sha256 = Get-Hash $Fixture.asset
    Write-Lock
}

function Restore-Fixture($Fixture, [string]$Output, [switch]$VerifyOnly) {
    & $restore -Backend $Fixture.pin.backend -RuntimeIdentifier $Fixture.pin.runtimeIdentifier -OutputDirectory $Output -CacheDirectory $cache -LockFile $lockFile -VerifyOnly:$VerifyOnly 6>$null
}

function Test-BuildScopeSelection {
    $scriptRoot = Join-Path $testRoot 'build-input'
    $tools = Join-Path $scriptRoot 'tools'
    $artifacts = Join-Path $scriptRoot 'artifacts'
    $report = Join-Path $scriptRoot 'tests/Shared'
    New-Item -ItemType Directory -Path $tools, $report -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $product 'Build.ps1') -Destination $scriptRoot
    'param([string[]]$Projects)' | Set-Content -LiteralPath (Join-Path $report 'Report-TestResources.ps1')
    @'
param($Backend, $RuntimeIdentifier, $OutputDirectory, $CacheDirectory)
if ($Backend -cne 'vulkan' -or $RuntimeIdentifier -cne 'linux-x64' -or $CacheDirectory -cne $env:MEMORIA_NATIVE_PACKAGE_CACHE) {
    throw 'Portable packaging selected the wrong package or archive cache.'
}
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
Add-Content -LiteralPath (Join-Path $PSScriptRoot 'calls.txt') 'package'
'@ | Set-Content -LiteralPath (Join-Path $tools 'Restore-NativeRuntimePackage.ps1')
    @'
param($RuntimeIdentifier, $OutputDirectory, $Distro)
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
Add-Content -LiteralPath (Join-Path $PSScriptRoot 'calls.txt') 'source'
'@ | Set-Content -LiteralPath (Join-Path $tools 'Build-NativeVulkan.ps1')
    $dotnetCalls = [Collections.Generic.List[object]]::new()
    function dotnet {
        $dotnetCalls.Add([string[]]$args)
        foreach ($argument in $args) {
            if ($argument -like '-p:MemoriaNativeVulkanDirectory=*' -and ![IO.Directory]::Exists($argument.Split('=', 2)[1])) {
                throw 'The portable graph received an unprepared native runtime directory.'
            }
        }
        $global:LASTEXITCODE = 0
    }
    $build = Join-Path $scriptRoot 'Build.ps1'
    $selection = Join-Path $tools 'calls.txt'
    $previousCache = $env:MEMORIA_NATIVE_PACKAGE_CACHE
    $previousFeed = $env:MEMORIA_NUGET_FEED
    try {
        $env:MEMORIA_NATIVE_PACKAGE_CACHE = Join-Path $scriptRoot 'offline-native'
        $env:MEMORIA_NUGET_FEED = Join-Path $scriptRoot 'offline-nuget'
        & $build -Scope Portable -RuntimeIdentifier linux-x64 -Test 6>$null
        if ((Get-Content -LiteralPath $selection -Raw).Trim() -cne 'package' -or $dotnetCalls.Count -ne 7 -or $dotnetCalls[0] -cnotcontains '-p:MemoriaNativePackages=true' -or $dotnetCalls[0] -cnotcontains "-p:MemoriaNativePackagesCache=$env:MEMORIA_NATIVE_PACKAGE_CACHE") {
            throw 'Release portable tests did not run against the verified Vulkan package.'
        }
        if (@(Get-ChildItem -LiteralPath $artifacts -Directory -Filter 'native-packages-work-*').Count -ne 0) {
            throw 'Portable packaging left an extracted native work directory.'
        }
        & $build -Scope Launcher -RuntimeIdentifier linux-x64 6>$null
        if ((Get-Content -LiteralPath $selection -Raw).Trim() -cne 'package' -or $dotnetCalls[7][0] -cne 'publish') {
            throw 'Launcher-only publishing unexpectedly prepared native graphics.'
        }
        & $build -Scope Portable -RuntimeIdentifier linux-x64 -Configuration Debug 6>$null
        & $build -Scope Portable -RuntimeIdentifier linux-x64 -BuildNativeFromSource 6>$null
        if ((@(Get-Content -LiteralPath $selection) -join ',') -cne 'package,source,source' -or $dotnetCalls[8] -cnotcontains '-p:MemoriaNativeVulkanPrepared=true' -or $dotnetCalls[9] -ccontains '-p:MemoriaNativePackages=true') {
            throw 'Debug and explicit source portable builds must prepare the historical recipe.'
        }
    }
    finally {
        $env:MEMORIA_NATIVE_PACKAGE_CACHE = $previousCache
        $env:MEMORIA_NUGET_FEED = $previousFeed
    }
}

try {
    foreach ($pin in $matrix) {
        $fixture = New-Fixture $pin
        Publish-Fixture $fixture
        $before = $downloads.Count
        Restore-Fixture $fixture $fixture.output
        Restore-Fixture $fixture $fixture.output
        Restore-Fixture $fixture $fixture.output -VerifyOnly
        if ($downloads.Count -ne $before + 1 -or !(Test-Path -LiteralPath (Join-Path $fixture.output $fixture.library) -PathType Leaf)) {
            throw 'Warm package verification must reuse the locked download and output.'
        }
        if ($pin.backend -eq 'vulkan' -and $pin.runtimeIdentifier -eq 'linux-x64') {
            $linuxFixture = $fixture
        }
    }

    # The final fixture is DX12. Earlier fixtures exercise all Vulkan RIDs.
    $before = $downloads.Count
    Add-Content -LiteralPath $fixture.cachedAsset 'Corrupted cache'
    Assert-Fails { Restore-Fixture $fixture $fixture.output -VerifyOnly } 'SHA-256 differs from the lock'
    Restore-Fixture $fixture $fixture.output
    if ($downloads.Count -ne $before + 1) {
        throw 'A stale package cache entry must be repaired from the locked release only during restore.'
    }
    Add-Content -LiteralPath (Join-Path $fixture.output $fixture.library) 'Corrupted output'
    Assert-Fails { Restore-Fixture $fixture $fixture.output -VerifyOnly } 'does not match the authenticated archive'

    $invalidOutput = Join-Path $testRoot 'invalid-output'
    $fixture.receipt.backend = 'Vulkan'
    Publish-Fixture $fixture
    Assert-Fails { Restore-Fixture $fixture $invalidOutput } 'locked producer, backend, version and RID'
    $fixture.receipt.backend = 'Direct3D12'
    $fixture.receipt.stockShaders[0].sha256 = 'invalid'
    Publish-Fixture $fixture
    Assert-Fails { Restore-Fixture $fixture $invalidOutput } 'invalid stock shader entry'
    $fixture.receipt.stockShaders[0].sha256 = $fakeSha256
    $linuxFixture.receipt.desktopWindowPatchSha256 = $fakeSha256
    Publish-Fixture $linuxFixture
    Assert-Fails { Restore-Fixture $linuxFixture $invalidOutput } 'does not match the pinned DesktopWindow.patch source patch'

    $fixture.receipt.files[0].sha256 = '0' * 64
    Publish-Fixture $fixture
    Assert-Fails { Restore-Fixture $fixture $invalidOutput } 'library does not match its build receipt hash'
    $fixture.receipt.files[0].sha256 = Get-Hash (Join-Path $fixture.directory $fixture.library)
    Add-Content -LiteralPath (Join-Path $fixture.directory 'licenses/MonoGame-LICENSE.txt') 'Corrupted notice'
    Publish-Fixture $fixture
    Assert-Fails { Restore-Fixture $fixture $invalidOutput } 'changed dependency notice'
    Set-Content -LiteralPath (Join-Path $fixture.directory 'unexpected.txt') 'Unexpected archive entry'
    Publish-Fixture $fixture
    Assert-Fails { Restore-Fixture $fixture $invalidOutput } 'unexpected or duplicate entry'
    $fixture.pin.sha256 = '0' * 64
    Write-Lock
    Assert-Fails { Restore-Fixture $fixture $invalidOutput } 'downloaded native package SHA-256 differs from the lock'
    if ([IO.Directory]::Exists($invalidOutput) -or @(Get-ChildItem -LiteralPath $testRoot -Recurse -Filter '*.tmp').Count -ne 0) {
        throw 'A rejected package must leave no output or pending download.'
    }
    Test-BuildScopeSelection
    Write-Host 'Native package restoration and verification contracts passed.'
}
finally {
    [IO.Directory]::Delete($testRoot, $true)
}
