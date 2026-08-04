/// <summary>
/// ChanceTable.fs
/// Chance Table transcribed from the "Basic Game Tables Card". Source:
/// rule 4.9 — British player rolls two dice twice each turn (once for
/// Bismarck, once for Prinz Eugen).
/// </summary>
module BismarckGame.Core.Tables.ChanceTable

/// <summary>
/// Result of one Chance Table roll for a single German ship/task force.
/// </summary>
type ChanceResult =
    | HuffDuff
    /// <summary>
    /// General Search succeeded: visibility level must be <= this value
    /// for the German ship/task force to be revealed (rule text: "General
    /// Search — visibility level must be equal or lower than the table
    /// value").
    /// </summary>
    | GeneralSearchThreshold of int
    /// <summary>
    /// No search possible at this visibility for this column ("—" on the card).
    /// </summary>
    | NoSearchPossible
    | ConvoyLocatedOnRoute
    | ConvoyLocatedNearRoute   // on patrol, within 2 zones of a convoy route
    | ConvoyLocatedAdjacentToRoute   // one zone away from route, need not be on patrol

/// <summary>
/// Which of columns A/B/C applies to a German ship/task force this turn —
/// determined by its position relative to the "white dot" reference line
/// and coastal zones (see `column` below).
/// </summary>
type ChanceColumn = ColumnA | ColumnB | ColumnC

let private generalSearchTable : Map<int * ChanceColumn, ChanceResult> =
    [ 3, ColumnA, GeneralSearchThreshold 3;  3, ColumnB, GeneralSearchThreshold 5;  3, ColumnC, GeneralSearchThreshold 6
      4, ColumnA, GeneralSearchThreshold 2;  4, ColumnB, GeneralSearchThreshold 4;  4, ColumnC, GeneralSearchThreshold 5
      5, ColumnA, NoSearchPossible;          5, ColumnB, GeneralSearchThreshold 1;  5, ColumnC, GeneralSearchThreshold 2
      6, ColumnA, GeneralSearchThreshold 2;  6, ColumnB, GeneralSearchThreshold 3;  6, ColumnC, GeneralSearchThreshold 4
      7, ColumnA, NoSearchPossible;          7, ColumnB, GeneralSearchThreshold 1;  7, ColumnC, GeneralSearchThreshold 2
      8, ColumnA, GeneralSearchThreshold 1;  8, ColumnB, GeneralSearchThreshold 2;  8, ColumnC, GeneralSearchThreshold 3
      9, ColumnA, GeneralSearchThreshold 1;  9, ColumnB, GeneralSearchThreshold 2;  9, ColumnC, GeneralSearchThreshold 3 ]
    |> List.map (fun (d, c, r) -> (d, c), r)
    |> Map.ofList

/// <summary>
/// Rolls 10-12 don't depend on column — they're convoy-location results.
/// </summary>
let private convoyTable : Map<int, ChanceResult> =
    [ 10, ConvoyLocatedOnRoute
      11, ConvoyLocatedNearRoute
      12, ConvoyLocatedAdjacentToRoute ]
    |> Map.ofList

/// <summary>
/// Determines which column (A/B/C) applies to a German ship/task force,
/// per the card's "FOR GERMAN PLAYER" rules text. The two booleans are
/// pre-computed by the caller from the Search Board (the "white dot"
/// reference zones are marked directly on the physical board — visible
/// as small tan dots in the Search Board photos — and are not yet
/// transcribed into SearchBoard.fs as data).
///
/// NOTE: General Search applies only in/east of the white-dot line and
/// in/south of row E; ships west of that line or north of row D ignore
/// the General Search result entirely (both cases: caller should not call
/// this function, not represented as a ChanceResult case).
/// </summary>
let column (nearWhiteDotBelowRowD: bool) (nearBritishOrIrishCoastOrShetland: bool) : ChanceColumn =
    if nearWhiteDotBelowRowD then ColumnA
    elif nearBritishOrIrishCoastOrShetland then ColumnC
    else ColumnB

/// <summary>
/// Resolves one Chance Table roll (2d6 sum, 2-12).
/// </summary>
let resolve (diceSum: int) (col: ChanceColumn) : ChanceResult =
    if diceSum = 2 then HuffDuff
    elif diceSum >= 3 && diceSum <= 9 then
        generalSearchTable.TryFind(diceSum, col) |> Option.defaultValue NoSearchPossible
    else
        convoyTable.TryFind diceSum |> Option.defaultValue NoSearchPossible
