namespace BismarckGame.Core.Counters.AirUnits

/// <summary>
/// Torpedo Bomber (rule 2.432). Basic Game bomber sub-type; resolved on
/// the British Torpedo Bomber Result table (Tables/BomberTables.fs) —
/// distinct from Level Bomber, which uses a different result table.
/// </summary>
type TorpedoBomberCounter(id, name, nationality, enduranceRating, searchStrengthDay, searchStrengthNight) =
    inherit AirUnitCounter(id, name, nationality, enduranceRating, searchStrengthDay, searchStrengthNight)
    override _.ClassCode = "TB"
