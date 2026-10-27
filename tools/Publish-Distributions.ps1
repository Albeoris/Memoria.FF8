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
$archives = @()
foreach ($rid in @('win-x64', 'linux-x64', 'osx-x64')) {
    $receipt = Get-Content -LiteralPath (Join-Path $Directory "receipt-$rid.json") -Raw | ConvertFrom-Json
    if ($receipt.source -ne $Source -or $receipt.rid -ne $rid -or $receipt.recipe -notmatch '^[0-9a-f]{64}$') {
        throw "The validated $rid receipt does not match the release source."
    }
    $extension = if ($rid -eq 'win-x64') { '.zip' } else { '.tar.gz' }
    $path = Join-Path $Directory "Memoria-FF8-$rid$extension"
    if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() -cne $receipt.sha256) {
        throw "The tested $rid artifact digest changed."
    }
    $archives += $path
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
else {
    foreach ($archive in $archives) {
        & (Join-Path $PSScriptRoot 'Set-DistributionVersion.ps1') -ArchivePath $archive -Version $Reference -BuildId $Source
    }
}
# Record post-metadata archive digests separately from immutable tested receipts.
$publication = @{ source = $Source; channel = $tag; artifacts = @($archives | ForEach-Object { @{ name = [IO.Path]::GetFileName($_); sha256 = (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash.ToLowerInvariant() } }) }
$publication | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $Directory 'release-artifacts.json') -Encoding utf8NoBOM
& gh release view $tag --repo $env:GITHUB_REPOSITORY *> $null
if ($LASTEXITCODE -ne 0) {
    $create = @('release', 'create', $tag, '--repo', $env:GITHUB_REPOSITORY, '--target', $Source, '--generate-notes')
    if ($tag -eq 'nightly') {
        $create += '--prerelease'
    }
    else {
        $create += '--verify-tag'
    }
    Invoke-Gh $create
}
if ($tag -eq 'nightly') {
    Invoke-Gh @('api', '--method', 'PATCH', "repos/$env:GITHUB_REPOSITORY/git/refs/tags/nightly", '-f', "sha=$Source", '-F', 'force=true')
}
$labels = @{
    'Memoria-FF8-win-x64.zip' = 'Windows_x64_'
    'Memoria-FF8-linux-x64.tar.gz' = 'Linux_x64_'
    'Memoria-FF8-osx-x64.tar.gz' = 'macOS_Intel_x64_'
}
$upload = @('release', 'upload', $tag, '--repo', $env:GITHUB_REPOSITORY, '--clobber') + @($archives | ForEach-Object { $_ + '#' + $labels[[IO.Path]::GetFileName($_)] + $Source }) + @((Join-Path $Directory 'release-artifacts.json'))
Invoke-Gh $upload
