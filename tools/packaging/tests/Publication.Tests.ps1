[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$product = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$temporary = Join-Path ([IO.Path]::GetTempPath()) ('memoria-publication-tests-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $temporary | Out-Null
$source = 'a' * 40
$global:MemoriaPublicationTestCalls = [Collections.Generic.List[string[]]]::new()
$global:MemoriaPublicationVersionCalls = [Collections.Generic.List[object]]::new()
# Exercise publication offline; no external gh executable is called.
function gh {
    $global:MemoriaPublicationTestCalls.Add([string[]]$args)
    $global:LASTEXITCODE = 0
    if ($args[0] -eq 'api' -and $args[1] -like '*/git/ref/heads/main') {
        return $source
    }
    if ($args[0] -eq 'release' -and $args[1] -eq 'view') {
        $global:LASTEXITCODE = 1
    }
}
try {
    foreach ($rid in @('win-x64', 'linux-x64', 'osx-x64', 'osx-arm64')) {
        $extension = if ($rid -eq 'win-x64') { '.zip' } else { '.tar.gz' }
        $archive = Join-Path $temporary ('Memoria-FF8-' + $rid + $extension)
        [IO.File]::WriteAllText($archive, $rid)
        @{ source = $source; rid = $rid; recipe = 'b' * 64; version = 'nightly'; macSignature = 'adhoc-v1'; sha256 = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant() } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $temporary ('receipt-' + $rid + '.json')) -Encoding utf8NoBOM
    }
    # Use the actual publication orchestrator with a versioning test double;
    # archive metadata behavior is covered by Packaging.Tests.ps1.
    $testTools = Join-Path $temporary 'tools'
    New-Item -ItemType Directory -Path $testTools | Out-Null
    $publisher = Join-Path $testTools 'Publish-Distributions.ps1'
    Copy-Item -LiteralPath (Join-Path $product 'tools/Publish-Distributions.ps1') -Destination $publisher
    Set-Content -LiteralPath (Join-Path $testTools 'Set-DistributionVersion.ps1') -Encoding utf8NoBOM -Value @'
param([string]$ArchivePath, [string]$Version, [string]$BuildId)
$global:MemoriaPublicationVersionCalls.Add(@{ ArchivePath = $ArchivePath; Version = $Version; BuildId = $BuildId })
'@
    & $publisher -Directory $temporary -Source $source -Reference main
    $create = @($global:MemoriaPublicationTestCalls | Where-Object { $_[0] -eq 'release' -and $_[1] -eq 'create' })
    if ($create.Count -ne 1 -or $create[0][2] -cne 'nightly' -or $create[0] -notcontains '--target' -or $create[0][[Array]::IndexOf($create[0], '--target') + 1] -cne $source -or $create[0] -notcontains '--prerelease' -or $global:MemoriaPublicationVersionCalls.Count -ne 0) {
        throw 'Nightly creation must target the validated main source without version stamping.'
    }
    $upload = @($global:MemoriaPublicationTestCalls | Where-Object { $_[0] -eq 'release' -and $_[1] -eq 'upload' })
    if ($upload.Count -ne 1 -or @($upload[0] | Where-Object { $_ -like '*Memoria-FF8-*.zip#*' -or $_ -like '*Memoria-FF8-*.tar.gz#*' }).Count -ne 4 -or @($upload[0] | Where-Object { $_ -like '*Memoria-FF8-osx-arm64.tar.gz#macOS_Apple_Silicon_arm64_*' }).Count -ne 1) {
        throw 'Publication must upload all four native archives with their platform labels.'
    }
    $publication = Get-Content -LiteralPath (Join-Path $temporary 'release-artifacts.json') -Raw | ConvertFrom-Json
    if ($publication.artifacts.Count -ne 4) {
        throw 'The release digest inventory must contain all four native targets.'
    }
    $macReceiptPath = Join-Path $temporary 'receipt-osx-arm64.json'
    $macReceiptText = Get-Content -LiteralPath $macReceiptPath -Raw
    foreach ($property in @('macSignature', 'version')) {
        $receipt = $macReceiptText | ConvertFrom-Json
        $receipt.$property = 'invalid'
        $receipt | ConvertTo-Json | Set-Content -LiteralPath $macReceiptPath -Encoding utf8NoBOM
        $global:MemoriaPublicationTestCalls.Clear()
        $rejected = $false
        try {
            & $publisher -Directory $temporary -Source $source -Reference main
        }
        catch {
            $rejected = $true
        }
        if (!$rejected -or $global:MemoriaPublicationTestCalls.Count -ne 0) {
            throw 'An unsigned or incorrectly versioned Mac receipt must reject publication before any release operation.'
        }
    }
    Set-Content -LiteralPath $macReceiptPath -Value $macReceiptText -Encoding utf8NoBOM
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
    Set-Content -LiteralPath $macReceiptPath -Value $macReceiptText -Encoding utf8NoBOM
    $version = 'v1.2.3'
    foreach ($rid in @('osx-x64', 'osx-arm64')) {
        $receiptPath = Join-Path $temporary "receipt-$rid.json"
        $receipt = Get-Content -LiteralPath $receiptPath -Raw | ConvertFrom-Json
        $receipt.version = $version
        $receipt | ConvertTo-Json | Set-Content -LiteralPath $receiptPath -Encoding utf8NoBOM
    }
    $global:MemoriaPublicationTestCalls.Clear()
    & $publisher -Directory $temporary -Source $source -Reference $version
    $create = @($global:MemoriaPublicationTestCalls | Where-Object { $_[0] -eq 'release' -and $_[1] -eq 'create' })
    if ($create.Count -ne 1 -or $create[0][2] -cne $version -or $create[0] -notcontains '--verify-tag' -or $create[0] -contains '--target' -or $create[0] -contains '--prerelease') {
        throw 'Versioned release creation must verify the existing tag without specifying a target commit.'
    }
    $stamped = @($global:MemoriaPublicationVersionCalls | ForEach-Object { [IO.Path]::GetFileName($_.ArchivePath) } | Sort-Object)
    if ($stamped.Count -ne 2 -or $stamped[0] -cne 'Memoria-FF8-linux-x64.tar.gz' -or $stamped[1] -cne 'Memoria-FF8-win-x64.zip' -or @($global:MemoriaPublicationVersionCalls | Where-Object { $_.Version -cne $version -or $_.BuildId -cne $source }).Count -ne 0) {
        throw 'Tagged publication must stamp only Windows and Linux archives with the validated release identity.'
    }
    Write-Output 'Four-target publication, nightly and existing-tag creation, version orchestration and invalid receipt rejection passed offline.'
}
finally {
    if ([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($temporary)) -ne [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar)) {
        throw 'Test cleanup escaped its dedicated temporary root.'
    }
    Remove-Item -LiteralPath $temporary -Recurse -Force
}
