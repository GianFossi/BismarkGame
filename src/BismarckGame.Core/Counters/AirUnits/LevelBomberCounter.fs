namespace BismarckGame.Core.Counters.AirUnits

/// <summary>
/// Level Bomber (rule 2.432). Basic Game bomber sub-type. Note rule
/// 2.432: German level bomber air units have no reconnaissance mode
/// (unlike British bombers) — that asymmetry is per-instance state
/// (AirUnitMode in Units.fs), not modeled as a class difference here
/// since it depends on nationality, not on being a Level Bomber per se.
/// </summary>
type LevelBomberCounter(id, name, nationality, enduranceRating, searchStrengthDay, searchStrengthNight) =
    inherit AirUnitCounter(id, name, nationality, enduranceRating, searchStrengthDay, searchStrengthNight)
    override _.ClassCode = "LB"
