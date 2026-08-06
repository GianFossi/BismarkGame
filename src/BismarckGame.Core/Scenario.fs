/// <summary>
/// Scenario.fs
/// Makes maps and orders-of-battle pluggable data rather than something
/// baked into the engine. A ScenarioDefinition is everything needed to
/// initialize a GameState; swapping it swaps the whole game (different
/// board, different ships, different turn structure) without touching
/// Update.fs or the domain types in Common/SearchBoard/Units/BattleBoard.
///
/// Concretely this exists because the Bismarck rules themselves ship a
/// second Search Board for a different historical action (River Plate,
/// published later in The Avalon Hill General) reusing the same engine —
/// same zone-grid concept, same phase sequence, different geography and
/// roster. This module is the seam that makes that kind of variant a
/// data-loading exercise instead of a code change.
/// </summary>
module BismarckGame.Core.Scenario

open BismarckGame.Core.Common
open BismarckGame.Core.SearchBoard
open BismarckGame.Core.Units
open BismarckGame.Core.Markers
open BismarckGame.Core.GameState
open BismarckGame.Core.VictoryConditions

/// <summary>
/// Declarative starting roster for one side.
/// </summary>
type OrderOfBattle =
    { Nationality: Nationality
      Ships: ShipCounter list
      AirUnits: AirUnitCounter list }

/// <summary>
/// Everything needed to initialize a GameState. A concrete instance
/// (e.g. "Bismarck 1941 Basic Game") lives in its own module under
/// Scenarios/ — this type only defines the shape.
/// </summary>
type ScenarioDefinition =
    { Id: string
      Name: string
      Description: string
      SearchBoard: SearchBoardMap
      OrdersOfBattle: OrderOfBattle list
      FirstTurn: GameTurn
      /// <summary>
      /// Basic Game turns are 4 hours (rule 1.2); kept as data since a
      /// variant scenario could differ.
      /// </summary>
      TurnLengthHours: int
      DamagePoints: DamagePoints
      /// <summary>
      /// Zones that make up the convoy route reference used by Chance
      /// Table convoy outcomes (rows 10-12 on the Basic Game Tables Card).
      /// </summary>
      ConvoyRouteZones: Set<GridCoordinate>
      /// <summary>
      /// Ordered convoy route path. Convoy units move along this list's
      /// indices, advancing by one per turn.
      /// </summary>
      ConvoyRoutePath: GridCoordinate list
      /// <summary>
      /// Initial convoy placement as route indices into ConvoyRoutePath.
      /// </summary>
      InitialConvoyRouteIndices: int list
      /// <summary>
      /// Number of convoy targets in this scenario (rule 12.44 lists the
      /// 1st..5th convoy scoring progression for the Basic Game).
      /// </summary>
      ConvoyCount: int
      /// <summary>
      /// Ships that start off-board and enter at a specific turn/zone
      /// (British Order of Battle "Reinforcements" section — Revenge,
      /// Dorsetshire). The ship itself is already in OrdersOfBattle with
      /// CurrentZone = None; this list is what tells Update.fs's
      /// AdvancePhase handler when and where to place it.
      /// </summary>
      PendingReinforcements: (int * ShipId * GridCoordinate) list }

/// <summary>
/// Builds a fresh GameState from a scenario, positioned at the start of
/// turn 1 / Unit Availability phase (rule 4.1).
/// </summary>
let initializeGame (scenario: ScenarioDefinition) : GameState =
    let path = scenario.ConvoyRoutePath |> List.toArray

    let directionFromTo (a: GridCoordinate) (b: GridCoordinate) : Heading =
        let dy = int b.Letter - int a.Letter
        let dx = b.Number - a.Number
        let sy = if dy = 0 then 0 elif dy > 0 then 1 else -1
        let sx = if dx = 0 then 0 elif dx > 0 then 1 else -1
        match sy, sx with
        | -1, 0 -> North
        | -1, 1 -> NorthEast
        | 0, 1 -> East
        | 1, 1 -> SouthEast
        | 1, 0 -> South
        | 1, -1 -> SouthWest
        | 0, -1 -> West
        | -1, -1 -> NorthWest
        | _ -> East

    let convoyUnits =
        scenario.InitialConvoyRouteIndices
        |> List.mapi (fun i idx ->
            let clampedIdx =
                if path.Length = 0 then 0
                elif idx < 0 then 0
                elif idx >= path.Length then path.Length - 1
                else idx
            let zone = if path.Length = 0 then { Letter = 'A'; Number = 1 } else path.[clampedIdx]
            let direction =
                if path.Length > 1 && clampedIdx < path.Length - 1 then directionFromTo zone path.[clampedIdx + 1]
                else East
            { Id = i + 1
              Zone = zone
              RouteIndex = clampedIdx
              Direction = direction
              IsSunk = false })

    let players =
        scenario.OrdersOfBattle
        |> List.map (fun oob ->
            let nat = oob.Nationality
            nat,
            { Nationality = nat
              Ships = oob.Ships |> List.map (fun s -> s.Id, s) |> Map.ofList
              AirUnits = oob.AirUnits |> List.map (fun a -> a.Id, a) |> Map.ofList
              TaskForces = Map.empty
              ConvoyEscorts = []
              Score = { Nationality = nat; Points = 0; Events = [] } })
        |> Map.ofList

    { Turn = scenario.FirstTurn
      Phase = UnitAvailability
      SearchBoard = scenario.SearchBoard
      ConvoyRouteZones = scenario.ConvoyRouteZones
      ConvoyRoutePath = scenario.ConvoyRoutePath
      ConvoyUnits = convoyUnits
      ConvoyContacts = []
      ConvoysAvailable = scenario.ConvoyCount
      ConvoysSunkByGerman = 0
      Players = players
      ShadowMarkers = []
      LocationMarkers = []
      ActiveBattles = []
      GermanLocatedTurn = None
      PendingReinforcements = scenario.PendingReinforcements
      GameEnded = None }
