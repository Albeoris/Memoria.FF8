#Requires -Version 7.4
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
Set-StrictMode -Version Latest

$product = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$source = 'https://api.nuget.org/v3/index.json'
$runtimes = @('win-x64', 'linux-x64', 'osx-x64', 'osx-arm64')
$packages = [Collections.Generic.SortedDictionary[string, object]]::new([StringComparer]::Ordinal)
$packageFolders = [Collections.Generic.HashSet[string]]::new()
$centralVersions = [Collections.Generic.Dictionary[string, string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($file in 'Directory.Packages.props', 'Directory.TransitivePackages.props') {
    [xml]$props = Get-Content -LiteralPath (Join-Path $product $file) -Raw
    foreach ($pin in $props.SelectNodes('//PackageVersion')) {
        if ($pin.Version -cnotmatch '^\[([^,\[\]]+)\]$' -or !$centralVersions.TryAdd($pin.Include, $Matches[1])) {
            throw "Expected one exact central version for $($pin.Include) in $file."
        }
    }
}

function Read-Json {
    param([string]$Path)

    Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json -AsHashtable
}

function Add-Package {
    param(
        [string]$Id,
        [string]$Version,
        [string]$Context,
        [string]$ContentHash,
        [string]$ArchiveHash
    )

    $id = $Id.ToLowerInvariant()
    if (![NuGet.Packaging.PackageIdValidator]::IsValidPackageId($id)) {
        throw "Invalid NuGet package ID '$Id' in $Context."
    }
    $version = [NuGet.Versioning.NuGetVersion]::Parse($Version).ToNormalizedString().ToLowerInvariant()
    if ($packages.ContainsKey($id)) {
        $entry = $packages[$id]
        if ($entry.Version -cne $version) {
            throw "$id resolves to $($entry.Version) in $($entry.Context) and $version in $Context. Pin one version for all release restores."
        }
        if ($ContentHash -and $entry.ContentHash -and $entry.ContentHash -cne $ContentHash) {
            throw "$id/$version has different NuGet content hashes in $($entry.Context) and $Context."
        }
        if ($ArchiveHash -and $entry.ArchiveHash -and $entry.ArchiveHash -cne $ArchiveHash) {
            throw "$id/$version has different archive SHA512 hashes in $($entry.Context) and $Context."
        }
        if ($ContentHash) {
            $entry.ContentHash = $ContentHash
        }
        if ($ArchiveHash) {
            $entry.ArchiveHash = $ArchiveHash
        }
        return
    }
    $packages.Add($id, @{
        Version = $version
        Context = $Context
        ContentHash = $ContentHash
        ArchiveHash = $ArchiveHash
    })
}

function Add-RestorePackages {
    param(
        [string]$Profile,
        [string]$Project,
        [string[]]$Properties
    )

    $artifacts = Join-Path $temporary $Profile
    Write-Host "Restoring $Profile"
    & dotnet restore $Project --artifacts-path $artifacts --configfile (Join-Path $product 'NuGet.Config') --source $source -p:Configuration=Release @Properties
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet restore failed for $Profile (exit code $LASTEXITCODE)."
    }

    $assetsFiles = @(Get-ChildItem -LiteralPath $artifacts -Filter 'project.assets.json' -File -Recurse)
    if ($assetsFiles.Count -eq 0) {
        throw "No NuGet assets were produced for $Profile."
    }
    foreach ($file in $assetsFiles) {
        $assets = Read-Json $file.FullName
        $context = "$Profile/$($file.Directory.Name)"
        foreach ($folder in $assets.packageFolders.Keys) {
            [void]$packageFolders.Add($folder)
        }
        foreach ($library in $assets.libraries.GetEnumerator()) {
            if ($library.Value.type -eq 'package') {
                $id, $version = $library.Key.Split('/')
                # The SDK pins this implicit reference and rejects an accompanying PackageVersion.
                if ($id -ne 'Microsoft.NET.ILLink.Tasks' -and (!$centralVersions.ContainsKey($id) -or $centralVersions[$id] -ne $version)) {
                    throw "Pin $id to the resolved version $version before refreshing the lock ($context)."
                }
                Add-Package $id $version $context -ContentHash $library.Value.sha512
            }
        }
        foreach ($framework in $assets.project.frameworks.Values) {
            foreach ($dependency in $framework['downloadDependencies']) {
                if ($dependency.version -cnotmatch '^\[([^,]+),\s*([^\]]+)\]$' -or $Matches[1] -cne $Matches[2]) {
                    throw "Expected an exact version for $($dependency.name) in $context, got $($dependency.version)."
                }
                Add-Package $dependency.name $Matches[1] $context
            }
        }
    }
}

function Get-ArchiveHash {
    param(
        [string]$Id,
        [hashtable]$Entry
    )

    $version = $Entry.Version
    $name = "$id.$version.nupkg"
    $archive = $null
    foreach ($folder in $packageFolders) {
        $candidate = Join-Path $folder "$id/$version/$name"
        $metadataPath = Join-Path (Split-Path -Parent $candidate) '.nupkg.metadata'
        if (!(Test-Path -LiteralPath $candidate -PathType Leaf) -or !(Test-Path -LiteralPath $metadataPath -PathType Leaf)) {
            continue
        }

        $metadata = Read-Json $metadataPath
        if ($metadata['source'] -cne $source) {
            continue
        }
        if ($Entry.ContentHash -and $metadata['contentHash'] -cne $Entry.ContentHash) {
            throw "Cached NuGet content hash differs from the restored graph for $id/$version."
        }
        $archive = $candidate
        break
    }

    if ($archive) {
        $hash = (Get-FileHash -Algorithm SHA512 -LiteralPath $archive).Hash
        if (Test-Path -LiteralPath "$archive.sha512" -PathType Leaf) {
            $cachedHash = (Get-Content -LiteralPath "$archive.sha512" -Raw).Trim()
            if ([Convert]::ToBase64String([Convert]::FromHexString($hash)) -cne $cachedHash) {
                throw "Cached archive SHA512 has changed for $id/$version."
            }
        }
    }
    else {
        $archive = Join-Path $temporary $name
        try {
            Invoke-WebRequest -Uri "https://api.nuget.org/v3-flatcontainer/$id/$version/$name" -OutFile $archive
            $hash = (Get-FileHash -Algorithm SHA512 -LiteralPath $archive).Hash
            if ($Entry.ContentHash) {
                $reader = [NuGet.Packaging.PackageArchiveReader]::new($archive)
                try {
                    if ($reader.GetContentHash([Threading.CancellationToken]::None) -cne $Entry.ContentHash) {
                        throw "Downloaded NuGet content hash differs from the restored graph for $id/$version."
                    }
                }
                finally {
                    $reader.Dispose()
                }
            }
        }
        finally {
            if (Test-Path -LiteralPath $archive -PathType Leaf) {
                Remove-Item -LiteralPath $archive
            }
        }
    }
    if ($Entry.ArchiveHash -and [Convert]::ToBase64String([Convert]::FromHexString($hash)) -cne $Entry.ArchiveHash) {
        throw "Archive SHA512 differs from the native runtime lock for $id/$version."
    }
    return $hash.ToLowerInvariant()
}

$temporary = [IO.Directory]::CreateTempSubdirectory('memoria-nuget-lock-').FullName
Push-Location -LiteralPath $product
try {
    $sdkVersion = (Read-Json 'global.json').sdk.version
    $selectedVersion = & dotnet --version
    if ($LASTEXITCODE -ne 0 -or $selectedVersion -cne $sdkVersion) {
        throw "NuGet lock refresh requires .NET SDK $sdkVersion from global.json."
    }
    $sdkLine = & dotnet --list-sdks | Select-String -Pattern ('^' + [Regex]::Escape($sdkVersion) + ' \[(.+)\]$') | Select-Object -First 1
    if (!$sdkLine) {
        throw "Could not locate .NET SDK $sdkVersion."
    }
    $sdkPath = Join-Path $sdkLine.Matches[0].Groups[1].Value $sdkVersion
    foreach ($assembly in 'NuGet.Common', 'NuGet.Versioning', 'NuGet.Configuration', 'NuGet.Packaging') {
        Add-Type -Path (Join-Path $sdkPath "$assembly.dll")
    }

    foreach ($rid in $runtimes) {
        Add-RestorePackages "$rid-vulkan" 'Memoria.FF8.Native.slnx' @(
            "-p:MemoriaBuildRuntimeIdentifier=$rid"
            "-p:MemoriaTargetRuntime=$rid"
            '-p:MemoriaGraphicsBackend=Vulkan'
            '-p:TargetFramework=net10.0'
            '-p:SelfContained=true'
            '-p:SkipMusicHost=true'
            '-p:PublishTrimmed=false'
            '-p:ShouldUnsetParentConfigurationAndPlatform=false'
        )
        Add-RestorePackages "$rid-packaging" 'tools/Memoria.FF8.Distribution.Check/Memoria.FF8.Distribution.Check.csproj' @("-p:MemoriaTargetRuntime=$rid")
    }
    foreach ($backend in 'Direct3D11', 'Direct3D12') {
        Add-RestorePackages "win-x64-$backend" 'src/Memoria.FF8.App/Memoria.FF8.App.csproj' @(
            '-r'
            'win-x64'
            "-p:MemoriaGraphicsBackend=$backend"
            '-p:SelfContained=true'
            '-p:SkipMusicHost=true'
            '-p:PublishTrimmed=false'
            '-p:EnableWindowsTargeting=true'
        )
    }

    # The SDK's locally installed apphost does not appear as a NuGet download dependency.
    $appHostVersions = @($packages.Keys | Where-Object { $_ -like 'microsoft.netcore.app.host.*' } | ForEach-Object { $packages[$_].Version } | Select-Object -Unique)
    if ($appHostVersions.Count -ne 1) {
        throw "Expected a single SDK apphost version across the release RIDs, got $($appHostVersions -join ', ')."
    }
    foreach ($rid in $runtimes) {
        Add-Package "microsoft.netcore.app.host.$rid" $appHostVersions[0] "SDK $sdkVersion"
    }

    foreach ($manifestPath in '.config/dotnet-tools.json', 'src/Memoria.FF8.TitleScene/.config/dotnet-tools.json') {
        $manifest = Read-Json $manifestPath
        foreach ($tool in $manifest.tools.GetEnumerator()) {
            Add-Package $tool.Key $tool.Value.version $manifestPath
        }
    }

    $dx12Path = 'vendor/MonoGame.Native.DX12/native-runtime-lock.json'
    $dx12 = Read-Json $dx12Path
    Add-Package $dx12.package $dx12.version $dx12Path -ArchiveHash $dx12.referencePackageSha512
    Add-Package $dx12.shaderCompiler.package $dx12.shaderCompiler.version $dx12Path
    $vulkanPath = 'vendor/MonoGame.Native.Vulkan/native-runtime-lock.json'
    $vulkan = Read-Json $vulkanPath
    foreach ($reference in $vulkan.referencePackages.Values) {
        Add-Package $reference.name $vulkan.version $vulkanPath -ArchiveHash $reference.sha512
    }
    Add-Package $vulkan.shaderCompiler.package $vulkan.shaderCompiler.version $vulkanPath

    $entries = foreach ($entry in $packages.GetEnumerator()) {
        [ordered]@{
            id = $entry.Key
            version = $entry.Value.Version
            sha512 = Get-ArchiveHash $entry.Key $entry.Value
        }
    }
    $lock = [ordered]@{ sdkVersion = $sdkVersion; packages = @($entries) }
    $destination = Join-Path $product '.github/nuget/packages.lock.json'
    New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
    $json = ($lock | ConvertTo-Json -Depth 4).Replace("`r`n", "`n")
    [IO.File]::WriteAllText($destination, $json + "`n")
    Write-Host "Wrote $($packages.Count) locked packages to $destination."
}
finally {
    Pop-Location
    Remove-Item -LiteralPath $temporary -Recurse -Force
}
