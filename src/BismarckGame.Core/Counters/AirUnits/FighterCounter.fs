namespace BismarckGame.Core.Counters.AirUnits

/// <summary>
/// Fighter Air Unit — Intermediate Game only (rule 28.0, "Fighter Air
/// Units"). Not used in the Basic Game.
/// </summary>
type FighterCounter(id, name, nationality, enduranceRating, searchStrengthDay, searchStrengthNight) =
    inherit AirUnitCounter(id, name, nationality, enduranceRating, searchStrengthDay, searchStrengthNight)
    override _.ClassCode = "FT"
    override _.IsBasicGameUnit = false
