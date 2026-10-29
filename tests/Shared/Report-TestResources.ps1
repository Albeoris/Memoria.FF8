param([string[]] $Projects)

$ErrorActionPreference = 'Stop'
$catalog = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'optional-fixture-inputs.json') -Raw | ConvertFrom-Json -AsHashtable
$requirements = foreach ($project in $catalog.Keys) {
    if (!$Projects -or $Projects.Where({ ([IO.Path]::GetFileName($_) -replace '\.csproj$', '') -eq $project }).Count -gt 0) {
        $catalog[$project]
    }
}
$missing = @($requirements | Sort-Object -Unique | Where-Object {
    $configured = @($_ -split '\|' | Where-Object { ![string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($_)) })
    $configured.Count -eq 0
})
if ($missing.Count -gt 0) {
    Write-Host "Optional fixture inputs unset: $($missing -join ', '). Dependent checks will be skipped; skip totals remain in the test summary."
}
