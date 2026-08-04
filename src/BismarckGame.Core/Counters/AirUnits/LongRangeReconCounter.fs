namespace BismarckGame.Core.Counters.AirUnits

/// <summary>
/// LR Recon — Long Range Reconnaissance (rule 2.43, 2.434). Greater
/// endurance, speed, and search capability than a bomber, but "can never
/// launch an air attack" — the one air unit type that is pure search.
/// </summary>
type LongRangeReconCounter(id, name, nationality, enduranceRating, searchStrengthDay, searchStrengthNight) =
    inherit AirUnitCounter(id, name, nationality, enduranceRating, searchStrengthDay, searchStrengthNight)
    override _.ClassCode = "LR"
    override _.CanAttack = false
