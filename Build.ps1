[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$Test
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if (![OperatingSystem]::IsWindows()) {
    throw 'Memoria FF8 requires Windows to build and run.'
}

function Invoke-DotNet {
    param([string[]]$Arguments)

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet failed with exit code $LASTEXITCODE."
    }
}

$root = $PSScriptRoot
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

Push-Location $root
try {
    $publishArguments = @('-c', $Configuration, '-r', 'win-x64', '--self-contained', 'true', '-p:DebugType=None', '-p:DebugSymbols=false', '-p:PublishTrimmed=false')
    Invoke-DotNet (@('publish', 'src/Memoria.FF8.App/Memoria.FF8.App.csproj', '-o', $data, '-p:PublishSingleFile=false') + $publishArguments)
    # DirectMusic is a separate x86 process and needs its own complete runtime.
    Invoke-DotNet @('publish', 'src/Memoria.FF8.Music/Memoria.FF8.Music.csproj', '-c', $Configuration, '-r', 'win-x86', '--self-contained', 'true', '-o', (Join-Path $data 'audio-host'), '-p:DebugType=None', '-p:DebugSymbols=false', '-p:PublishSingleFile=false')
    Invoke-DotNet (@('publish', 'src/Memoria.Launcher/Memoria.Launcher.csproj', '-o', $launcher, '-p:PublishSingleFile=true', '-p:IncludeNativeLibrariesForSelfExtract=true') + $publishArguments)
    Invoke-DotNet @('run', '--project', 'tools/Memoria.FF8.Distribution.Check', '-c', $Configuration, '--', $data)

    if ($Test) {
        Invoke-DotNet @('run', '--project', 'tests/Memoria.Launcher.Tests', '-c', $Configuration)
        Invoke-DotNet @('run', '--project', 'tests/Memoria.FF8.Remastered.Tests', '-c', $Configuration)
        Invoke-DotNet @('run', '--project', 'tests/Memoria.FF8.Ui.Tests', '-c', $Configuration)
        Invoke-DotNet @('run', '--project', 'tests/Memoria.FF8.Runtime.Tests', '-c', $Configuration)
    }

    foreach ($relative in @('Memoria.FF8.exe', 'audio-host/Memoria.FF8.Music.exe', 'audio-host/coreclr.dll')) {
        if (!(Test-Path -LiteralPath (Join-Path $data $relative) -PathType Leaf)) {
            throw "Incomplete distribution: Data/$relative is missing."
        }
    }
    $launcherExecutable = Join-Path $launcher 'Memoria.Launcher.exe'
    if (!(Test-Path -LiteralPath $launcherExecutable -PathType Leaf)) {
        throw 'The launcher publish did not produce Memoria.Launcher.exe.'
    }
    Move-Item -LiteralPath $launcherExecutable -Destination $staging
    if ([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($launcher)) -ne [IO.Path]::GetFullPath($staging)) {
        throw 'The launcher staging path is outside the generated staging directory.'
    }
    Remove-Item -LiteralPath $launcher -Recurse -Force
    Copy-Item -LiteralPath (Join-Path $root 'LICENSE'), (Join-Path $root 'THIRD-PARTY.md') -Destination $data
    Copy-Item -LiteralPath (Join-Path $root 'vendor/Albeoris.Frameworks/LICENSE') -Destination (Join-Path $data 'Albeoris.Frameworks.LICENSE')

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
        foreach ($entry in $zip.Entries) {
            if ($entry.FullName.StartsWith('Mods/', [StringComparison]::Ordinal) -and !$entry.FullName.EndsWith('/', [StringComparison]::Ordinal)) {
                throw "The distribution archive contains a file in Mods: $($entry.FullName)."
            }
        }
    }
    finally {
        $zip.Dispose()
    }
    Write-Host "Distribution: $package"
    Write-Host "Archive: $archive"
}
finally {
    Pop-Location
}
