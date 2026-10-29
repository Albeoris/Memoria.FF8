[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$product = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$temporary = Join-Path ([IO.Path]::GetTempPath()) ('memoria-publication-tests-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $temporary | Out-Null
$source = 'a' * 40
$global:MemoriaPublicationTestCalls = [Collections.Generic.List[string[]]]::new()
# Exercise publication offline; no external gh executable is called.
function gh {
    $global:MemoriaPublicationTestCalls.Add([string[]]$args)
    $global:LASTEXITCODE = 0
    if ($args[0] -eq 'api' -and $args[1] -like '*/git/ref/heads/main') {
        return $source
    }
}
try {
    foreach ($rid in @('win-x64', 'linux-x64', 'osx-x64', 'osx-arm64')) {
        $extension = if ($rid -eq 'win-x64') { '.zip' } else { '.tar.gz' }
        $archive = Join-Path $temporary ('Memoria-FF8-' + $rid + $extension)
        [IO.File]::WriteAllText($archive, $rid)
        @{ source = $source; rid = $rid; recipe = 'b' * 64; sha256 = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant() } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $temporary ('receipt-' + $rid + '.json')) -Encoding utf8NoBOM
    }
    $publisher = Join-Path $product 'tools/Publish-Distributions.ps1'
    & $publisher -Directory $temporary -Source $source -Reference main
    $upload = @($global:MemoriaPublicationTestCalls | Where-Object { $_[0] -eq 'release' -and $_[1] -eq 'upload' })
    if ($upload.Count -ne 1 -or @($upload[0] | Where-Object { $_ -like '*Memoria-FF8-*.zip#*' -or $_ -like '*Memoria-FF8-*.tar.gz#*' }).Count -ne 4 -or @($upload[0] | Where-Object { $_ -like '*Memoria-FF8-osx-arm64.tar.gz#macOS_Apple_Silicon_arm64_*' }).Count -ne 1) {
        throw 'Publication must upload all four native archives with their platform labels.'
    }
    $publication = Get-Content -LiteralPath (Join-Path $temporary 'release-artifacts.json') -Raw | ConvertFrom-Json
    if ($publication.artifacts.Count -ne 4) {
        throw 'The release digest inventory must contain all four native targets.'
    }
    Remove-Item -LiteralPath (Join-Path $temporary 'receipt-osx-arm64.json')
    $global:MemoriaPublicationTestCalls.Clear()
    $rejected = $false
    try {
        & $publisher -Directory $temporary -Source $source -Reference main
    }
    catch {
        $rejected = $true
    }
    if (!$rejected -or $global:MemoriaPublicationTestCalls.Count -ne 0) {
        throw 'A missing Apple Silicon receipt must reject publication before any release operation.'
    }
    Write-Output 'Four-target publication and missing ARM64 receipt rejection passed offline.'
}
finally {
    if ([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($temporary)) -ne [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar)) {
        throw 'Test cleanup escaped its dedicated temporary root.'
    }
    Remove-Item -LiteralPath $temporary -Recurse -Force
}
