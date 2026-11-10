[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Destination,
    [switch]$BuildSupport
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$version = '1.4.2'
$sha256 = 'f95765a6229cb7b915990a2890ce12ebe36a730b021545d3d52ae69ce4c4024e'
$cache = Join-Path (Split-Path $PSScriptRoot -Parent) 'artifacts/dependencies'
New-Item -ItemType Directory -Force -Path $cache | Out-Null
$archive = Join-Path $cache "MoltenVK-$version-macos.tar"
if (!(Test-Path -LiteralPath $archive) -or (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant() -cne $sha256) {
    $pending = $archive + '.' + [Guid]::NewGuid().ToString('N') + '.tmp'
    try {
        Invoke-WebRequest -Uri "https://github.com/KhronosGroup/MoltenVK/releases/download/v$version/MoltenVK-macos.tar" -OutFile $pending
        if ((Get-FileHash -LiteralPath $pending -Algorithm SHA256).Hash.ToLowerInvariant() -cne $sha256) {
            throw 'The pinned MoltenVK release checksum does not match.'
        }
        Move-Item -LiteralPath $pending -Destination $archive -Force
    }
    finally {
        if (Test-Path -LiteralPath $pending) {
            Remove-Item -LiteralPath $pending
        }
    }
}
$extraction = Join-Path $cache ('moltenvk-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $extraction | Out-Null
$staticLibrary = 'MoltenVK/MoltenVK/static/MoltenVK.xcframework/macos-arm64_x86_64/libMoltenVK.a'
$files = if ($BuildSupport) {
    @('MoltenVK/MoltenVK/include', $staticLibrary, 'MoltenVK/LICENSE')
} else {
    @('MoltenVK/MoltenVK/dynamic/dylib/macOS/libMoltenVK.dylib', 'MoltenVK/LICENSE')
}
& tar -xf $archive -C $extraction @files
if ($LASTEXITCODE -ne 0) {
    throw 'The pinned MoltenVK archive could not be extracted.'
}
New-Item -ItemType Directory -Force -Path $Destination | Out-Null
if ($BuildSupport) {
    Copy-Item -LiteralPath (Join-Path $extraction 'MoltenVK/MoltenVK/include') -Destination $Destination -Recurse -Force
    Copy-Item -LiteralPath (Join-Path $extraction $staticLibrary) -Destination $Destination
} else {
    Copy-Item -LiteralPath (Join-Path $extraction 'MoltenVK/MoltenVK/dynamic/dylib/macOS/libMoltenVK.dylib') -Destination $Destination
}
Copy-Item -LiteralPath (Join-Path $extraction 'MoltenVK/LICENSE') -Destination (Join-Path $Destination 'MoltenVK.LICENSE')
# Only this freshly generated extraction directory is removed; the verified archive is reused.
if ([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($extraction)) -ne [IO.Path]::GetFullPath($cache)) {
    throw 'Dependency extraction escaped its dedicated cache.'
}
Remove-Item -LiteralPath $extraction -Recurse -Force
