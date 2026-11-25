[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('vulkan', 'dx12', IgnoreCase = $false)][string]$Backend,
    [Parameter(Mandatory)][ValidateSet('win-x64', 'linux-x64', 'osx-x64', 'osx-arm64', IgnoreCase = $false)][string]$RuntimeIdentifier,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [string]$CacheDirectory = (Join-Path $PSScriptRoot '../artifacts/native-packages'),
    [string]$LockFile = (Join-Path $PSScriptRoot '../vendor/MonoGame.Native.Packages/native-runtime-packages.lock.json'),
    [switch]$VerifyOnly
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = Split-Path -Parent $PSScriptRoot
$repository = 'https://github.com/Albeoris/MonoGame'
$isVulkan = $Backend -eq 'vulkan'
$isMac = $RuntimeIdentifier -like 'osx-*'
if (!$isVulkan -and $RuntimeIdentifier -ne 'win-x64') {
    throw 'The Direct3D 12 native runtime is available only for win-x64.'
}

function Get-Hash([string]$Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Get-TextHash([string]$Text) {
    $normalized = $Text.Replace("`r`n", "`n")
    return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($normalized))).ToLowerInvariant()
}

function Read-PackageLock {
    if (!(Test-Path -LiteralPath $LockFile -PathType Leaf)) {
        throw 'The native runtime package lock is missing. Restore the source checkout or use Build.ps1 -BuildNativeFromSource to reproduce the native libraries.'
    }
    $lock = Get-Content -LiteralPath $LockFile -Raw | ConvertFrom-Json -AsHashtable
    if ($lock -isnot [Collections.IDictionary] -or $lock['schemaVersion'] -ne 1 -or $lock['version'] -cnotmatch '^[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+-memoria\.[1-9][0-9]*$' -or $lock['producerCommit'] -cnotmatch '^[0-9a-f]{40}$' -or $lock['packages'] -isnot [array]) {
        throw 'The native runtime package lock has an invalid identity.'
    }
    $missing = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($key in @('dx12/win-x64', 'vulkan/win-x64', 'vulkan/linux-x64', 'vulkan/osx-x64', 'vulkan/osx-arm64')) {
        [void]$missing.Add($key)
    }
    foreach ($entry in $lock.packages) {
        if ($entry -isnot [Collections.IDictionary] -or $entry['sha256'] -cnotmatch '^[0-9a-f]{64}$' -or !$missing.Remove("$($entry['backend'])/$($entry['runtimeIdentifier'])")) {
            throw 'The native runtime package lock has an invalid or duplicate package pin.'
        }
    }
    if ($missing.Count -ne 0) {
        throw 'The native runtime package lock must cover all five backend and RID combinations.'
    }
    return $lock
}

function Get-Receipt($Entry) {
    $reader = [IO.StreamReader]::new($Entry.Open())
    try {
        return $reader.ReadToEnd() | ConvertFrom-Json -AsHashtable
    }
    finally {
        $reader.Dispose()
    }
}

$packageLock = Read-PackageLock
$pin = $packageLock.packages | Where-Object { $_.backend -ceq $Backend -and $_.runtimeIdentifier -ceq $RuntimeIdentifier }
$version = $packageLock.version
$asset = "Memoria-MonoGame-$version-$Backend-$RuntimeIdentifier.zip"
$cache = Join-Path ([IO.Path]::GetFullPath($CacheDirectory)) $version
$archive = Join-Path $cache $asset
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
$recipeName = if ($isVulkan) { 'MonoGame.Native.Vulkan' } else { 'MonoGame.Native.DX12' }
$recipe = Join-Path $root "vendor/$recipeName"
$sourceLock = Get-Content -LiteralPath (Join-Path $recipe 'native-runtime-lock.json') -Raw | ConvertFrom-Json -AsHashtable
$library = switch ($RuntimeIdentifier) {
    'win-x64' { 'mgruntime.dll' }
    'linux-x64' { 'libmgruntime.so' }
    default { 'libmgruntime.dylib' }
}

function Assert-Receipt($Receipt) {
    $expectedBackend = if ($isVulkan) { 'Vulkan' } else { 'Direct3D12' }
    if ($Receipt -isnot [Collections.IDictionary] -or $Receipt['schemaVersion'] -ne 1 -or $Receipt['runtimeIdentifier'] -cne $RuntimeIdentifier -or $Receipt['backend'] -cne $expectedBackend -or $Receipt['packageVersion'] -cne $version -or $Receipt['producer'] -isnot [Collections.IDictionary] -or $Receipt.producer['repository'] -cne $repository -or $Receipt.producer['commit'] -cne $packageLock.producerCommit) {
        throw 'The native package receipt does not match the locked producer, backend, version and RID.'
    }
    if (!$version.StartsWith($sourceLock.version + '-memoria.', [StringComparison]::Ordinal)) {
        throw 'The native package version does not match the pinned MonoGame source version.'
    }
    $tools = Get-Content -LiteralPath (Join-Path $root '.config/dotnet-tools.json') -Raw | ConvertFrom-Json -AsHashtable
    if ($Receipt['shaderCompiler'] -isnot [Collections.IDictionary] -or $Receipt.shaderCompiler['package'] -cne $sourceLock.shaderCompiler.package -or $Receipt.shaderCompiler['version'] -cne $sourceLock.shaderCompiler.version -or $Receipt.shaderCompiler['profile'] -cne $sourceLock.shaderCompiler.profile -or $tools.tools[$sourceLock.shaderCompiler.package].version -cne $sourceLock.shaderCompiler.version) {
        throw 'The native package stock shader compiler differs from the MonoGame source and product pins.'
    }
    $shaderDirectory = if ($isVulkan) { 'vulkan' } else { 'directx12' }
    $shaderSuffix = if ($isVulkan) { 'vk' } else { 'dx12' }
    $shaders = @('AlphaTestEffect', 'BasicEffect', 'DualTextureEffect', 'EnvironmentMapEffect', 'SkinnedEffect', 'SpriteEffect')
    if ($Receipt['stockShaders'] -isnot [array] -or $Receipt.stockShaders.Count -ne $shaders.Count) {
        throw 'The native package receipt has an incomplete stock shader inventory.'
    }
    foreach ($shader in $shaders) {
        $entry = @($Receipt.stockShaders | Where-Object { $_['path'] -ceq "native/monogame/$shaderDirectory/$shader.$shaderSuffix.mgfxo.h" })
        if ($entry.Count -ne 1 -or $entry[0]['sha256'] -cnotmatch '^[0-9a-f]{64}$') {
            throw "The native package receipt has an invalid stock shader entry for $shader."
        }
    }
    if ($Receipt['exportedApiSha256'] -cnotmatch '^[0-9a-f]{64}$') {
        throw 'The native package receipt lacks its verified native API fingerprint.'
    }
    if ($isVulkan) {
        $referenceKey = if ($isMac) { 'osx' } else { $RuntimeIdentifier }
        $reference = $sourceLock.referencePackages[$referenceKey]
        $referencePackage = $reference.name
        $referenceSha512 = $reference.sha512
        $dependencies = $Receipt['dependencyCommits']
        $patches = @{'BackBufferReadback.patch' = 'patchSha256'; 'DesktopWindow.patch' = 'desktopWindowPatchSha256'; 'FAudioCurveAlignment.patch' = 'faudioPatchSha256'}
        if ($Receipt['upstreamCommit'] -cne $sourceLock.upstream.commit -or $Receipt['recipeHashes'] -isnot [Collections.IDictionary] -or $Receipt.recipeHashes.Count -eq 0 -or $Receipt['toolchain'] -isnot [Collections.IDictionary] -or $Receipt.toolchain['configuration'] -cne 'Release' -or $Receipt.toolchain['cppStandard'] -cne 'c++17') {
            throw 'The Vulkan package receipt has an invalid source and build recipe identity.'
        }
        foreach ($fingerprint in $Receipt.recipeHashes.Values) {
            if ($fingerprint -cnotmatch '^[0-9a-f]{64}$') {
                throw 'The Vulkan package receipt contains an invalid producer recipe fingerprint.'
            }
        }
        foreach ($patch in $patches.Keys) {
            $key = $patches[$patch]
            $sha256 = $sourceLock[$key]
            if ((Get-TextHash ([IO.File]::ReadAllText((Join-Path $recipe $patch)))) -cne $sha256 -or $Receipt[$key] -cne $sha256) {
                throw "The Vulkan package does not match the pinned $patch source patch."
            }
        }
    }
    else {
        $referencePackage = $sourceLock.package
        $referenceSha512 = $sourceLock.referencePackageSha512
        $dependencies = $Receipt['dependencies']
        if ($Receipt['upstream'] -isnot [Collections.IDictionary] -or $Receipt.upstream['commit'] -cne $sourceLock.upstream.commit -or $Receipt.upstream['repository'] -cne $sourceLock.upstream.repository -or $Receipt['version'] -cne $sourceLock.version -or $Receipt['package'] -cne $sourceLock.package -or $Receipt['configuration'] -cne 'Release' -or $Receipt['exportedSymbols'] -ne 140 -or $Receipt['recipeSha256'] -cnotmatch '^[0-9a-f]{64}$' -or $Receipt['buildScriptSha256'] -cnotmatch '^[0-9a-f]{64}$') {
            throw 'The Direct3D 12 package receipt has an invalid source, recipe or ABI identity.'
        }
        if ((Get-TextHash ([IO.File]::ReadAllText((Join-Path $recipe 'RenderTargetMSAA.patch')))) -cne $sourceLock.patchSha256 -or $Receipt['patchSha256'] -cne $sourceLock.patchSha256) {
            throw 'The Direct3D 12 package does not match the pinned RenderTargetMSAA.patch source patch.'
        }
    }
    if ($dependencies -isnot [array] -or $dependencies.Count -ne $sourceLock.dependencies.Count) {
        throw 'The native package receipt has an incomplete source dependency inventory.'
    }
    foreach ($dependency in $sourceLock.dependencies) {
        $match = @($dependencies | Where-Object { $_['path'] -ceq $dependency.path -and $_['commit'] -ceq $dependency.commit -and ($isVulkan -or $_['repository'] -ceq $dependency.repository) })
        if ($match.Count -ne 1) {
            throw "The native package source dependency does not match the pinned revision for $($dependency.path)."
        }
    }
    if ($Receipt['referencePackageSha512'] -cne $referenceSha512) {
        throw 'The native package receipt does not match the pinned MonoGame reference package checksum.'
    }
    [xml]$packages = Get-Content -LiteralPath (Join-Path $root 'Directory.Packages.props') -Raw
    foreach ($package in @('MonoGame.Framework.Native', $referencePackage)) {
        $matches = @($packages.SelectNodes('/Project/ItemGroup/PackageVersion') | Where-Object { $_.GetAttribute('Include') -eq $package })
        if ($matches.Count -ne 1 -or $matches[0].GetAttribute('Version') -cne "[$($sourceLock.version)]") {
            throw "The current $package NuGet pin does not match the native package receipt."
        }
    }
    if ($isMac -and ($Receipt['moltenVK'] -isnot [Collections.IDictionary] -or $Receipt.moltenVK['version'] -cne '1.4.2' -or $Receipt.moltenVK['sha256'] -cne 'f95765a6229cb7b915990a2890ce12ebe36a730b021545d3d52ae69ce4c4024e')) {
        throw 'The macOS native package receipt does not match the pinned MoltenVK release.'
    }
}

function Get-PackageEntries($Zip) {
    $notices = Join-Path $recipe 'licenses'
    $expected = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    [void]$expected.Add($library)
    [void]$expected.Add('native-runtime.json')
    foreach ($notice in Get-ChildItem -LiteralPath $notices -File -Recurse) {
        $relative = [IO.Path]::GetRelativePath($notices, $notice.FullName).Replace('\', '/')
        [void]$expected.Add("licenses/$relative")
    }
    if ($isMac) {
        [void]$expected.Add('licenses/MoltenVK.LICENSE')
    }
    $entries = [Collections.Generic.Dictionary[string, IO.Compression.ZipArchiveEntry]]::new([StringComparer]::Ordinal)
    foreach ($entry in $Zip.Entries) {
        if (!$expected.Contains($entry.FullName) -or !$entries.TryAdd($entry.FullName, $entry)) {
            throw "The native package contains an unexpected or duplicate entry: $($entry.FullName)."
        }
    }
    if ($entries.Count -ne $expected.Count) {
        throw 'The native package is missing a library, receipt or required dependency notice.'
    }
    return ,$entries
}

function Assert-PackageOutput([string]$Directory, $Entries, $Receipt) {
    $outputFiles = @(Get-ChildItem -LiteralPath $Directory -Recurse -Force -File)
    if ($outputFiles.Count -ne $Entries.Count) {
        throw 'The restored native package contains missing or unexpected files.'
    }
    foreach ($file in $outputFiles) {
        $name = [IO.Path]::GetRelativePath($Directory, $file.FullName).Replace('\', '/')
        if (!$Entries.ContainsKey($name)) {
            throw "The restored native package contains an unexpected file: $name."
        }
        $stream = $Entries[$name].Open()
        try {
            $sha256 = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream)).ToLowerInvariant()
        }
        finally {
            $stream.Dispose()
        }
        if ((Get-Hash $file.FullName) -cne $sha256) {
            throw "The restored native package does not match the authenticated archive: $name."
        }
        if ($name.StartsWith('licenses/', [StringComparison]::Ordinal) -and $name -cne 'licenses/MoltenVK.LICENSE') {
            $expectedNotice = [IO.File]::ReadAllText((Join-Path $recipe $name))
            if ((Get-TextHash ([IO.File]::ReadAllText($file.FullName))) -cne (Get-TextHash $expectedNotice)) {
                throw "The native package has a changed dependency notice: $name."
            }
        }
    }
    $libraryPath = Join-Path $Directory $library
    $runtimeHash = Get-Hash $libraryPath
    if ($isVulkan) {
        if ($Receipt['outputSha256'] -cne $runtimeHash) {
            throw 'The Vulkan native library does not match its build receipt hash.'
        }
    }
    elseif ($Receipt['files'] -isnot [array] -or $Receipt.files.Count -ne 1 -or $Receipt.files[0]['path'] -cne $library -or $Receipt.files[0]['sha256'] -cne $runtimeHash) {
        throw 'The Direct3D 12 native library does not match its build receipt hash.'
    }
}

function Open-LockedArchive {
    if (!(Test-Path -LiteralPath $archive -PathType Leaf)) {
        return $null
    }
    $stream = [IO.File]::OpenRead($archive)
    $verified = $false
    try {
        $sha256 = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream)).ToLowerInvariant()
        if ($sha256 -cne $pin.sha256) {
            return $null
        }
        $stream.Position = 0
        $verified = $true
        return $stream
    }
    finally {
        if (!$verified) {
            $stream.Dispose()
        }
    }
}

$archiveStream = Open-LockedArchive
if (!$VerifyOnly -and !$archiveStream) {
    [IO.Directory]::CreateDirectory($cache) | Out-Null
    $pending = "$archive.$([Guid]::NewGuid().ToString('N')).tmp"
    try {
        Invoke-WebRequest -Uri "$repository/releases/download/memoria-native-$version/$asset" -OutFile $pending
        if ((Get-Hash $pending) -cne $pin.sha256) {
            throw "The downloaded native package SHA-256 differs from the lock for $asset."
        }
        try {
            [IO.File]::Move($pending, $archive, $true)
        }
        catch [IO.IOException] {
            # Another restore may have published the same version while we downloaded it.
            $archiveStream = Open-LockedArchive
            if (!$archiveStream) {
                throw
            }
        }
    }
    finally {
        [IO.File]::Delete($pending)
    }
    if (!$archiveStream) {
        $archiveStream = Open-LockedArchive
    }
}
if (!$archiveStream) {
    throw "The native package archive is missing or its SHA-256 differs from the lock: $archive."
}
try {
    $zip = [IO.Compression.ZipArchive]::new($archiveStream, [IO.Compression.ZipArchiveMode]::Read, $true)
    try {
        $entries = Get-PackageEntries $zip
        $receipt = Get-Receipt $entries['native-runtime.json']
        Assert-Receipt $receipt
        if ($VerifyOnly -or [IO.Directory]::Exists($OutputDirectory)) {
            Assert-PackageOutput $OutputDirectory $entries $receipt
        }
        else {
            $pendingOutput = "$OutputDirectory.$([Guid]::NewGuid().ToString('N')).tmp"
            [IO.Directory]::CreateDirectory($pendingOutput) | Out-Null
            try {
                foreach ($entry in $entries.Values) {
                    $path = Join-Path $pendingOutput $entry.FullName
                    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($path)) | Out-Null
                    [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $path)
                }
                if (!$IsWindows) {
                    [IO.File]::SetUnixFileMode((Join-Path $pendingOutput $library), [IO.UnixFileMode]493)
                }
                Assert-PackageOutput $pendingOutput $entries $receipt
                try {
                    [IO.Directory]::Move($pendingOutput, $OutputDirectory)
                }
                catch [IO.IOException] {
                    if (![IO.Directory]::Exists($OutputDirectory)) {
                        throw
                    }
                    Assert-PackageOutput $OutputDirectory $entries $receipt
                }
            }
            finally {
                if ([IO.Directory]::Exists($pendingOutput)) {
                    [IO.Directory]::Delete($pendingOutput, $true)
                }
            }
        }
    }
    finally {
        $zip.Dispose()
    }
}
finally {
    $archiveStream.Dispose()
}
Write-Host "Verified $asset against the native package lock, receipt and dependency notices."
