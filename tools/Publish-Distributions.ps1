[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Directory,
    [Parameter(Mandatory)][ValidatePattern('^[0-9a-f]{40,64}$')][string]$Source,
    [Parameter(Mandatory)][string]$Reference
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
function Invoke-Gh([string[]]$Arguments) {
    & gh @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw 'GitHub release operation failed.'
    }
}
function Invoke-GhJson([string[]]$Arguments, [System.Collections.IDictionary]$Body) {
    $inputPath = [IO.Path]::GetTempFileName()
    try {
        [IO.File]::WriteAllText($inputPath, ($Body | ConvertTo-Json -Depth 12 -Compress))
        Invoke-Gh ($Arguments + @('--input', $inputPath)) | ConvertFrom-Json -AsHashtable
    }
    finally {
        Remove-Item -LiteralPath $inputPath
    }
}
function Read-UpdatesReference {
    $result = & gh api "repos/$env:GITHUB_REPOSITORY/git/ref/heads/updates" 2>&1
    if ($LASTEXITCODE -eq 0) {
        return $result | ConvertFrom-Json -AsHashtable
    }
    if (($result -join "`n") -notmatch 'HTTP 404\b') {
        throw "Could not read the updates branch: $($result -join ' ')"
    }
    return $null
}
function Publish-UpdateFeed([string]$Channel, [System.Collections.IDictionary]$Entry) {
    $repository = "repos/$env:GITHUB_REPOSITORY"
    $reference = Read-UpdatesReference
    $feed = @{ schemaVersion = 1; channels = @{} }
    $previousSha = $null
    if ($null -ne $reference) {
        $current = Invoke-Gh @('api', "$repository/contents/updates-v1.json?ref=$($reference.object.sha)") | ConvertFrom-Json -AsHashtable
        $previousSha = $current.sha
        $feed = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($current.content)) | ConvertFrom-Json -AsHashtable
        if ($feed.schemaVersion -ne 1 -or $feed.channels -isnot [System.Collections.IDictionary]) {
            throw 'The existing engine update feed has an unsupported schema.'
        }
        if ($Channel -eq 'release' -and $feed.channels.Contains('release') -and [version]$feed.channels.release.tag.Substring(1) -gt [version]$Entry.tag.Substring(1)) {
            Write-Host 'Keeping the newer stable channel in the update feed.'
            return
        }
    }
    $feed.channels[$Channel] = $Entry
    $json = ($feed | ConvertTo-Json -Depth 10) + "`n"
    $message = "Publish $($Entry.tag) engine update metadata"
    if ($null -ne $reference) {
        $content = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($json))
        $null = Invoke-GhJson @('api', '--method', 'PUT', "$repository/contents/updates-v1.json") @{ branch = 'updates'; sha = $previousSha; message = $message; content = $content }
        return
    }
    $tree = Invoke-GhJson @('api', '--method', 'POST', "$repository/git/trees") @{ tree = @(@{ path = 'updates-v1.json'; mode = '100644'; type = 'blob'; content = $json }) }
    $commit = Invoke-GhJson @('api', '--method', 'POST', "$repository/git/commits") @{ message = $message; tree = $tree.sha; parents = @() }
    $null = Invoke-GhJson @('api', '--method', 'POST', "$repository/git/refs") @{ ref = 'refs/heads/updates'; sha = $commit.sha }
}
$archives = @()
$validated = [ordered]@{}
$engineVersion = $null
foreach ($rid in @('win-x64', 'linux-x64', 'osx-x64', 'osx-arm64')) {
    $receipt = Get-Content -LiteralPath (Join-Path $Directory "receipt-$rid.json") -Raw | ConvertFrom-Json
    if ($receipt.source -ne $Source -or $receipt.rid -ne $rid -or $receipt.recipe -notmatch '^[0-9a-f]{64}$') {
        throw "The validated $rid receipt does not match the release source."
    }
    $extension = if ($rid -eq 'win-x64') { '.zip' } else { '.tar.gz' }
    $path = Join-Path $Directory "Memoria-FF8-$rid$extension"
    if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() -cne $receipt.sha256) {
        throw "The tested $rid artifact digest changed."
    }
    if (!$receipt.PSObject.Properties['version'] -or $receipt.version -cne 'source') {
        throw "The $rid receipt must describe an immutable source payload."
    }
    if (!$receipt.PSObject.Properties['engineCompatibilityVersion'] -or $receipt.engineCompatibilityVersion -cnotmatch '^\d+\.\d+\.\d+(?:\.\d+)?(?:-[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?(?:\+[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?$' -or ($null -ne $engineVersion -and $receipt.engineCompatibilityVersion -cne $engineVersion)) {
        throw "The $rid receipt must declare the same engine compatibility version as every other platform."
    }
    $engineVersion = $receipt.engineCompatibilityVersion
    if ($rid -like 'osx-*' -and (!$receipt.PSObject.Properties['macSignature'] -or $receipt.macSignature -cne 'adhoc-v1')) {
        throw "The $rid archive needs native signature validation."
    }
    $archives += $path
    $validated[$rid] = @{ name = [IO.Path]::GetFileName($path); sha256 = $receipt.sha256; size = (Get-Item -LiteralPath $path).Length }
}
$tag = $Reference
if ($Reference -eq 'main') {
    $main = & gh api "repos/$env:GITHUB_REPOSITORY/git/ref/heads/main" --jq .object.sha
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not resolve current main.'
    }
    if ($main -ne $Source) {
        Write-Host 'Skipping nightly publication because this source is no longer current main.'
        return
    }
    $tag = 'nightly'
}
elseif ($Reference -notmatch '^v[0-9]+\.[0-9]+\.[0-9]+(?:\.[0-9]+)?$') {
    throw 'Unsupported release reference.'
}
# Publish the original tested bytes to every channel, including signed bundles.
$publication = @{ source = $Source; channel = $tag; artifacts = @($archives | ForEach-Object { @{ name = [IO.Path]::GetFileName($_); sha256 = (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash.ToLowerInvariant() } }) }
$publication | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $Directory 'release-artifacts.json') -Encoding utf8NoBOM
& gh release view $tag --repo $env:GITHUB_REPOSITORY *> $null
if ($LASTEXITCODE -ne 0) {
    $create = @('release', 'create', $tag, '--repo', $env:GITHUB_REPOSITORY)
    if ($tag -eq 'nightly') {
        Invoke-Gh ($create + @('--target', $Source, '--prerelease', '--generate-notes'))
    }
    else {
        $notes = & (Join-Path $PSScriptRoot 'Get-ReleaseNotes.ps1') -Tag $tag -Source $Source -Repository $env:GITHUB_REPOSITORY
        $notesFile = [IO.Path]::GetTempFileName()
        try {
            [IO.File]::WriteAllText($notesFile, $notes)
            Invoke-Gh ($create + @('--verify-tag', '--notes-file', $notesFile))
        }
        finally {
            Remove-Item -LiteralPath $notesFile
        }
    }
}
if ($tag -eq 'nightly') {
    Invoke-Gh @('api', '--method', 'PATCH', "repos/$env:GITHUB_REPOSITORY/git/refs/tags/nightly", '-f', "sha=$Source", '-F', 'force=true')
}
$labels = @{
    'Memoria-FF8-win-x64.zip' = 'Windows_x64_'
    'Memoria-FF8-linux-x64.tar.gz' = 'Linux_x64_'
    'Memoria-FF8-osx-x64.tar.gz' = 'macOS_Intel_x64_'
    'Memoria-FF8-osx-arm64.tar.gz' = 'macOS_Apple_Silicon_arm64_'
}
$upload = @('release', 'upload', $tag, '--repo', $env:GITHUB_REPOSITORY, '--clobber') + @($archives | ForEach-Object { $_ + '#' + $labels[[IO.Path]::GetFileName($_)] + $Source }) + @((Join-Path $Directory 'release-artifacts.json'))
Invoke-Gh $upload

$release = Invoke-Gh @('api', "repos/$env:GITHUB_REPOSITORY/releases/tags/$tag") | ConvertFrom-Json -AsHashtable
if ($release.tag_name -cne $tag -or $release.draft -ne $false -or $release.prerelease -ne ($tag -eq 'nightly')) {
    throw 'The uploaded release does not match the intended channel.'
}
$published = [ordered]@{}
$publishedAt = [DateTimeOffset]::MinValue
foreach ($rid in $validated.Keys) {
    $expected = $validated[$rid]
    $assets = @($release.assets | Where-Object { $_.name -ceq $expected.name })
    $url = "https://github.com/$env:GITHUB_REPOSITORY/releases/download/$tag/$($expected.name)"
    if ($assets.Count -ne 1 -or $assets[0].state -cne 'uploaded' -or $assets[0].label -cne ($labels[$expected.name] + $Source) -or $assets[0].digest -cne "sha256:$($expected.sha256)" -or $assets[0].size -ne $expected.size -or $assets[0].browser_download_url -cne $url) {
        throw "The uploaded $rid asset does not match the validated archive. The update feed was not changed."
    }
    $updatedAt = [DateTimeOffset]$assets[0].updated_at
    if ($updatedAt -gt $publishedAt) {
        $publishedAt = $updatedAt
    }
    $published[$rid] = @{ url = $url; size = $expected.size; sha256 = $expected.sha256 }
}
$channel = if ($tag -eq 'nightly') { 'nightly' } else { 'release' }
$name = if ([string]::IsNullOrWhiteSpace($release.name)) { $tag } else { $release.name }
Publish-UpdateFeed $channel @{ tag = $tag; name = $name; buildId = $Source; engineCompatibilityVersion = $engineVersion; publishedAt = $publishedAt.ToUniversalTime().ToString('o'); artifacts = $published }
