module BismarckGame.Core.EventLogger

open System
open BismarckGame.Core.GameState
open BismarckGame.Core.Simulation
open BismarckGame.Core.Configuration
open BismarckGame.Core.Persistence

[<CLIMutable>]
type GameEventLogEntryDto =
    { Sequence: int
      TurnNumber: int
      Phase: string
      Label: string
      Command: string
      Succeeded: bool
      Message: string
      IsMovement: bool
      LoggedAtUtc: string }

[<CLIMutable>]
type GameEventLogDto =
    { ScenarioId: string
      Entries: GameEventLogEntryDto array }

let private isMovementCommand (command: Command) : bool =
    match command with
    | MoveShip _
    | MoveAirUnit _
    | MoveShipInBattle _ -> true
    | _ -> false

let toLogEntries (turnNumber: int) (phase: Phase) (startSequence: int) (events: SimulationEvent list) : GameEventLogEntryDto list =
    events
    |> List.mapi (fun idx evt ->
        { Sequence = startSequence + idx
          TurnNumber = turnNumber
          Phase = string phase
          Label = evt.Label
          Command = string evt.Command
          Succeeded = evt.Succeeded
          Message = evt.Message |> Option.defaultValue ""
          IsMovement = isMovementCommand evt.Command
          LoggedAtUtc = DateTime.UtcNow.ToString("o") })

let saveEventLogToFile (opts: XmlPersistenceOptions) (filePath: string) (scenarioId: string) (entries: GameEventLogEntryDto list) : unit =
    let dto = { ScenarioId = scenarioId; Entries = entries |> List.toArray }
    writeXmlToFile opts filePath dto

let loadEventLogFromFile (filePath: string) : GameEventLogDto =
    readXmlFromFile<GameEventLogDto> filePath
