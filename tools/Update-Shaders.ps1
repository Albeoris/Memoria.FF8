param(
    [switch]$Check
)

$ErrorActionPreference = 'Stop'
$productRoot = Split-Path $PSScriptRoot -Parent
$manifestPath = Join-Path $PSScriptRoot 'shader-manifest.json'
$compilerVersion = '3.8.5.1'
$sources = @(
    'src/Memoria.FF8.App/Shaders/Triangles.fx',
    'src/Memoria.FF8.App/Shaders/FrameDifference.fx',
    'src/Memoria.FF8.TitleScene/Shaders/TitleScene.fx'
)
$profiles = @(
    @{ Name = 'DirectX_11'; Suffix = '.mgfxo'; Id = 1 },
    @{ Name = 'Vulkan'; Suffix = '.vulkan.mgfxo'; Id = 80 }
)

function Get-Sha256([string]$RelativePath)
{
    return (Get-FileHash -LiteralPath (Join-Path $productRoot $RelativePath) -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Get-SourceSha256([string]$RelativePath)
{
    # Source control may check text out as either LF or CRLF on different hosts.
    $source = [IO.File]::ReadAllText((Join-Path $productRoot $RelativePath)).Replace("`r`n", "`n")
    $sha = [Security.Cryptography.SHA256]::Create()
    try
    {
        return ([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($source)))).Replace('-', '').ToLowerInvariant()
    }
    finally
    {
        $sha.Dispose()
    }
}

function Assert-EffectProfile([string]$RelativePath, [int]$ProfileId)
{
    $bytes = [IO.File]::ReadAllBytes((Join-Path $productRoot $RelativePath))
    if ($bytes.Length -lt 10 -or [Text.Encoding]::ASCII.GetString($bytes, 0, 4) -cne 'MGFX' -or $bytes[4] -ne 11 -or $bytes[5] -ne $ProfileId)
    {
        throw "Unexpected MGFX format or backend profile in $RelativePath."
    }
}

$toolManifest = Get-Content -LiteralPath (Join-Path $productRoot '.config/dotnet-tools.json') -Raw | ConvertFrom-Json
if ($toolManifest.tools.'dotnet-mgfxc'.version -cne $compilerVersion)
{
    throw "Update the shader generator and regenerate the manifest for the pinned compiler version. Expected $compilerVersion."
}

Push-Location $productRoot
try
{
    if ($Check)
    {
        $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
        if ($manifest.schemaVersion -ne 1 -or $manifest.sourceNormalization -cne 'utf8-lf' -or $manifest.compiler.package -cne 'dotnet-mgfxc' -or $manifest.compiler.version -cne $compilerVersion -or @($manifest.effects).Count -ne $sources.Count * $profiles.Count)
        {
            throw 'Shader manifest schema, compiler or effect count does not match the required shader inventory.'
        }
        foreach ($source in $sources)
        {
            foreach ($profile in $profiles)
            {
                $output = $source.Substring(0, $source.Length - 3) + $profile.Suffix
                $entries = @($manifest.effects | Where-Object { $_.source -ceq $source -and $_.profile -ceq $profile.Name })
                if ($entries.Count -ne 1)
                {
                    throw "Expected exactly one manifest entry for $source / $($profile.Name)."
                }
                $entry = $entries[0]
                if ($entry.output -cne $output -or $entry.sourceSha256 -cne (Get-SourceSha256 $source) -or $entry.outputSha256 -cne (Get-Sha256 $output))
                {
                    throw "Stale shader source or output: $source / $($profile.Name). Run tools/Update-Shaders.ps1 on Windows and commit all generated artifacts."
                }
                Assert-EffectProfile $output $profile.Id
            }
        }
        Write-Host 'Validated all six shader artifacts, source/output hashes, compiler pin and backend profiles.'
    }
    else
    {
        if (-not $IsWindows -and $PSVersionTable.PSEdition -ne 'Desktop')
        {
            throw 'Generate shader artifacts on Windows; use -Check to validate committed artifacts on other hosts.'
        }
        & dotnet tool restore
        if ($LASTEXITCODE -ne 0)
        {
            throw 'Could not restore the pinned shader compiler.'
        }
        $effects = @()
        foreach ($source in $sources)
        {
            foreach ($profile in $profiles)
            {
                $output = $source.Substring(0, $source.Length - 3) + $profile.Suffix
                & dotnet tool run mgfxc $source $output "/Profile:$($profile.Name)"
                if ($LASTEXITCODE -ne 0)
                {
                    throw "Shader compilation failed: $source / $($profile.Name)."
                }
                Assert-EffectProfile $output $profile.Id
                $effects += [ordered]@{
                    source = $source
                    sourceSha256 = Get-SourceSha256 $source
                    profile = $profile.Name
                    output = $output
                    outputSha256 = Get-Sha256 $output
                }
            }
        }
        $manifest = [ordered]@{
            schemaVersion = 1
            sourceNormalization = 'utf8-lf'
            compiler = [ordered]@{ package = 'dotnet-mgfxc'; version = $compilerVersion }
            effects = $effects
        }
        $json = ($manifest | ConvertTo-Json -Depth 6) + "`n"
        [IO.File]::WriteAllText($manifestPath, $json.Replace("`r`n", "`n"), [Text.UTF8Encoding]::new($false))
        Write-Host 'Generated all six shader artifacts and tools/shader-manifest.json.'
    }
}
finally
{
    Pop-Location
}
