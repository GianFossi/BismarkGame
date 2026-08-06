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
open BismarckGame.Core.BattleBoard
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

    // Rule 4.8 demonstration: Prince of Wales finds Bismarck in the same
    // Search Board zone and opens naval combat.  Keep this independent of
    // the automatic run so its output remains deterministic.
    let demonstrateBritishBattleshipEncounter () =
        let encounterState = initializeGame BismarckGame.Core.Scenarios.BismarckBasicGame.scenario
        let bismarckId = ShipId "GER-BB-Bismarck"
        let prinzEugenId = ShipId "GER-CA-PrinzEugen"
        let princeOfWalesId = ShipId "GBR-BB-PrinceOfWales"
        let scapaFlowBomberId = AirUnitId "GBR-AIR-Bomber-ScapaFlow"
        let bismarck = encounterState.Players.[German].Ships.[bismarckId]
        let princeOfWales = encounterState.Players.[British].Ships.[princeOfWalesId]
        let encounterZone = bismarck.CurrentZone |> Option.defaultWith (fun () -> failwith "Bismarck must start on the Search Board")
        let german =
            { encounterState.Players.[German] with
                Ships =
                    encounterState.Players.[German].Ships
                    |> Map.change prinzEugenId (Option.map (fun ship -> { ship with CurrentZone = None })) }
        let british =
            { encounterState.Players.[British] with
                Ships = encounterState.Players.[British].Ships.Add(princeOfWalesId, { princeOfWales with CurrentZone = Some encounterZone }) }
        let stateWithContact =
            { encounterState with
                Phase = NavalCombat
                Players = encounterState.Players |> Map.add German german |> Map.add British british }

        let fireAtTarget firer target (currentState: GameState) =
            let targetBefore = (currentState.ActiveBattles |> List.head).Ships.[target]
            let order =
                { Firer = firer
                  Target = target
                  Section = BowGuns
                  SalvoesFired = 1
                  Range = RangeA
                  Aspect = Broadside }
            match update tables roll (FireInBattle order) currentState with
            | Error msg ->
                printfn "  FAIL %s firing at %s: %s" (string firer) (string target) msg
                currentState
            | Ok after ->
                let targetAfter = (after.ActiveBattles |> List.head).Ships.[target]
                printfn "  %s fires at %s: midships %d -> %d, secondary %d -> %d, sunk=%b"
                    (string firer) (string target)
                    targetBefore.MidshipsHits targetAfter.MidshipsHits
                    targetBefore.SecondaryHits targetAfter.SecondaryHits
                    targetAfter.IsSunk
                after

        let rec simulateBattleRounds round (currentState: GameState) =
            let battle = currentState.ActiveBattles |> List.head
            let princeOfWalesInBattle = battle.Ships.[princeOfWalesId]
            let bismarckInBattle = battle.Ships.[bismarckId]
            if round > 5 || princeOfWalesInBattle.IsSunk || bismarckInBattle.IsSunk then
                currentState
            else
                printfn "  Battle round %d" round
                let afterBritishFire = fireAtTarget princeOfWalesId bismarckId currentState
                let bismarckAfterFire = (afterBritishFire.ActiveBattles |> List.head).Ships.[bismarckId]
                let afterGermanFire =
                    if bismarckAfterFire.IsSunk then
                        afterBritishFire
                    else
                        fireAtTarget bismarckId princeOfWalesId afterBritishFire
                simulateBattleRounds (round + 1) afterGermanFire

        printfn "=== British battleship contact ==="
        printfn "Prince of Wales finds Bismarck at %O and initiates naval combat." encounterZone
        match update tables roll (InitiateNavalCombat(encounterZone, British)) stateWithContact with
        | Ok after ->
            printfn "  OK   Naval battle %d started with %d ships." after.ActiveBattles.Head.Id after.ActiveBattles.Head.Ships.Count
            let afterFiveRounds = simulateBattleRounds 1 after
            let battle = afterFiveRounds.ActiveBattles |> List.head
            let princeOfWalesAfter = battle.Ships.[princeOfWalesId]
            let bismarckAfter = battle.Ships.[bismarckId]
            printfn "  Battle result: Prince of Wales sunk=%b; Bismarck sunk=%b" princeOfWalesAfter.IsSunk bismarckAfter.IsSunk
            let stateAfterBattle =
                match update tables roll (EndNavalCombat battle.Id) afterFiveRounds with
                | Ok resolved -> resolved
                | Error msg ->
                    printfn "  FAIL Ending naval combat: %s" msg
                    afterFiveRounds
            let bismarckAfterBattle = stateAfterBattle.Players.[German].Ships.[bismarckId]
            if bismarckAfterBattle.IsSunk then
                printfn "  Air attack skipped because Bismarck has sunk."
            else
                let bomber = stateAfterBattle.Players.[British].AirUnits.[scapaFlowBomberId]
                let britishWithBomber =
                    { stateAfterBattle.Players.[British] with
                        AirUnits =
                            stateAfterBattle.Players.[British].AirUnits
                            |> Map.add scapaFlowBomberId
                                { bomber with
                                    Mode = BomberAttack
                                    CurrentZone = Some encounterZone
                                    IsAtBase = false
                                    AirAttacksLaunchedThisTurn = 0 } }
                let stateForAirAttack =
                    { stateAfterBattle with
                        Phase = AirAttack
                        Players = stateAfterBattle.Players |> Map.add British britishWithBomber }
                printfn "  British Level Bomber launches an air attack against Bismarck."
                match update tables roll (LaunchAirAttack(scapaFlowBomberId, bismarckId)) stateForAirAttack with
                | Ok afterAirAttack ->
                    let bismarckAfterAirAttack = afterAirAttack.Players.[German].Ships.[bismarckId]
                    printfn "  Air attack result: midships %d -> %d, evasion %d -> %d, sunk=%b"
                        bismarckAfterBattle.MidshipsHits bismarckAfterAirAttack.MidshipsHits
                        bismarckAfterBattle.EvasionRating bismarckAfterAirAttack.EvasionRating
                        bismarckAfterAirAttack.IsSunk
                | Error msg -> printfn "  FAIL Air attack: %s" msg
        | Error msg -> printfn "  FAIL Naval combat initiation              %s" msg
        printfn ""

    demonstrateBritishBattleshipEncounter ()

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
