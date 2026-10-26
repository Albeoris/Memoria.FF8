[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [ValidateSet('Distribution', 'Portable', 'Launcher')]
    [string]$Scope = 'Distribution',
    [ValidateSet('', 'win-x64', 'linux-x64', 'osx-x64')]
    [string]$RuntimeIdentifier = '',
    [switch]$Test
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if (!$RuntimeIdentifier) {
    $RuntimeIdentifier = if ([OperatingSystem]::IsWindows()) { 'win-x64' } elseif ([OperatingSystem]::IsLinux()) { 'linux-x64' } elseif ([OperatingSystem]::IsMacOS()) { 'osx-x64' } else { throw 'Unsupported desktop host.' }
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
                foreach ($project in @('Memoria.FF8.Platform.Tests', 'Memoria.FF8.Audio.Tests', 'Memoria.FF8.Presentation.Tests', 'Memoria.Launcher.Tests')) {
                    Invoke-DotNet @('run', '--project', "tests/$project", '-c', $Configuration)
                }
                Invoke-DotNet @('run', '--project', 'tests/Memoria.FF8.Runtime.Tests', '-c', $Configuration, '--', '--filter-class', '*SteamAchievementBackendTests')
            }
        }
        else {
            $destination = Join-Path $root "artifacts/launcher/$RuntimeIdentifier"
            Invoke-DotNet @('publish', 'src/Memoria.Launcher/Memoria.Launcher.csproj', '-c', $Configuration, '-r', $RuntimeIdentifier, '--self-contained', 'true', '-p:PublishTrimmed=false', '-o', $destination)
            if ($Test) {
                Invoke-DotNet @('run', '--project', 'tests/Memoria.Launcher.Tests', '-c', $Configuration)
            }
            Write-Host "Launcher-only payload (game host supplied separately): $destination"
        }
    }
    finally {
        Pop-Location
    }
    return
}
if (![OperatingSystem]::IsWindows() -or $RuntimeIdentifier -ne 'win-x64') {
    throw 'The complete game distribution currently requires Windows x64. Use -Scope Portable or -Scope Launcher for the other desktop targets until their game hosts are available.'
}
$artifacts = Join-Path $root 'artifacts'
$package = Join-Path $artifacts 'package'
$staging = Join-Path $artifacts ('staging-' + [Guid]::NewGuid().ToString('N'))
$data = Join-Path $staging 'Data'
$launcher = Join-Path $staging 'launcher'
$modDirectories = @('Tonberry', 'Demaster', 'Junction')
New-Item -ItemType Directory -Force -Path $data, $launcher | Out-Null
foreach ($directory in $modDirectories) {
    New-Item -ItemType Directory -Path (Join-Path $staging "Mods/$directory") | Out-Null
}

$bundledMods = Join-Path $root 'Mods/Memoria'
$bundledModFiles = @(Get-ChildItem -LiteralPath $bundledMods -File -Recurse)
$expectedModFiles = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($file in $bundledModFiles) {
    $relative = 'Data/BundledMods/Memoria/' + [IO.Path]::GetRelativePath($bundledMods, $file.FullName).Replace('\', '/')
    $destination = Join-Path $staging $relative
    New-Item -ItemType Directory -Force -Path ([IO.Path]::GetDirectoryName($destination)) | Out-Null
    Copy-Item -LiteralPath $file.FullName -Destination $destination
    [void]$expectedModFiles.Add($relative)
}

Push-Location $root
try {
    $publishArguments = @('-c', $Configuration, '-r', 'win-x64', '--self-contained', 'true', '-p:DebugType=None', '-p:DebugSymbols=false', '-p:PublishTrimmed=false')
    Invoke-DotNet (@('publish', 'src/Memoria.FF8.App/Memoria.FF8.App.csproj', '-o', $data, '-p:PublishSingleFile=false') + $publishArguments)
    # Keep the released worker path with the common x64 music implementation and its complete runtime.
    Invoke-DotNet @('publish', 'src/Memoria.FF8.Music/Memoria.FF8.Music.csproj', '-c', $Configuration, '-r', 'win-x64', '--self-contained', 'true', '-o', (Join-Path $data 'audio-host'), '-p:DebugType=None', '-p:DebugSymbols=false', '-p:PublishSingleFile=false')
    Invoke-DotNet (@('publish', 'src/Memoria.Launcher/Memoria.Launcher.csproj', '-o', $launcher, '-p:PublishSingleFile=true', '-p:IncludeNativeLibrariesForSelfExtract=true') + $publishArguments)
    Invoke-DotNet @('run', '--project', 'tools/Memoria.FF8.Distribution.Check', '-c', $Configuration, '--', $data)

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

    foreach ($relative in @('Memoria.FF8.exe', 'audio-host/Memoria.FF8.Music.exe', 'audio-host/coreclr.dll', 'Steam/steam_api64.dll', 'Steam/NOTICE.md')) {
        if (!(Test-Path -LiteralPath (Join-Path $data $relative) -PathType Leaf)) {
            throw "Incomplete distribution: Data/$relative is missing."
        }
    }
    $launcherExecutable = Join-Path $launcher 'Memoria.Launcher.exe'
    if (!(Test-Path -LiteralPath $launcherExecutable -PathType Leaf)) {
        throw 'The launcher publish did not produce Memoria.Launcher.exe.'
    }
    Invoke-DotNet @('run', '--project', 'tools/Memoria.FF8.Distribution.Check', '-c', $Configuration, '--', '--native-image', 'win-x64', $launcherExecutable)
    Move-Item -LiteralPath $launcherExecutable -Destination $staging
    if ([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($launcher)) -ne [IO.Path]::GetFullPath($staging)) {
        throw 'The launcher staging path is outside the generated staging directory.'
    }
    Remove-Item -LiteralPath $launcher -Recurse -Force
    Copy-Item -LiteralPath (Join-Path $root 'LICENSE'), (Join-Path $root 'THIRD-PARTY.md') -Destination $data
    Copy-Item -LiteralPath (Join-Path $root 'vendor/Albeoris.Frameworks/LICENSE') -Destination (Join-Path $data 'Albeoris.Frameworks.LICENSE')
    Copy-Item -LiteralPath (Join-Path $root 'vendor/Topten.RichTextKit.Bidi/LICENSE.txt') -Destination (Join-Path $data 'Text.Unicode.LICENSE')
    Copy-Item -LiteralPath (Join-Path $root 'vendor/Topten.RichTextKit.Bidi/UNICODE-LICENSE.txt') -Destination (Join-Path $data 'Text.Unicode.Data.LICENSE')
    Copy-Item -LiteralPath (Join-Path $root 'vendor/Topten.RichTextKit.Bidi/UNICODE-TRIE-LICENSE.txt') -Destination (Join-Path $data 'Text.Unicode.Trie.LICENSE')

    $licenses = Join-Path $data 'licenses'
    New-Item -ItemType Directory -Force -Path $licenses | Out-Null
    $copiedPackages = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($project in @('Memoria.FF8.App', 'Memoria.FF8.Music', 'Memoria.Launcher')) {
        $assetsPath = Join-Path $root "src/$project/obj/project.assets.json"
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
    & (Join-Path $root 'tools/New-DistributionManifest.ps1') -PackageRoot $staging

    # Replace only this script's known output directory after every publish succeeds.
    $resolvedArtifacts = [IO.Path]::GetFullPath($artifacts)
    $resolvedPackage = [IO.Path]::GetFullPath($package)
    if ([IO.Path]::GetDirectoryName($resolvedPackage) -ne $resolvedArtifacts) {
        throw 'The package path is outside the artifacts directory.'
    }
    if (Test-Path -LiteralPath $package) {
        Remove-Item -LiteralPath $package -Recurse -Force
    }
    Move-Item -LiteralPath $staging -Destination $package
    $archive = Join-Path $artifacts 'Memoria-FF8-win-x64.zip'
    Compress-Archive -LiteralPath (Join-Path $package 'Memoria.Launcher.exe'), (Join-Path $package 'Data'), (Join-Path $package 'Mods') -DestinationPath $archive -Force
    $zip = [IO.Compression.ZipFile]::OpenRead($archive)
    try {
        foreach ($directory in $modDirectories) {
            if ($null -eq $zip.GetEntry("Mods/$directory/")) {
                throw "Incomplete distribution archive: Mods/$directory/ is missing."
            }
        }
        foreach ($relative in $expectedModFiles) {
            if ($null -eq $zip.GetEntry($relative)) {
                throw "Incomplete distribution archive: $relative is missing."
            }
        }
        foreach ($entry in $zip.Entries) {
            if ($entry.FullName.StartsWith('Mods/', [StringComparison]::Ordinal) -and !$entry.FullName.EndsWith('/', [StringComparison]::Ordinal) -and !$expectedModFiles.Contains($entry.FullName)) {
                throw "The distribution archive contains an unexpected mod file: $($entry.FullName)."
            }
        }
    }
    finally {
        $zip.Dispose()
    }
    # Validate with the immutable installed-updater contract on every build, including builds without -Test.
    Invoke-DotNet @('run', '--project', 'tools/Memoria.FF8.Updater.Compatibility', '-c', $Configuration, '--', $archive)
    if ($Test) {
        $previousDistributionPath = $env:MEMORIA_FF8_DISTRIBUTION_PATH
        try {
            $env:MEMORIA_FF8_DISTRIBUTION_PATH = $archive
            Invoke-DotNet @('run', '--project', 'tests/Memoria.Launcher.Tests', '-c', $Configuration, '--no-build', '--', '--filter-class', '*DistributionArchiveTests')
        }
        finally {
            $env:MEMORIA_FF8_DISTRIBUTION_PATH = $previousDistributionPath
        }
    }
    Write-Host "Distribution: $package"
    Write-Host "Archive: $archive"
}
finally {
    Pop-Location
}
