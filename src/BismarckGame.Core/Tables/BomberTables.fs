/// <summary>
/// BomberTables.fs
/// Air attack result tables transcribed from the Bismarck Battleboard.
/// Unlike naval gunfire, the British and German bomber tables are NOT
/// identical: the British have separate Dive/Level Bomber and Torpedo
/// Bomber tables, while the single German table covers both of their
/// bomber sub-types (consistent with rule 2.432 — Basic Game German
/// bombers are silhouette-only variants of one counter type).
/// </summary>
module BismarckGame.Core.Tables.BomberTables

open BismarckGame.Core.BattleBoard

/// <summary>
/// One effect of an air attack result. A single table row can award more
/// than one effect at once (e.g. "1 secondary, 1 midships"), so table
/// entries are effect *lists*, unlike the single-outcome naval FireResult.
/// </summary>
type BomberHitEffect =
    | BMiss
    | BSecondary of count: int
    | BSection of GunSectionType * count: int
    | BMidships of count: int * evasionReduction: int option

/// <summary>
/// A table row. Some rows print a parenthetical alternate result specific
/// to battleship/battlecruiser targets (they shrug off some aircraft hits
/// that would hurt a lighter ship more) — e.g. British row 2: "1
/// secondary, 2 midships (1 midships if BB/BC)".
/// </summary>
type private BomberTableEntry =
    { Default: BomberHitEffect list
      IfTargetIsBattleshipOrBattlecruiser: BomberHitEffect list option }

let private simple effects = { Default = effects; IfTargetIsBattleshipOrBattlecruiser = None }

/// <summary>
/// British Dive/Level Bomber Result (2d6).
/// </summary>
let private britishDiveLevelBomber : Map<int, BomberTableEntry> =
    [ 2, { Default = [ BSecondary 1; BMidships(2, None) ]
           IfTargetIsBattleshipOrBattlecruiser = Some [ BSecondary 1; BMidships(1, None) ] }
      3, simple [ BSecondary 1 ]
      4, { Default = [ BSection(BowGuns, 2) ]
           IfTargetIsBattleshipOrBattlecruiser = Some [ BSection(BowGuns, 1) ] }
      5, simple [ BMiss ]
      6, simple [ BMiss ]
      7, simple [ BMiss ]
      8, simple [ BMiss ]
      9, simple [ BMiss ]
      10, simple [ BMiss ]
      11, simple [ BSection(SternGuns, 2) ]
      12, simple [ BSecondary 1; BSection(BowGuns, 1); BMidships(1, None) ] ]
    |> Map.ofList

/// <summary>
/// British Torpedo Bomber Result (2d6).
/// </summary>
let britishTorpedoBomber : Map<int, BomberHitEffect list> =
    [ 2, [ BMidships(1, None) ]
      3, [ BMidships(1, Some 20) ]
      4, [ BMidships(1, Some 5) ]
      5, [ BMiss ]
      6, [ BMiss ]
      7, [ BMiss ]
      8, [ BMiss ]
      9, [ BMiss ]
      10, [ BMiss ]
      11, [ BMiss ]
      12, [ BMiss ] ]
    |> Map.ofList

/// <summary>
/// German Dive/Level Bomber Result (2d6) — covers both German bomber
/// sub-types in the Basic Game. No BB/BC-specific overrides are printed
/// on this table.
/// </summary>
let germanBomber : Map<int, BomberHitEffect list> =
    [ 2, [ BSecondary 1; BMidships(1, None) ]
      3, [ BSecondary 1 ]
      4, [ BSection(BowGuns, 1) ]
      5, [ BMiss ]
      6, [ BMiss ]
      7, [ BMiss ]
      8, [ BMiss ]
      9, [ BMiss ]
      10, [ BSection(SternGuns, 1) ]
      11, [ BSection(BowGuns, 2); BMidships(2, None) ]
      12, [ BSecondary 1; BSection(SternGuns, 1); BMidships(1, None) ] ]
    |> Map.ofList

/// <summary>
/// Resolves a British Dive/Level Bomber attack, applying the BB/BC
/// alternate result when the target is a battleship or battlecruiser.
/// </summary>
let resolveBritishDiveLevelBomber (targetIsBattleshipOrBattlecruiser: bool) (rollTwoDice: unit -> int) : BomberHitEffect list =
    match britishDiveLevelBomber.TryFind(rollTwoDice ()) with
    | None -> [ BMiss ]
    | Some entry ->
        if targetIsBattleshipOrBattlecruiser then
            entry.IfTargetIsBattleshipOrBattlecruiser |> Option.defaultValue entry.Default
        else
            entry.Default
