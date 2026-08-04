namespace BismarckGame.Core.Counters.AirUnits

/// <summary>
/// Dive Bomber. Appears as a named result table ("British Dive/Level
/// Bomber Result", Tables/BomberTables.fs) but it's not confirmed from
/// the rules transcribed so far whether the Basic Game actually issues a
/// separate Dive Bomber counter, or whether that table just covers
/// level-bomber attacks flown in a diving profile. Marked absent from
/// the Basic Game pending that confirmation — safer than assuming a
/// counter type exists that may not.
/// </summary>
type DiveBomberCounter(id, name, nationality, enduranceRating, searchStrengthDay, searchStrengthNight) =
    inherit AirUnitCounter(id, name, nationality, enduranceRating, searchStrengthDay, searchStrengthNight)
    override _.ClassCode = "DB"
    override _.IsBasicGameUnit = false
