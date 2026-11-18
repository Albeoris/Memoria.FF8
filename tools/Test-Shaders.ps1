param(
    [switch]$Compile
)

$ErrorActionPreference = 'Stop'
$productRoot = Split-Path $PSScriptRoot -Parent
$fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) ('memoria-shader-check-' + [Guid]::NewGuid().ToString('N'))
$checkScript = Join-Path $fixtureRoot 'tools/Update-Shaders.ps1'
$manifestRelative = 'tools/shader-manifest.json'

function Assert-DirectXBindings
{
    $manifest = Get-Content -LiteralPath (Join-Path $productRoot $manifestRelative) -Raw | ConvertFrom-Json
    foreach ($entry in ($manifest.effects | Where-Object { $_.profile -eq 'DirectX_11' }))
    {
        $stream = [IO.File]::OpenRead((Join-Path $productRoot $entry.output))
        $reader = [IO.BinaryReader]::new($stream)
        try
        {
            # MGFX v11 stores its signature, version, profile and hash before the effect data.
            $signature = [Text.Encoding]::ASCII.GetString($reader.ReadBytes(4))
            $version = $reader.ReadByte()
            $profile = $reader.ReadByte()
            if ($signature -ne 'MGFX' -or $version -ne 11 -or $profile -ne 1)
            {
                throw "Unexpected DX11 effect format: $($entry.output)."
            }
            $null = $reader.ReadInt32()
            $constantBufferCount = $reader.ReadInt32()
            for ($buffer = 0; $buffer -lt $constantBufferCount; $buffer++)
            {
                $null = $reader.ReadString()
                $null = $reader.ReadUInt16()
                $parameterCount = $reader.ReadInt32()
                $null = $reader.ReadBytes($parameterCount * 6)
            }
            $shaderCount = $reader.ReadInt32()
            for ($shader = 0; $shader -lt $shaderCount; $shader++)
            {
                $null = $reader.ReadBoolean()
                $null = $reader.ReadString()
                $entrypoint = $reader.ReadString()
                $null = $reader.ReadBytes($reader.ReadInt32())
                $samplerCount = $reader.ReadByte()
                $textureSlots = @()
                $samplerSlots = @()
                for ($sampler = 0; $sampler -lt $samplerCount; $sampler++)
                {
                    $null = $reader.ReadByte()
                    $textureSlot = $reader.ReadByte()
                    $samplerSlot = $reader.ReadByte()
                    if ($textureSlot -ge 16 -or $samplerSlot -ge 16)
                    {
                        throw "DX11 binding exceeds MonoGame's 16 slots: $($entry.output), $entrypoint, t$textureSlot/s$samplerSlot."
                    }
                    $textureSlots += $textureSlot
                    $samplerSlots += $samplerSlot
                    if ($reader.ReadBoolean())
                    {
                        # Three address modes, RGBA border, filter, two integers and a float.
                        $null = $reader.ReadBytes(20)
                    }
                    $null = $reader.ReadString()
                    $null = $reader.ReadByte()
                }
                if ($entry.source -like '*Triangles.fx' -and $samplerCount -gt 0)
                {
                    $expectedCount = if ($entrypoint -eq 'FieldPixelMain') { 9 } else { 8 }
                    $expectedSlots = (0..($expectedCount - 1)) -join ','
                    if ($samplerCount -ne $expectedCount -or (($textureSlots | Sort-Object) -join ',') -ne $expectedSlots -or (($samplerSlots | Sort-Object) -join ',') -ne $expectedSlots)
                    {
                        throw "DX11 triangle bindings must occupy t0..$($expectedCount - 1) and s0..$($expectedCount - 1): $entrypoint."
                    }
                }
                $null = $reader.ReadBytes($reader.ReadByte())
                $attributeCount = $reader.ReadByte()
                for ($attribute = 0; $attribute -lt $attributeCount; $attribute++)
                {
                    $null = $reader.ReadString()
                    $null = $reader.ReadBytes(4)
                }
            }
        }
        finally
        {
            $reader.Dispose()
        }
    }
    Write-Host 'PASS: DX11 effect bindings fit MonoGame slots and triangle bindings remain compact'
}

function Reset-Fixture
{
    $manifest = Get-Content -LiteralPath (Join-Path $productRoot $manifestRelative) -Raw | ConvertFrom-Json
    $files = @('tools/Update-Shaders.ps1', $manifestRelative, '.config/dotnet-tools.json')
    $files += @($manifest.effects | ForEach-Object { $_.source; $_.output })
    foreach ($file in ($files | Select-Object -Unique))
    {
        $destination = Join-Path $fixtureRoot $file
        [IO.Directory]::CreateDirectory((Split-Path $destination -Parent)) | Out-Null
        Copy-Item -LiteralPath (Join-Path $productRoot $file) -Destination $destination -Force
    }
}

function Assert-Check([bool]$ShouldPass, [string]$Scenario)
{
    $passed = $true
    try
    {
        & $checkScript -Check | Out-Null
    }
    catch
    {
        $passed = $false
    }
    if ($passed -ne $ShouldPass)
    {
        throw "Unexpected shader validation result: $Scenario."
    }
    Write-Host "PASS: $Scenario"
}

try
{
    Assert-DirectXBindings
    Reset-Fixture
    Assert-Check $true 'Committed artifacts validate from a different directory'
    if ($Compile)
    {
        Push-Location $fixtureRoot
        try
        {
            $manifest = Get-Content -LiteralPath (Join-Path $fixtureRoot $manifestRelative) -Raw | ConvertFrom-Json
            & dotnet tool restore
            if ($LASTEXITCODE -ne 0)
            {
                throw 'Could not restore the pinned shader compiler in the isolated fixture.'
            }
            foreach ($entry in $manifest.effects)
            {
                $fixtureOutput = Join-Path $fixtureRoot $entry.output
                # MGFX records the input path: retain the same product-relative path.
                & dotnet tool run mgfxc $entry.source $entry.output "/Profile:$($entry.profile)" "/Defines:$($entry.defines)"
                if ($LASTEXITCODE -ne 0 -or (Get-FileHash -LiteralPath $fixtureOutput -Algorithm SHA256).Hash.ToLowerInvariant() -cne $entry.outputSha256)
                {
                    throw "Shader did not reproduce from a separate source directory: $($entry.output)."
                }
            }
            Write-Host 'PASS: All payloads reproduce byte-for-byte from a separate source directory'
        }
        finally
        {
            Pop-Location
        }
    }

    $source = Join-Path $fixtureRoot 'src/Memoria.FF8.App/Shaders/Triangles.fx'
    $text = [IO.File]::ReadAllText($source).Replace("`r`n", "`n").Replace("`n", "`r`n")
    [IO.File]::WriteAllText($source, $text, [Text.UTF8Encoding]::new($false))
    Assert-Check $true 'CRLF checkout preserves source identity'

    [IO.File]::AppendAllText($source, "`n// changed source`n")
    Assert-Check $false 'Changed source requires regeneration'
    Reset-Fixture

    $manifestPath = Join-Path $fixtureRoot $manifestRelative
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    $entry = $manifest.effects | Where-Object { $_.source -like '*Triangles.fx' -and $_.profile -eq 'Vulkan' }
    $wrongOutput = Join-Path $fixtureRoot $entry.output
    Copy-Item -LiteralPath (Join-Path $fixtureRoot 'src/Memoria.FF8.App/Shaders/Triangles.mgfxo') -Destination $wrongOutput -Force
    $entry.outputSha256 = (Get-FileHash -LiteralPath $wrongOutput -Algorithm SHA256).Hash.ToLowerInvariant()
    $manifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $manifestPath
    Assert-Check $false 'DX11 payload cannot satisfy a Vulkan profile even with an updated hash'
    Reset-Fixture

    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    $entry = $manifest.effects | Where-Object { $_.source -like '*Triangles.fx' -and $_.profile -eq 'DirectX_12' }
    $wrongOutput = Join-Path $fixtureRoot $entry.output
    Copy-Item -LiteralPath (Join-Path $fixtureRoot 'src/Memoria.FF8.App/Shaders/Triangles.mgfxo') -Destination $wrongOutput -Force
    $entry.outputSha256 = (Get-FileHash -LiteralPath $wrongOutput -Algorithm SHA256).Hash.ToLowerInvariant()
    $manifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $manifestPath
    Assert-Check $false 'DX11 payload cannot satisfy the DX12 profile even with an updated hash'
    Reset-Fixture

    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    $entry = $manifest.effects | Where-Object { $_.source -like '*Triangles.fx' -and $_.profile -eq 'DirectX_12' }
    $entry.defines = ''
    $manifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $manifestPath
    Assert-Check $false 'DX12 compiler define drift is rejected'
    Reset-Fixture

    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    $manifest.effects = @($manifest.effects | Select-Object -Skip 1)
    $manifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $manifestPath
    Assert-Check $false 'Missing required profile is rejected'
    Reset-Fixture

    $toolPath = Join-Path $fixtureRoot '.config/dotnet-tools.json'
    (Get-Content -LiteralPath $toolPath -Raw).Replace('3.8.5.1', '3.8.5.2') | Set-Content -LiteralPath $toolPath
    Assert-Check $false 'Compiler pin drift is rejected'
}
finally
{
    # Only this test's freshly created temporary directory is removed.
    $temporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    $resolvedFixture = [IO.Path]::GetFullPath($fixtureRoot)
    if ($resolvedFixture.StartsWith($temporaryRoot, [StringComparison]::OrdinalIgnoreCase) -and [IO.Path]::GetFileName($resolvedFixture).StartsWith('memoria-shader-check-', [StringComparison]::Ordinal) -and (Test-Path -LiteralPath $resolvedFixture))
    {
        Remove-Item -LiteralPath $resolvedFixture -Recurse -Force
    }
}
