module BismarckGame.Tests.SimulationTests

open Xunit
open BismarckGame.Core.Common
open BismarckGame.Core.Units
open BismarckGame.Core.GameState
open BismarckGame.Core.SearchBoard
open BismarckGame.Core.Simulation
open BismarckGame.Tests.TestHelpers

[<Fact>]
let ``simulateFullTurn advances to the next turn without failed events`` () =
    let state = { (testState ()) with Phase = UnitAvailability }
    match simulateFullTurn unusedTables (constantRoll 3) state with
    | Error msg -> Assert.Fail msg
    | Ok (state', events) ->
        Assert.Equal(state.Turn.Number + 1, state'.Turn.Number)
        Assert.DoesNotContain(events, fun e -> not e.Succeeded)

[<Fact>]
let ``simulateCurrentPhase search reacts to current ship positions`` () =
    let state =
        { (testState ()) with
            Phase = Search
            Turn = { (testState ()).Turn with Visibility = VisibilityLevel 1 } }

    let britishShip =
        { state.Players.[British].Ships.[ShipId "GBR-1"] with
            CurrentZone = Some(coord 'C' 3)
            Mode = Patrol }

    let germanShip =
        { state.Players.[German].Ships.[ShipId "GER-1"] with
            CurrentZone = Some(coord 'C' 3)
            Mode = Patrol }

    let state' =
        { state with
            Players =
                state.Players
                |> Map.add British { state.Players.[British] with Ships = state.Players.[British].Ships.Add(britishShip.Id, britishShip) }
                |> Map.add German { state.Players.[German] with Ships = state.Players.[German].Ships.Add(germanShip.Id, germanShip) } }

    match simulateCurrentPhase unusedTables (constantRoll 3) state' with
    | Error msg -> Assert.Fail msg
    | Ok (afterSearch, events) ->
        Assert.Contains(afterSearch.LocationMarkers, fun m -> m.Owner = German && m.Zone = coord 'C' 3)
        Assert.Equal(AirAttack, afterSearch.Phase)
        Assert.Contains(events, fun e -> e.Label.Contains("search"))

[<Fact>]
let ``simulateCurrentPhase NavalCombat auto-attacks convoy contacts`` () =
    let state =
        { (testState ()) with
            Phase = NavalCombat
            ConvoyUnits =
                [ { Id = 1
                    Zone = coord 'A' 1
                    RouteIndex = 0
                    Direction = BismarckGame.Core.Markers.East
                    IsSunk = false } ]
            ConvoyContacts =
                [ { Zone = coord 'A' 1
                    ConvoyId = Some 1
                    Discoverer = German
                    Source = BismarckGame.Core.Markers.ChanceOnRoute
                    TurnLocated = 4 } ] }

    match simulateCurrentPhase unusedTables (constantRoll 3) state with
    | Error msg -> Assert.Fail msg
    | Ok (afterPhase, events) ->
        Assert.Equal(Chance, afterPhase.Phase)
        Assert.Empty(afterPhase.ConvoyContacts)
        Assert.Equal(1, afterPhase.ConvoysSunkByGerman)
        Assert.Equal(6, afterPhase.Players.[German].Score.Points)
        Assert.Contains(events, fun e -> e.Label.Contains("Convoy attack"))
