[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidatePattern('^v[0-9]+\.[0-9]+\.[0-9]+(?:\.[0-9]+)?$')][string]$Tag,
    [Parameter(Mandatory)][ValidatePattern('^[0-9a-f]{40,64}$')][string]$Source,
    [Parameter(Mandatory)][string]$Repository
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Invoke-Git([string[]]$Arguments) {
    $result = & git @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not read Git history for the release notes.'
    }
    $result
}

function Invoke-Gh([string[]]$Arguments) {
    $result = & gh @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not read GitHub release notes data.'
    }
    $result
}

if ((Invoke-Git @('rev-parse', '--is-shallow-repository')) -eq 'true') {
    throw 'Release notes require a checkout with full history and tags.'
}
if ((Invoke-Git @('rev-parse', '--verify', "refs/tags/$Tag^{commit}")) -cne $Source) {
    throw "The tag $Tag does not point to the validated release source."
}

$versionPattern = '^v[0-9]+\.[0-9]+\.[0-9]+(?:\.[0-9]+)?$'
$currentVersion = [version]$Tag.Substring(1)
$localTags = @(Invoke-Git @('tag', '--list'))
$publishedTags = @(Invoke-Gh @('api', '--paginate', "repos/$Repository/releases?per_page=100", '--jq', '.[] | select(.draft == false and .prerelease == false) | .tag_name'))
$candidates = $publishedTags | Where-Object { $_ -cmatch $versionPattern -and $_ -cin $localTags -and [version]$_.Substring(1) -lt $currentVersion } | Sort-Object { [version]$_.Substring(1) } -Descending
$previousTag = $null
foreach ($candidate in $candidates) {
    & git merge-base --is-ancestor "refs/tags/$candidate" $Source
    if ($LASTEXITCODE -eq 0) {
        $previousTag = $candidate
        break
    }
    if ($LASTEXITCODE -ne 1) {
        throw "Could not check ancestry for $candidate."
    }
}

$generate = @('api', '--method', 'POST', "repos/$Repository/releases/generate-notes", '-f', "tag_name=$Tag", '--jq', '.body')
$range = $Source
if ($previousTag) {
    $generate += @('-f', "previous_tag_name=$previousTag")
    $range = "refs/tags/$previousTag..${Source}"
}
$notes = (Invoke-Gh $generate) -join "`n"

$server = if ($env:GITHUB_SERVER_URL) { $env:GITHUB_SERVER_URL.TrimEnd('/') } else { 'https://github.com' }
$repositoryUrl = "$server/$Repository"
$pullPattern = [regex]::Escape("$repositoryUrl/pull/") + '([0-9]+)\b'
$listedPulls = @([regex]::Matches($notes, $pullPattern) | ForEach-Object { $_.Groups[1].Value })
# First-parent history follows the release line. The range exclusion still
# follows every parent of the previous release, including publication merges.
$commits = @(Invoke-Git @('log', '--first-parent', '--no-merges', '--format=%H%x09%s', $range))
$entries = foreach ($commit in $commits) {
    $sha, $subject = $commit -split "`t", 2
    if ($listedPulls.Count -gt 0) {
        $pulls = @(Invoke-Gh @('api', '--paginate', "repos/$Repository/commits/$sha/pulls?per_page=100", '--jq', '.[] | select(.merged_at != null) | .number'))
        if ($pulls | Where-Object { $_ -in $listedPulls }) {
            continue
        }
    }
    $escapedSubject = [Net.WebUtility]::HtmlEncode($subject) -replace '([\\`*_~\[\]])', '\$1'
    "* [$($sha.Substring(0, 8))]($repositoryUrl/commit/$sha) $escapedSubject"
}

if ($entries) {
    $heading = [regex]::Match($notes, "(?m)^## What's Changed\r?$")
    $commitNotes = $entries -join "`n"
    if ($heading.Success) {
        $notes = $notes.Insert($heading.Index + $heading.Length, "`n`n$commitNotes`n")
    }
    else {
        $notes = "## What's Changed`n`n$commitNotes`n`n$notes"
    }
}
$notes.TrimEnd()
