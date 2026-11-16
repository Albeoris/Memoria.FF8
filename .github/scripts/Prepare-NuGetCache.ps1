#Requires -Version 7.2
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Directory,
    [string]$GlobalPackagesDirectory,
    [switch]$VerifyOnly
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$product = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$inventory = Get-Content -LiteralPath (Join-Path $product '.github/nuget/packages.lock.json') -Raw | ConvertFrom-Json
$sdk = Get-Content -LiteralPath (Join-Path $product 'global.json') -Raw | ConvertFrom-Json
if ($inventory.sdkVersion -cne $sdk.sdk.version -or $sdk.sdk.rollForward -cne 'disable' -or $inventory.packages.Count -eq 0) {
    throw 'The shared NuGet lock must match the exact SDK in global.json and list at least one package.'
}

$destination = [IO.Path]::GetFullPath($Directory)
$expected = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$ids = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($package in $inventory.packages) {
    if ($package.id -cnotmatch '^[a-z0-9][a-z0-9._-]*$' -or $package.version -cnotmatch '^[0-9][a-z0-9.-]*$' -or $package.sha512 -cnotmatch '^[0-9a-f]{128}$' -or !$ids.Add($package.id)) {
        throw "Invalid or duplicate NuGet lock entry: $($package.id)."
    }
    [void]$expected.Add("$($package.id).$($package.version).nupkg")
}

if (!$VerifyOnly) {
    New-Item -ItemType Directory -Force -Path $destination | Out-Null
}
foreach ($item in Get-ChildItem -LiteralPath $destination -Force) {
    if ($item.PSIsContainer -or !$expected.Contains($item.Name) -or $item.Attributes.HasFlag([IO.FileAttributes]::ReparsePoint)) {
        throw "Unexpected entry in the shared NuGet feed: $($item.Name)."
    }
}
$packageBytes = $inventory.packages | ForEach-Object -Parallel {
    $ErrorActionPreference = 'Stop'
    $ProgressPreference = 'SilentlyContinue'
    $package = $_
    $name = "$($package.id).$($package.version).nupkg"
    $path = Join-Path $using:destination $name
    if (!(Test-Path -LiteralPath $path -PathType Leaf)) {
        if ($using:VerifyOnly) {
            throw "The shared NuGet feed is missing $name."
        }
        $temporary = "$path.download"
        try {
            $existing = if ($using:GlobalPackagesDirectory) {
                Join-Path $using:GlobalPackagesDirectory "$($package.id)/$($package.version)/$name"
            }
            if ($existing -and (Test-Path -LiteralPath $existing -PathType Leaf)) {
                Copy-Item -LiteralPath $existing -Destination $temporary
            }
            else {
                Invoke-WebRequest -Uri "https://api.nuget.org/v3-flatcontainer/$($package.id)/$($package.version)/$name" -OutFile $temporary -MaximumRetryCount 3 -RetryIntervalSec 2
            }
            if ((Get-FileHash -LiteralPath $temporary -Algorithm SHA512).Hash.ToLowerInvariant() -cne $package.sha512) {
                throw "NuGet package SHA-512 mismatch: $name."
            }
            Move-Item -LiteralPath $temporary -Destination $path
        }
        finally {
            if (Test-Path -LiteralPath $temporary) {
                Remove-Item -LiteralPath $temporary
            }
        }
    }
    elseif ((Get-FileHash -LiteralPath $path -Algorithm SHA512).Hash.ToLowerInvariant() -cne $package.sha512) {
        throw "NuGet package SHA-512 mismatch: $name."
    }
    (Get-Item -LiteralPath $path).Length
} -ThrottleLimit 8

$size = ($packageBytes | Measure-Object -Sum).Sum
Write-Host "Verified shared NuGet feed: $($expected.Count) locked packages, $size bytes."
