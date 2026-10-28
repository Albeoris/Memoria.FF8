[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [ValidateSet('Distribution', 'Portable', 'Launcher')]
    [string]$Scope = 'Distribution',
    [ValidateSet('', 'win-x64', 'linux-x64', 'osx-x64', 'osx-arm64')]
    [string]$RuntimeIdentifier = '',
    [switch]$Test
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if (!$RuntimeIdentifier) {
    $RuntimeIdentifier = if ([OperatingSystem]::IsWindows()) { 'win-x64' } elseif ([OperatingSystem]::IsLinux()) { 'linux-x64' } elseif ([OperatingSystem]::IsMacOS()) { if ([Runtime.InteropServices.RuntimeInformation]::OSArchitecture -eq 'Arm64') { 'osx-arm64' } else { 'osx-x64' } } else { throw 'Unsupported desktop host.' }
}

function Invoke-DotNet {
    param([string[]]$Arguments)

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet failed with exit code $LASTEXITCODE."
    }
}

$root = $PSScriptRoot
if ($Scope -ne 'Distribution') {
    Push-Location $root
    try {
        if ($Scope -eq 'Portable') {
            Invoke-DotNet @('build', 'Memoria.FF8.Portable.slnx', '-c', $Configuration, '-p:TargetFramework=net10.0', '-m:1')
            if ($Test) {
                foreach ($project in @('Memoria.FF8.Platform.Tests', 'Memoria.FF8.Bootstrap.Tests', 'Memoria.FF8.Audio.Tests', 'Memoria.FF8.Presentation.Tests', 'Memoria.Launcher.Tests', 'Memoria.FF8.LinuxTesting.Tests')) {
                    Invoke-DotNet @('test', '--project', "tests/$project", '-c', $Configuration, '--no-progress')
                }
                Invoke-DotNet @('test', '--project', 'tests/Memoria.FF8.Runtime.Tests', '-c', $Configuration, '--no-progress', '--filter-class', '*SteamAchievementBackendTests')
            }
        }
        else {
            $destination = Join-Path $root "artifacts/launcher/$RuntimeIdentifier"
            Invoke-DotNet @('publish', 'src/Memoria.Launcher/Memoria.Launcher.csproj', '-c', $Configuration, '-r', $RuntimeIdentifier, '--self-contained', 'true', '-p:PublishTrimmed=false', '-o', $destination)
            if ($Test) {
                Invoke-DotNet @('test', '--project', 'tests/Memoria.Launcher.Tests', '-c', $Configuration, '--no-progress')
            }
            Write-Host "Launcher-only payload (game host supplied separately): $destination"
        }
    }
    finally {
        Pop-Location
    }
    return
}
$artifacts = Join-Path $root 'artifacts'
$package = Join-Path $artifacts ('package/' + $RuntimeIdentifier)
$staging = Join-Path $artifacts ('staging-' + [Guid]::NewGuid().ToString('N'))
$payload = $staging
if ($RuntimeIdentifier -like 'osx-*') {
    $payload = Join-Path $staging 'Memoria.FF8.app/Contents/MacOS'
}
$data = Join-Path $payload 'Data'
$extension = if ($RuntimeIdentifier -eq 'win-x64') { '.exe' } else { '' }
$executable = 'Memoria.FF8' + $extension
$launcherName = 'Memoria.Launcher' + $extension
$backends = if ($RuntimeIdentifier -eq 'win-x64') { @(@{ Name = 'Direct3D11'; Directory = 'dx11' }, @{ Name = 'Vulkan'; Directory = 'vulkan' }, @{ Name = 'Direct3D12'; Directory = 'dx12' }) } else { @(@{ Name = 'Vulkan'; Directory = 'vulkan' }) }
$hostAssets = @()
$launcher = Join-Path $staging 'launcher'
$modDirectories = @('Tonberry', 'Demaster', 'Junction')
New-Item -ItemType Directory -Force -Path $data, $launcher | Out-Null
foreach ($directory in $modDirectories) {
    New-Item -ItemType Directory -Path (Join-Path $payload "Mods/$directory") | Out-Null
}

$bundledMods = Join-Path $root 'Mods/Memoria'
$bundledModFiles = @(Get-ChildItem -LiteralPath $bundledMods -File -Recurse)
$expectedModFiles = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($file in $bundledModFiles) {
    $relative = 'Data/BundledMods/Memoria/' + [IO.Path]::GetRelativePath($bundledMods, $file.FullName).Replace('\', '/')
    $destination = Join-Path $payload $relative
    New-Item -ItemType Directory -Force -Path ([IO.Path]::GetDirectoryName($destination)) | Out-Null
    Copy-Item -LiteralPath $file.FullName -Destination $destination
    [void]$expectedModFiles.Add($relative)
}

Push-Location $root
try {
    & (Join-Path $root 'tools/Update-Shaders.ps1') -Check
    $nativeVulkan = Join-Path $artifacts 'native-vulkan/linux-x64'
    $nativeVulkanArguments = @()
    if ($RuntimeIdentifier -eq 'linux-x64') {
        & (Join-Path $root 'tools/Build-NativeVulkan.ps1') -RuntimeIdentifier $RuntimeIdentifier -OutputDirectory $nativeVulkan -Distro $env:MemoriaNativeVulkanDistro
        $nativeVulkanArguments = @(('-p:MemoriaNativeVulkanDirectory=' + $nativeVulkan), '-p:MemoriaNativeVulkanPrepared=true')
    }
    Invoke-DotNet (@('build', 'Memoria.FF8.Native.slnx', '-c', $Configuration, ('-p:MemoriaTargetRuntime=' + $RuntimeIdentifier), '-p:MemoriaGraphicsBackend=Vulkan', '-p:TargetFramework=net10.0', '-p:SkipMusicHost=true', '--artifacts-path', (Join-Path $artifacts ('build/' + $RuntimeIdentifier + '/graph')), '-m:1') + $nativeVulkanArguments)
    $nativeDirect3D12 = Join-Path $artifacts 'native-dx12'
    if ($RuntimeIdentifier -eq 'win-x64') {
        & (Join-Path $root 'tools/Build-NativeDirect3D12.ps1') -OutputDirectory $nativeDirect3D12
    }
    $publishArguments = @('-c', $Configuration, '-r', $RuntimeIdentifier, '--self-contained', 'true', '-p:DebugType=None', '-p:DebugSymbols=false', '-p:PublishTrimmed=false')
    foreach ($backend in $backends) {
        $hostOutput = Join-Path $data ('Game/' + $backend.Directory)
        $hostBuild = Join-Path $artifacts ('build/' + $RuntimeIdentifier + '/' + $backend.Directory)
        $hostAssets += Join-Path $hostBuild 'obj/Memoria.FF8.App/project.assets.json'
        $nativePublishArguments = @()
        if ($RuntimeIdentifier -eq 'linux-x64' -and $backend.Name -eq 'Vulkan') {
            $nativePublishArguments += $nativeVulkanArguments
        }
        if ($env:MEMORIA_FF8_PSF_DECODER_DIRECTORY) {
            $nativePublishArguments += @(('-p:MemoriaPsfDecoderDirectory=' + [IO.Path]::GetFullPath($env:MEMORIA_FF8_PSF_DECODER_DIRECTORY)), '-p:MemoriaPsfDecoderPrepared=true')
        }
        if ($RuntimeIdentifier -eq 'win-x64' -and $backend.Name -eq 'Direct3D12') {
            $nativePublishArguments += @(('-p:MemoriaNativeDirect3D12Directory=' + $nativeDirect3D12), '-p:MemoriaNativeDirect3D12Prepared=true')
        }
        Invoke-DotNet (@('publish', 'src/Memoria.FF8.App/Memoria.FF8.App.csproj', '-o', $hostOutput, '--artifacts-path', $hostBuild, ('-p:MemoriaGraphicsBackend=' + $backend.Name), '-p:SkipMusicHost=true', '-p:PublishSingleFile=false', '-m:1') + $nativePublishArguments + $publishArguments)
        if ($RuntimeIdentifier -eq 'linux-x64' -and $backend.Name -eq 'Vulkan') {
            & (Join-Path $root 'tools/Build-NativeVulkan.ps1') -RuntimeIdentifier $RuntimeIdentifier -OutputDirectory $nativeVulkan -VerifyOnly
            Copy-Item -LiteralPath (Join-Path $nativeVulkan 'libmgruntime.so'), (Join-Path $nativeVulkan 'native-runtime.json') -Destination $hostOutput -Force
            $nativeNotices = Join-Path $data 'licenses/MonoGame.Native.Vulkan'
            New-Item -ItemType Directory -Force -Path $nativeNotices | Out-Null
            Get-ChildItem -LiteralPath (Join-Path $nativeVulkan 'licenses') -Force | Copy-Item -Destination $nativeNotices -Recurse -Force
        }
        if ($RuntimeIdentifier -eq 'win-x64' -and $backend.Name -eq 'Direct3D12') {
            $nativeReceipt = Get-Content -LiteralPath (Join-Path $nativeDirect3D12 'native-runtime.json') -Raw | ConvertFrom-Json
            $nativeLock = Get-Content -LiteralPath (Join-Path $root 'vendor/MonoGame.Native.DX12/native-runtime-lock.json') -Raw | ConvertFrom-Json
            $runtimeFile = @($nativeReceipt.files | Where-Object path -CEQ 'mgruntime.dll')
            if ($nativeReceipt.schemaVersion -ne 1 -or $nativeReceipt.runtimeIdentifier -cne 'win-x64' -or $nativeReceipt.backend -cne 'Direct3D12' -or $nativeReceipt.version -cne $nativeLock.version -or $nativeReceipt.upstream.commit -cne $nativeLock.upstream.commit -or $nativeReceipt.patchSha256 -cne $nativeLock.patchSha256 -or $runtimeFile.Count -ne 1) {
                throw 'The patched Direct3D 12 runtime receipt does not match the locked source recipe.'
            }
            if ((Get-FileHash -LiteralPath (Join-Path $nativeDirect3D12 'mgruntime.dll') -Algorithm SHA256).Hash.ToLowerInvariant() -cne $runtimeFile[0].sha256) {
                throw 'The patched Direct3D 12 runtime does not match its build receipt.'
            }
            Copy-Item -LiteralPath (Join-Path $nativeDirect3D12 'mgruntime.dll'), (Join-Path $nativeDirect3D12 'native-runtime.json') -Destination $hostOutput -Force
            $nativeNotices = Join-Path $data 'licenses/MonoGame.Native.DX12'
            New-Item -ItemType Directory -Force -Path $nativeNotices | Out-Null
            Get-ChildItem -LiteralPath (Join-Path $nativeDirect3D12 'licenses') -Force | Copy-Item -Destination $nativeNotices -Recurse -Force
        }
        if ($RuntimeIdentifier -like 'osx-*') {
            & (Join-Path $root 'tools/Get-MoltenVK.ps1') -Destination $hostOutput
        }
        Invoke-DotNet @('run', '--project', 'tools/Memoria.FF8.Distribution.Check', '-c', $Configuration, '--', $hostOutput, $RuntimeIdentifier)
        Invoke-DotNet @('run', '--project', 'tools/Memoria.FF8.Distribution.Check', '-c', $Configuration, '--', '--graphics-payload', $hostOutput, $backend.Name, (Join-Path $root 'tools/shader-manifest.json'), $RuntimeIdentifier)
    }
    Invoke-DotNet (@('publish', 'src/Memoria.FF8.Bootstrap/Memoria.FF8.Bootstrap.csproj', '-o', $data, '--artifacts-path', (Join-Path $artifacts ('build/' + $RuntimeIdentifier + '/bootstrap')), '-p:PublishSingleFile=false') + $publishArguments)
    # Keep the released worker path with the common x64 music implementation and its complete runtime.
    Invoke-DotNet @('publish', 'src/Memoria.FF8.Music/Memoria.FF8.Music.csproj', '-c', $Configuration, '-r', $RuntimeIdentifier, '--self-contained', 'true', '-o', (Join-Path $data 'audio-host'), '--artifacts-path', (Join-Path $artifacts ('build/' + $RuntimeIdentifier + '/music')), '-p:DebugType=None', '-p:DebugSymbols=false', '-p:PublishSingleFile=false')
    $launcherSingleFile = if ($RuntimeIdentifier -eq 'win-x64') { 'true' } else { 'false' }
    Invoke-DotNet (@('publish', 'src/Memoria.Launcher/Memoria.Launcher.csproj', '-o', $launcher, '--artifacts-path', (Join-Path $artifacts ('build/' + $RuntimeIdentifier + '/launcher')), ('-p:PublishSingleFile=' + $launcherSingleFile), '-p:IncludeNativeLibrariesForSelfExtract=true', '-p:PublishDocumentationFile=false', '-p:PublishReferencesDocumentationFiles=false') + $publishArguments)
    Invoke-DotNet @('run', '--project', 'tools/Memoria.FF8.Distribution.Check', '-c', $Configuration, '--', '--native-image', $RuntimeIdentifier, (Join-Path $data $executable))

    if ($Test) {
        Invoke-DotNet @('run', '--project', 'tests/Memoria.FF8.Remastered.Tests', '-c', $Configuration)
        $previousAppPath = $env:MEMORIA_FF8_APP_PATH
        try {
            $env:MEMORIA_FF8_APP_PATH = Join-Path $data 'Memoria.FF8.exe'
            Invoke-DotNet @('run', '--project', 'tests/Memoria.Launcher.Tests', '-c', $Configuration)
            Invoke-DotNet @('run', '--project', 'tests/Memoria.FF8.App.Tests', '-c', $Configuration, '--', '--filter-class', '*Remastered*', '--filter-class', '*FieldActorCaptureTests', '--filter-class', '*OpeningPresentationTests')
        }
        finally {
            $env:MEMORIA_FF8_APP_PATH = $previousAppPath
        }
        Invoke-DotNet @('run', '--project', 'tests/Memoria.FF8.Ui.Tests', '-c', $Configuration)
        Invoke-DotNet @('run', '--project', 'tests/Memoria.FF8.Assets.Tests', '-c', $Configuration, '--', '--filter-class', '*FieldPoseInterpolationTests', '--filter-class', '*FieldRenderInterpolationTests')
        Invoke-DotNet @('run', '--project', 'tests/Memoria.FF8.Runtime.Tests', '-c', $Configuration)
    }

    foreach ($relative in @($executable, ('audio-host/Memoria.FF8.Music' + $extension)) + @($backends | ForEach-Object { 'Game/' + $_.Directory + '/' + $executable; 'Game/' + $_.Directory + '/Memoria.PsfDecoder' + $extension })) {
        if (!(Test-Path -LiteralPath (Join-Path $data $relative) -PathType Leaf)) {
            throw "Incomplete distribution: Data/$relative is missing."
        }
    }
    if ($RuntimeIdentifier -eq 'win-x64') {
        New-Item -ItemType Directory -Force -Path (Join-Path $data 'Steam') | Out-Null
        Copy-Item -LiteralPath (Join-Path $root 'vendor/Steamworks/redistributable_bin/win64/steam_api64.dll') -Destination (Join-Path $data 'Steam')
        Copy-Item -LiteralPath (Join-Path $root 'vendor/Steamworks/README.md') -Destination (Join-Path $data 'Steam/NOTICE.md')
    }
    $launcherExecutable = Join-Path $launcher $launcherName
    if (!(Test-Path -LiteralPath $launcherExecutable -PathType Leaf)) {
        throw "The launcher publish did not produce $launcherName."
    }
    Invoke-DotNet @('run', '--project', 'tools/Memoria.FF8.Distribution.Check', '-c', $Configuration, '--', '--native-image', $RuntimeIdentifier, $launcherExecutable)
    if ($RuntimeIdentifier -eq 'win-x64') {
        Move-Item -LiteralPath $launcherExecutable -Destination $payload
        if (@(Get-ChildItem -LiteralPath $launcher -Force).Count -ne 0) {
            throw 'The Windows single-file launcher produced unexpected companion files. Its complete runtime must be embedded before packaging.'
        }
    }
    else {
        Get-ChildItem -LiteralPath $launcher | Move-Item -Destination $payload
    }
    if ([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($launcher)) -ne [IO.Path]::GetFullPath($staging)) {
        throw 'The launcher staging path is outside the generated staging directory.'
    }
    Remove-Item -LiteralPath $launcher -Recurse -Force
    Copy-Item -LiteralPath (Join-Path $root 'LICENSE'), (Join-Path $root 'THIRD-PARTY.md') -Destination $data
    Copy-Item -LiteralPath (Join-Path $root 'tools/shader-manifest.json') -Destination (Join-Path $data 'shader-manifest.json')
    Copy-Item -LiteralPath (Join-Path $root 'vendor/Albeoris.Frameworks/LICENSE') -Destination (Join-Path $data 'Albeoris.Frameworks.LICENSE')
    Copy-Item -LiteralPath (Join-Path $root 'vendor/Topten.RichTextKit.Bidi/LICENSE.txt') -Destination (Join-Path $data 'Text.Unicode.LICENSE')
    Copy-Item -LiteralPath (Join-Path $root 'vendor/Topten.RichTextKit.Bidi/UNICODE-LICENSE.txt') -Destination (Join-Path $data 'Text.Unicode.Data.LICENSE')
    Copy-Item -LiteralPath (Join-Path $root 'vendor/Topten.RichTextKit.Bidi/UNICODE-TRIE-LICENSE.txt') -Destination (Join-Path $data 'Text.Unicode.Trie.LICENSE')

    $licenses = Join-Path $data 'licenses'
    New-Item -ItemType Directory -Force -Path $licenses | Out-Null
    $copiedPackages = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $assetFiles = $hostAssets + @( (Join-Path $artifacts ('build/' + $RuntimeIdentifier + '/bootstrap/obj/Memoria.FF8.Bootstrap/project.assets.json')), (Join-Path $artifacts ('build/' + $RuntimeIdentifier + '/music/obj/Memoria.FF8.Music/project.assets.json')), (Join-Path $artifacts ('build/' + $RuntimeIdentifier + '/launcher/obj/Memoria.Launcher/project.assets.json')))
    foreach ($assetsPath in $assetFiles) {
        $assets = Get-Content -LiteralPath $assetsPath -Raw | ConvertFrom-Json -AsHashtable
        foreach ($entry in $assets.libraries.GetEnumerator()) {
            if ($entry.Value.type -ne 'package' -or !$copiedPackages.Add($entry.Key)) {
                continue
            }
            foreach ($folder in $assets.packageFolders.Keys) {
                $packagePath = Join-Path $folder $entry.Value.path
                if (!(Test-Path -LiteralPath $packagePath -PathType Container)) {
                    continue
                }
                $destination = Join-Path $licenses ($entry.Key.Replace('/', '-'))
                New-Item -ItemType Directory -Force -Path $destination | Out-Null
                foreach ($file in Get-ChildItem -LiteralPath $packagePath -File) {
                    if ($file.Name -match '^(LICENSE|COPYING|NOTICE)' -or $file.Extension -eq '.nuspec') {
                        Copy-Item -LiteralPath $file.FullName -Destination $destination
                    }
                }
                break
            }
        }
    }
    if ($RuntimeIdentifier -ne 'win-x64') {
        foreach ($path in @((Join-Path $payload $launcherName), (Join-Path $data $executable), (Join-Path $data ('audio-host/Memoria.FF8.Music' + $extension))) + @($backends | ForEach-Object { Join-Path $data ('Game/' + $_.Directory + '/' + $executable); Join-Path $data ('Game/' + $_.Directory + '/Memoria.PsfDecoder' + $extension) })) {
            if (![OperatingSystem]::IsWindows()) {
                [IO.File]::SetUnixFileMode($path, [IO.UnixFileMode]493)
            }
        }
    }
    if ($RuntimeIdentifier -like 'osx-*') {
        $plist = Join-Path $staging 'Memoria.FF8.app/Contents/Info.plist'
        Copy-Item -LiteralPath (Join-Path $root 'tools/packaging/Info.plist') -Destination $plist
        if ($RuntimeIdentifier -eq 'osx-arm64') {
            # Apple Silicon bundles contain only arm64 executables.
            [IO.File]::WriteAllText($plist, [IO.File]::ReadAllText($plist).Replace('<key>LSArchitecturePriority</key><array><string>x86_64</string></array>', '<key>LSArchitecturePriority</key><array><string>arm64</string></array>'))
        }
    }
    $manifestRoot = if ($RuntimeIdentifier -like 'osx-*') { Join-Path $staging 'Memoria.FF8.app' } else { $payload }
    & (Join-Path $root 'tools/New-DistributionManifest.ps1') -PackageRoot $manifestRoot -RuntimeIdentifier $RuntimeIdentifier

    # Replace only this script's known output directory after every publish succeeds.
    $resolvedArtifacts = [IO.Path]::GetFullPath((Join-Path $artifacts "package"))
    $resolvedPackage = [IO.Path]::GetFullPath($package)
    if ([IO.Path]::GetDirectoryName($resolvedPackage) -ne $resolvedArtifacts) {
        throw 'The package path is outside the artifacts directory.'
    }
    if (Test-Path -LiteralPath $package) {
        Remove-Item -LiteralPath $package -Recurse -Force
    }
    New-Item -ItemType Directory -Force -Path ([IO.Path]::GetDirectoryName($package)) | Out-Null
    Move-Item -LiteralPath $staging -Destination $package
    $suffix = if ($RuntimeIdentifier -eq 'win-x64') { '.zip' } else { '.tar.gz' }
    $archive = Join-Path $artifacts ('Memoria-FF8-' + $RuntimeIdentifier + $suffix)
    if ($RuntimeIdentifier -eq 'win-x64') {
        Compress-Archive -LiteralPath (Join-Path $package $launcherName), (Join-Path $package 'Data'), (Join-Path $package 'Mods') -DestinationPath $archive -Force
        # The released Windows updater remains an immutable gate on every build.
        Invoke-DotNet @('run', '--project', 'tools/Memoria.FF8.Updater.Compatibility', '-c', $Configuration, '--', $archive)
    }
    else {
        Invoke-DotNet @('run', '--project', 'tools/Memoria.FF8.Distribution.Check', '-c', $Configuration, '--', '--create-tar', $package, $archive)
    }
    & (Join-Path $root 'tools/Test-Distribution.ps1') -ArchivePath $archive -RuntimeIdentifier $RuntimeIdentifier -Configuration $Configuration
    Write-Host "Distribution: $package"
    Write-Host "Archive: $archive"
}
finally {
    Pop-Location
}
