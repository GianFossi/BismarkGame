namespace BismarckGame.Core.Counters.Ships

/// <summary>
/// PB — Pocket Battleship (rule 2.422: "more accurately termed Armored
/// Cruiser"). E.g. Admiral Scheer. German Panzerschiffe — heavier-gunned
/// than a cruiser, lighter and faster than a battleship.
/// </summary>
type PocketBattleship(id, name, nationality, evasionRating, searchStrengthDay, searchStrengthNight) =
    inherit ShipCounter(id, name, nationality, evasionRating, searchStrengthDay, searchStrengthNight)
    override _.ClassCode = "PB"
