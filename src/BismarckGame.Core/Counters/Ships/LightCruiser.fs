namespace BismarckGame.Core.Counters.Ships

/// <summary>
/// CL — Light Cruiser (rule 2.422). E.g. Arethusa, Manchester, Birmingham,
/// Sheffield, Kenya, Edinburgh, Hermione, Aurora, Galatea, Koln, Nurnberg.
/// </summary>
type LightCruiser(id, name, nationality, evasionRating, searchStrengthDay, searchStrengthNight) =
    inherit ShipCounter(id, name, nationality, evasionRating, searchStrengthDay, searchStrengthNight)
    override _.ClassCode = "CL"
