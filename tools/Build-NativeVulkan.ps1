[CmdletBinding()]
param(
    [ValidateSet('win-x64', 'linux-x64', 'osx-x64', 'osx-arm64')][string]$RuntimeIdentifier = 'linux-x64',
    [Parameter(Mandatory)][string]$OutputDirectory,
    [string]$BuildDirectory,
    [string]$Distro,
    [switch]$VerifyOnly
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$PSNativeCommandUseErrorActionPreference = $false
$productRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$recipe = Join-Path $productRoot 'vendor/MonoGame.Native.Vulkan'
$lock = Get-Content (Join-Path $recipe 'native-runtime-lock.json') -Raw | ConvertFrom-Json
$isMacRuntime = $RuntimeIdentifier -like 'osx-*'
$referenceKey = if ($isMacRuntime) { 'osx' } else { $RuntimeIdentifier }
$reference = $lock.referencePackages.$referenceKey
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
$receiptPath = Join-Path $OutputDirectory 'native-runtime.json'
$libraryName = switch ($RuntimeIdentifier) {
    'win-x64' { 'mgruntime.dll' }
    'linux-x64' { 'libmgruntime.so' }
    default { 'libmgruntime.dylib' }
}
$libraryPath = Join-Path $OutputDirectory $libraryName
$recipeFiles = @('native-runtime-lock.json', 'BackBufferReadback.patch', 'FAudioCurveAlignment.patch', 'CMakeLists.txt', 'build.sh', 'NativeVulkan.targets')
function Get-Hash([string]$Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}
function Get-TextHash([string]$Path) {
    $text = [IO.File]::ReadAllText($Path).Replace("`r`n", "`n")
    return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($text))).ToLowerInvariant()
}
$recipeHashes = [ordered]@{}
foreach ($file in $recipeFiles) {
    $recipeHashes[$file] = Get-TextHash (Join-Path $recipe $file)
}
$recipeHashes['Build-NativeVulkan.ps1'] = Get-TextHash $PSCommandPath
$recipeHashes['dotnet-tools.json'] = Get-TextHash (Join-Path $productRoot '.config/dotnet-tools.json')
if ($isMacRuntime) {
    $recipeHashes['Get-MoltenVK.ps1'] = Get-TextHash (Join-Path $PSScriptRoot 'Get-MoltenVK.ps1')
}
if ($recipeHashes['BackBufferReadback.patch'] -cne $lock.patchSha256) {
    throw 'The Vulkan source patch does not match its locked SHA-256.'
}
if ($recipeHashes['FAudioCurveAlignment.patch'] -cne $lock.faudioPatchSha256) {
    throw 'The FAudio source patch does not match its locked SHA-256.'
}
$tools = Get-Content (Join-Path $productRoot '.config/dotnet-tools.json') -Raw | ConvertFrom-Json
if ($tools.tools.'dotnet-mgfxc'.version -cne $lock.shaderCompiler.version) {
    throw 'The stock shader compiler differs from its locked version.'
}
function Test-Receipt {
    if (!(Test-Path -LiteralPath $receiptPath) -or !(Test-Path -LiteralPath $libraryPath)) {
        throw 'The patched Vulkan runtime and receipt are required.'
    }
    $receipt = Get-Content -LiteralPath $receiptPath -Raw | ConvertFrom-Json
    if ($receipt.schemaVersion -ne 1 -or $receipt.runtimeIdentifier -cne $RuntimeIdentifier -or $receipt.backend -cne 'Vulkan' -or $receipt.upstreamCommit -cne $lock.upstream.commit -or $receipt.referencePackageSha512 -cne $reference.sha512) {
        throw 'The Vulkan runtime receipt identity is invalid.'
    }
    foreach ($file in $recipeHashes.Keys) {
        if ($receipt.recipeHashes.$file -cne $recipeHashes[$file]) {
            throw "The Vulkan runtime recipe changed: $file."
        }
    }
    if ($receipt.outputSha256 -cne (Get-Hash $libraryPath)) {
        throw 'The Vulkan runtime does not match its receipt hash.'
    }
    $bytes = [IO.File]::ReadAllBytes($libraryPath)
    $matchesTarget = switch ($RuntimeIdentifier) {
        'linux-x64' {
            $bytes.Length -ge 20 -and [BitConverter]::ToUInt32($bytes, 0) -eq 0x464c457f -and $bytes[4] -eq 2 -and $bytes[5] -eq 1 -and [BitConverter]::ToUInt16($bytes, 18) -eq 62
        }
        'win-x64' {
            $offset = if ($bytes.Length -ge 64) { [BitConverter]::ToInt32($bytes, 60) } else { -1 }
            $offset -ge 64 -and $offset -le $bytes.Length - 6 -and [BitConverter]::ToUInt16($bytes, 0) -eq 0x5a4d -and [BitConverter]::ToUInt32($bytes, $offset) -eq 0x4550 -and [BitConverter]::ToUInt16($bytes, $offset + 4) -eq 0x8664
        }
        default {
            $cpuType = if ($RuntimeIdentifier -eq 'osx-arm64') { 0x0100000c } else { 0x01000007 }
            $bytes.Length -ge 16 -and [BitConverter]::ToInt32($bytes, 0) -eq 0xfeedfacf -and [BitConverter]::ToInt32($bytes, 4) -eq $cpuType -and [BitConverter]::ToInt32($bytes, 12) -eq 6
        }
    }
    if (!$matchesTarget) {
        throw "The Vulkan runtime binary does not match $RuntimeIdentifier."
    }
    foreach ($notice in Get-ChildItem -LiteralPath (Join-Path $recipe 'licenses') -File -Recurse) {
        $relative = [IO.Path]::GetRelativePath((Join-Path $recipe 'licenses'), $notice.FullName)
        $copied = Join-Path (Join-Path $OutputDirectory 'licenses') $relative
        if (!(Test-Path -LiteralPath $copied) -or (Get-Hash $copied) -cne (Get-Hash $notice.FullName)) {
            throw "The Vulkan runtime notice is missing or changed: $relative."
        }
    }
}
if ($VerifyOnly) {
    Test-Receipt
    Write-Host "Verified the patched Vulkan runtime and source recipe receipt for $RuntimeIdentifier."
    return
}
if ((Test-Path -LiteralPath $receiptPath) -and (Test-Path -LiteralPath $libraryPath)) {
    try {
        Test-Receipt
        Write-Host "Reused the verified Vulkan runtime for $RuntimeIdentifier."
        return
    }
    catch {
        Write-Host 'The cached Vulkan runtime is invalid; rebuilding from the pinned recipe.'
    }
}
$useWsl = $IsWindows -and $RuntimeIdentifier -eq 'linux-x64'
if ($RuntimeIdentifier -eq 'linux-x64' -and !$IsLinux -and !$useWsl) {
    throw 'Build the Linux Vulkan runtime on Linux or through WSL.'
}
if ($useWsl -and !$Distro) {
    throw 'Pass -Distro with a configured WSL distribution to build the Linux Vulkan runtime on Windows.'
}
if ($RuntimeIdentifier -eq 'win-x64' -and !$IsWindows) {
    throw 'Build the Windows Vulkan runtime with native Windows C++ tools.'
}
if ($isMacRuntime -and !$IsMacOS) {
    throw 'Build the macOS Vulkan runtime with native Xcode tools.'
}
if (!$BuildDirectory) {
    $BuildDirectory = $OutputDirectory + '-build'
}
$BuildDirectory = [IO.Path]::GetFullPath($BuildDirectory)
New-Item -ItemType Directory -Force $OutputDirectory, $BuildDirectory | Out-Null
$dependencyPlan = Join-Path $BuildDirectory 'dependencies.tsv'
$lines = @($lock.dependencies | ForEach-Object { "$($_.path)`t$($_.repository)`t$($_.commit)" })
[IO.File]::WriteAllText($dependencyPlan, ($lines -join "`n") + "`n", [Text.UTF8Encoding]::new($false))
function Convert-BuildPath([string]$Path) {
    if (!$useWsl) {
        if ($IsWindows) {
            return $Path.Replace('\', '/')
        }
        return $Path
    }
    $converted = & wsl.exe -d $Distro --exec wslpath -a -u $Path
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not convert a native Vulkan build path for WSL.'
    }
    return $converted.Trim()
}
$workerArguments = @((Convert-BuildPath (Join-Path $recipe 'build.sh')), (Convert-BuildPath $recipe), (Convert-BuildPath $OutputDirectory), (Convert-BuildPath $BuildDirectory), $lock.upstream.repository, $lock.upstream.commit, $lock.version, $reference.sha512, (Convert-BuildPath $dependencyPlan), $RuntimeIdentifier, $reference.name)
if ($useWsl) {
    & wsl.exe -d $Distro --exec bash @workerArguments
} elseif ($IsWindows) {
    $gitRoot = Split-Path (Split-Path (Get-Command git.exe).Source -Parent) -Parent
    $gitBash = Join-Path $gitRoot 'bin/bash.exe'
    if (!(Test-Path -LiteralPath $gitBash)) {
        throw 'Git for Windows Bash is required to build the Vulkan runtime.'
    }
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
    $installation = & $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -format json | ConvertFrom-Json | Select-Object -First 1
    if (!$installation) {
        throw 'Visual Studio C++ desktop tools were not found.'
    }
    $generator = switch (([version]$installation.installationVersion).Major) {
        17 { 'Visual Studio 17 2022' }
        18 { 'Visual Studio 18 2026' }
        default { throw 'Use a supported Visual Studio 2022 or 2026 C++ toolchain.' }
    }
    $toolset = Get-ChildItem -LiteralPath (Join-Path $installation.installationPath 'VC/Tools/MSVC') -Directory | Sort-Object Name -Descending | Select-Object -First 1
    $dumpbin = Join-Path $toolset.FullName 'bin/Hostx64/x64/dumpbin.exe'
    $workerArguments += @((Convert-BuildPath $dumpbin), $generator, (Convert-BuildPath $installation.installationPath))
    & $gitBash --noprofile --norc @workerArguments
}
else {
    if ($isMacRuntime) {
        & (Join-Path $PSScriptRoot 'Get-MoltenVK.ps1') -Destination (Join-Path $BuildDirectory 'moltenvk') -BuildSupport
    }
    & bash @workerArguments
}
if ($LASTEXITCODE -ne 0) {
    throw "The native Vulkan source build failed with exit code $LASTEXITCODE."
}
$toolchain = @(Get-Content (Join-Path $OutputDirectory 'native-toolchain.txt'))
if (!$useWsl -and !$IsLinux) {
    $binary = [IO.File]::ReadAllBytes($libraryPath)
    $ascii = [Text.Encoding]::ASCII.GetString($binary)
    $unicode = [Text.Encoding]::Unicode.GetString($binary)
    $unaligned = [Text.Encoding]::Unicode.GetString($binary, 1, $binary.Length - 1)
    foreach ($path in @((Join-Path $BuildDirectory 'source'), (Join-Path $BuildDirectory 'build'), [Environment]::GetFolderPath('UserProfile'))) {
        foreach ($variant in @($path.Replace('\', '/'), $path.Replace('/', '\'))) {
            if ($ascii.Contains($variant, [StringComparison]::OrdinalIgnoreCase) -or $unicode.Contains($variant, [StringComparison]::OrdinalIgnoreCase) -or $unaligned.Contains($variant, [StringComparison]::OrdinalIgnoreCase)) {
                throw 'The Vulkan runtime contains a build-machine path.'
            }
        }
    }
}
$receipt = [ordered]@{
    schemaVersion = 1
    runtimeIdentifier = $RuntimeIdentifier
    backend = 'Vulkan'
    upstreamCommit = $lock.upstream.commit
    dependencyCommits = @($lock.dependencies | ForEach-Object { [ordered]@{ path = $_.path; commit = $_.commit } })
    referencePackageSha512 = $reference.sha512
    recipeHashes = $recipeHashes
    outputSha256 = Get-Hash $libraryPath
    exportedApiSha256 = Get-Hash (Join-Path $OutputDirectory 'native-exports.txt')
    sharedDependencies = @(Get-Content (Join-Path $OutputDirectory 'native-needed.txt'))
    sharedAbiVersions = @(Get-Content (Join-Path $OutputDirectory 'native-abi-versions.txt'))
    toolchain = [ordered]@{ cmake = $toolchain[0]; compiler = $toolchain[1]; dotnet = $toolchain[2]; configuration = 'Release'; cppStandard = 'c++17'; sourcePathMap = '.'; buildPathMap = 'build' }
}
Copy-Item -LiteralPath (Join-Path $recipe 'licenses') -Destination $OutputDirectory -Recurse -Force
$receipt | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $receiptPath -Encoding utf8NoBOM
Remove-Item -LiteralPath (Join-Path $OutputDirectory 'native-exports.txt'), (Join-Path $OutputDirectory 'native-needed.txt'), (Join-Path $OutputDirectory 'native-toolchain.txt'), (Join-Path $OutputDirectory 'native-abi-versions.txt')
Test-Receipt
Write-Host "Built and verified the patched Vulkan runtime for $RuntimeIdentifier."
