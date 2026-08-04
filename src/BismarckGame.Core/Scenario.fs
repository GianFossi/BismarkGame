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
      Players = players
      ShadowMarkers = []
      LocationMarkers = []
      ActiveBattles = []
      GermanLocatedTurn = None
      PendingReinforcements = scenario.PendingReinforcements
      GameEnded = None }
