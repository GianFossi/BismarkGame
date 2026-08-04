namespace BismarckGame.Core.Counters.Ships

/// <summary>
/// BC — Battlecruiser (rule 2.422). E.g. Hood, Repulse, Renown, Scharnhorst,
/// Gneisenau. Historically a capital-ship hull with lighter armor traded
/// for speed; in this ruleset that shows up as a generally higher evasion
/// rating than a Battleship of similar gun power, carried per-instance
/// (see Tables/ShipStats.fs), not as a class-level override here.
/// </summary>
type Battlecruiser(id, name, nationality, evasionRating, searchStrengthDay, searchStrengthNight) =
    inherit ShipCounter(id, name, nationality, evasionRating, searchStrengthDay, searchStrengthNight)
    override _.ClassCode = "BC"
