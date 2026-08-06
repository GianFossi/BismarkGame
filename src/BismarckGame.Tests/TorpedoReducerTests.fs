module BismarckGame.Tests.TorpedoReducerTests

open Xunit
open BismarckGame.Core.BattleBoard
open BismarckGame.Core.Common
open BismarckGame.Core.GameState
open BismarckGame.Core.PlayerView
open BismarckGame.Core.Update
open BismarckGame.Tests.TestHelpers

let private battleShip id name cls pos =
    { ShipId = ShipId id; Name = name; Class = cls; Position = pos; Facing = HexN
      GunSections = []; SecondaryHits = 0; EvasionRating = 29; MidshipsHits = 0
      MaxMidshipsHits = 10; PermanentEvasionLoss = 0; IsWithdrawing = false; IsSunk = false }

let private stateWithBattle () =
    let baseState = testState ()
    let g = { testShip "GER-1" "Prinz Eugen" German HeavyCruiser (coord 'A' 1) with TorpedoesRemaining = 6 }
    let b = testShip "GBR-1" "Hood" British Battleship (coord 'A' 1)
    let battle =
        { Id = 1; Zone = coord 'A' 1
          Ships = [ battleShip "GER-1" "Prinz Eugen" HeavyCruiser HexCoord.Zero
                    battleShip "GBR-1" "Hood" Battleship (hexNeighbor HexCoord.Zero HexN) ] |> List.map (fun s -> s.ShipId, s) |> Map.ofList
          Round = 1; ReinforcementAttempts = Map.empty; TaskForceReinforcementAttempts = Map.empty
          TorpedoSalvosFired = Map.empty; TorpedoTargets = Map.empty; DefensiveFireResolved = Set.empty
          SpecialFireChecked = Set.empty; FiredOrders = Set.empty }
    { baseState with Phase = TorpedoAttack; Players = baseState.Players |> Map.add German { baseState.Players.[German] with Ships = Map.ofList [ g.Id, g ] } |> Map.add British { baseState.Players.[British] with Ships = Map.ofList [ b.Id, b ] }; ActiveBattles = [ battle ] }

let private unwrap result =
    match result with
    | Ok value -> value
    | Error message -> failwith message

[<Fact>]
let ``launch consumes salvos and records the target`` () =
    let result = update unusedTables (constantRoll 6) (LaunchTorpedoSalvo(1, ShipId "GER-1", ShipId "GBR-1", 2)) (stateWithBattle ())
    match result with
    | Error e -> Assert.Fail e
    | Ok state ->
        Assert.Equal(4, state.Players.[German].Ships.[ShipId "GER-1"].TorpedoesRemaining)
        Assert.Equal(Some(ShipId "GBR-1"), state.ActiveBattles.Head.TorpedoTargets.TryFind(ShipId "GER-1"))

[<Fact>]
let ``torpedo resolution consumes one pending salvo`` () =
    let state = stateWithBattle ()
    let launched = update unusedTables (constantRoll 6) (LaunchTorpedoSalvo(1, ShipId "GER-1", ShipId "GBR-1", 2)) state |> unwrap
    let resolved = update unusedTables (constantRoll 2) (ResolveBritishTorpedoSalvo(1, ShipId "GBR-1")) launched |> unwrap
    Assert.Equal(Some 1, Map.tryFind (ShipId "GER-1") resolved.ActiveBattles.Head.TorpedoSalvosFired)

[<Fact>]
let ``player view exposes only own torpedo salvos`` () =
    let state = stateWithBattle ()
    let battle = { state.ActiveBattles.Head with TorpedoSalvosFired = Map.ofList [ ShipId "GER-1", 2; ShipId "GBR-1", 1 ] }
    let view = project { state with ActiveBattles = [ battle ] } German
    Assert.Equal<list<ShipId * int>>([ (ShipId "GER-1", 2) ], view.VisibleBattles.Head.OwnTorpedoSalvosFired)

[<Fact>]
let ``replenish restores the printed torpedo capacity in a friendly port`` () =
    let state = stateWithBattle ()
    let ship = state.Players.[German].Ships.[ShipId "GER-1"]
    let damaged = { state with ActiveBattles = []; Players = state.Players |> Map.add German { state.Players.[German] with Ships = state.Players.[German].Ships.Add(ship.Id, { ship with TorpedoesRemaining = 0 }) } }
    let result = update unusedTables (constantRoll 6) (ReplenishTorpedoes ship.Id) damaged |> unwrap
    Assert.Equal(6, result.Players.[German].Ships.[ship.Id].TorpedoesRemaining)
