namespace BismarckGame.Core.Counters.Ships

/// <summary>
/// CV — Aircraft Carrier (rule 2.422). E.g. Victorious, Ark Royal, Eagle,
/// Graf Zeppelin. The one ship class that cannot patrol (rule 2.423) and
/// cannot shadow (Shadow Table card).
/// </summary>
type AircraftCarrier(id, name, nationality, evasionRating, searchStrengthDay, searchStrengthNight) =
    inherit ShipCounter(id, name, nationality, evasionRating, searchStrengthDay, searchStrengthNight)
    override _.ClassCode = "CV"
    override _.CanPatrol = false
    override _.CanShadow = false
