#requires -Version 7.0
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$tokens = $null
$errors = $null
$scriptPath = Join-Path $PSScriptRoot '../Invoke-CompleteCatalog.ps1'
$ast = [Management.Automation.Language.Parser]::ParseFile($scriptPath, [ref]$tokens, [ref]$errors)
if ($errors.Count) { throw 'Complete catalog script failed parsing.' }
$function = @($ast.FindAll({ param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Get-OrdinaryCatalogCase' }, $false))
if ($function.Count -ne 1) { throw 'Expected one ordinary scenario selection helper.' }
. ([scriptblock]::Create($function[0].Extent.Text))
$directory = [IO.Path]::GetFullPath((Join-Path ([IO.Path]::GetTempPath()) ('catalog-selection-' + [Guid]::NewGuid().ToString('N'))))
[IO.Directory]::CreateDirectory($directory) > $null
try {
    $stock = Join-Path $directory 'stock.json'
    $slots = Join-Path $directory 'slots.json'
    '{"driver":{"expected_effect":9,"input":{"ability":"Magic"}}}' | Set-Content -LiteralPath $stock
    '{"driver":{"expected_effect":9,"input":{"ability":"LimitBreak"},"slots_admission":{"magic":53}}}' | Set-Content -LiteralPath $slots
    $ordinary = [pscustomobject]@{ id='a-stock'; status='RUNNABLE'; effect_id=9; recipe=$stock; recipe_sha256=(Get-FileHash -LiteralPath $stock).Hash }
    $owned = [pscustomobject]@{ id='z-owned'; status='RUNNABLE'; effect_id=9; recipe=$slots; recipe_sha256=(Get-FileHash -LiteralPath $slots).Hash }
    if ((Get-OrdinaryCatalogCase @($ordinary, $owned) 9).id -ne 'z-owned') { throw 'Declared native invocation owner must precede lexical stocked Magic.' }
    if ((Get-OrdinaryCatalogCase @($owned, $ordinary) 9).id -ne 'z-owned') { throw 'Declared owner selection must be independent of manifest order.' }
    if ((Get-OrdinaryCatalogCase @($ordinary) 9).id -ne 'a-stock') { throw 'An ordinary supported candidate must remain selectable.' }
    if (@(Get-OrdinaryCatalogCase @($ordinary) 9 @('a-stock')).Count -ne 0) { throw 'A cached unsupported stocked invocation cannot remain runnable without a declared owner.' }
    if ((Get-OrdinaryCatalogCase @($ordinary, $owned) 9 @('a-stock')).id -ne 'z-owned') { throw 'Current runtime capability exclusions must preserve the declared native owner.' }
    if (@(Get-OrdinaryCatalogCase @($ordinary, $owned) 10).Count -ne 0) { throw 'Another effect cannot supply an ordinary candidate.' }
    $owned.status = 'PLANNED_BLOCKED'
    if ((Get-OrdinaryCatalogCase @($ordinary, $owned) 9).id -ne 'a-stock') { throw 'Blocked declared owners cannot replace runnable candidates.' }
    $owned.status = 'RUNNABLE'
    '{}' | Set-Content -LiteralPath $slots
    $rejected = $false
    try { Get-OrdinaryCatalogCase @($ordinary, $owned) 9 > $null } catch { $rejected = $true }
    if (-not $rejected) { throw 'Selection must authenticate candidate recipe bytes before considering owner declarations.' }
}
finally {
    Remove-Item -LiteralPath (Join-Path $directory 'stock.json'), (Join-Path $directory 'slots.json')
    [IO.Directory]::Delete($directory)
}
