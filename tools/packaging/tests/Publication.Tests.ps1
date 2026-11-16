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
$global:MemoriaPublicationReleaseNotes = $null
$global:MemoriaPublishedAssets = @()
$global:MemoriaUpdateFeed = $null
$global:MemoriaPendingUpdateFeed = $null
$global:MemoriaCorruptPublishedAsset = $false
# Exercise publication offline; no external gh executable is called.
function gh {
    $global:MemoriaPublicationTestCalls.Add([string[]]$args)
    $global:LASTEXITCODE = 0
    if ($args[0] -eq 'api' -and $args[1] -like '*/git/ref/heads/main') {
        return $source
    }
    if ($args[0] -eq 'release' -and $args[1] -eq 'upload') {
        $tag = $args[2]
        $global:MemoriaPublishedAssets = @($args | Where-Object { $_ -like '*Memoria-FF8-*#*' } | ForEach-Object {
            $path, $label = $_.Split('#', 2)
            $name = [IO.Path]::GetFileName($path)
            @{
                name = $name; label = $label; state = 'uploaded'; size = (Get-Item -LiteralPath $path).Length
                digest = 'sha256:' + (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
                browser_download_url = "https://github.com/$env:GITHUB_REPOSITORY/releases/download/$tag/$name"
                updated_at = '2026-11-16T12:00:00Z'
            }
        })
        return
    }
    if ($args[0] -eq 'api' -and $args[1] -like '*/releases/tags/*') {
        $tag = ($args[1] -split '/')[-1]
        if ($global:MemoriaCorruptPublishedAsset) {
            $global:MemoriaPublishedAssets[0].digest = 'sha256:' + ('0' * 64)
        }
        return @{ tag_name = $tag; name = $tag; draft = $false; prerelease = $tag -eq 'nightly'; assets = $global:MemoriaPublishedAssets } | ConvertTo-Json -Depth 6
    }
    if ($args[0] -eq 'api' -and $args[1] -like '*/git/ref/heads/updates') {
        if ($null -eq $global:MemoriaUpdateFeed) {
            $global:LASTEXITCODE = 1
            return 'gh: Not Found (HTTP 404)'
        }
        return '{"object":{"sha":"test-update-feed-commit"}}'
    }
    if ($args[0] -eq 'api' -and $args[1] -like '*/contents/updates-v1.json?ref=*') {
        $content = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($global:MemoriaUpdateFeed))
        return @{ content = $content; sha = 'previous-feed-blob' } | ConvertTo-Json
    }
    if ($args[0] -eq 'api' -and $args -contains '--input') {
        $body = Get-Content -LiteralPath $args[([Array]::IndexOf($args, '--input') + 1)] -Raw | ConvertFrom-Json -AsHashtable
        if ($args[3] -like '*/git/trees') {
            $global:MemoriaPendingUpdateFeed = $body.tree[0].content
            return '{"sha":"test-tree"}'
        }
        if ($args[3] -like '*/git/commits') {
            if ($body.parents.Count -ne 0 -or $body.tree -cne 'test-tree') {
                throw 'The first feed commit must contain only the new feed tree.'
            }
            return '{"sha":"test-update-feed-commit"}'
        }
        if ($args[3] -like '*/git/refs') {
            if ($body.ref -cne 'refs/heads/updates' -or $body.sha -cne 'test-update-feed-commit') {
                throw 'The first feed must publish to the updates branch.'
            }
            $global:MemoriaUpdateFeed = $global:MemoriaPendingUpdateFeed
            return '{}'
        }
        if ($args[3] -like '*/contents/updates-v1.json') {
            if ($body.branch -cne 'updates' -or $body.sha -cne 'previous-feed-blob') {
                throw 'Feed updates must compare against the previous feed blob.'
            }
            $global:MemoriaUpdateFeed = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($body.content))
            return '{}'
        }
    }
    if ($args[0] -eq 'release' -and $args[1] -eq 'view') {
        $global:LASTEXITCODE = 1
    }
    if ($args[0] -eq 'api' -and $args[2] -like '*/releases?per_page=100') {
        return 'v1.2.2', 'v1.2.0', 'v1.2.10', 'v1.2.1'
    }
    if ($args[0] -eq 'api' -and $args[3] -like '*/releases/generate-notes') {
        return @"
## What's Changed
* Squashed change by @contributor in https://github.com/$env:GITHUB_REPOSITORY/pull/42

**Full Changelog**: https://github.com/$env:GITHUB_REPOSITORY/compare/v1.2.1...v1.2.3
"@
    }
    if ($args[0] -eq 'api' -and $args[2] -like '*/commits/*/pulls?per_page=100' -and $args[2] -like "*/commits/$squash/pulls?per_page=100") {
        return '42'
    }
    if ($args[0] -eq 'release' -and $args[1] -eq 'create' -and $args -contains '--notes-file') {
        $global:MemoriaPublicationReleaseNotes = Get-Content -LiteralPath $args[([Array]::IndexOf($args, '--notes-file') + 1)] -Raw
    }
}
function Invoke-TestGit([string[]]$Arguments) {
    $output = & git -C $temporary -c user.name='Release Notes Test' -c user.email='release-notes@example.invalid' @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not construct the release history fixture.'
    }
    $output
}
$savedRepository = $env:GITHUB_REPOSITORY
$savedServer = $env:GITHUB_SERVER_URL
$env:GITHUB_REPOSITORY = 'owner/repository'
$env:GITHUB_SERVER_URL = 'https://github.com'
try {
    foreach ($rid in @('win-x64', 'linux-x64', 'osx-x64', 'osx-arm64')) {
        $extension = if ($rid -eq 'win-x64') { '.zip' } else { '.tar.gz' }
        $archive = Join-Path $temporary ('Memoria-FF8-' + $rid + $extension)
        [IO.File]::WriteAllText($archive, $rid)
        @{ source = $source; rid = $rid; recipe = 'b' * 64; version = 'source'; engineCompatibilityVersion = '0.8.10'; macSignature = 'adhoc-v1'; sha256 = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant() } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $temporary ('receipt-' + $rid + '.json')) -Encoding utf8NoBOM
    }
    # A versioning test double records any accidental mutation by the publisher;
    # archive metadata tooling is covered separately by Packaging.Tests.ps1.
    $testTools = Join-Path $temporary 'tools'
    New-Item -ItemType Directory -Path $testTools | Out-Null
    $publisher = Join-Path $testTools 'Publish-Distributions.ps1'
    Copy-Item -LiteralPath (Join-Path $product 'tools/Publish-Distributions.ps1') -Destination $publisher
    Copy-Item -LiteralPath (Join-Path $product 'tools/Get-ReleaseNotes.ps1') -Destination $testTools
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
    $nightlyFeed = $global:MemoriaUpdateFeed | ConvertFrom-Json -AsHashtable
    if ($nightlyFeed.schemaVersion -ne 1 -or $nightlyFeed.channels.Count -ne 1 -or $nightlyFeed.channels.nightly.buildId -cne $source -or $nightlyFeed.channels.nightly.engineCompatibilityVersion -cne '0.8.10' -or $nightlyFeed.channels.nightly.artifacts.Count -ne 4) {
        throw 'The static feed must describe the verified nightly platform set.'
    }
    $savedFeed = $global:MemoriaUpdateFeed
    $global:MemoriaCorruptPublishedAsset = $true
    $rejected = $false
    try {
        & $publisher -Directory $temporary -Source $source -Reference main
    }
    catch {
        if ($_.Exception.Message -notlike '*does not match the validated archive*') { throw }
        $rejected = $true
    }
    finally {
        $global:MemoriaCorruptPublishedAsset = $false
    }
    if (!$rejected -or $savedFeed -cne $global:MemoriaUpdateFeed) {
        throw 'An unverified uploaded asset must leave the feed untouched.'
    }
    $macReceiptPath = Join-Path $temporary 'receipt-osx-arm64.json'
    $macReceiptText = Get-Content -LiteralPath $macReceiptPath -Raw
    foreach ($property in @('macSignature', 'version', 'engineCompatibilityVersion')) {
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
    $null = Invoke-TestGit @('init', '--quiet', '--object-format=sha1')
    $tree = Invoke-TestGit @('write-tree')
    $root = Invoke-TestGit @('commit-tree', $tree, '-m', 'Old release')
    $prior = Invoke-TestGit @('commit-tree', $tree, '-p', $root, '-m', 'Prior release on second parent')
    $unrelated = Invoke-TestGit @('commit-tree', $tree, '-p', $root, '-m', 'Unrelated release')
    $direct = Invoke-TestGit @('commit-tree', $tree, '-p', $root, '-m', 'Direct change with [brackets] & a <tag>')
    $squash = Invoke-TestGit @('commit-tree', $tree, '-p', $direct, '-m', 'Squash subject already covered by PR')
    $source = Invoke-TestGit @('commit-tree', $tree, '-p', $squash, '-p', $prior, '-m', 'Publication merge')
    Invoke-TestGit @('tag', 'v1.2.0', $root)
    Invoke-TestGit @('tag', 'v1.2.1', $prior)
    Invoke-TestGit @('tag', 'v1.2.2', $unrelated)
    Invoke-TestGit @('tag', 'v1.2.2.1', $direct)
    Invoke-TestGit @('tag', $version, $source)
    Invoke-TestGit @('tag', 'v1.2.10', $source)
    foreach ($receiptPath in (Get-ChildItem -LiteralPath $temporary -Filter 'receipt-*.json')) {
        $receipt = Get-Content -LiteralPath $receiptPath.FullName -Raw | ConvertFrom-Json
        $receipt.source = $source
        $receipt | ConvertTo-Json | Set-Content -LiteralPath $receiptPath.FullName -Encoding utf8NoBOM
    }
    $global:MemoriaPublicationTestCalls.Clear()
    Push-Location -LiteralPath $temporary
    try {
        & $publisher -Directory $temporary -Source $source -Reference $version
    }
    finally {
        Pop-Location
    }
    $create = @($global:MemoriaPublicationTestCalls | Where-Object { $_[0] -eq 'release' -and $_[1] -eq 'create' })
    if ($create.Count -ne 1 -or $create[0][2] -cne $version -or $create[0] -notcontains '--verify-tag' -or $create[0] -contains '--target' -or $create[0] -contains '--prerelease') {
        throw 'Versioned release creation must verify the existing tag without specifying a target commit.'
    }
    if ($global:MemoriaPublicationVersionCalls.Count -ne 0) {
        throw 'Tagged publication must reuse every tested archive without version stamping.'
    }
    $generate = @($global:MemoriaPublicationTestCalls | Where-Object { $_.Length -gt 3 -and $_[3] -like '*/releases/generate-notes' })
    if ($generate.Count -ne 1 -or $generate[0] -notcontains 'previous_tag_name=v1.2.1') {
        throw 'Release notes must compare against the preceding published stable ancestor, including second parents.'
    }
    $notes = $global:MemoriaPublicationReleaseNotes
    if ($notes -notlike "*$direct*" -or !$notes.Contains('Direct change with \[brackets\] &amp; a &lt;tag&gt;') -or $notes -notlike '*/pull/42*' -or $notes -notlike '*/compare/v1.2.1...v1.2.3*' -or $notes -match 'Squash subject|Old release|Publication merge|Prior release') {
        throw 'Release notes must combine direct commits and GitHub pull requests without repeating commits already represented by a PR.'
    }
    $publication = Get-Content -LiteralPath (Join-Path $temporary 'release-artifacts.json') -Raw | ConvertFrom-Json
    if ($publication.channel -cne $version -or $publication.source -cne $source) {
        throw 'The release identity must be recorded outside the immutable packages.'
    }
    $feed = $global:MemoriaUpdateFeed | ConvertFrom-Json -AsHashtable
    if ($feed.channels.Count -ne 2 -or $feed.channels.nightly.buildId -cne $nightlyFeed.channels.nightly.buildId -or $feed.channels.release.tag -cne $version -or $feed.channels.release.buildId -cne $source -or $feed.channels.release.artifacts.Count -ne 4) {
        throw 'Publishing a tagged release must update the stable channel and preserve the nightly channel.'
    }
    foreach ($artifact in $publication.artifacts) {
        $rid = $artifact.name -replace '^Memoria-FF8-', '' -replace '\.(zip|tar\.gz)$', ''
        $receipt = Get-Content -LiteralPath (Join-Path $temporary "receipt-$rid.json") -Raw | ConvertFrom-Json
        if ($artifact.sha256 -cne $receipt.sha256 -or (Get-FileHash -LiteralPath (Join-Path $temporary $artifact.name) -Algorithm SHA256).Hash.ToLowerInvariant() -cne $receipt.sha256) {
            throw 'Both release channels must retain the tested archive digest.'
        }
        $staticArtifact = $feed.channels.release.artifacts[$rid]
        if ($staticArtifact.sha256 -cne $receipt.sha256 -or $staticArtifact.url -cne "https://github.com/$env:GITHUB_REPOSITORY/releases/download/$version/$($artifact.name)" -or $staticArtifact.size -ne (Get-Item -LiteralPath (Join-Path $temporary $artifact.name)).Length) {
            throw 'The static feed must publish the verified digest, size and anonymous download URL.'
        }
    }
    Write-Output 'Four-target publication, release notes, immutable archive reuse, invalid receipt rejection and verified static feed publication passed offline.'
}
finally {
    $env:GITHUB_REPOSITORY = $savedRepository
    $env:GITHUB_SERVER_URL = $savedServer
    if ([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($temporary)) -ne [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar)) {
        throw 'Test cleanup escaped its dedicated temporary root.'
    }
    Remove-Item -LiteralPath $temporary -Recurse -Force
}
