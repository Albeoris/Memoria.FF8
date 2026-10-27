param(
    [Parameter(Mandatory = $true)]
    [string]$CheckerAssembly,
    [string]$Dx11Host = (Join-Path $PSScriptRoot '../artifacts/package/Data/Game/dx11'),
    [string]$VulkanHost = (Join-Path $PSScriptRoot '../artifacts/package/Data/Game/vulkan'),
    [string]$ShaderManifest = (Join-Path $PSScriptRoot 'shader-manifest.json')
)

$ErrorActionPreference = 'Stop'
$CheckerAssembly = [IO.Path]::GetFullPath($CheckerAssembly)
$Dx11Host = [IO.Path]::GetFullPath($Dx11Host)
$VulkanHost = [IO.Path]::GetFullPath($VulkanHost)
$ShaderManifest = [IO.Path]::GetFullPath($ShaderManifest)
$fixture = Join-Path ([IO.Path]::GetTempPath()) ('memoria-graphics-payload-' + [Guid]::NewGuid().ToString('N'))
$fixtureManifest = Join-Path $fixture 'shader-manifest.json'

function Assert-Payload([string]$Directory, [string]$Backend, [string]$Manifest, [bool]$Expected, [string]$Scenario)
{
    $output = & dotnet $CheckerAssembly --graphics-payload $Directory $Backend $Manifest 2>&1
    if (($LASTEXITCODE -eq 0) -ne $Expected)
    {
        throw "Unexpected result for $Scenario`: $output"
    }
    Write-Host "PASS: $Scenario"
}

function Reset-Fixture
{
    [IO.Directory]::CreateDirectory($fixture) | Out-Null
    foreach ($name in @('Memoria.FF8.dll', 'Memoria.FF8.TitleScene.dll', 'MonoGame.Framework.dll', 'Memoria.FF8.deps.json', 'mgruntime.dll'))
    {
        Copy-Item -LiteralPath (Join-Path $VulkanHost $name) -Destination (Join-Path $fixture $name) -Force
    }
    Copy-Item -LiteralPath $ShaderManifest -Destination $fixtureManifest -Force
}

function Write-Manifest($Manifest)
{
    $Manifest | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $fixtureManifest
}

try
{
    Assert-Payload $Dx11Host 'Direct3D11' $ShaderManifest $true 'Packaged DX11 host passes'
    Assert-Payload $VulkanHost 'Vulkan' $ShaderManifest $true 'Packaged Vulkan host passes'
    Reset-Fixture
    Assert-Payload $fixture 'Vulkan' $fixtureManifest $true 'Copied manifest validates without source checkout or framework load'

    Copy-Item -LiteralPath (Join-Path $Dx11Host 'MonoGame.Framework.dll') -Destination (Join-Path $fixture 'MonoGame.Framework.dll') -Force
    Assert-Payload $fixture 'Vulkan' $fixtureManifest $false 'DX11 framework cannot satisfy Vulkan deps metadata'
    Reset-Fixture

    Copy-Item -LiteralPath (Join-Path $Dx11Host 'Memoria.FF8.dll') -Destination (Join-Path $fixture 'Memoria.FF8.dll') -Force
    Assert-Payload $fixture 'Vulkan' $fixtureManifest $false 'DX11 embedded scene/frame shaders rejected by Vulkan host'
    Reset-Fixture

    Copy-Item -LiteralPath (Join-Path $Dx11Host 'Memoria.FF8.TitleScene.dll') -Destination (Join-Path $fixture 'Memoria.FF8.TitleScene.dll') -Force
    Assert-Payload $fixture 'Vulkan' $fixtureManifest $false 'DX11 title shader rejected by Vulkan host'
    Reset-Fixture

    $depsPath = Join-Path $fixture 'Memoria.FF8.deps.json'
    (Get-Content -LiteralPath $depsPath -Raw).Replace('MonoGame.Framework.Native', 'MonoGame.Framework.WindowsDX') | Set-Content -LiteralPath $depsPath
    Assert-Payload $fixture 'Vulkan' $fixtureManifest $false 'Wrong dependency family rejected'
    Reset-Fixture

    Copy-Item -LiteralPath (Join-Path $VulkanHost 'Memoria.FF8.TitleScene.dll') -Destination (Join-Path $fixture 'mgruntime.dll') -Force
    Assert-Payload $fixture 'Vulkan' $fixtureManifest $false 'Managed image cannot masquerade as native runtime'
    Reset-Fixture

    $manifest = Get-Content -LiteralPath $fixtureManifest -Raw | ConvertFrom-Json
    $manifest.compiler.version = '3.8.5.2'
    Write-Manifest $manifest
    Assert-Payload $fixture 'Vulkan' $fixtureManifest $false 'Compiler version drift rejected'
    Reset-Fixture

    $manifest = Get-Content -LiteralPath $fixtureManifest -Raw | ConvertFrom-Json
    $manifest.effects[1].sourceSha256 = '0' * 64
    Write-Manifest $manifest
    Assert-Payload $fixture 'Vulkan' $fixtureManifest $false 'Source hash mismatch across profile variants rejected'
    Reset-Fixture

    $manifest = Get-Content -LiteralPath $fixtureManifest -Raw | ConvertFrom-Json
    $manifest.effects[1].outputSha256 = '0' * 64
    Write-Manifest $manifest
    Assert-Payload $fixture 'Vulkan' $fixtureManifest $false 'Embedded artifact hash mismatch rejected'
}
finally
{
    $temporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    $resolvedFixture = [IO.Path]::GetFullPath($fixture)
    if ($resolvedFixture.StartsWith($temporaryRoot, [StringComparison]::OrdinalIgnoreCase) -and [IO.Path]::GetFileName($resolvedFixture).StartsWith('memoria-graphics-payload-', [StringComparison]::Ordinal))
    {
        Remove-Item -LiteralPath $resolvedFixture -Recurse -Force
    }
}
