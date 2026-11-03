[CmdletBinding()]
param([string]$Configuration = 'Release')

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if (![OperatingSystem]::IsMacOS()) {
    Write-Output 'Skipped native macOS signing checks: a macOS host is required.'
    return
}

$product = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$checker = Join-Path $product 'tools/Memoria.FF8.Distribution.Check'
$temporary = Join-Path ([IO.Path]::GetTempPath()) ('memoria-mac-signing-tests-' + [Guid]::NewGuid().ToString('N'))
$rid = if ([Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture -eq 'Arm64') { 'osx-arm64' } else { 'osx-x64' }
$buildId = 'a' * 40
$version = 'v1.2.3'
$previousEnvironment = @{}
foreach ($name in @('GITHUB_ACTIONS', 'GITHUB_SHA', 'GITHUB_REF_TYPE', 'GITHUB_REF_NAME')) {
    $previousEnvironment[$name] = [Environment]::GetEnvironmentVariable($name)
}

function Invoke-Checker([string[]]$Arguments, [switch]$ExpectFailure) {
    Write-Host ('Distribution checker: {0}' -f ($Arguments -join ' '))
    $output = @(& dotnet $checkerAssembly @Arguments 2>&1)
    if (($LASTEXITCODE -ne 0) -ne [bool]$ExpectFailure) {
        throw "Unexpected distribution checker result for $($Arguments[0]): $($output -join [Environment]::NewLine)"
    }
}

function Test-RejectedMutation([string]$Name, [scriptblock]$Mutation) {
    $caseParent = Join-Path $temporary $Name
    New-Item -ItemType Directory -Path $caseParent | Out-Null
    $caseRoot = Join-Path $caseParent 'Memoria.FF8.app'
    Copy-Item -LiteralPath $bundle -Destination $caseRoot -Recurse
    & $Mutation $caseRoot
    Invoke-Checker @('--validate-mac-inventory', $caseRoot) -ExpectFailure
}

function Set-TamperedByte([string]$Path) {
    $bytes = [IO.File]::ReadAllBytes($Path)
    if ($bytes.Length -le 4096) {
        throw 'The native signing fixture is unexpectedly small.'
    }
    $bytes[4096] = $bytes[4096] -bxor 1
    [IO.File]::WriteAllBytes($Path, $bytes)
}

try {
    New-Item -ItemType Directory -Path $temporary | Out-Null
    & dotnet build $checker -c $Configuration --nologo
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not build the distribution checker.'
    }
    $checkerAssembly = & dotnet msbuild $checker "-p:Configuration=$Configuration" -getProperty:TargetPath -nologo
    if ($LASTEXITCODE -ne 0 -or !(Test-Path -LiteralPath $checkerAssembly -PathType Leaf)) {
        throw 'Could not locate the built distribution checker.'
    }

    # Exercise the SDK apphost layout, including any linker-provided signature.
    $source = Join-Path $temporary 'source'
    $published = Join-Path $temporary 'published'
    New-Item -ItemType Directory -Path $source | Out-Null
    $project = Join-Path $source 'SigningFixture.csproj'
    $targetImport = [Security.SecurityElement]::Escape((Join-Path $product 'tools/packaging/MacBundleLauncher.targets'))
    $projectText = '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><UseAppHost>true</UseAppHost></PropertyGroup></Project>'
    $projectText = $projectText.Replace('</Project>', "<Import Project=`"$targetImport`" /></Project>")
    Set-Content -LiteralPath $project -Encoding utf8NoBOM -Value $projectText
    Set-Content -LiteralPath (Join-Path $source 'Program.cs') -Encoding utf8NoBOM -Value 'System.Console.WriteLine("Signing fixture");'
    & dotnet publish $project -c $Configuration -r $rid --self-contained true -o $published --nologo
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not publish the native .NET signing fixture.'
    }

    $parent = Join-Path $temporary 'package'
    $bundle = Join-Path $parent 'Memoria.FF8.app'
    $resources = Join-Path $bundle 'Contents/Resources'
    New-Item -ItemType Directory -Force -Path $resources | Out-Null
    Get-ChildItem -LiteralPath $published | Copy-Item -Destination $resources -Recurse
    foreach ($file in Get-ChildItem -LiteralPath $resources -File -Recurse) {
        [IO.File]::SetUnixFileMode($file.FullName, [IO.UnixFileMode]420)
    }
    # NuGet can publish managed assemblies with executable bits; finalization
    # must enforce the distribution's data-file mode before sealing the app.
    [IO.File]::SetUnixFileMode((Join-Path $resources 'SigningFixture.dll'), [IO.UnixFileMode]493)
    foreach ($relative in @('Memoria.Launcher', 'Data/Memoria.FF8', 'Data/audio-host/Memoria.FF8.Music', 'Data/Game/vulkan/Memoria.FF8', 'Data/Game/vulkan/Memoria.PsfDecoder')) {
        $path = Join-Path $bundle ('Contents/Resources/' + $relative)
        New-Item -ItemType Directory -Force -Path ([IO.Path]::GetDirectoryName($path)) | Out-Null
        Copy-Item -LiteralPath (Join-Path $published 'SigningFixture') -Destination $path
        [IO.File]::SetUnixFileMode($path, [IO.UnixFileMode]493)
    }
    Remove-Item -LiteralPath (Join-Path $resources 'SigningFixture')
    $entrypoint = Join-Path $bundle 'Contents/MacOS/Memoria.Launcher'
    & dotnet build $project --no-restore -c $Configuration -r $rid --self-contained true -t:CreateMacBundleLauncher "-p:MacBundleLauncherPath=$entrypoint" --nologo
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not create the native apphost pointing to Resources.'
    }
    [IO.File]::SetUnixFileMode($entrypoint, [IO.UnixFileMode]493)
    $plist = Join-Path $bundle 'Contents/Info.plist'
    Copy-Item -LiteralPath (Join-Path $product 'tools/packaging/Info.plist') -Destination $plist
    if ($rid -eq 'osx-arm64') {
        [IO.File]::WriteAllText($plist, [IO.File]::ReadAllText($plist).Replace('<key>LSArchitecturePriority</key><array><string>x86_64</string></array>', '<key>LSArchitecturePriority</key><array><string>arm64</string></array>'))
    }
    Set-Content -LiteralPath (Join-Path $bundle 'Contents/Resources/Data/fixture.txt') -Encoding utf8NoBOM -Value 'Signed resource fixture.'
    $env:GITHUB_ACTIONS = 'true'
    $env:GITHUB_SHA = $buildId
    $env:GITHUB_REF_TYPE = 'tag'
    $env:GITHUB_REF_NAME = $version
    & (Join-Path $product 'tools/New-DistributionManifest.ps1') -PackageRoot $bundle -RuntimeIdentifier $rid
    Invoke-Checker @('--sign-mac-bundle', $bundle)
    Invoke-Checker @('--validate-mac-inventory', $bundle)
    Invoke-Checker @('--validate-mac-signatures', $bundle)
    $launchOutput = @(& (Join-Path $bundle 'Contents/MacOS/Memoria.Launcher') 2>&1)
    if ($LASTEXITCODE -ne 0 -or ($launchOutput -join [Environment]::NewLine).Trim() -cne 'Signing fixture') {
        throw "The signed .NET fixture could not start its runtime: $($launchOutput -join [Environment]::NewLine)"
    }
    $manifestPath = Join-Path $bundle 'Contents/Resources/Data/distribution.json'
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    $managed = @($manifest.Files | Where-Object Path -ceq 'Contents/Resources/SigningFixture.dll')
    if ($managed.Count -ne 1 -or $managed[0].UnixMode -ne 420 -or [int][IO.File]::GetUnixFileMode((Join-Path $resources 'SigningFixture.dll')) -ne 420) {
        throw 'Managed assembly modes were not normalized before signing.'
    }
    $launcher = @($manifest.Files | Where-Object Path -ceq 'Contents/MacOS/Memoria.Launcher')
    if ($manifest.SchemaVersion -ne 3 -or $manifest.Version -cne $version -or $manifest.BuildId -cne $buildId -or $launcher.Count -ne 1 -or $launcher[0].HashKind -cne 'macos-unsigned-sha256') {
        throw 'The signed bundle identity or canonical launcher inventory is incorrect.'
    }
    if (@($manifest.Files | Where-Object Path -ceq 'Contents/_CodeSignature/CodeResources').Count -ne 0) {
        throw 'The generated outer resource seal must not create an inventory cycle.'
    }

    $archive = Join-Path $temporary 'signed.tar.gz'
    Invoke-Checker @('--create-tar', $parent, $archive)
    $archiveHash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash
    Invoke-Checker @('--set-version', $archive, $version, $buildId)
    if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -cne $archiveHash) {
        throw 'A same-version signed archive operation must leave every archive byte intact.'
    }
    Invoke-Checker @('--set-version', $archive, 'v1.2.4', $buildId) -ExpectFailure
    if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -cne $archiveHash) {
        throw 'Refusing signed archive mutation changed the original archive.'
    }
    $extracted = Join-Path $temporary 'extracted'
    New-Item -ItemType Directory -Path $extracted | Out-Null
    & tar -xf $archive -C $extracted
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not extract the signed fixture archive.'
    }
    Invoke-Checker @('--validate-mac-inventory', (Join-Path $extracted 'Memoria.FF8.app'))
    Invoke-Checker @('--validate-mac-signatures', (Join-Path $extracted 'Memoria.FF8.app'))

    Test-RejectedMutation 'plist' {
        param($root)
        $path = Join-Path $root 'Contents/Info.plist'
        [IO.File]::WriteAllText($path, [IO.File]::ReadAllText($path).Replace('<string>Memoria.Launcher</string>', '<string>Changed.Launcher</string>'))
    }
    Test-RejectedMutation 'launcher' {
        param($root)
        Set-TamperedByte (Join-Path $root 'Contents/MacOS/Memoria.Launcher')
    }
    Test-RejectedMutation 'nested-native' {
        param($root)
        Set-TamperedByte (Join-Path $root 'Contents/Resources/Data/Game/vulkan/Memoria.PsfDecoder')
    }
    Test-RejectedMutation 'resource' {
        param($root)
        Add-Content -LiteralPath (Join-Path $root 'Contents/Resources/Data/fixture.txt') -Value 'Changed.'
    }
    Test-RejectedMutation 'missing-seal' {
        param($root)
        Remove-Item -LiteralPath (Join-Path $root 'Contents/_CodeSignature/CodeResources')
    }
    Test-RejectedMutation 'modified-seal' {
        param($root)
        [IO.File]::WriteAllText((Join-Path $root 'Contents/_CodeSignature/CodeResources'), 'Invalid resource seal.')
    }
    Test-RejectedMutation 'extra-file' {
        param($root)
        Set-Content -LiteralPath (Join-Path $root 'Contents/Resources/Data/unexpected.txt') -Value 'Uninventoried.'
    }

    # Outer signing may replace signature bytes without changing canonical code.
    $manifestHash = (Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).Hash
    & codesign --force --sign - --timestamp=none $bundle
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not re-sign the outer fixture bundle.'
    }
    Invoke-Checker @('--validate-mac-inventory', $bundle)
    Invoke-Checker @('--validate-mac-signatures', $bundle)
    if ((Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).Hash -cne $manifestHash) {
        throw 'Re-signing changed the embedded canonical inventory.'
    }
    Write-Output 'Native macOS SDK apphost signing, canonical inventory, archive roundtrip, version immutability, tamper rejection and outer re-signing passed.'
}
finally {
    foreach ($name in $previousEnvironment.Keys) {
        [Environment]::SetEnvironmentVariable($name, $previousEnvironment[$name])
    }
    if ([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($temporary)) -ne [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar)) {
        throw 'Test cleanup escaped its dedicated temporary root.'
    }
    if (Test-Path -LiteralPath $temporary) {
        Remove-Item -LiteralPath $temporary -Recurse -Force
    }
}
