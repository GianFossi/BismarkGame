param([string]$Root = (Split-Path -Parent $PSScriptRoot))
$ErrorActionPreference = "Stop"
$ids = @('5.28','7.23','9.64','9.716','9.717','9.84','9.94','9.98','9.99','17.3','19.7','23.53','27.52','40.7','41.5')
$tests = (Get-ChildItem (Join-Path $Root 'src\BismarckGame.Tests') -Filter '*.fs' -Recurse | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }) -join "`n"
$missing = $ids | Where-Object { $tests -notmatch [regex]::Escape($_) }
if ($missing) { throw "Missing dedicated audit coverage: $($missing -join ', ')" }
foreach ($id in $ids) {
    if ($tests -notmatch [regex]::Escape($id)) { throw "Rule $id has no test reference" }
}
Write-Host "Rule audit passed for $($ids.Count) numbered BASIC/errata rules."
