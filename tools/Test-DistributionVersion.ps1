[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$scriptPath = Join-Path $PSScriptRoot 'Set-DistributionVersion.ps1'
$testRoot = Join-Path ([IO.Path]::GetTempPath()) ('distribution-version-test-' + [Guid]::NewGuid().ToString('N'))
$buildId = '0123456789abcdef0123456789abcdef01234567'
$encoding = [Text.UTF8Encoding]::new($false)

function Assert-Equal($Expected, $Actual, [string]$Message) {
    if ($Expected -cne $Actual) {
        throw $Message
    }
}

function Get-ArchiveContents([string]$Path) {
    $archive = [IO.Compression.ZipFile]::OpenRead($Path)
    try {
        $result = [ordered]@{}
        foreach ($entry in $archive.Entries) {
            $stream = $entry.Open()
            $buffer = [IO.MemoryStream]::new()
            try {
                $stream.CopyTo($buffer)
                $result.Add($entry.FullName, [Convert]::ToBase64String($buffer.ToArray()))
            }
            finally {
                $buffer.Dispose()
                $stream.Dispose()
            }
        }
        return $result
    }
    finally {
        $archive.Dispose()
    }
}

function Assert-RejectedUnchanged([string]$Path, [string]$RequestedVersion, [string]$RequestedBuild) {
    $before = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
    $rejected = $false
    try {
        & $scriptPath -ArchivePath $Path -Version $RequestedVersion -BuildId $RequestedBuild | Out-Null
    }
    catch {
        $rejected = $true
    }
    if (-not $rejected) {
        throw 'Invalid relabel request was accepted.'
    }
    Assert-Equal $before (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash 'Rejected request changed the original archive bytes.'
}

try {
    [IO.Directory]::CreateDirectory($testRoot) | Out-Null
    $path = Join-Path $testRoot 'distribution.zip'
    $manifest = '{ "SchemaVersion": 1, "BuildId": "' + $buildId + '", "Version": "nightly", "BuiltAt": "2026-01-02T03:04:05.0000000+00:00", "Files": [{"Path":"Memoria.Launcher.exe","Size":7,"Sha256":"arbitrary-test-hash"}], "Extra":{"Version":"keep","Number":1.00} }' + "`n"
    $contents = [ordered]@{
        'Memoria.Launcher.exe' = [byte[]]@(0, 1, 2, 255, 3, 4, 5)
        'Data/payload.bin' = [byte[]]@(255, 0, 128, 42)
        'Data/distribution.json' = $encoding.GetBytes($manifest)
        'Data/empty/' = [byte[]]@()
    }
    $archive = [IO.Compression.ZipFile]::Open($path, [IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($name in $contents.Keys) {
            $stream = $archive.CreateEntry($name).Open()
            try {
                $stream.Write($contents[$name], 0, $contents[$name].Length)
            }
            finally {
                $stream.Dispose()
            }
        }
    }
    finally {
        $archive.Dispose()
    }
    $original = Get-ArchiveContents $path
    foreach ($version in @('v1.2.3', 'v2.3.4.5')) {
        & $scriptPath -ArchivePath $path -Version $version -BuildId $buildId | Out-Null
        $updated = Get-ArchiveContents $path
        Assert-Equal ($original.Keys -join "`n") ($updated.Keys -join "`n") 'Archive inventory changed.'
        foreach ($name in $original.Keys) {
            if ($name -cne 'Data/distribution.json') {
                Assert-Equal $original[$name] $updated[$name] "Payload changed: $name"
            }
        }
        $expected = $manifest.Replace('"Version": "nightly"', ('"Version": "' + $version + '"'))
        Assert-Equal $expected ($encoding.GetString([Convert]::FromBase64String($updated['Data/distribution.json']))) 'Manifest changed outside the top-level Version value.'
        $hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
        & $scriptPath -ArchivePath $path -Version $version -BuildId $buildId | Out-Null
        Assert-Equal $hash (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash 'Repeated relabel was not byte-idempotent.'
    }
    Assert-RejectedUnchanged $path 'v3.4.5' ('f' * 40)
    foreach ($invalid in @('1.2.3', 'nightly', 'v1.2', 'v1.2.3.4.5', 'v1.2.3-beta', "v1.2.3`n")) {
        Assert-RejectedUnchanged $path $invalid $buildId
    }
    Assert-RejectedUnchanged $path 'v3.4.5' 'not-a-sha'
    $archive = [IO.Compression.ZipFile]::Open($path, [IO.Compression.ZipArchiveMode]::Update)
    try {
        $stream = $archive.GetEntry('Data/distribution.json').Open()
        try {
            $stream.SetLength(0)
            $bytes = $encoding.GetBytes($manifest.Replace('"SchemaVersion": 1', '"SchemaVersion": 2'))
            $stream.Write($bytes, 0, $bytes.Length)
        }
        finally {
            $stream.Dispose()
        }
    }
    finally {
        $archive.Dispose()
    }
    Assert-RejectedUnchanged $path 'v3.4.5' $buildId
    Assert-Equal 1 @(Get-ChildItem -LiteralPath $testRoot -Force).Count 'Temporary update files were left behind.'
    Write-Output 'Distribution version tests passed: promotion, relabel, idempotence, rejected schema/identity/tags, inventory and payload preservation.'
}
finally {
    if ([IO.Directory]::Exists($testRoot)) {
        $resolvedTestRoot = [IO.Path]::GetFullPath($testRoot)
        $resolvedTempRoot = [IO.Path]::TrimEndingDirectorySeparator([IO.Path]::GetFullPath([IO.Path]::GetTempPath()))
        if ([IO.Path]::GetDirectoryName($resolvedTestRoot) -ne $resolvedTempRoot -or -not [IO.Path]::GetFileName($resolvedTestRoot).StartsWith('distribution-version-test-', [StringComparison]::Ordinal)) {
            throw 'The test cleanup path is outside the generated temporary directory.'
        }
        [IO.Directory]::Delete($testRoot, $true)
    }
}
