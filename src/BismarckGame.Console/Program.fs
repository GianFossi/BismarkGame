/// <summary>
/// Program.fs
/// Console harness: references BismarckGame.Core and plays a couple of
/// turns of the historical 1941 Basic Game scenario, printing every
/// command's result. This is a debugging/smoke-test tool, not a real
/// game client — it exists so the whole command pipeline (phase
/// sequencing, movement, search, chance, the two PlayerViews) can be
/// exercised end-to-end without a UI, and so a human can eyeball the
/// output for anything obviously wrong.
/// </summary>
module BismarckGame.Console.Program

open System.IO
open BismarckGame.Core.Common
open BismarckGame.Core.SearchBoard
open BismarckGame.Core.GameState
open BismarckGame.Core.Update
open BismarckGame.Core.Scenario
open BismarckGame.Core.Dice
open BismarckGame.Core.PlayerView
open BismarckGame.Core.Simulation
open BismarckGame.Core.Configuration
open BismarckGame.Core.Persistence
open BismarckGame.Core.EventLogger

[<EntryPoint>]
let main _argv =
    printfn "=== Bismarck Basic Game -- console harness ==="
    printfn ""

    printfn "Validating scenario '%s'..." BismarckGame.Core.Scenarios.BismarckBasicGame.scenario.Name
    match BismarckGame.Core.Validation.validate BismarckGame.Core.Scenarios.BismarckBasicGame.scenario with
    | [] -> printfn "  OK -- no issues found."
    | issues ->
        printfn "  %d issue(s) found:" issues.Length
        for i in issues do
            printfn "    - %A" i
    printfn ""

    // Fixed seed so this harness's output is reproducible run to run --
    // useful for diffing behavior after a code change.
    let dice = create (Some 42)
    let roll = asRollFn dice
    let tables = BismarckGame.Core.Tables.RulesTablesImpl.basicGame

    let mutable state = initializeGame BismarckGame.Core.Scenarios.BismarckBasicGame.scenario
    let mutable eventLogEntries : GameEventLogEntryDto list = []

    let printEvent (evt: SimulationEvent) =
        match evt.Succeeded, evt.Message with
        | true, _ -> printfn "  OK   %-34s %A" evt.Label evt.Command
        | false, Some msg -> printfn "  FAIL %-34s %s" evt.Label msg
        | false, None -> printfn "  FAIL %-34s (unknown simulation error)" evt.Label

    let playOneAutomaticTurn () =
        let turnBefore = state.Turn.Number
        let phaseBefore = state.Phase

        printfn "--- Turn %d (night=%b, C-turn=%b, visibility=%A) phase=%A ---"
            state.Turn.Number state.Turn.IsNightTurn state.Turn.IsEmergencyMovementTurn state.Turn.Visibility state.Phase

        match simulateFullTurn tables roll state with
        | Error msg ->
            printfn "  FAIL automatic simulation            %s" msg
        | Ok (nextState, events) ->
            events |> List.iter printEvent
            let startSeq = eventLogEntries.Length + 1
            let newEntries = toLogEntries turnBefore phaseBefore startSeq events
            eventLogEntries <- eventLogEntries @ newEntries
            state <- nextState
        printfn ""

    playOneAutomaticTurn ()
    playOneAutomaticTurn ()

    printfn "=== Final state ==="
    printfn "Turn %d, phase %A, GameEnded=%A" state.Turn.Number state.Phase state.GameEnded
    printfn ""

    for viewer in [ British; German ] do
        let view = BismarckGame.Core.PlayerView.project state viewer
        printfn "--- %A player view ---" viewer
        printfn "  Own ships: %d   Own air units: %d   Score: %d pts" view.OwnShips.Length view.OwnAirUnits.Length view.OwnScore.Points
        printfn "  Revealed enemy contacts: %d" view.RevealedEnemyContacts.Length
        for c in view.RevealedEnemyContacts do
            printfn "    - %A at %O (shadowed=%b)" c.ShipClass c.Zone c.IsShadowed
        printfn ""

    let xmlDir = Path.Combine(defaultStoragePaths.RootPath, defaultStoragePaths.XmlDirectory)
    Directory.CreateDirectory(xmlDir) |> ignore
    let optionsPath = Path.Combine(xmlDir, defaultStoragePaths.OptionsFileName)
    let configPath = Path.Combine(xmlDir, defaultStoragePaths.ConfigurationFileName)
    let statusPath = Path.Combine(xmlDir, defaultStoragePaths.GameStatusFileName)
    let logDir = Path.Combine(defaultStoragePaths.RootPath, defaultStoragePaths.LogsDirectory)
    let eventLogPath = Path.Combine(logDir, defaultStoragePaths.EventLogFileName)

    let options =
        { defaultGameOptions with
            ScenarioId = BismarckGame.Core.Scenarios.BismarckBasicGame.scenario.Id
            EnableEventLogging = true }
    saveGameOptionsToFile defaultXmlPersistenceOptions optionsPath options
    let loadedOptions = loadGameOptionsFromFile optionsPath

    let appConfig =
        { defaultAppConfiguration with
            Paths = { defaultStoragePaths with LogsDirectory = defaultStoragePaths.LogsDirectory; EventLogFileName = defaultStoragePaths.EventLogFileName }
            LastScenarioId = loadedOptions.ScenarioId
            LastStatusFilePath = statusPath }
    saveConfigurationToFile defaultXmlPersistenceOptions configPath appConfig
    let loadedConfig = loadConfigurationFromFile configPath

    saveGameStatusToFile loadedConfig.Xml statusPath state
    let restoredState = loadGameStatusFromFile statusPath (initializeGame BismarckGame.Core.Scenarios.BismarckBasicGame.scenario)

    if loadedOptions.EnableEventLogging then
        Directory.CreateDirectory(logDir) |> ignore
        saveEventLogToFile loadedConfig.Xml eventLogPath loadedOptions.ScenarioId eventLogEntries
        let loadedLog = loadEventLogFromFile eventLogPath
        let movementCount = loadedLog.Entries |> Array.filter (fun e -> e.IsMovement) |> Array.length
        printfn "Event log written: %s (entries=%d, movements=%d)" eventLogPath loadedLog.Entries.Length movementCount
    else
        printfn "Event logging disabled by options."

    printfn "Persistence files written to: %s" xmlDir
    printfn "Restored state turn %d, phase %A" restoredState.Turn.Number restoredState.Phase

    0
