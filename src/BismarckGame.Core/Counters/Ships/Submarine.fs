namespace BismarckGame.Core.Counters.Ships

/// <summary>
/// SS/UB — Submarine (UB = U-Boat). Rule 2.422: "not used in Basic Game."
/// Included for the Intermediate Game (rule 22.0, "Submarines"). Submarines
/// have no printed evasion/search stats on the Hit Record Pad transcribed
/// so far (that pad only covers surface ships) — search strength fields
/// are placeholders (0) until a submarine-specific source is transcribed.
/// </summary>
type Submarine(id, name, nationality, evasionRating, searchStrengthDay, searchStrengthNight) =
    inherit ShipCounter(id, name, nationality, evasionRating, searchStrengthDay, searchStrengthNight)
    override _.ClassCode = "SS"
    override _.IsBasicGameUnit = false
    // CanShadow left at the base default (true) — not confirmed either
    // way by any rule text transcribed so far, and moot while
    // IsBasicGameUnit is false.
