module BismarckGame.Tests.PersistenceTests

open System
open System.IO
open Xunit
open BismarckGame.Core.Common
open BismarckGame.Core.SearchBoard
open BismarckGame.Core.GameState
open BismarckGame.Core.Markers
open BismarckGame.Core.Units
open BismarckGame.Core.Configuration
open BismarckGame.Core.Scenarios.BismarckBasicGame
open BismarckGame.Core.Persistence
open BismarckGame.Core.EventLogger
open BismarckGame.Core.Simulation
open BismarckGame.Tests.TestHelpers

let private tempFile (prefix: string) =
    let dir = Path.Combine(Path.GetTempPath(), "bismarck-tests")
    Directory.CreateDirectory(dir) |> ignore
    Path.Combine(dir, $"{prefix}-{Guid.NewGuid():N}.xml")

[<Fact>]
let ``GameOptions XML round-trip preserves values`` () =
    let path = tempFile "options"
    try
        let options =
            { defaultGameOptions with
                ScenarioId = "custom-scenario"
                UseFixedSeed = false
                RandomSeed = 1234
                AutoSimulateTurns = 5
                AutoSaveEnabled = true
                AutoSaveEveryTurns = 2
                EnableEventLogging = true }

        saveGameOptionsToFile defaultXmlPersistenceOptions path options
        let loaded = loadGameOptionsFromFile path

        Assert.Equal(options.ScenarioId, loaded.ScenarioId)
        Assert.Equal(options.UseFixedSeed, loaded.UseFixedSeed)
        Assert.Equal(options.RandomSeed, loaded.RandomSeed)
        Assert.Equal(options.AutoSimulateTurns, loaded.AutoSimulateTurns)
        Assert.Equal(options.AutoSaveEnabled, loaded.AutoSaveEnabled)
        Assert.Equal(options.AutoSaveEveryTurns, loaded.AutoSaveEveryTurns)
        Assert.Equal(options.EnableEventLogging, loaded.EnableEventLogging)
    finally
        if File.Exists(path) then File.Delete(path)

[<Fact>]
let ``AppConfiguration XML round-trip preserves path and xml settings`` () =
    let path = tempFile "configuration"
    try
        let cfg =
            { defaultAppConfiguration with
                Paths =
                    { defaultStoragePaths with
                        RootPath = "C:\\Data"
                        SaveDirectory = "saves2"
                        LogsDirectory = "logs2"
                        DatabasePath = "db\\core.sqlite"
                        ImagesDirectory = "assets\\images"
                        XmlDirectory = "xml"
                        OptionsFileName = "opts.xml"
                        ConfigurationFileName = "cfg.xml"
                        GameStatusFileName = "status.xml"
                        EventLogFileName = "events.xml" }
                Xml =
                    { defaultXmlPersistenceOptions with
                        IndentOutput = false
                        OmitXmlDeclaration = true
                        EncodingName = "utf-8" }
                LastScenarioId = "scenario-z"
                LastStatusFilePath = "xml\\status.xml" }

        saveConfigurationToFile defaultXmlPersistenceOptions path cfg
        let loaded = loadConfigurationFromFile path

        Assert.Equal(cfg.Paths.RootPath, loaded.Paths.RootPath)
        Assert.Equal(cfg.Paths.SaveDirectory, loaded.Paths.SaveDirectory)
        Assert.Equal(cfg.Paths.LogsDirectory, loaded.Paths.LogsDirectory)
        Assert.Equal(cfg.Paths.DatabasePath, loaded.Paths.DatabasePath)
        Assert.Equal(cfg.Paths.ImagesDirectory, loaded.Paths.ImagesDirectory)
        Assert.Equal(cfg.Paths.XmlDirectory, loaded.Paths.XmlDirectory)
        Assert.Equal(cfg.Paths.OptionsFileName, loaded.Paths.OptionsFileName)
        Assert.Equal(cfg.Paths.ConfigurationFileName, loaded.Paths.ConfigurationFileName)
        Assert.Equal(cfg.Paths.GameStatusFileName, loaded.Paths.GameStatusFileName)
        Assert.Equal(cfg.Paths.EventLogFileName, loaded.Paths.EventLogFileName)
        Assert.Equal(cfg.Xml.IndentOutput, loaded.Xml.IndentOutput)
        Assert.Equal(cfg.Xml.OmitXmlDeclaration, loaded.Xml.OmitXmlDeclaration)
        Assert.Equal(cfg.Xml.EncodingName, loaded.Xml.EncodingName)
        Assert.Equal(cfg.LastScenarioId, loaded.LastScenarioId)
        Assert.Equal(cfg.LastStatusFilePath, loaded.LastStatusFilePath)
    finally
        if File.Exists(path) then File.Delete(path)

[<Fact>]
let ``Game status XML save-load restores dynamic state`` () =
    let path = tempFile "status"
    try
        let baseState = testState ()

        let german = baseState.Players.[German]
        let bShip = german.Ships.[ShipId "GER-1"]
        let updatedShip =
            { bShip with
                CurrentZone = Some (coord 'A' 2)
                Mode = Patrol
                MidshipsHits = 2
                IsSunk = false
                Fuel = Some { FactorsRemaining = 3; InEmergencyMovement = true } }

        let british = baseState.Players.[British]
        let recon = testAirUnit "AIR-TEST" "TestRecon" British LongRangeRecon (coord 'C' 2)

        let stateToSave : GameState =
            { baseState with
                Turn = { Number = 9; IsNightTurn = true; IsEmergencyMovementTurn = true; Visibility = VisibilityLevel 3 }
                Phase = Chance
                ConvoysAvailable = 4
                ConvoysSunkByGerman = 1
                ConvoyContacts =
                    [ { Zone = coord 'A' 2
                        ConvoyId = Some 1
                        Discoverer = German
                        Source = ChanceNearRoute
                        TurnLocated = 9 } ]
                ConvoyUnits =
                    [ { Id = 1; Zone = coord 'B' 3; RouteIndex = 3; Direction = East; IsSunk = true }
                      { Id = 2; Zone = coord 'C' 3; RouteIndex = 4; Direction = East; IsSunk = false } ]
                Players =
                    baseState.Players
                    |> Map.add German { german with Ships = german.Ships.Add(updatedShip.Id, updatedShip) }
                    |> Map.add British { british with AirUnits = british.AirUnits.Add(recon.Id, recon) }
                GermanLocatedTurn = Some 8 }

        saveGameStatusToFile defaultXmlPersistenceOptions path stateToSave

        let loaded = loadGameStatusFromFile path (testState ())

        Assert.Equal(9, loaded.Turn.Number)
        Assert.True(loaded.Turn.IsNightTurn)
        Assert.Equal(Chance, loaded.Phase)
        Assert.Equal(4, loaded.ConvoysAvailable)
        Assert.Equal(1, loaded.ConvoysSunkByGerman)
        Assert.Equal(2, loaded.ConvoyUnits.Length)
        Assert.Equal(Some 8, loaded.GermanLocatedTurn)

        let loadedShip = loaded.Players.[German].Ships.[ShipId "GER-1"]
        Assert.Equal(Some (coord 'A' 2), loadedShip.CurrentZone)
        Assert.Equal(Patrol, loadedShip.Mode)
        Assert.Equal(2, loadedShip.MidshipsHits)

        match loadedShip.Fuel with
        | Some fuel ->
            Assert.Equal(3, fuel.FactorsRemaining)
            Assert.True(fuel.InEmergencyMovement)
        | None -> Assert.Fail("Expected fuel to be restored")

        let loadedAir = loaded.Players.[British].AirUnits.[AirUnitId "AIR-TEST"]
        Assert.Equal(Some (coord 'C' 2), loadedAir.CurrentZone)
        Assert.Equal(recon.Mode, loadedAir.Mode)
    finally
        if File.Exists(path) then File.Delete(path)

[<Fact>]
let ``Search map XML load preserves zones and key attributes`` () =
    let path = tempFile "search-map"
    try
        saveSearchMapToFile defaultXmlPersistenceOptions path searchBoard
        let loaded = loadSearchMapFromFile path

        Assert.Equal(searchBoard.Zones.Count, loaded.Zones.Count)

        let c19 = { Letter = 'C'; Number = 19 }
        let h18 = { Letter = 'H'; Number = 18 }
        let e3 = { Letter = 'E'; Number = 3 }

        let zoneC19 = loaded.Zones.[c19]
        let zoneH18 = loaded.Zones.[h18]
        let zoneE3 = loaded.Zones.[e3]

        match zoneC19.Terrain with
        | Port German -> ()
        | _ -> Assert.Fail("Expected C19 to remain a German port")

        match zoneH18.Terrain with
        | Port British -> ()
        | _ -> Assert.Fail("Expected H18 to remain a British port")

        Assert.True(zoneE3.IsWhiteDot)
    finally
        if File.Exists(path) then File.Delete(path)

[<Fact>]
let ``Event logger writes and reads XML entries including movement tagging`` () =
    let path = tempFile "events"
    try
        let events : SimulationEvent list =
            [ { Phase = ShipMovement
                Label = "move"
                Command = MoveShip(ShipId "GER-1", coord 'A' 2)
                Succeeded = true
                Message = None }
              { Phase = ShipMovement
                Label = "phase"
                Command = AdvancePhase
                Succeeded = true
                Message = None } ]

        let entries = toLogEntries 4 ShipMovement 1 events
        saveEventLogToFile defaultXmlPersistenceOptions path "scenario-test" entries
        let loaded = loadEventLogFromFile path

        Assert.Equal("scenario-test", loaded.ScenarioId)
        Assert.Equal(2, loaded.Entries.Length)
        Assert.True(loaded.Entries.[0].IsMovement)
        Assert.False(loaded.Entries.[1].IsMovement)
    finally
        if File.Exists(path) then File.Delete(path)
