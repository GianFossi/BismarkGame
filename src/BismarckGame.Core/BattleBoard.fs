/// <summary>
/// BattleBoard.fs
/// The tactical combat map — the ONLY hexagonal board in the game
/// (rule 2.3, 2.6, 9.5x, 9.6x). Entered when opposing located ships share
/// a Search Board zone and either player calls for combat.
/// </summary>
module BismarckGame.Core.BattleBoard

open BismarckGame.Core.Common

/// <summary>
/// Cubic hex coordinates (q + r + s = 0). Chosen over offset coordinates
/// because range/distance and firing-arc math (rules 9.53, 9.61) become
/// simple integer arithmetic.
/// </summary>
type HexCoord =
    { Q: int; R: int; S: int }

    member this.DistanceTo(other: HexCoord) =
        (abs (this.Q - other.Q) + abs (this.R - other.R) + abs (this.S - other.S)) / 2

    /// The hex marked "START" on the physical board (rule 9.2x default
    /// placement reference) — the board's own center hex.
    static member Zero = { Q = 0; R = 0; S = 0 }

/// <summary>
/// One of the six sides of a hex — used for ship facing/bow direction
/// (rule 9.52-9.55) and firing arcs.
/// </summary>
type HexSide =
    | HexN | HexNE | HexSE
    | HexS | HexSW | HexNW

    /// <summary>
    /// Cubic-coordinate offset for moving one hex in this direction.
    /// Axis convention: Q increases moving NE/SE-ward, R increases moving
    /// S-ward, consistent with `HexCoord.DistanceTo`'s Manhattan-on-cube
    /// formula (any consistent convention works as long as this table and
    /// that formula agree — they do, both here).
    /// </summary>
    member this.Offset =
        match this with
        | HexN -> { Q = 0; R = -1; S = 1 }
        | HexNE -> { Q = 1; R = -1; S = 0 }
        | HexSE -> { Q = 1; R = 0; S = -1 }
        | HexS -> { Q = 0; R = 1; S = -1 }
        | HexSW -> { Q = -1; R = 1; S = 0 }
        | HexNW -> { Q = -1; R = 0; S = 1 }

    /// <summary>
    /// The opposite side — e.g. a ship facing HexN has its stern facing
    /// HexS. Used to resolve which gun section (bow/stern) can bear.
    /// </summary>
    member this.Opposite =
        match this with
        | HexN -> HexS | HexS -> HexN
        | HexNE -> HexSW | HexSW -> HexNE
        | HexSE -> HexNW | HexNW -> HexSE

    /// <summary>
    /// The side reached by rotating one step clockwise — one "direction
    /// change" per the Combat Movement Chart (Tables/EvasionEffects.fs).
    /// </summary>
    member this.RotateClockwise =
        match this with
        | HexN -> HexNE | HexNE -> HexSE | HexSE -> HexS
        | HexS -> HexSW | HexSW -> HexNW | HexNW -> HexN

    member this.RotateCounterclockwise =
        match this with
        | HexN -> HexNW | HexNW -> HexSW | HexSW -> HexS
        | HexS -> HexSE | HexSE -> HexNE | HexNE -> HexN

/// <summary>
/// Adds a HexSide's offset to a HexCoord — "the hex you'd be in after
/// moving one step in that direction."
/// </summary>
let hexNeighbor (coord: HexCoord) (side: HexSide) : HexCoord =
    let o = side.Offset
    { Q = coord.Q + o.Q; R = coord.R + o.R; S = coord.S + o.S }

/// <summary>
/// Approximate board radius (rings out from START/center). Not measured
/// precisely against the physical board's printed hex count — this is a
/// generous bound so early movement/placement logic has *some* limit
/// rather than none, not a transcribed fact. Tighten once the board's
/// actual extent is counted from a photo.
/// </summary>
let approximateBoardRadius = 8

let isOnBoard (coord: HexCoord) : bool =
    coord.DistanceTo(HexCoord.Zero) <= approximateBoardRadius

/// <summary>
/// Firing range bands (rule 9.61): 1-3 hexes = A range, 4-6 hexes = B
/// range, 7+ hexes = out of range.
/// </summary>
type FireRange =
    | RangeA   // 1-3 hexes
    | RangeB   // 4-6 hexes
    | OutOfRange

    static member OfDistance(hexes: int) =
        match hexes with
        | d when d >= 1 && d <= 3 -> RangeA
        | d when d >= 4 && d <= 6 -> RangeB
        | _ -> OutOfRange

/// <summary>
/// A ship's main and secondary armament is divided into four gun
/// sections, each with its own salvo count and firing arc (rule 9.64,
/// 2.64). A section with salvo count 0 has no fire power.
/// </summary>
type GunSectionType =
    | BowGuns
    | SternGuns
    | PortGuns
    | StarboardGuns

type GunSection =
    { Section: GunSectionType
      MaxSalvo: int
      SalvoRemaining: int
      /// <summary>
      /// True only for Bow/Stern sections, which alone can fire into both
      /// A and B range arcs (rule 9.63); Port/Starboard sections are
      /// further restricted to their own sector.
      /// </summary>
      CanFireBothRanges: bool }

/// <summary>
/// A ship's tactical state while on the Battle Board. This is distinct
/// from Units.ShipCounter (Search Board state) — the two are linked via
/// ShipId and reconciled when combat ends.
/// </summary>
type BattleShipState =
    { ShipId: ShipId
      /// <summary>
      /// Denormalized from the Search Board ShipCounter at battle start —
      /// needed here (not just looked up) because damage resolution
      /// (rule 9.723-9.726's per-class evasion loss, rule 9.714's
      /// fill-the-track sinking) depends on name/class and must apply
      /// immediately during combat, not only when the battle ends.
      /// </summary>
      Name: string
      Class: ShipClass
      Position: HexCoord
      /// <summary>
      /// The hex side the bow currently points toward (rule 9.52).
      /// </summary>
      Facing: HexSide
      GunSections: GunSection list
      /// <summary>
      /// Secondary-armament (Port/Starboard) hits — rule 9.66 note:
      /// "secondary hits can be recorded either port or starboard" (the
      /// firer's choice), so this is a single running count rather than
      /// being split between the Port/Starboard GunSection entries, which
      /// already track the SHIP'S OWN secondary guns firing out, a
      /// different thing from damage taken.
      /// </summary>
      SecondaryHits: int
      /// <summary>
      /// Same value as ShipCounter.EvasionRating; on the Battle Board it
      /// governs move/turn allowance (Combat Movement Chart, rule 9.53)
      /// and withdrawal ability (rule 2.62).
      /// </summary>
      EvasionRating: int
      MidshipsHits: int
      /// <summary>
      /// Rule 9.714: the ship sinks once MidshipsHits reaches this — see
      /// Tables/ShipStats.fs for the (best-effort) box counts.
      /// </summary>
      MaxMidshipsHits: int
      /// <summary>
      /// Rule 9.722: accumulated PERMANENT evasion loss from explicit
      /// "reduce evasion by N" results — never repaired, tracked
      /// separately from the temporary per-hit loss (9.723-9.726) that
      /// EvasionRating otherwise absorbs.
      /// </summary>
      PermanentEvasionLoss: int
      IsWithdrawing: bool
      IsSunk: bool }

/// <summary>
/// The arc from which the target is being engaged — this is what the
/// printed Naval Fire tables key on (column headers "FIRING AT BROADSIDE
/// / BOW / STERN"), distinct from which of the FIRER's own gun sections
/// is shooting (GunSectionType above).
/// </summary>
type TargetAspect =
    | Broadside
    | BowOn
    | SternOn

/// <summary>
/// A single fire resolution: one ship shooting at one target with a given
/// number of salvoes, at a given range and target aspect (rule 9.6x).
/// </summary>
type FireOrder =
    { Firer: ShipId
      Target: ShipId
      Section: GunSectionType
      SalvoesFired: int      // already halved per rule 9.651 if applicable
      Range: FireRange
      Aspect: TargetAspect }

/// <summary>
/// Result of resolving one FireOrder against the printed Naval Fire /
/// Special Damage tables (Bismarck Battleboard, both sides — the two
/// tables are numerically identical). A "consult special damage" main-
/// table result is resolved internally by the table implementation
/// (a second 2d6 roll) and never surfaces here — callers only see the
/// final outcome.
/// </summary>
type FireResult =
    | Miss
    | HitSecondary
    | HitSection of GunSectionType
    /// <summary>
    /// Number of midships hits, plus an evasion-rating reduction when the
    /// hit came from the Special Damage table (rows 9-12 there knock
    /// speed down as well as scoring hits).
    /// </summary>
    | HitMidships of count: int * evasionReduction: int option
    /// <summary>
    /// Special Damage table row 12 at A range: instant kill for ordinary
    /// ships. King George V, Prince of Wales and Bismarck are exempted
    /// (see NavalFireTables.fs) and take a heavy midships hit instead.
    /// </summary>
    | Sunk

/// <summary>
/// The six edges of the physical Battle Board are numbered 1-6 (printed
/// on the board itself) and used when rules call for a die roll to pick
/// an entry edge (e.g. rule 9.44 reinforcement entry). Clockwise from
/// the top: 4 (top), 5 (upper-right), 6 (lower-right), 1 (bottom),
/// 2 (lower-left), 3 (upper-left).
/// </summary>
type BoardEdge =
    | Edge1 | Edge2 | Edge3 | Edge4 | Edge5 | Edge6

    member this.ToHexSide() =
        match this with
        | Edge4 -> HexN
        | Edge5 -> HexNE
        | Edge6 -> HexSE
        | Edge1 -> HexS
        | Edge2 -> HexSW
        | Edge3 -> HexNW

/// <summary>
/// The Battle Board instance active for one naval combat action. Multiple
/// simultaneous combat actions are possible if the Bismarck and Prinz
/// Eugen are engaged separately (rule 9.26) — each gets its own instance.
/// </summary>
type BattleBoardState =
    { Id: int
      Ships: Map<ShipId, BattleShipState>
      Round: int }
