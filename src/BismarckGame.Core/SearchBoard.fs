/// <summary>
/// SearchBoard.fs
/// The strategic map: a grid of square "zones" (NOT hexagons — that is the
/// Battle Board only, see BattleBoard.fs). Source: rules 2.2, 5.1x-5.4x, 7.x.
/// </summary>
module BismarckGame.Core.SearchBoard

open BismarckGame.Core.Common

/// <summary>
/// A zone is identified by one letter and one-or-two digits (rule 2.2).
/// "Partial zones" without a grid-coordinate (other than Bordeaux) cannot
/// be entered by any unit — represented by Zone.Coordinate = None.
/// </summary>
type GridCoordinate =
    { Letter: char
      Number: int }
    override this.ToString() = sprintf "%c%d" this.Letter this.Number

/// <summary>
/// Terrain effects that restrict entry/exit or movement cost (rule 5.18,
/// Terrain Effects Key on the manual's cover).
/// </summary>
type TerrainFeature =
    | OpenSea
    | Port of ownedBy: Nationality
    | IrishSea                     // German ships may never enter (rule 5.18)
    | RestrictedEntry               // zone-specific entry/exit restriction, detailed per-zone
    | BordeauxAirBase              // the one partial zone that IS enterable

/// <summary>
/// A single zone on the Search Board.
/// </summary>
type Zone =
    { Coordinate: GridCoordinate option   // None = un-enterable partial zone
      Terrain: TerrainFeature
      /// <summary>
      /// Zones south of/on row E form the British patrol line (rule 11.11/11.13)
      /// </summary>
      IsOnBritishPatrolLine: bool
      /// <summary>
      /// True for zones marked with a printed white dot — the reference
      /// line the Chance Table's General Search column (A/B/C) is keyed
      /// against (rule text on the Basic Game Tables Card: "Use column A
      /// if German ship or task force is in or is one or two zones away
      /// from any zone with a white dot and below row D"). See
      /// Scenarios/BismarckBasicGame.fs for which zones are marked, and
      /// SearchBoard.isNearWhiteDot for the "one or two zones away" check.
      /// </summary>
      IsWhiteDot: bool }

/// <summary>
/// The full Search Board is just a lookup of coordinate -> zone.
/// Each player has an identical copy (rule 2.2) but movement/location on
/// it is hidden from the opponent — that visibility rule lives in
/// GameState, not here.
/// </summary>
type SearchBoardMap =
    { Zones: Map<GridCoordinate, Zone> }

    member this.TryFind(coord: GridCoordinate) = this.Zones.TryFind coord

/// <summary>
/// The four orthogonal neighbor coordinates of a zone. Rule 5.18 speaks
/// only of "adjacent" zones with no mention of diagonals, and the
/// physical board is a printed square grid, so neighbors are computed as
/// up/down/left/right on (Letter, Number) rather than hand-authored per
/// zone. NOTE: this assumes board rows are contiguous letters with no
/// skipped row — true on the physical board (A through Z).
/// </summary>
let orthogonalOffsets (coord: GridCoordinate) : GridCoordinate list =
    [ { coord with Number = coord.Number - 1 }
      { coord with Number = coord.Number + 1 }
      { coord with Letter = char (int coord.Letter - 1) }
      { coord with Letter = char (int coord.Letter + 1) } ]

/// <summary>
/// Neighbors that actually exist on the given board (excludes off-board
/// and un-enterable partial zones). Movement-blocking rules that are NOT
/// about physical adjacency (Irish Sea, port entry restrictions —
/// rule 5.18) are applied separately by the movement command handler,
/// not baked into this function, so the same board data can serve
/// different scenarios with different restriction sets.
/// </summary>
let neighbors (map: SearchBoardMap) (coord: GridCoordinate) : GridCoordinate list =
    orthogonalOffsets coord |> List.filter map.Zones.ContainsKey

/// <summary>
/// Graph distance in zones between two coordinates, following real board
/// adjacency (`neighbors`) rather than straight-line coordinate math —
/// needed because several rules phrase conditions as "within N zones"
/// (the white-dot reference line, Huff-Duff's "one adjacent zone", etc.)
/// and the board isn't a perfect grid once land and off-board gaps are
/// accounted for. Returns None if unreachable within `maxDistance` hops.
/// </summary>
let distanceWithin (map: SearchBoardMap) (maxDistance: int) (a: GridCoordinate) (b: GridCoordinate) : int option =
    if a = b then
        Some 0
    else
        let rec bfs (frontier: Set<GridCoordinate>) (visited: Set<GridCoordinate>) (dist: int) =
            if dist > maxDistance || Set.isEmpty frontier then
                None
            else
                let nextFrontier =
                    frontier
                    |> Set.toList
                    |> List.collect (neighbors map)
                    |> List.filter (fun c -> not (visited.Contains c))
                    |> Set.ofList
                if nextFrontier.Contains b then
                    Some dist
                else
                    bfs nextFrontier (Set.union visited nextFrontier) (dist + 1)
        bfs (Set.singleton a) (Set.singleton a) 1

/// <summary>
/// True if `target` is the same zone as, or within `maxDistance` zones
/// of, any coordinate satisfying `predicate` on this board.
/// </summary>
let isNearAny (map: SearchBoardMap) (maxDistance: int) (predicate: GridCoordinate -> bool) (target: GridCoordinate) : bool =
    map.Zones
    |> Map.toSeq
    |> Seq.map fst
    |> Seq.filter predicate
    |> Seq.exists (fun c -> (distanceWithin map maxDistance target c).IsSome)

// --- Chance Table column-selection helpers (rule text on the Basic Game
// Tables Card, "FOR GERMAN PLAYER") ---------------------------------------
//
// Deliberately generic over `map` rather than closed over one scenario's
// board: any future scenario just needs to set Zone.IsWhiteDot and
// Terrain = Port British correctly in its own board data (see
// Scenarios/BismarckBasicGame.fs for the 1941 board's white-dot line),
// and these functions work unchanged.

/// <summary>
/// True if `coord` is itself a white-dot zone, or within 2 zones of one.
/// </summary>
let isNearWhiteDot (map: SearchBoardMap) (coord: GridCoordinate) : bool =
    let isWhiteDot c = match map.TryFind c with Some z -> z.IsWhiteDot | None -> false
    isNearAny map 2 isWhiteDot coord

/// <summary>
/// Chance Table column A's condition: near a white-dot zone AND in/south
/// of row E (the card's General Search applicability note; this also
/// covers the separately-worded "below row D" phrasing on the column-A
/// line itself — both name the same D/E boundary).
/// </summary>
let nearWhiteDotBelowRowD (map: SearchBoardMap) (coord: GridCoordinate) : bool =
    coord.Letter >= 'E' && isNearWhiteDot map coord

/// <summary>
/// Chance Table column C's condition: near a British/Irish coastal zone,
/// i.e. within 2 zones of a zone marked `Port British` in the loaded
/// board. INCOMPLETE: the rule also triggers near "the Shetland Islands
/// zone", but Shetland has no orthogonal-grid coordinate in the 1941
/// board's data (it's a land gap, not an enterable zone — see the RowSpec
/// gap in row G of Scenarios/BismarckBasicGame.fs), so that half of the
/// condition can't be evaluated yet. Callers relying on Shetland
/// proximity specifically should not use this function as-is.
/// </summary>
let nearBritishOrIrishCoast (map: SearchBoardMap) (coord: GridCoordinate) : bool =
    let isBritishPort c =
        match map.TryFind c with
        | Some z -> (match z.Terrain with Port British -> true | _ -> false)
        | None -> false
    isNearAny map 2 isBritishPort coord

/// <summary>
/// Fuel bookkeeping applies only to battleships and aircraft carriers with
/// max speed 2 — cruisers are exempt in the Basic Game (rule 5.21).
/// </summary>
type FuelState =
    { FactorsRemaining: int
      InEmergencyMovement: bool }   // rule 5.23: once fuel is exhausted

/// <summary>
/// Visibility level for the current turn, tracked on a 1..N track
/// (rule 4.2, 7.1x). Determines search strength effectiveness and fog.
/// </summary>
type VisibilityLevel = VisibilityLevel of int
