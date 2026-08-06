module BismarckGame.Tests.FeatureCoverageTests

open System
open System.IO
open Xunit
open BismarckGame.Core.Common
open BismarckGame.Core.Configuration
open BismarckGame.Core.GameState
open BismarckGame.Core.Persistence
open BismarckGame.Core.Units
open BismarckGame.Core.Update
open BismarckGame.Tests.TestHelpers

let private tempFile prefix =
    let path = Path.Combine(Path.GetTempPath(), $"bismarck-{prefix}-{Guid.NewGuid():N}.xml")
    path

[<Fact>]
let ``all rule options survive XML round trip`` () =
    let path = tempFile "options"
    try
        let options =
            { defaultGameOptions with
                EnableFullBattleBoardRules = false
                EnableFullWithdrawalRules = false
                EnableBattleReinforcements = false
                EnableSpecialShipRules = false
                EnablePerZoneFog = false
                EnableCompleteHuffDuff = false }
        saveGameOptionsToFile defaultXmlPersistenceOptions path options
        let loaded = loadGameOptionsFromFile path
        Assert.Equal(options.EnableFullBattleBoardRules, loaded.EnableFullBattleBoardRules)
        Assert.Equal(options.EnableFullWithdrawalRules, loaded.EnableFullWithdrawalRules)
        Assert.Equal(options.EnableBattleReinforcements, loaded.EnableBattleReinforcements)
        Assert.Equal(options.EnableSpecialShipRules, loaded.EnableSpecialShipRules)
        Assert.Equal(options.EnablePerZoneFog, loaded.EnablePerZoneFog)
        Assert.Equal(options.EnableCompleteHuffDuff, loaded.EnableCompleteHuffDuff)
    finally
        if File.Exists path then File.Delete path

[<Fact>]
let ``search is blocked only in the fogged zone`` () =
    let state = { testState () with Phase = Search; FogZones = Set.singleton (coord 'C' 3) }
    let blocked = update unusedTables (constantRoll 3) (SearchZone(British, coord 'C' 3)) state
    let clear = update unusedTables (constantRoll 3) (SearchZone(British, coord 'A' 1)) state
    Assert.True(Result.isError blocked)
    Assert.True(Result.isOk clear)

[<Fact>]
let ``Huff-Duff accepts the current or an adjacent zone and reveals only location`` () =
    let state = { testState () with Phase = Chance }
    match update unusedTables (constantRoll 3) (ResolveHuffDuff(ShipId "GER-1", coord 'A' 2)) state with
    | Error message -> Assert.Fail message
    | Ok updated ->
        let marker = Assert.Single updated.LocationMarkers
        Assert.Equal(coord 'A' 2, marker.Zone)
        Assert.Equal(None, marker.RevealedShipClass)

[<Fact>]
let ``Huff-Duff rejects a non-adjacent zone`` () =
    let state = { testState () with Phase = Chance }
    let result = update unusedTables (constantRoll 3) (ResolveHuffDuff(ShipId "GER-1", coord 'C' 3)) state
    Assert.True(Result.isError result)

[<Fact>]
let ``air unit cannot launch during mandatory refit`` () =
    let state = testState ()
    let bomber = { testAirUnit "AIR-REFIT" "Test Bomber" British TorpedoBomber (coord 'C' 3) with IsAtBase = false }
    let airborne = { state with Phase = AirAttack; Players = state.Players.Add(British, { state.Players.[British] with AirUnits = Map.ofList [ bomber.Id, bomber ] }) }
    let returned = update unusedTables (constantRoll 3) (ReturnAirUnitToBase bomber.Id) airborne
    match returned with
    | Error message -> Assert.Fail message
    | Ok landed ->
        let nextTurn = { landed with Phase = AirMovement }
        let result = update unusedTables (constantRoll 3) (MoveAirUnit(bomber.Id, coord 'C' 3)) nextTurn
        Assert.True(Result.isError result)

[<Fact>]
let ``naval combat creates a Battle Board action for located opposing ships`` () =
    let state = testState ()
    let german = { state.Players.[German].Ships.[ShipId "GER-1"] with CurrentZone = Some(coord 'C' 3); EvasionRating = 36; MaxEvasionRating = 36 }
    let british = { state.Players.[British].Ships.[ShipId "GBR-1"] with CurrentZone = Some(coord 'C' 3); EvasionRating = 35; MaxEvasionRating = 35 }
    let combatState =
        { state with
            Phase = NavalCombat
            Players =
                state.Players
                |> Map.add German { state.Players.[German] with Ships = Map.ofList [ german.Id, german ] }
                |> Map.add British { state.Players.[British] with Ships = Map.ofList [ british.Id, british ] } }
    match update unusedTables (constantRoll 3) (InitiateNavalCombat(coord 'C' 3, German)) combatState with
    | Error message -> Assert.Fail message
    | Ok updated ->
        let battle = Assert.Single updated.ActiveBattles
        Assert.Equal(coord 'C' 3, battle.Zone)
        Assert.Equal(1, battle.Round)
        Assert.Equal(2, battle.Ships.Count)

[<Fact>]
let ``air attack respects night restriction from the basic errata rules`` () =
    let state = testState ()
    let bomber = { testAirUnit "AIR-NIGHT" "Test Bomber" British TorpedoBomber (coord 'C' 3) with IsAtBase = false }
    let target = { state.Players.[German].Ships.[ShipId "GER-1"] with CurrentZone = Some(coord 'C' 3) }
    let attackState =
        { state with
            Phase = AirAttack
            Turn = { state.Turn with IsNightTurn = true }
            Players =
                state.Players
                |> Map.add German { state.Players.[German] with Ships = Map.ofList [ target.Id, target ] }
                |> Map.add British { state.Players.[British] with AirUnits = Map.ofList [ bomber.Id, bomber ] } }
    let result = update unusedTables (constantRoll 3) (LaunchAirAttack(bomber.Id, target.Id)) attackState
    Assert.True(Result.isError result)
