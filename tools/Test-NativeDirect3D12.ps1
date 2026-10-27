[CmdletBinding()]
param([Parameter(Mandatory)][string]$OutputDirectory)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$recipe = Join-Path $PSScriptRoot '../vendor/MonoGame.Native.DX12'
$lock = Get-Content (Join-Path $recipe 'native-runtime-lock.json') -Raw | ConvertFrom-Json
$receipt = Get-Content (Join-Path $OutputDirectory 'native-runtime.json') -Raw | ConvertFrom-Json
$patchText = [IO.File]::ReadAllText((Join-Path $recipe 'RenderTargetMSAA.patch')).Replace("`r`n", "`n")
$patchHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($patchText))).ToLowerInvariant()
if ($patchHash -cne $lock.patchSha256) {
    throw 'The current native source patch does not match its locked hash.'
}
function Get-Hash([string]$Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}
if ($receipt.schemaVersion -ne 1 -or $receipt.runtimeIdentifier -cne 'win-x64' -or $receipt.backend -cne 'Direct3D12' -or $receipt.package -cne $lock.package -or $receipt.version -cne $lock.version -or $receipt.upstream.commit -cne $lock.upstream.commit -or $receipt.patchSha256 -cne $lock.patchSha256) {
    throw 'The prepared Direct3D 12 runtime does not match the pinned runtime recipe.'
}
if ($receipt.recipeSha256 -cne (Get-Hash (Join-Path $recipe 'CMakeLists.txt')) -or $receipt.buildScriptSha256 -cne (Get-Hash (Join-Path $PSScriptRoot 'Build-NativeDirect3D12.ps1'))) {
    throw 'The prepared Direct3D 12 runtime was built with a different build recipe.'
}
if (@($receipt.dependencies).Count -ne @($lock.dependencies).Count -or $receipt.upstream.repository -cne $lock.upstream.repository) {
    throw 'The prepared runtime has unexpected source dependencies.'
}
foreach ($dependency in $lock.dependencies) {
    $actual = @($receipt.dependencies | Where-Object { $_.path -ceq $dependency.path -and $_.repository -ceq $dependency.repository -and $_.commit -ceq $dependency.commit })
    if ($actual.Count -ne 1) {
        throw 'The prepared runtime has an unexpected source dependency identity.'
    }
}
if ($receipt.shaderCompiler.package -cne $lock.shaderCompiler.package -or $receipt.shaderCompiler.version -cne $lock.shaderCompiler.version -or $receipt.shaderCompiler.profile -cne $lock.shaderCompiler.profile -or $receipt.configuration -cne 'Release' -or $receipt.exportedSymbols -ne 140) {
    throw 'The prepared runtime compiler, configuration or exported ABI differs from the pinned contract.'
}
if (@($receipt.files).Count -ne 1 -or $receipt.files[0].path -cne 'mgruntime.dll' -or $receipt.files[0].sha256 -cne (Get-Hash (Join-Path $OutputDirectory 'mgruntime.dll'))) {
    throw 'The prepared native runtime binary does not match its build receipt.'
}
foreach ($license in Get-ChildItem -LiteralPath (Join-Path $recipe 'licenses') -File) {
    if ((Get-Hash $license.FullName) -cne (Get-Hash (Join-Path $OutputDirectory ('licenses/' + $license.Name)))) {
        throw 'The prepared runtime has incomplete dependency notices.'
    }
}
Write-Host 'Verified the pinned Direct3D 12 native runtime and dependency notices.'
