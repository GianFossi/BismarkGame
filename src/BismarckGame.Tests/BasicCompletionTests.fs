module BismarckGame.Tests.BasicCompletionTests

open System
open System.IO
open Xunit
open BismarckGame.Core.Common
open BismarckGame.Core.GameState
open BismarckGame.Core.Persistence
open BismarckGame.Core.Configuration
open BismarckGame.Core.Update
open BismarckGame.Tests.TestHelpers

let private tempFile () = Path.Combine(Path.GetTempPath(), $"bismarck-round-{Guid.NewGuid():N}.xml")

[<Fact>]
let ``multiple XML snapshots restore each round independently`` () =
    let path = tempFile ()
    try
        let initial = testState ()
        let round1 = { initial with Turn = { initial.Turn with Number = 5 }; Phase = Search }
        let round2 = { round1 with Turn = { round1.Turn with Number = 6 }; Phase = Chance; FogZones = Set.ofList [ coord 'A' 2 ] }
        saveGameStatusToFile defaultXmlPersistenceOptions path round1
        let loaded1 = loadGameStatusFromFile path initial
        saveGameStatusToFile defaultXmlPersistenceOptions path round2
        let loaded2 = loadGameStatusFromFile path initial
        Assert.Equal(5, loaded1.Turn.Number)
        Assert.Equal(Search, loaded1.Phase)
        Assert.Equal(6, loaded2.Turn.Number)
        Assert.Equal(Chance, loaded2.Phase)
        Assert.Contains(coord 'A' 2, loaded2.FogZones)
    finally
        if File.Exists path then File.Delete path

[<Fact>]
let ``repair phase does not repair in an enemy port`` () =
    let state = testState ()
    let ship = state.Players.[German].Ships.[ShipId "GER-1"]
    let damaged = { ship with EvasionRating = 20; ZonesMovedThisTurn = 0; CurrentZone = Some (coord 'C' 3) }
    let state' = { state with Phase = ShipMovement; Players = state.Players |> Map.add German { state.Players.[German] with Ships = Map.add damaged.Id damaged state.Players.[German].Ships } }
    let result = update unusedTables (constantRoll 1) AdvancePhase state'
    match result with
    | Error e -> Assert.Fail e
    | Ok updated -> Assert.Equal(20, updated.Players.[German].Ships.[damaged.Id].EvasionRating)
