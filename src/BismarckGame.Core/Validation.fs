/// <summary>
/// Validation.fs
/// Sanity-checks a ScenarioDefinition before `Scenario.initializeGame`
/// turns it into a live GameState. Catches the kind of mistake that
/// would otherwise surface as a confusing runtime error deep in Update.fs
/// (or worse, silently do the wrong thing) — a PendingReinforcement
/// naming a ShipId that isn't in any order of battle, a starting zone
/// that doesn't exist on the scenario's own SearchBoard, two ships
/// sharing an Id, etc.
///
/// Deliberately returns a list of ALL problems found, not just the
/// first — a scenario author fixing data wants the full list in one
/// pass, not a whack-a-mole loop of one-error-at-a-time reruns.
/// </summary>
module BismarckGame.Core.Validation

open BismarckGame.Core.Common
open BismarckGame.Core.SearchBoard
open BismarckGame.Core.Scenario

type ValidationIssue =
    | DuplicateShipId of ShipId
    | DuplicateAirUnitId of AirUnitId
    | ShipStartsOffBoard of ShipId * GridCoordinate
    | ShipStartsOnUnenterableZone of ShipId * GridCoordinate
    | AirUnitStartsOffBoard of AirUnitId * GridCoordinate
    | ReinforcementUnknownShip of turn: int * ShipId
    | ReinforcementZoneOffBoard of turn: int * ShipId * GridCoordinate
    | ReinforcementDuplicateShip of ShipId
    | ReinforcementNonPositiveTurn of turn: int * ShipId
    | NoShipsForNationality of Nationality
    | ConvoyRouteZoneOffBoard of GridCoordinate
    | ConvoyRoutePathEmpty
    | ConvoyRoutePathZoneOffBoard of GridCoordinate
    | InitialConvoyRouteIndexOutOfRange of int
    | ConvoyCountNonPositive of int
    | EmptySearchBoard
    | CarrierCanPatrol of ShipId   // rule 2.423 violation if scenario data sets this incorrectly

let private describe (issue: ValidationIssue) : string =
    match issue with
    | DuplicateShipId (ShipId id) -> $"Ship id '{id}' is used more than once across the orders of battle"
    | DuplicateAirUnitId (AirUnitId id) -> $"Air unit id '{id}' is used more than once across the orders of battle"
    | ShipStartsOffBoard (ShipId id, coord) -> $"Ship '{id}' starts at {coord} which isn't a zone on this scenario's Search Board"
    | ShipStartsOnUnenterableZone (ShipId id, coord) -> $"Ship '{id}' starts at {coord}, a zone with no grid-coordinate (rule 5.18 — no unit can enter it)"
    | AirUnitStartsOffBoard (AirUnitId id, coord) -> $"Air unit '{id}' starts at {coord} which isn't a zone on this scenario's Search Board"
    | ReinforcementUnknownShip (turn, ShipId id) -> $"PendingReinforcement at turn {turn} names ship '{id}', which isn't in any order of battle"
    | ReinforcementZoneOffBoard (turn, ShipId id, coord) -> $"PendingReinforcement for '{id}' at turn {turn} places it at {coord}, not a zone on this scenario's Search Board"
    | ReinforcementDuplicateShip (ShipId id) -> $"Ship '{id}' appears more than once in PendingReinforcements"
    | ReinforcementNonPositiveTurn (turn, ShipId id) -> $"PendingReinforcement for '{id}' has a non-positive turn number ({turn})"
    | NoShipsForNationality nat -> $"{nat} has no ships in its order of battle"
    | ConvoyRouteZoneOffBoard coord -> $"Convoy route includes {coord}, which isn't a zone on this scenario's Search Board"
    | ConvoyRoutePathEmpty -> "ConvoyRoutePath must contain at least one zone"
    | ConvoyRoutePathZoneOffBoard coord -> $"ConvoyRoutePath includes {coord}, which isn't a zone on this scenario's Search Board"
    | InitialConvoyRouteIndexOutOfRange i -> $"Initial convoy route index {i} is outside ConvoyRoutePath bounds"
    | ConvoyCountNonPositive n -> $"ConvoyCount must be positive, got {n}"
    | EmptySearchBoard -> "SearchBoard has no zones at all"
    | CarrierCanPatrol (ShipId id) -> $"'{id}' is an AircraftCarrier but isn't marked CanPatrol=false (rule 2.423)"

/// <summary>
/// Validates a scenario. Empty list = no problems found. This checks
/// internal consistency (dangling ids, off-board coordinates) — it does
/// NOT check historical/rules accuracy of the data itself (e.g. it won't
/// catch a wrong evasion rating), which is out of scope for an automated
/// check.
/// </summary>
let validate (scenario: ScenarioDefinition) : ValidationIssue list =
    let allShips = scenario.OrdersOfBattle |> List.collect (fun oob -> oob.Ships)
    let allAirUnits = scenario.OrdersOfBattle |> List.collect (fun oob -> oob.AirUnits)
    let shipIds = allShips |> List.map (fun s -> s.Id)
    let airUnitIds = allAirUnits |> List.map (fun a -> a.Id)
    let knownShipIds = Set.ofList shipIds

    let isValidZone (coord: GridCoordinate) =
        match scenario.SearchBoard.TryFind coord with
        | Some z -> z.Coordinate.IsSome
        | None -> false

    let duplicateShipIds =
        shipIds
        |> List.countBy id
        |> List.filter (fun (_, n) -> n > 1)
        |> List.map (fst >> DuplicateShipId)

    let duplicateAirUnitIds =
        airUnitIds
        |> List.countBy id
        |> List.filter (fun (_, n) -> n > 1)
        |> List.map (fst >> DuplicateAirUnitId)

    let shipZoneIssues =
        allShips
        |> List.choose (fun s ->
            match s.CurrentZone with
            | None -> None   // off-board on purpose (e.g. a timed reinforcement) — fine
            | Some coord ->
                match scenario.SearchBoard.TryFind coord with
                | None -> Some(ShipStartsOffBoard(s.Id, coord))
                | Some z when z.Coordinate.IsNone -> Some(ShipStartsOnUnenterableZone(s.Id, coord))
                | Some _ -> None)

    let airUnitZoneIssues =
        allAirUnits
        |> List.choose (fun a ->
            match a.CurrentZone with
            | None -> None
            | Some coord -> if isValidZone coord || scenario.SearchBoard.TryFind coord |> Option.isSome then None else Some(AirUnitStartsOffBoard(a.Id, coord)))

    let reinforcementIssues =
        scenario.PendingReinforcements
        |> List.collect (fun (turn, shipId, coord) ->
            [ if turn <= 0 then yield ReinforcementNonPositiveTurn(turn, shipId)
              if not (knownShipIds.Contains shipId) then yield ReinforcementUnknownShip(turn, shipId)
              if not (isValidZone coord) then yield ReinforcementZoneOffBoard(turn, shipId, coord) ])

    let reinforcementDuplicates =
        scenario.PendingReinforcements
        |> List.map (fun (_, shipId, _) -> shipId)
        |> List.countBy id
        |> List.filter (fun (_, n) -> n > 1)
        |> List.map (fst >> ReinforcementDuplicateShip)

    let nationalityIssues =
        scenario.OrdersOfBattle
        |> List.filter (fun oob -> oob.Ships.IsEmpty)
        |> List.map (fun oob -> NoShipsForNationality oob.Nationality)

    let convoyRouteIssues =
        scenario.ConvoyRouteZones
        |> Set.toList
        |> List.choose (fun coord -> if scenario.SearchBoard.Zones.ContainsKey coord then None else Some(ConvoyRouteZoneOffBoard coord))

    let convoyRoutePathIssues =
        [ if scenario.ConvoyRoutePath.IsEmpty then
              yield ConvoyRoutePathEmpty
          for coord in scenario.ConvoyRoutePath do
              if not (scenario.SearchBoard.Zones.ContainsKey coord) then
                  yield ConvoyRoutePathZoneOffBoard coord ]

    let initialConvoyIndexIssues =
        scenario.InitialConvoyRouteIndices
        |> List.choose (fun i ->
            if scenario.ConvoyRoutePath.IsEmpty then
                Some(InitialConvoyRouteIndexOutOfRange i)
            elif i < 0 || i >= scenario.ConvoyRoutePath.Length then
                Some(InitialConvoyRouteIndexOutOfRange i)
            else None)

    let carrierPatrolIssues =
        allShips
        |> List.filter (fun s -> s.Class = AircraftCarrier && s.CanPatrol)
        |> List.map (fun s -> CarrierCanPatrol s.Id)

    let boardIssue = if scenario.SearchBoard.Zones.IsEmpty then [ EmptySearchBoard ] else []
    let convoyCountIssue = if scenario.ConvoyCount <= 0 then [ ConvoyCountNonPositive scenario.ConvoyCount ] else []

    duplicateShipIds
    @ duplicateAirUnitIds
    @ shipZoneIssues
    @ airUnitZoneIssues
    @ reinforcementIssues
    @ reinforcementDuplicates
    @ nationalityIssues
    @ convoyRouteIssues
    @ convoyRoutePathIssues
    @ initialConvoyIndexIssues
    @ carrierPatrolIssues
    @ boardIssue
    @ convoyCountIssue

/// <summary>
/// Convenience: human-readable report, one issue per line, or a
/// single "no issues found" line.
/// </summary>
let report (scenario: ScenarioDefinition) : string =
    match validate scenario with
    | [] -> "No validation issues found."
    | issues -> issues |> List.map describe |> String.concat "\n"
