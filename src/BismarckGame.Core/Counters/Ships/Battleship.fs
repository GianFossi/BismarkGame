namespace BismarckGame.Core.Counters.Ships

/// <summary>
/// BB — Battleship (rule 2.422). E.g. Bismarck, King George V, Rodney,
/// Ramillies. No behavior differs from the ShipCounter base beyond the
/// class code — battleships are the "default" capital ship.
/// </summary>
type Battleship(id, name, nationality, evasionRating, searchStrengthDay, searchStrengthNight) =
    inherit ShipCounter(id, name, nationality, evasionRating, searchStrengthDay, searchStrengthNight)
    override _.ClassCode = "BB"
