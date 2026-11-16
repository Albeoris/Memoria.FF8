[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
# dotnet resolves global.json itself, including rollForward and allowPrerelease.
# Hosted images normally have a compatible SDK; setup-dotnet is the fallback.
$available = $false
if (Get-Command dotnet -ErrorAction SilentlyContinue) {
    & dotnet --version
    $available = $LASTEXITCODE -eq 0
}
"available=$($available.ToString().ToLowerInvariant())" | Add-Content -LiteralPath $env:GITHUB_OUTPUT
# GitHub Actions propagates the last native exit code from pwsh steps.
exit 0
