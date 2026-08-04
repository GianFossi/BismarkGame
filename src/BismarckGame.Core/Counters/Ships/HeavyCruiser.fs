namespace BismarckGame.Core.Counters.Ships

/// <summary>
/// CA — Heavy Cruiser (rule 2.422). E.g. Norfolk, Suffolk, Dorsetshire,
/// Prinz Eugen, Hipper.
/// </summary>
type HeavyCruiser(id, name, nationality, evasionRating, searchStrengthDay, searchStrengthNight) =
    inherit ShipCounter(id, name, nationality, evasionRating, searchStrengthDay, searchStrengthNight)
    override _.ClassCode = "CA"
