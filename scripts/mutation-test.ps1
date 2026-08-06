param(
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$source = Join-Path $root "src\BismarckGame.Core\Update.fs"
$original = Get-Content -Raw -LiteralPath $source
$mutations = @(
    @{ Name = "search-phase-guard"; From = 'state.Phase <> Search'; To = 'state.Phase = Search' },
    @{ Name = "naval-phase-guard"; From = 'state.Phase <> NavalCombat'; To = 'state.Phase = NavalCombat' },
    @{ Name = "chance-phase-guard"; From = 'state.Phase <> Chance'; To = 'state.Phase = Chance' },
    @{ Name = "torpedo-phase-guard"; From = 'state.Phase <> TorpedoAttack'; To = 'state.Phase = TorpedoAttack' },
    @{ Name = "torpedo-consumption"; From = 'TorpedoesRemaining = s.TorpedoesRemaining - salvoes'; To = 'TorpedoesRemaining = s.TorpedoesRemaining' },
    @{ Name = "pending-target-validation"; From = 'if pending <= 0 then'; To = 'if pending <= 2 then' }
    ,@{ Name = "friendly-port-repair-eligibility"; From = 's.ZonesMovedThisTurn <= 1'; To = 's.ZonesMovedThisTurn < 0' }
)

try {
    foreach ($mutation in $mutations) {
        if (-not $original.Contains($mutation.From)) { throw "Mutation '$($mutation.Name)' no longer matches source" }
        $mutated = $original.Replace($mutation.From, $mutation.To)
        Set-Content -LiteralPath $source -Value $mutated -NoNewline
        dotnet test (Join-Path $root "BismarckGame.sln") --no-restore --configuration $Configuration
        if ($LASTEXITCODE -eq 0) { throw "Mutation '$($mutation.Name)' survived" }
        Write-Host "Killed mutation: $($mutation.Name)"
    }
}
finally {
    Set-Content -LiteralPath $source -Value $original -NoNewline
}

Write-Host "All mutations were detected."
