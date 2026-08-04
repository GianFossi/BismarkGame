namespace BismarckGame.Core.Counters.Ships

/// <summary>
/// DD/CT — Destroyer (CT = Contre-Torpilleur, fast French destroyer).
/// Rule 2.422: "not used in Basic Game." Included here for completeness
/// and for the Intermediate Game (rule 23.0, "Destroyers") once that
/// level is modeled.
/// </summary>
type Destroyer(id, name, nationality, evasionRating, searchStrengthDay, searchStrengthNight) =
    inherit ShipCounter(id, name, nationality, evasionRating, searchStrengthDay, searchStrengthNight)
    override _.ClassCode = "DD"
    override _.IsBasicGameUnit = false
