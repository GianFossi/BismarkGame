module BismarckGame.Tests.EngineTests

open Xunit
open BismarckGame.Core.Common
open BismarckGame.Core.Units
open BismarckGame.Core.GameState
open BismarckGame.Core.Markers
open BismarckGame.Core.Update
open BismarckGame.Tests.TestHelpers

let private isError (r: Result<'a, 'b>) =
    match r with
    | Error _ -> true
    | Ok _ -> false

// --- movement legality ----------------------------------------------------

[<Fact>]
let ``MoveShip to an adjacent zone succeeds`` () =
    let state = testState ()
    let result = update unusedTables (constantRoll 3) (MoveShip(ShipId "GBR-1", coord 'C' 2)) state
    match result with
    | Ok state' -> Assert.Equal(Some(coord 'C' 2), state'.Players.[British].Ships.[ShipId "GBR-1"].CurrentZone)
    | Error msg -> Assert.Fail $"expected success, got: {msg}"

[<Fact>]
let ``MoveShip to a non-adjacent zone fails`` () =
    let state = testState ()
    let result = update unusedTables (constantRoll 3) (MoveShip(ShipId "GBR-1", coord 'A' 1)) state
    Assert.True(isError result)

[<Fact>]
let ``German ship cannot enter the Irish Sea`` () =
    let state = testState ()
    // Move the German ship next to B2 first isn't needed for this check —
    // canEnterZone is what's being tested, reachable via a direct attempt
    // from A1 -> requires adjacency too, so use a ship already at B1.
    let germanAtB1 = { (testShip "GER-2" "TestScheer" German PocketBattleship (coord 'B' 1)) with EvasionRating = 26; MaxEvasionRating = 26 }
    let state' =
        { state with
            Players =
                state.Players
                |> Map.add German { state.Players.[German] with Ships = state.Players.[German].Ships.Add(germanAtB1.Id, germanAtB1) } }
    let result = update unusedTables (constantRoll 3) (MoveShip(ShipId "GER-2", coord 'B' 2)) state'
    Assert.True(isError result)

[<Fact>]
let ``German ship cannot enter a British port zone`` () =
    let state = testState ()
    let germanAtC2 = testShip "GER-3" "TestHipper" German HeavyCruiser (coord 'C' 2)
    let state' =
        { state with
            Players =
                state.Players
                |> Map.add German { state.Players.[German] with Ships = state.Players.[German].Ships.Add(germanAtC2.Id, germanAtC2) } }
    let result = update unusedTables (constantRoll 3) (MoveShip(ShipId "GER-3", coord 'C' 3)) state'
    Assert.True(isError result)

[<Fact>]
let ``A ship in patrol mode cannot move`` () =
    let state = testState ()
    let patrolling = { state.Players.[British].Ships.[ShipId "GBR-1"] with Mode = Patrol }
    let state' = { state with Players = state.Players |> Map.add British { state.Players.[British] with Ships = state.Players.[British].Ships.Add(patrolling.Id, patrolling) } }
    let result = update unusedTables (constantRoll 3) (MoveShip(ShipId "GBR-1", coord 'C' 2)) state'
    Assert.True(isError result)

[<Fact>]
let ``SetAirUnitMode updates the air unit's mode in the Visibility phase`` () =
    let state = { (testState ()) with Phase = Visibility }
    let airUnit = testAirUnit "AIR-1" "TestRecon" British LongRangeRecon (coord 'A' 1)
    let state' =
        { state with
            Players =
                state.Players
                |> Map.add British { state.Players.[British] with AirUnits = state.Players.[British].AirUnits.Add(airUnit.Id, airUnit) } }
    match update unusedTables (constantRoll 3) (SetAirUnitMode(airUnit.Id, ReconPatrol)) state' with
    | Ok updated -> Assert.Equal(ReconPatrol, updated.Players.[British].AirUnits.[airUnit.Id].Mode)
    | Error msg -> Assert.Fail msg

[<Fact>]
let ``A ship with no evasion-derived speed left this turn cannot move again`` () =
    let state = testState ()
    let usedUp = { state.Players.[British].Ships.[ShipId "GBR-1"] with ZonesMovedThisTurn = 2 }   // evasion 29 -> Speed2 -> allowance 2
    let state' = { state with Players = state.Players |> Map.add British { state.Players.[British] with Ships = state.Players.[British].Ships.Add(usedUp.Id, usedUp) } }
    let result = update unusedTables (constantRoll 3) (MoveShip(ShipId "GBR-1", coord 'C' 2)) state'
    Assert.True(isError result)

// --- fuel (rule 5.2x) ------------------------------------------------------

[<Fact>]
let ``A cruiser with no Fuel tracked moves for free`` () =
    let state = testState ()   // GBR-1 is a HeavyCruiser with Fuel = None
    let result = update unusedTables (constantRoll 3) (MoveShip(ShipId "GBR-1", coord 'C' 2)) state
    match result with
    | Ok state' -> Assert.Equal(None, state'.Players.[British].Ships.[ShipId "GBR-1"].Fuel)
    | Error msg -> Assert.Fail msg

[<Fact>]
let ``A battleship's second zone this turn costs one fuel factor`` () =
    let state = testState ()
    let fueledBB =
        { (testShip "GER-BB" "TestBB" German Battleship (coord 'B' 1)) with
            Fuel = Some { FactorsRemaining = 3; InEmergencyMovement = false } }
    let state' = { state with Players = state.Players |> Map.add German { state.Players.[German] with Ships = state.Players.[German].Ships.Add(fueledBB.Id, fueledBB) } }
    // First zone: B1 -> A1 is not adjacent (A1..B1 differ by letter only,
    // which IS adjacent) — use B1 -> B2 is Irish Sea (blocked for German),
    // so go B1 -> A1 (adjacent via letter) as the first zone instead.
    let afterFirst = update unusedTables (constantRoll 3) (MoveShip(ShipId "GER-BB", coord 'A' 1)) state'
    match afterFirst with
    | Error msg -> Assert.Fail $"first zone should succeed: {msg}"
    | Ok s1 ->
        let afterSecond = update unusedTables (constantRoll 3) (MoveShip(ShipId "GER-BB", coord 'A' 2)) s1
        match afterSecond with
        | Error msg -> Assert.Fail $"second zone should succeed: {msg}"
        | Ok s2 ->
            let ship = s2.Players.[German].Ships.[ShipId "GER-BB"]
            match ship.Fuel with
            | Some fuel -> Assert.Equal(2, fuel.FactorsRemaining)   // 3 - 1 for the second zone
            | None -> Assert.Fail "expected fuel to still be tracked"

[<Fact>]
let ``Bismarck gets the turn-1 breakout bonus allowing more than 2 zones`` () =
    let state = testState ()   // turn 4 = the real "first turn of play"
    let bismarck =
        { (testShip "GER-BB-Bismarck" "Bismarck" German Battleship (coord 'A' 1)) with
            Name = "Bismarck"
            Fuel = Some { FactorsRemaining = 5; InEmergencyMovement = false } }
    let state' = { state with Players = state.Players |> Map.add German { state.Players.[German] with Ships = state.Players.[German].Ships.Add(bismarck.Id, bismarck) } }
    // Move 3 zones (should all be free under the breakout bonus) —
    // A1->A2->A3->B3 (each step adjacent per the 3x3 test board).
    let r1 = update unusedTables (constantRoll 3) (MoveShip(ShipId "GER-BB-Bismarck", coord 'A' 2)) state'
    let r2 = r1 |> Result.bind (update unusedTables (constantRoll 3) (MoveShip(ShipId "GER-BB-Bismarck", coord 'A' 3)))
    let r3 = r2 |> Result.bind (update unusedTables (constantRoll 3) (MoveShip(ShipId "GER-BB-Bismarck", coord 'B' 3)))
    match r3 with
    | Ok s3 ->
        let ship = s3.Players.[German].Ships.[ShipId "GER-BB-Bismarck"]
        Assert.Equal(3, ship.ZonesMovedThisTurn)
        match ship.Fuel with
        | Some fuel -> Assert.Equal(5, fuel.FactorsRemaining)   // zones 1-3 are free
        | None -> Assert.Fail "expected fuel to still be tracked"
    | Error msg -> Assert.Fail $"breakout move should succeed: {msg}"

// --- search (rule 6.0/7.22) -------------------------------------------------

[<Fact>]
let ``SearchZone reveals an enemy ship when capacity meets visibility`` () =
    let state = testState ()
    // British ship (search strength 1/1) at C3 searching its own zone
    // against visibility 4 will NOT be enough -- lower visibility first.
    let lowVis = { state with Phase = Search; Turn = { state.Turn with Visibility = BismarckGame.Core.SearchBoard.VisibilityLevel 1 } }
    let germanAtC3Adjacent = testShip "GER-X" "TestContact" German LightCruiser (coord 'C' 3)
    let state' =
        { lowVis with
            Players =
                lowVis.Players
                |> Map.add German { lowVis.Players.[German] with Ships = lowVis.Players.[German].Ships.Add(germanAtC3Adjacent.Id, germanAtC3Adjacent) } }
    let result = update unusedTables (constantRoll 3) (SearchZone(British, coord 'C' 3)) state'
    match result with
    | Ok state'' ->
        Assert.Equal(1, state''.LocationMarkers.Length)
        Assert.True(state''.GermanLocatedTurn.IsSome)
    | Error msg -> Assert.Fail msg

[<Fact>]
let ``SearchZone finds nothing when capacity is below visibility`` () =
    let state = { (testState ()) with Phase = Search }   // visibility 4, ship search strength only 1
    let result = update unusedTables (constantRoll 3) (SearchZone(British, coord 'C' 3)) state
    match result with
    | Ok state' -> Assert.Equal(0, state'.LocationMarkers.Length)
    | Error msg -> Assert.Fail msg

// --- chance table convoy mechanics -----------------------------------------

[<Fact>]
let ``Chance roll 10 creates a convoy contact when the German ship is on route`` () =
    let state = { (testState ()) with Phase = Chance }
    let germanOnRoute = { state.Players.[German].Ships.[ShipId "GER-1"] with CurrentZone = Some(coord 'C' 3) }
    let state' =
        { state with
            Players =
                state.Players
                |> Map.add German { state.Players.[German] with Ships = state.Players.[German].Ships.Add(germanOnRoute.Id, germanOnRoute) } }

    match update unusedTables (constantRoll 5) (RollChanceForShip(ShipId "GER-1")) state' with
    | Error msg -> Assert.Fail msg
    | Ok after ->
        let contact = Assert.Single(after.ConvoyContacts)
        Assert.Equal(coord 'C' 3, contact.Zone)
        Assert.Equal(Some 1, contact.ConvoyId)
        Assert.Equal(German, contact.Discoverer)
        Assert.Equal(ChanceOnRoute, contact.Source)

[<Fact>]
let ``Chance roll 11 requires patrol and route proximity`` () =
    let state = { (testState ()) with Phase = Chance }
    let germanNearRoute =
        { state.Players.[German].Ships.[ShipId "GER-1"] with
            CurrentZone = Some(coord 'C' 1)
            Mode = Patrol }
    let state' =
        { state with
            Players =
                state.Players
                |> Map.add German { state.Players.[German] with Ships = state.Players.[German].Ships.Add(germanNearRoute.Id, germanNearRoute) } }

    let mutable seq11 = [ 5; 6 ]
    let rollEleven () =
        match seq11 with
        | x :: xs -> seq11 <- xs; x
        | [] -> 5

    match update unusedTables rollEleven (RollChanceForShip(ShipId "GER-1")) state' with
    | Error msg -> Assert.Fail msg
    | Ok after ->
        let contact = Assert.Single(after.ConvoyContacts)
        Assert.Equal(coord 'C' 3, contact.Zone)
        Assert.Equal(Some 1, contact.ConvoyId)
        Assert.Equal(ChanceNearRoute, contact.Source)

[<Fact>]
let ``Chance roll 11 without patrol creates no convoy contact`` () =
    let state = { (testState ()) with Phase = Chance }
    let germanNearRouteNoPatrol =
        { state.Players.[German].Ships.[ShipId "GER-1"] with
            CurrentZone = Some(coord 'C' 1)
            Mode = Movement }
    let state' =
        { state with
            Players =
                state.Players
                |> Map.add German { state.Players.[German] with Ships = state.Players.[German].Ships.Add(germanNearRouteNoPatrol.Id, germanNearRouteNoPatrol) } }

    let mutable seq11 = [ 5; 6 ]
    let rollEleven () =
        match seq11 with
        | x :: xs -> seq11 <- xs; x
        | [] -> 5

    match update unusedTables rollEleven (RollChanceForShip(ShipId "GER-1")) state' with
    | Error msg -> Assert.Fail msg
    | Ok after -> Assert.Empty(after.ConvoyContacts)

[<Fact>]
let ``Chance roll 12 creates a convoy contact when adjacent to route`` () =
    let state = { (testState ()) with Phase = Chance }
    let germanAdjacent = { state.Players.[German].Ships.[ShipId "GER-1"] with CurrentZone = Some(coord 'C' 2) }
    let state' =
        { state with
            Players =
                state.Players
                |> Map.add German { state.Players.[German] with Ships = state.Players.[German].Ships.Add(germanAdjacent.Id, germanAdjacent) } }

    let rollSix () = 6   // 6 + 6 = 12
    match update unusedTables rollSix (RollChanceForShip(ShipId "GER-1")) state' with
    | Error msg -> Assert.Fail msg
    | Ok after ->
        let contact = Assert.Single(after.ConvoyContacts)
        Assert.Equal(coord 'C' 3, contact.Zone)
        Assert.Equal(Some 1, contact.ConvoyId)
        Assert.Equal(ChanceAdjacentToRoute, contact.Source)

[<Fact>]
let ``AttackConvoy in NavalCombat consumes contact and awards first-convoy VP`` () =
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
                    Source = ChanceOnRoute
                    TurnLocated = 4 } ] }

    match update unusedTables (constantRoll 3) (AttackConvoy(ShipId "GER-1", coord 'A' 1)) state with
    | Error msg -> Assert.Fail msg
    | Ok after ->
        Assert.Empty(after.ConvoyContacts)
        Assert.Equal(1, after.ConvoysSunkByGerman)
        Assert.Equal(6, after.Players.[German].Score.Points)

[<Fact>]
let ``AttackConvoy outside NavalCombat is rejected`` () =
    let state =
        { (testState ()) with
            Phase = Search
            ConvoyContacts =
                [ { Zone = coord 'A' 1
                    ConvoyId = Some 1
                    Discoverer = German
                    Source = ChanceOnRoute
                    TurnLocated = 4 } ] }
    let result = update unusedTables (constantRoll 3) (AttackConvoy(ShipId "GER-1", coord 'A' 1)) state
    Assert.True(isError result)

[<Fact>]
let ``AttackConvoy follows 12.44 VP progression for first three convoys`` () =
    let state0 =
        { (testState ()) with
            Phase = NavalCombat
            ConvoyUnits =
                [ { Id = 1
                    Zone = coord 'A' 1
                    RouteIndex = 0
                    Direction = BismarckGame.Core.Markers.East
                    IsSunk = false }
                  { Id = 2
                    Zone = coord 'A' 1
                    RouteIndex = 0
                    Direction = BismarckGame.Core.Markers.East
                    IsSunk = false }
                  { Id = 3
                    Zone = coord 'A' 1
                    RouteIndex = 0
                    Direction = BismarckGame.Core.Markers.East
                    IsSunk = false } ]
            ConvoyContacts =
                [ { Zone = coord 'A' 1; ConvoyId = Some 1; Discoverer = German; Source = ChanceOnRoute; TurnLocated = 4 } ] }

    let state1 =
        { state0 with
            ConvoysSunkByGerman = 1
            ConvoyUnits =
                [ { Id = 1
                    Zone = coord 'A' 1
                    RouteIndex = 0
                    Direction = BismarckGame.Core.Markers.East
                    IsSunk = true }
                  { Id = 2
                    Zone = coord 'A' 1
                    RouteIndex = 0
                    Direction = BismarckGame.Core.Markers.East
                    IsSunk = false }
                  { Id = 3
                    Zone = coord 'A' 1
                    RouteIndex = 0
                    Direction = BismarckGame.Core.Markers.East
                    IsSunk = false } ]
            ConvoyContacts = [ { Zone = coord 'A' 1; ConvoyId = Some 2; Discoverer = German; Source = ChanceOnRoute; TurnLocated = 4 } ]
            Players =
                state0.Players
                |> Map.add German { state0.Players.[German] with Score = { state0.Players.[German].Score with Points = 6 } } }

    let state2 =
        { state0 with
            ConvoysSunkByGerman = 2
            ConvoyUnits =
                [ { Id = 1
                    Zone = coord 'A' 1
                    RouteIndex = 0
                    Direction = BismarckGame.Core.Markers.East
                    IsSunk = true }
                  { Id = 2
                    Zone = coord 'A' 1
                    RouteIndex = 0
                    Direction = BismarckGame.Core.Markers.East
                    IsSunk = true }
                  { Id = 3
                    Zone = coord 'A' 1
                    RouteIndex = 0
                    Direction = BismarckGame.Core.Markers.East
                    IsSunk = false } ]
            ConvoyContacts = [ { Zone = coord 'A' 1; ConvoyId = Some 3; Discoverer = German; Source = ChanceOnRoute; TurnLocated = 4 } ]
            Players =
                state0.Players
                |> Map.add German { state0.Players.[German] with Score = { state0.Players.[German].Score with Points = 12 } } }

    let p1 =
        match update unusedTables (constantRoll 3) (AttackConvoy(ShipId "GER-1", coord 'A' 1)) state0 with
        | Ok after -> after.Players.[German].Score.Points
        | Error msg -> Assert.Fail msg; 0

    let p2 =
        match update unusedTables (constantRoll 3) (AttackConvoy(ShipId "GER-1", coord 'A' 1)) state1 with
        | Ok after -> after.Players.[German].Score.Points
        | Error msg -> Assert.Fail msg; 0

    let p3 =
        match update unusedTables (constantRoll 3) (AttackConvoy(ShipId "GER-1", coord 'A' 1)) state2 with
        | Ok after -> after.Players.[German].Score.Points
        | Error msg -> Assert.Fail msg; 0

    Assert.Equal(6, p1)
    Assert.Equal(12, p2)
    Assert.Equal(20, p3)

// --- phase sequencing -------------------------------------------------------

[<Fact>]
let ``AdvancePhase cycles through all nine phases back to UnitAvailability`` () =
    let state = { (testState ()) with Phase = UnitAvailability }
    let rec advanceN n s =
        if n = 0 then s else advanceN (n - 1) (update unusedTables (constantRoll 3) AdvancePhase s |> Result.defaultWith (fun e -> failwith e))
    let afterNine = advanceN 9 state
    Assert.Equal(UnitAvailability, afterNine.Phase)

[<Fact>]
let ``AdvancePhase from Chance increments the turn number using real card numbering`` () =
    let state = { (testState ()) with Phase = Chance }
    match update unusedTables (constantRoll 3) AdvancePhase state with
    | Ok state' -> Assert.Equal(state.Turn.Number + 1, state'.Turn.Number)
    | Error msg -> Assert.Fail msg

[<Fact>]
let ``AdvancePhase from Chance moves live convoy units one route step`` () =
    let state =
        { (testState ()) with
            Phase = Chance
            ConvoyRoutePath = [ coord 'A' 1; coord 'A' 2; coord 'A' 3 ]
            ConvoyUnits = [ { Id = 1; Zone = coord 'A' 1; RouteIndex = 0; Direction = BismarckGame.Core.Markers.East; IsSunk = false } ]
            ConvoyContacts =
                [ { Zone = coord 'A' 1
                    ConvoyId = Some 1
                    Discoverer = German
                    Source = ChanceOnRoute
                    TurnLocated = 4 } ] }

    match update unusedTables (constantRoll 3) AdvancePhase state with
    | Error msg -> Assert.Fail msg
    | Ok after ->
        let convoy = Assert.Single(after.ConvoyUnits)
        Assert.Equal(coord 'A' 2, convoy.Zone)
        Assert.Equal(1, convoy.RouteIndex)
        let contact = Assert.Single(after.ConvoyContacts)
        Assert.Equal(coord 'A' 2, contact.Zone)
        Assert.Equal(Some 1, contact.ConvoyId)

[<Fact>]
let ``Sunk convoy units stop while live convoy units keep advancing across turns`` () =
    let state0 =
        { (testState ()) with
            Phase = Chance
            ConvoyRoutePath = [ coord 'A' 1; coord 'A' 2; coord 'A' 3; coord 'A' 4 ]
            ConvoyUnits =
                [ { Id = 1
                    Zone = coord 'A' 2
                    RouteIndex = 1
                    Direction = BismarckGame.Core.Markers.East
                    IsSunk = true }
                  { Id = 2
                    Zone = coord 'A' 2
                    RouteIndex = 1
                    Direction = BismarckGame.Core.Markers.East
                    IsSunk = false } ]
            ConvoyContacts =
                [ { Zone = coord 'A' 2
                    ConvoyId = Some 2
                    Discoverer = German
                    Source = ChanceOnRoute
                    TurnLocated = 4 } ] }

    let state1 =
        match update unusedTables (constantRoll 3) AdvancePhase state0 with
        | Error msg -> Assert.Fail msg; state0
        | Ok after -> after

    let sunkAfter1 = state1.ConvoyUnits |> List.find (fun c -> c.Id = 1)
    let liveAfter1 = state1.ConvoyUnits |> List.find (fun c -> c.Id = 2)
    Assert.Equal(coord 'A' 2, sunkAfter1.Zone)
    Assert.Equal(1, sunkAfter1.RouteIndex)
    Assert.Equal(coord 'A' 3, liveAfter1.Zone)
    Assert.Equal(2, liveAfter1.RouteIndex)

    let state2 =
        let toChance = { state1 with Phase = Chance }
        match update unusedTables (constantRoll 3) AdvancePhase toChance with
        | Error msg -> Assert.Fail msg; state1
        | Ok after -> after

    let sunkAfter2 = state2.ConvoyUnits |> List.find (fun c -> c.Id = 1)
    let liveAfter2 = state2.ConvoyUnits |> List.find (fun c -> c.Id = 2)
    Assert.Equal(coord 'A' 2, sunkAfter2.Zone)
    Assert.Equal(1, sunkAfter2.RouteIndex)
    Assert.Equal(coord 'A' 4, liveAfter2.Zone)
    Assert.Equal(3, liveAfter2.RouteIndex)

[<Fact>]
let ``AttackConvoy is blocked while convoy escort remains active in zone`` () =
    let baseState = testState ()
    let britishWithEscort =
        { baseState.Players.[British] with
            Ships =
                baseState.Players.[British].Ships
                |> Map.add (ShipId "GBR-ESC-1")
                    { (testShip "GBR-ESC-1" "EscortShip" British Battleship (coord 'A' 1)) with
                        IsConvoyEscort = true } }
    let state =
        { baseState with
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
                    Source = ChanceOnRoute
                    TurnLocated = 4 } ]
            Players = baseState.Players |> Map.add British britishWithEscort }

    match update unusedTables (constantRoll 3) (AttackConvoy(ShipId "GER-1", coord 'A' 1)) state with
    | Ok _ -> Assert.Fail "Expected attack to fail while escort ship is active"
    | Error msg -> Assert.Contains("screened by active escort", msg)
