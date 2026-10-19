[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ArchivePath,
    [Parameter(Mandatory)][ValidatePattern('\Av[0-9]+\.[0-9]+\.[0-9]+(?:\.[0-9]+)?\z')][string]$Version,
    [Parameter(Mandatory)][ValidatePattern('\A[0-9a-fA-F]{40}(?:[0-9a-fA-F]{24})?\z')][string]$BuildId
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$manifestName = 'Data/distribution.json'
$encoding = [Text.UTF8Encoding]::new($false, $true)

function Read-ArchiveState([string]$Path) {
    $archive = [IO.Compression.ZipFile]::OpenRead($Path)
    try {
        $entries = [ordered]@{}
        $manifestText = $null
        foreach ($entry in $archive.Entries) {
            if ($entries.Contains($entry.FullName)) {
                throw "Duplicate archive entry: $($entry.FullName)"
            }
            $stream = $entry.Open()
            try {
                $hash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream))
                $entries.Add($entry.FullName, "$($entry.Length):$hash")
                if ($entry.FullName -ceq $manifestName) {
                    $manifestStream = $entry.Open()
                    $buffer = [IO.MemoryStream]::new()
                    try {
                        $manifestStream.CopyTo($buffer)
                        $manifestText = $encoding.GetString($buffer.ToArray())
                    }
                    finally {
                        $buffer.Dispose()
                        $manifestStream.Dispose()
                    }
                }
            }
            finally {
                $stream.Dispose()
            }
        }
        if ($null -eq $manifestText) {
            throw "Archive is missing $manifestName."
        }
        $document = [Text.Json.JsonDocument]::Parse($manifestText.TrimStart([char]0xfeff))
        try {
            $root = $document.RootElement
            if ($root.ValueKind -ne [Text.Json.JsonValueKind]::Object) {
                throw 'The distribution manifest must be an object.'
            }
            $names = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
            foreach ($property in $root.EnumerateObject()) {
                if (-not $names.Add($property.Name)) {
                    throw "Duplicate manifest property: $($property.Name)"
                }
            }
            if ($root.GetProperty('SchemaVersion').GetRawText() -cne '1') {
                throw 'Unsupported distribution manifest schema.'
            }
            $source = $root.GetProperty('BuildId').GetString()
            if ($source -notmatch '\A[0-9a-fA-F]{40}(?:[0-9a-fA-F]{24})?\z' -or -not [string]::Equals($source, $BuildId, [StringComparison]::OrdinalIgnoreCase)) {
                throw 'The distribution source identity does not match the requested build.'
            }
            if ([string]::IsNullOrWhiteSpace($root.GetProperty('Version').GetString()) -or [string]::IsNullOrWhiteSpace($root.GetProperty('BuiltAt').GetString()) -or $root.GetProperty('Files').ValueKind -ne [Text.Json.JsonValueKind]::Array) {
                throw 'The distribution manifest is incomplete.'
            }
            return [pscustomobject]@{ Text = $manifestText; Entries = $entries; Version = $root.GetProperty('Version').GetString() }
        }
        finally {
            $document.Dispose()
        }
    }
    finally {
        $archive.Dispose()
    }
}

# Locate the top-level string token so every other manifest byte remains intact,
# including unknown properties, number formatting and the inventory ordering.
function Set-VersionText([string]$Text) {
    $tokens = [regex]::Matches($Text, '"(?:[^"\\]|\\.)*"|[{}\[\]:,]')
    $depth = 0
    for ($index = 0; $index -lt $tokens.Count; $index++) {
        $token = $tokens[$index]
        if ($depth -eq 1 -and $token.Value.StartsWith('"') -and $index + 2 -lt $tokens.Count -and $tokens[$index + 1].Value -eq ':') {
            $propertyName = ConvertFrom-Json -InputObject $token.Value
            if ($propertyName -ceq 'Version') {
                $valueToken = $tokens[$index + 2]
                return $Text.Substring(0, $valueToken.Index) + '"' + $Version + '"' + $Text.Substring($valueToken.Index + $valueToken.Length)
            }
        }
        if ($token.Value -eq '{' -or $token.Value -eq '[') {
            $depth++
        }
        elseif ($token.Value -eq '}' -or $token.Value -eq ']') {
            $depth--
        }
    }
    throw 'The distribution manifest has no Version property.'
}

$path = (Get-Item -LiteralPath $ArchivePath).FullName
$before = Read-ArchiveState $path
if ($before.Version -ceq $Version) {
    Write-Output "Distribution already has version $Version."
    return
}
$updatedText = Set-VersionText $before.Text
$temporaryPath = Join-Path ([IO.Path]::GetDirectoryName($path)) ('.distribution-version-' + [Guid]::NewGuid().ToString('N') + '.zip')
try {
    [IO.File]::Copy($path, $temporaryPath, $false)
    $archive = [IO.Compression.ZipFile]::Open($temporaryPath, [IO.Compression.ZipArchiveMode]::Update)
    try {
        $stream = $archive.GetEntry($manifestName).Open()
        try {
            $stream.SetLength(0)
            $bytes = $encoding.GetBytes($updatedText)
            $stream.Write($bytes, 0, $bytes.Length)
        }
        finally {
            $stream.Dispose()
        }
    }
    finally {
        $archive.Dispose()
    }
    $after = Read-ArchiveState $temporaryPath
    if ($after.Text -cne $updatedText -or $after.Version -cne $Version -or $after.Entries.Count -ne $before.Entries.Count) {
        throw 'The updated archive failed manifest verification.'
    }
    foreach ($name in $before.Entries.Keys) {
        if (-not $after.Entries.Contains($name) -or ($name -cne $manifestName -and $after.Entries[$name] -cne $before.Entries[$name])) {
            throw "The updated archive changed an unrelated entry: $name"
        }
    }
    # Same-directory replacement keeps the original intact until validation succeeds.
    [IO.File]::Replace($temporaryPath, $path, [System.Management.Automation.Language.NullString]::Value)
    Write-Output "Set distribution version to $Version for build $BuildId."
}
finally {
    if ([IO.File]::Exists($temporaryPath)) {
        [IO.File]::Delete($temporaryPath)
    }
}
