module BismarckGame.Tests.Rule9ErrataTests

open Xunit
open BismarckGame.Core.BattleBoard
open BismarckGame.Core.Common
open BismarckGame.Core.GameState
open BismarckGame.Core.Markers
open BismarckGame.Core.Tables
open BismarckGame.Core.Units
open BismarckGame.Core.Update
open BismarckGame.Core.VictoryConditions
open BismarckGame.Tests.TestHelpers

let private battleState state round ships =
    { Id = 99
      Zone = coord 'C' 3
      Ships = ships |> List.map (fun s -> s.ShipId, s) |> Map.ofList
      Round = round
      ReinforcementAttempts = Map.empty
      TaskForceReinforcementAttempts = Map.empty
      TorpedoSalvosFired = Map.empty
      TorpedoTargets = Map.empty
      DefensiveFireResolved = Set.empty
      SpecialFireChecked = Set.empty
      FiredOrders = Set.empty }

let private battleShip id name cls position evasion withdrawing =
    { ShipId = ShipId id
      Name = name
      Class = cls
      Position = position
      Facing = HexN
      GunSections = []
      SecondaryHits = 0
      EvasionRating = evasion
      MidshipsHits = 0
      MaxMidshipsHits = 10
      PermanentEvasionLoss = 0
      IsWithdrawing = withdrawing
      IsSunk = false }

[<Fact>]
let ``9.91 withdrawal is rejected before a round is complete`` () =
    let state = testState ()
    let german = battleShip "GER-1" "TestBismarck" Battleship HexCoord.Zero 35 false
    let british = battleShip "GBR-1" "TestCruiser" HeavyCruiser (hexNeighbor HexCoord.Zero HexN) 20 false
    let state' = { state with ActiveBattles = [ battleState state 1 [ german; british ] ] }
    let result = update unusedTables (constantRoll 3) (WithdrawFromBattle(ShipId "GER-1")) state'
    Assert.True(Result.isOk result)

[<Fact>]
let ``9.93 withdrawal requires higher evasion than eligible enemies`` () =
    let state = testState ()
    let german = battleShip "GER-1" "TestBismarck" Battleship HexCoord.Zero 20 false
    let british = battleShip "GBR-1" "TestCruiser" HeavyCruiser (hexNeighbor HexCoord.Zero HexN) 20 false
    let state' = { state with ActiveBattles = [ battleState state 1 [ german; british ] ] }
    let result = update unusedTables (constantRoll 3) (WithdrawFromBattle(ShipId "GER-1")) state'
    Assert.True(Result.isError result)

[<Fact>]
let ``9.95 to 9.97 withdrawing ship receives a bonus move and exits beyond six hexes`` () =
    let state = testState ()
    let german = battleShip "GER-1" "TestBismarck" Battleship HexCoord.Zero 35 true
    let british = battleShip "GBR-1" "TestCruiser" HeavyCruiser { Q = 7; R = -7; S = 0 } 20 false
    let state' =
        { state with
            ActiveBattles = [ battleState state 1 [ german; british ] ]
            Players = state.Players |> Map.add German { state.Players.[German] with Ships = state.Players.[German].Ships.Add(ShipId "GER-1", state.Players.[German].Ships.[ShipId "GER-1"]) } }
    match update unusedTables (constantRoll 3) (AdvanceBattleRound 99) state' with
    | Error message -> Assert.Fail message
    | Ok updated -> Assert.Empty updated.ActiveBattles

[<Fact>]
let ``9.32 and 12.7 prevent ending a port battle while both sides remain`` () =
    let state = testState ()
    let german = battleShip "GER-1" "TestBismarck" Battleship HexCoord.Zero 35 false
    let british = battleShip "GBR-1" "TestCruiser" HeavyCruiser (hexNeighbor HexCoord.Zero HexN) 20 false
    let state' = { state with ActiveBattles = [ battleState state 1 [ german; british ] ] }
    let result = update unusedTables (constantRoll 3) (EndNavalCombat 99) state'
    Assert.True(Result.isError result)

[<Fact>]
let ``9.41 reinforcement cannot be attempted before round three`` () =
    let state = testState ()
    let german = battleShip "GER-1" "TestBismarck" Battleship HexCoord.Zero 35 false
    let british = battleShip "GBR-1" "TestCruiser" HeavyCruiser (hexNeighbor HexCoord.Zero HexN) 20 false
    let state' = { state with ActiveBattles = [ battleState state 2 [ german; british ] ] }
    let result = update unusedTables (constantRoll 1) (AttemptBattleReinforcement(99, ShipId "GER-1")) state'
    Assert.True(Result.isError result)

[<Fact>]
let ``9.41 subsequent reinforcement attempts use the progressive threshold`` () =
    let state = testState ()
    let german = battleShip "GER-1" "TestBismarck" Battleship HexCoord.Zero 35 false
    let british = battleShip "GBR-1" "TestCruiser" HeavyCruiser { Q = 7; R = -7; S = 0 } 20 false
    let candidate = testShip "GER-2" "Reinforcement" German Battleship (coord 'C' 3)
    let battle = { battleState state 3 [ german; british ] with ReinforcementAttempts = Map.ofList [ candidate.Id, 1 ] }
    let state' =
        { state with
            ActiveBattles = [ battle ]
            Players = state.Players |> Map.add German { state.Players.[German] with Ships = state.Players.[German].Ships.Add(candidate.Id, candidate) } }
    let result = update unusedTables (constantRoll 2) (AttemptBattleReinforcement(99, candidate.Id)) state'
    Assert.True(Result.isOk result)

[<Fact>]
let ``9.84 cruisers use special damage table A`` () =
    let order = { Firer = ShipId "GER-BB-Test"; Target = ShipId "GBR-CA-Test"; Section = BowGuns; SalvoesFired = 1; Range = RangeB; Aspect = Broadside }
    let result = NavalFireTables.resolve Set.empty order (fun () -> 11)
    match result with
    | HitMidships _ -> ()
    | _ -> Assert.Fail "Expected cruiser special damage to use table A"

[<Fact>]
let ``9.81 and 9.82 Rodney data exposes the special turret profile`` () =
    let rodney = ShipStats.shipStats.["Rodney"]
    Assert.Equal(8, rodney.BowMaxSalvo)
    Assert.Equal(4, rodney.SternMaxSalvo)
    Assert.Equal(1, rodney.MaxSpeedZones)
    Assert.Equal(6, rodney.MaxMidshipsHits)

[<Fact>]
let ``9.83 KGV and Prince of Wales use the asterisked heavy-armor profile`` () =
    for name in [ "King George V"; "Prince of Wales" ] do
        let stats = ShipStats.shipStats.[name]
        Assert.Equal(7, stats.BowMaxSalvo)
        Assert.Equal(5, stats.SternMaxSalvo)
        Assert.Equal(7, stats.MaxMidshipsHits)
    let order = { Firer = ShipId "GER-BB-Test"; Target = ShipId "GBR-BB-KingGeorgeV"; Section = BowGuns; SalvoesFired = 1; Range = RangeA; Aspect = Broadside }
    match NavalFireTables.resolve NavalFireTables.heavyArmoredShipNames order (fun () -> 12) with
    | HitMidships(3, Some 10) -> ()
    | result -> Assert.Fail $"Unexpected heavy-armor result: {result}"

[<Fact>]
let ``40.7 a damaged ship in port repairs two evasion factors`` () =
    let state = testState ()
    let ship = { state.Players.[British].Ships.[ShipId "GBR-1"] with EvasionRating = 20; MaxEvasionRating = 29; CurrentZone = Some(coord 'C' 3); ZonesMovedThisTurn = 0 }
    let state' = { state with Phase = ShipMovement; Players = state.Players |> Map.add British { state.Players.[British] with Ships = Map.ofList [ ship.Id, ship ] } }
    match update unusedTables (constantRoll 1) AdvancePhase state' with
    | Error message -> Assert.Fail message
    | Ok updated -> Assert.Equal(22, updated.Players.[British].Ships.[ship.Id].EvasionRating)

[<Fact>]
let ``Victory schedule covers Victorious Ark Royal Renown Revenge heavy and light cruisers`` () =
    let outcomes : ShipOutcome list =
        [ { Name = "Victorious"; Nationality = British; Class = AircraftCarrier; IsSunk = true; MidshipsHits = 0 }
          { Name = "Ark Royal"; Nationality = British; Class = AircraftCarrier; IsSunk = true; MidshipsHits = 0 }
          { Name = "Renown"; Nationality = British; Class = Battlecruiser; IsSunk = true; MidshipsHits = 0 }
          { Name = "Revenge"; Nationality = British; Class = Battleship; IsSunk = true; MidshipsHits = 0 }
          { Name = "Heavy"; Nationality = British; Class = HeavyCruiser; IsSunk = true; MidshipsHits = 0 }
          { Name = "Light"; Nationality = British; Class = LightCruiser; IsSunk = true; MidshipsHits = 0 } ]
    let german = evaluate basicGameDamagePoints outcomes |> List.find (fun s -> s.Nationality = German)
    Assert.Equal(24 + 20 + 10 + 8 + 6 + 4, german.Points)

[<Fact>]
let ``12.31 and 12.41 sinking schedule values are scored`` () =
    let outcomes : ShipOutcome list =
        [ { Name = "Bismarck"; Nationality = German; Class = Battleship; IsSunk = true; MidshipsHits = 10 }
          { Name = "Victorious"; Nationality = British; Class = AircraftCarrier; IsSunk = true; MidshipsHits = 0 }
          { Name = "Renown"; Nationality = British; Class = Battlecruiser; IsSunk = true; MidshipsHits = 0 } ]
    let scores = evaluate basicGameDamagePoints outcomes
    Assert.Equal(30, scores |> List.find (fun s -> s.Nationality = British) |> fun s -> s.Points)
    Assert.Equal(34, scores |> List.find (fun s -> s.Nationality = German) |> fun s -> s.Points)
