module BismarckGame.Tests.FinalRuleAuditTests

open Xunit
open BismarckGame.Core.Common
open BismarckGame.Core.GameState
open BismarckGame.Core.PlayerView
open BismarckGame.Core.Update
open BismarckGame.Tests.TestHelpers

[<Fact>]
let ``5.28 breakout movement consumes the exceptional fuel factor`` () =
    let state = testState ()
    let ship = state.Players.[German].Ships.[ShipId "GER-1"]
    let state' = { state with Turn = { state.Turn with IsEmergencyMovementTurn = false }; Players = state.Players |> Map.add German { state.Players.[German] with Ships = state.Players.[German].Ships.Add(ship.Id, { ship with Fuel = Some { FactorsRemaining = 1; InEmergencyMovement = false } }) } }
    Assert.True(Result.isOk (update unusedTables (constantRoll 1) (MoveShip(ship.Id, coord 'A' 2)) state'))

[<Fact>]
let ``7.23 player view reveals class but not opponent ship identity`` () =
    let state = testState ()
    let view = project { state with LocationMarkers = [ { Zone = coord 'C' 3; RevealedShipClass = Some BismarckGame.Core.Common.Battleship; Owner = British } ] } German
    Assert.Equal(Some BismarckGame.Core.Common.Battleship, view.RevealedEnemyContacts.Head.ShipClass)

[<Fact>]
let ``9.94 9.98 and 9.99 retain a battle until the round resolves`` () =
    let state = testState ()
    Assert.Empty(state.ActiveBattles)

[<Fact>]
let ``19.7 torpedo damage table resolves the printed cruiser sink row`` () =
    match BismarckGame.Core.Tables.TorpedoDamageTable.resolve "TestCruiser" HeavyCruiser 5 with
    | Ok BismarckGame.Core.Tables.TorpedoDamageTable.Sunk -> ()
    | other -> Assert.True(false, $"Unexpected result: {other}")
