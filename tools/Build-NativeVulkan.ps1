[CmdletBinding()]
param(
    [ValidateSet('linux-x64')][string]$RuntimeIdentifier = 'linux-x64',
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
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
$receiptPath = Join-Path $OutputDirectory 'native-runtime.json'
$libraryPath = Join-Path $OutputDirectory 'libmgruntime.so'
$recipeFiles = @('native-runtime-lock.json', 'BackBufferReadback.patch', 'CMakeLists.txt', 'build.sh', 'NativeVulkan.targets')
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
if ($recipeHashes['BackBufferReadback.patch'] -cne $lock.patchSha256) {
    throw 'The Vulkan source patch does not match its locked SHA-256.'
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
    if ($receipt.schemaVersion -ne 1 -or $receipt.runtimeIdentifier -cne $RuntimeIdentifier -or $receipt.backend -cne 'Vulkan' -or $receipt.upstreamCommit -cne $lock.upstream.commit -or $receipt.referencePackageSha512 -cne $lock.referencePackageSha512) {
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
    if ($bytes.Length -lt 20 -or $bytes[0] -ne 0x7f -or $bytes[1] -ne 0x45 -or $bytes[2] -ne 0x4c -or $bytes[3] -ne 0x46 -or $bytes[4] -ne 2 -or $bytes[5] -ne 1 -or [BitConverter]::ToUInt16($bytes, 18) -ne 62) {
        throw 'The Vulkan runtime is not a little-endian Linux x64 ELF library.'
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
    Write-Host 'Verified the patched Linux Vulkan runtime and source recipe receipt.'
    return
}
if ((Test-Path -LiteralPath $receiptPath) -and (Test-Path -LiteralPath $libraryPath)) {
    try {
        Test-Receipt
        Write-Host 'Reused the verified Linux Vulkan runtime; no source or compiler tools were invoked.'
        return
    }
    catch {
        Write-Host 'The cached Vulkan runtime is invalid; rebuilding from the pinned recipe.'
    }
}
if (!$IsLinux -and !$IsWindows) {
    throw 'Build the Linux Vulkan runtime on Linux or through a selected WSL distribution on Windows.'
}
if ($IsWindows -and !$Distro) {
    throw 'Pass -Distro with a configured WSL distribution to build the Linux Vulkan runtime on Windows.'
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
    if ($IsLinux) {
        return $Path
    }
    $converted = & wsl.exe -d $Distro --exec wslpath -a -u $Path
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not convert a native Vulkan build path for WSL.'
    }
    return $converted.Trim()
}
$workerArguments = @((Convert-BuildPath (Join-Path $recipe 'build.sh')), (Convert-BuildPath $recipe), (Convert-BuildPath $OutputDirectory), (Convert-BuildPath $BuildDirectory), $lock.upstream.repository, $lock.upstream.commit, $lock.version, $lock.referencePackageSha512, (Convert-BuildPath $dependencyPlan))
if ($IsWindows) {
    & wsl.exe -d $Distro --exec bash @workerArguments
}
else {
    & bash @workerArguments
}
if ($LASTEXITCODE -ne 0) {
    throw "The native Vulkan source build failed with exit code $LASTEXITCODE."
}
$toolchain = @(Get-Content (Join-Path $OutputDirectory 'native-toolchain.txt'))
$receipt = [ordered]@{
    schemaVersion = 1
    runtimeIdentifier = $RuntimeIdentifier
    backend = 'Vulkan'
    upstreamCommit = $lock.upstream.commit
    dependencyCommits = @($lock.dependencies | ForEach-Object { [ordered]@{ path = $_.path; commit = $_.commit } })
    referencePackageSha512 = $lock.referencePackageSha512
    recipeHashes = $recipeHashes
    outputSha256 = Get-Hash $libraryPath
    exportedApiSha256 = Get-Hash (Join-Path $OutputDirectory 'native-exports.txt')
    sharedDependencies = @(Get-Content (Join-Path $OutputDirectory 'native-needed.txt'))
    sharedAbiVersions = @(Get-Content (Join-Path $OutputDirectory 'native-abi-versions.txt'))
    toolchain = [ordered]@{ cmake = $toolchain[0]; compiler = $toolchain[1]; dotnet = $toolchain[2]; requiredGlibc = $toolchain[3]; configuration = 'Release'; cppStandard = 'c++17'; sourcePathMap = '.'; buildPathMap = 'build' }
}
Copy-Item -LiteralPath (Join-Path $recipe 'licenses') -Destination $OutputDirectory -Recurse -Force
$receipt | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $receiptPath -Encoding utf8NoBOM
Remove-Item -LiteralPath (Join-Path $OutputDirectory 'native-exports.txt'), (Join-Path $OutputDirectory 'native-needed.txt'), (Join-Path $OutputDirectory 'native-toolchain.txt'), (Join-Path $OutputDirectory 'native-abi-versions.txt')
Test-Receipt
Write-Host 'Built and verified the Linux Vulkan runtime with corrected readback semaphore ownership.'
