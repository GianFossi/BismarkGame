/// <summary>
/// ShadowTable.fs
/// Shadow Table transcribed from the "Basic Game Tables Card". Source:
/// rule 4.3 / 8.1x — British player rolls one die per shadow attempt.
///
/// COLUMN-IDENTITY CAVEAT: the printed table has four result columns
/// following a clean staircase (lose-contact thresholds at die 3, 4, 5,
/// 6), but the day-letter header row (A, B, C) is printed shifted right
/// of the night-letter row (X, Y, Z) by roughly half a column width —
/// apparently because "DAY➤" as a text prefix pushes "A" rightward in
/// the printing. The reference list below (which ship/unit uses which
/// letter) only ever uses X, Y, Z — never a fourth letter — so this
/// module exposes three named categories (X/A, Y/B, Z/C) mapped to the
/// first three columns by direct alignment with the unshifted night-letter
/// row. The fourth (rightmost, hardest-to-lose) column is transcribed as
/// data but left unnamed and unreachable via `categoryOf`, since no unit
/// in the Basic Game roster is assigned to it and its true letter could
/// not be confirmed from the photo. Re-verify against the physical card
/// if a scenario ever needs it.
/// </summary>
module BismarckGame.Core.Tables.ShadowTable

type ShadowCategory =
    | CategoryX   // day letter A
    | CategoryY   // day letter B
    | CategoryZ   // day letter C

type ShadowResult =
    | HoldContact
    | LoseContact

/// <summary>
/// die roll (1-6, post-modification) -> result, per category.
/// </summary>
let private columnX = [ 1, HoldContact; 2, HoldContact; 3, LoseContact; 4, LoseContact; 5, LoseContact; 6, LoseContact ] |> Map.ofList
let private columnY = [ 1, HoldContact; 2, HoldContact; 3, HoldContact; 4, LoseContact; 5, LoseContact; 6, LoseContact ] |> Map.ofList
let private columnZ = [ 1, HoldContact; 2, HoldContact; 3, HoldContact; 4, HoldContact; 5, LoseContact; 6, LoseContact ] |> Map.ofList
/// <summary>
/// Fourth, unnamed column — see module doc. Not exposed via categoryOf.
/// </summary>
let private columnUnnamed4 = [ 1, HoldContact; 2, HoldContact; 3, HoldContact; 4, HoldContact; 5, HoldContact; 6, LoseContact ] |> Map.ofList

/// <summary>
/// Every ship/air unit named on the printed reference list (British and
/// British-allied units only — the Basic Game has only the British side
/// attempt to shadow; "Aircraft Carriers cannot shadow" per the card).
/// </summary>
let categoryOf : Map<string, ShadowCategory> =
    [ "Ramillies", CategoryX; "Revenge", CategoryX; "Rodney", CategoryY
      "Br. LR Recon", CategoryY; "Birmingham", CategoryX; "Dorsetshire", CategoryX
      "Hood", CategoryY; "Kenya", CategoryX; "King George V", CategoryY
      "Manchester", CategoryX; "Norfolk", CategoryX; "Prince of Wales", CategoryY
      "Repulse", CategoryX; "Renown", CategoryX; "Sheffield", CategoryX
      "Arethusa", CategoryZ; "Aurora", CategoryZ; "Edinburgh", CategoryZ
      "Galatea", CategoryZ; "Hermione", CategoryZ; "Suffolk", CategoryZ ]
    |> Map.ofList

/// <summary>
/// Die-roll modifications (printed directly under the table):
///  1. If target is moving 2 zones this turn, add 1.
///  2. Add/subtract the value below the current visibility level.
/// </summary>
let visibilityModifier : Map<int, int> =
    [ 0, -1; 1, 0; 2, 0; 3, 0; 4, 0; 5, 0; 6, 1; 7, 1; 8, 2 ]
    |> Map.ofList
    // Level 'X' (fog) also gives +2, same as level 8 — represented by the
    // caller passing 9 for X if using SearchBoard.VisibilityLevel's 1..9
    // encoding (see Tables/TimeAndVisibility.fs); add a 9 -> 2 entry there
    // if wiring this up directly against that type.

/// <summary>
/// Resolves one shadow attempt. `visibilityLevel` should be the raw
/// printed-track value (0-8, or use 8's modifier for 'X'/fog).
/// `targetMoving2Zones` is rule modification 1.
/// </summary>
let resolve (category: ShadowCategory) (rawDieRoll: int) (visibilityLevel: int) (targetMoving2Zones: bool) : ShadowResult =
    let modifier =
        (visibilityModifier.TryFind visibilityLevel |> Option.defaultValue 0)
        + (if targetMoving2Zones then 1 else 0)
    let modifiedRoll = max 1 (min 6 (rawDieRoll + modifier))
    let column = match category with CategoryX -> columnX | CategoryY -> columnY | CategoryZ -> columnZ
    column.TryFind modifiedRoll |> Option.defaultValue HoldContact
