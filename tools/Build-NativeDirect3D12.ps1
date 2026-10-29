[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$OutputDirectory,
    [string]$BuildDirectory
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$PSNativeCommandUseErrorActionPreference = $false
if (!$IsWindows) {
    throw 'The patched Direct3D 12 runtime must be built on Windows with the Visual Studio C++ desktop tools.'
}
$productRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$recipe = Join-Path $productRoot 'vendor/MonoGame.Native.DX12'
$lock = Get-Content (Join-Path $recipe 'native-runtime-lock.json') -Raw | ConvertFrom-Json
if (!$BuildDirectory) {
    $BuildDirectory = Join-Path $productRoot 'artifacts/native-dx12-build'
}
$BuildDirectory = [IO.Path]::GetFullPath($BuildDirectory)
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
if ((Test-Path -LiteralPath (Join-Path $OutputDirectory 'mgruntime.dll')) -and (Test-Path -LiteralPath (Join-Path $OutputDirectory 'native-runtime.json'))) {
    try {
        & (Join-Path $PSScriptRoot 'Test-NativeDirect3D12.ps1') -OutputDirectory $OutputDirectory
        Write-Host 'Reused the verified Direct3D 12 native runtime; no source or compiler tools were invoked.'
        return
    }
    catch {
        Write-Host 'The cached Direct3D 12 native runtime is invalid; rebuilding it from the pinned recipe.'
    }
}
$source = Join-Path $BuildDirectory 'source'
$nativeBuild = Join-Path $BuildDirectory 'build'
$patch = Join-Path $recipe 'RenderTargetMSAA.patch'
function Invoke-Checked([string]$Command, [string[]]$Arguments) {
    & $Command @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$Command failed with exit code $LASTEXITCODE."
    }
}
function Get-Hash([string]$Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}
function Assert-Revision([string]$Directory, [string]$Revision) {
    $actual = (& git -C $Directory rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0 -or $actual -cne $Revision) {
        throw "Unexpected source revision; expected $Revision."
    }
}
$patchText = [IO.File]::ReadAllText($patch).Replace("`r`n", "`n")
$patchHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($patchText))).ToLowerInvariant()
if ($patchHash -cne $lock.patchSha256) {
    throw 'The native patch does not match its locked hash.'
}
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
if (!(Test-Path -LiteralPath $vswhere)) {
    throw 'Install Visual Studio C++ desktop build tools, including vswhere and a Windows SDK.'
}
$installation = (& $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -format json | ConvertFrom-Json | Select-Object -First 1)
if (!$installation) {
    throw 'Visual Studio C++ desktop tools were not found.'
}
$major = ([version]$installation.installationVersion).Major
$generator = switch ($major) {
    17 {
        'Visual Studio 17 2022'
    }
    18 {
        'Visual Studio 18 2026'
    }
    default {
        throw 'Use a supported Visual Studio 2022 or 2026 C++ toolchain.'
    }
}
$toolset = Get-ChildItem -LiteralPath (Join-Path $installation.installationPath 'VC/Tools/MSVC') -Directory | Sort-Object Name -Descending | Select-Object -First 1
$compiler = Join-Path $toolset.FullName 'bin/Hostx64/x64/cl.exe'
$dumpbin = Join-Path $toolset.FullName 'bin/Hostx64/x64/dumpbin.exe'
New-Item -ItemType Directory -Force $BuildDirectory, $OutputDirectory | Out-Null
if (!(Test-Path -LiteralPath (Join-Path $source '.git'))) {
    Invoke-Checked git @('clone', '--filter=blob:none', '--no-checkout', '--sparse', $lock.upstream.repository, $source)
    Invoke-Checked git @('-C', $source, 'sparse-checkout', 'set', '--skip-checks', 'native/monogame', 'MonoGame.Framework/Platform/Graphics/Effect/Resources', 'external/stb')
    Invoke-Checked git @('-C', $source, 'checkout', '--detach', $lock.upstream.commit)
}
Assert-Revision $source $lock.upstream.commit
$patch = Join-Path $BuildDirectory 'render-target-msaa.patch'
[IO.File]::WriteAllText($patch, $patchText, [Text.UTF8Encoding]::new($false))
Invoke-Checked git @('-C', $source, 'submodule', 'update', '--init', '--depth', '1', 'native/monogame/external/sdl2', 'external/stb')
Invoke-Checked git @('-C', (Join-Path $source 'native/monogame/external/sdl2'), 'submodule', 'update', '--init', '--depth', '1', 'sdl')
foreach ($dependency in $lock.dependencies) {
    $directory = Join-Path $source $dependency.path
    Assert-Revision $directory $dependency.commit
    Invoke-Checked git @('-C', $directory, 'diff', '--exit-code')
}
& git -C $source apply --check $patch 2>$null
if ($LASTEXITCODE -eq 0) {
    Invoke-Checked git @('-C', $source, 'apply', $patch)
}
else {
    & git -C $source apply --reverse --check $patch 2>$null
    if ($LASTEXITCODE -ne 0) {
        throw 'The native source cache differs from the pinned source and patch.'
    }
}
$expectedChanges = @('native/monogame/directx12/CommandContext.cpp', 'native/monogame/directx12/CommandContext.h', 'native/monogame/directx12/MGG_DX12.cpp')
$changed = @(& git -C $source diff --name-only)
if ($LASTEXITCODE -ne 0 -or @($changed | Where-Object { $_ -notin $expectedChanges }).Count -ne 0) {
    throw 'Unexpected modified upstream source files.'
}
Copy-Item -LiteralPath (Join-Path $recipe 'CMakeLists.txt') -Destination (Join-Path $source 'CMakeLists.txt') -Force
New-Item -ItemType Directory -Force (Join-Path $source '.config') | Out-Null
Copy-Item -LiteralPath (Join-Path $productRoot '.config/dotnet-tools.json') -Destination (Join-Path $source '.config/dotnet-tools.json') -Force
$tools = Get-Content (Join-Path $source '.config/dotnet-tools.json') -Raw | ConvertFrom-Json
if ($tools.tools.'dotnet-mgfxc'.version -cne $lock.shaderCompiler.version) {
    throw 'The native stock shader compiler pin differs from the product tool manifest.'
}
Push-Location $source
try {
    Invoke-Checked dotnet @('tool', 'restore')
    foreach ($shader in Get-ChildItem 'MonoGame.Framework/Platform/Graphics/Effect/Resources' -Filter '*.fx' | Sort-Object Name) {
        Invoke-Checked dotnet @('mgfxc', ('MonoGame.Framework/Platform/Graphics/Effect/Resources/' + $shader.Name), ('native/monogame/directx12/' + $shader.BaseName + '.dx12.mgfxo.h'), '/Profile:DirectX_12')
    }
}
finally {
    Pop-Location
}
Invoke-Checked cmake @('-S', $source, '-B', $nativeBuild, '-G', $generator, '-A', 'x64', ('-DCMAKE_GENERATOR_INSTANCE=' + $installation.installationPath))
Invoke-Checked cmake @('--build', $nativeBuild, '--config', 'Release', '--target', 'mgruntime', '--parallel')
$dll = Join-Path $nativeBuild 'Release/mgruntime.dll'
$bytes = [IO.File]::ReadAllBytes($dll)
$peOffset = [BitConverter]::ToInt32($bytes, 60)
if ([BitConverter]::ToUInt16($bytes, $peOffset + 4) -ne 0x8664) {
    throw 'The native runtime is not x64.'
}
$ascii = [Text.Encoding]::ASCII.GetString($bytes)
$unicode = [Text.Encoding]::Unicode.GetString($bytes)
foreach ($prefix in @($source, $nativeBuild, [Environment]::GetFolderPath('UserProfile'))) {
    foreach ($variant in @($prefix.Replace('\', '/'), $prefix.Replace('/', '\'))) {
        if ($ascii.Contains($variant, [StringComparison]::OrdinalIgnoreCase) -or $unicode.Contains($variant, [StringComparison]::OrdinalIgnoreCase)) {
            throw 'The native binary contains a build-machine path.'
        }
    }
}
$referencePackage = Join-Path $BuildDirectory 'reference.nupkg'
if (!(Test-Path -LiteralPath $referencePackage)) {
    Invoke-WebRequest "https://api.nuget.org/v3-flatcontainer/monogame.runtime.windows.dx12/$($lock.version)/monogame.runtime.windows.dx12.$($lock.version).nupkg" -OutFile $referencePackage
}
$referenceHash = [Convert]::ToBase64String([Security.Cryptography.SHA512]::HashData([IO.File]::ReadAllBytes($referencePackage)))
if ($referenceHash -cne $lock.referencePackageSha512) {
    throw 'The reference NuGet runtime does not match its locked SHA-512.'
}
$referenceDirectory = Join-Path $BuildDirectory 'reference'
[IO.Compression.ZipFile]::ExtractToDirectory($referencePackage, $referenceDirectory, $true)
function Get-Exports([string]$Path) {
    $lines = & $dumpbin /exports $Path
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not inspect native exports.'
    }
    return @($lines | ForEach-Object {
        if ($_ -match '^\s*\d+\s+[0-9A-F]+\s+[0-9A-F]+\s+(\S+)') {
            $Matches[1]
        }
    } | Sort-Object)
}
$exports = @(Get-Exports $dll)
$referenceExports = @(Get-Exports (Join-Path $referenceDirectory 'runtimes/win-x64/native/mgruntime.dll'))
if ($exports.Count -eq 0 -or (Compare-Object $exports $referenceExports)) {
    throw 'The native ABI differs from MonoGame 3.8.5.1.'
}
$imports = @(& $dumpbin /dependents $dll | ForEach-Object {
    if ($_ -match '^\s*(\S+\.dll)\s*$') {
        $Matches[1]
    }
} | Sort-Object)
if ($LASTEXITCODE -ne 0 -or @($imports | Where-Object { $_ -match '^(msvcp|vcruntime)' }).Count -ne 0) {
    throw 'Native runtime dependency inspection failed or found a dynamic C++ runtime dependency.'
}
[xml]$project = Get-Content (Join-Path $nativeBuild 'mgruntime.vcxproj') -Raw
$receipt = [ordered]@{
    schemaVersion = 1
    runtimeIdentifier = 'win-x64'
    backend = 'Direct3D12'
    package = $lock.package
    version = $lock.version
    upstream = $lock.upstream
    dependencies = $lock.dependencies
    shaderCompiler = $lock.shaderCompiler
    patchSha256 = $lock.patchSha256
    recipeSha256 = Get-Hash (Join-Path $recipe 'CMakeLists.txt')
    buildScriptSha256 = Get-Hash $PSCommandPath
    toolchain = [ordered]@{
        cmake = (& cmake --version | Select-Object -First 1)
        compiler = (Get-Item $compiler).VersionInfo.FileVersion
        toolset = $toolset.Name
        windowsSdk = [string]$project.SelectSingleNode("//*[local-name()='WindowsTargetPlatformVersion']").InnerText
    }
    configuration = 'Release'
    exportedSymbols = $exports.Count
    nativeImports = $imports
    files = @([ordered]@{ path = 'mgruntime.dll'; sha256 = Get-Hash $dll })
}
Copy-Item -LiteralPath $dll -Destination (Join-Path $OutputDirectory 'mgruntime.dll') -Force
Copy-Item -LiteralPath (Join-Path $recipe 'licenses') -Destination $OutputDirectory -Recurse -Force
$receipt | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'native-runtime.json') -Encoding utf8
Write-Host "Built patched Direct3D 12 runtime with $($exports.Count) exports."
