/// <summary>
/// Simulation.fs
/// Automatic driver for the existing command-based engine. This module
/// sequences phases and issues legal commands based on current state so a
/// full turn can be simulated without a UI.
/// </summary>
module BismarckGame.Core.Simulation

open BismarckGame.Core.Common
open BismarckGame.Core.SearchBoard
open BismarckGame.Core.Units
open BismarckGame.Core.GameState
open BismarckGame.Core.Update

/// <summary>
/// One command emitted by the automatic simulator and its outcome.
/// </summary>
type SimulationEvent =
    { Phase: Phase
      Label: string
      Command: Command
      Succeeded: bool
      Message: string option }

let private okEvent phase label cmd =
    { Phase = phase
      Label = label
      Command = cmd
      Succeeded = true
      Message = None }

let private errEvent phase label cmd msg =
    { Phase = phase
      Label = label
      Command = cmd
      Succeeded = false
      Message = Some msg }

let private tryFindShip (state: GameState) (shipId: ShipId) : (Nationality * ShipCounter) option =
    state.Players
    |> Map.toSeq
    |> Seq.tryPick (fun (_, player) -> player.Ships |> Map.tryFind shipId |> Option.map (fun ship -> player.Nationality, ship))

let private allShipsOrdered (state: GameState) : (Nationality * ShipCounter) list =
    state.Players
    |> Map.toSeq
    |> Seq.collect (fun (_, p) -> p.Ships |> Map.toSeq |> Seq.map (fun (_, s) -> p.Nationality, s))
    |> Seq.sortBy (fun (_, s) -> string s.Id)
    |> Seq.toList

let private applyCommand (tables: IRulesTables) (roll: unit -> int) (label: string) (cmd: Command) (state: GameState) : GameState * SimulationEvent =
    match update tables roll cmd state with
    | Ok nextState -> nextState, okEvent state.Phase label cmd
    | Error msg -> state, errEvent state.Phase label cmd msg

let private moveShipAsFarAsPossible (tables: IRulesTables) (roll: unit -> int) (nat: Nationality) (shipId: ShipId) (state: GameState) : GameState * SimulationEvent list =
    let rec loop (st: GameState) (acc: SimulationEvent list) =
        match tryFindShip st shipId with
        | None -> st, List.rev acc
        | Some (shipNat, ship) when shipNat <> nat -> st, List.rev acc
        | Some (_, ship) ->
            match ship.CurrentZone with
            | None -> st, List.rev acc
            | Some origin ->
                let legalMove =
                    neighbors st.SearchBoard origin
                    |> List.sortBy (fun z -> z.Letter, z.Number)
                    |> List.tryPick (fun destination ->
                        let cmd = MoveShip(shipId, destination)
                        match update tables roll cmd st with
                        | Ok nextState -> Some (nextState, okEvent st.Phase (sprintf "%s: %O -> %O" ship.Name origin destination) cmd)
                        | Error _ -> None)
                match legalMove with
                | Some (nextState, evt) -> loop nextState (evt :: acc)
                | None -> st, List.rev acc
    loop state []

let private runShipMovement (tables: IRulesTables) (roll: unit -> int) (state: GameState) : GameState * SimulationEvent list =
    allShipsOrdered state
    |> List.fold
        (fun (st, events) (nat, ship) ->
            let st', evts = moveShipAsFarAsPossible tables roll nat ship.Id st
            st', events @ evts)
        (state, [])

let private runSearchPhase (tables: IRulesTables) (roll: unit -> int) (state: GameState) : GameState * SimulationEvent list =
    let collectZones (nat: Nationality) =
        match state.Players.TryFind nat with
        | None -> []
        | Some p ->
            let shipZones = p.Ships |> Map.toSeq |> Seq.choose (fun (_, s) -> s.CurrentZone)
            let airZones = p.AirUnits |> Map.toSeq |> Seq.choose (fun (_, a) -> a.CurrentZone)
            Seq.append shipZones airZones |> Seq.distinct |> Seq.sortBy (fun z -> z.Letter, z.Number) |> Seq.toList

    [ British; German ]
    |> List.collect (fun nat -> collectZones nat |> List.map (fun zone -> nat, zone))
    |> List.fold
        (fun (st, events) (nat, zone) ->
            let label = sprintf "%A search %O" nat zone
            let st', evt = applyCommand tables roll label (SearchZone(nat, zone)) st
            st', events @ [ evt ])
        (state, [])

let private runChancePhase (tables: IRulesTables) (roll: unit -> int) (state: GameState) : GameState * SimulationEvent list =
    allShipsOrdered state
    |> List.choose (fun (nat, ship) -> if nat = German && ship.CurrentZone.IsSome then Some ship.Id else None)
    |> List.fold
        (fun (st, events) shipId ->
            let label = sprintf "Chance roll: %A" shipId
            let st', evt = applyCommand tables roll label (RollChanceForShip shipId) st
            st', events @ [ evt ])
        (state, [])

let private runNavalCombatPhase (tables: IRulesTables) (roll: unit -> int) (state: GameState) : GameState * SimulationEvent list =
    let germanShipsOnContacts =
        allShipsOrdered state
        |> List.choose (fun (nat, ship) ->
            if nat <> German then
                None
            else
                match ship.CurrentZone with
                | Some z when state.ConvoyContacts |> List.exists (fun c -> c.Discoverer = German && c.Zone = z) -> Some(ship.Id, z)
                | _ -> None)

    germanShipsOnContacts
    |> List.fold
        (fun (st, events) (shipId, zone) ->
            let label = sprintf "Convoy attack: %A at %O" shipId zone
            let st', evt = applyCommand tables roll label (AttackConvoy(shipId, zone)) st
            st', events @ [ evt ])
        (state, [])

/// <summary>
/// Simulates the work for the current phase, then advances to the next
/// phase. The returned events capture every emitted command.
/// </summary>
let simulateCurrentPhase (tables: IRulesTables) (roll: unit -> int) (state: GameState) : Result<GameState * SimulationEvent list, string> =
    let runAdvance (st: GameState) (events: SimulationEvent list) : Result<GameState * SimulationEvent list, string> =
        let st', evt = applyCommand tables roll (sprintf "Advance from %A" st.Phase) AdvancePhase st
        if evt.Succeeded then
            Ok(st', events @ [ evt ])
        else
            Error(evt.Message |> Option.defaultValue "AdvancePhase failed")

    match state.Phase with
    | UnitAvailability -> runAdvance state []
    | Visibility ->
        let st1, evt1 = applyCommand tables roll "Roll visibility change" RollVisibilityChange state
        let preEvents = [ evt1 ]
        if evt1.Succeeded then runAdvance st1 preEvents
        else Error(evt1.Message |> Option.defaultValue "RollVisibilityChange failed")
    | ShadowDetermination -> runAdvance state []
    | AirMovement -> runAdvance state []
    | ShipMovement ->
        let st1, moveEvents = runShipMovement tables roll state
        runAdvance st1 moveEvents
    | Search ->
        let st1, searchEvents = runSearchPhase tables roll state
        runAdvance st1 searchEvents
    | AirAttack -> runAdvance state []
    | TorpedoAttack -> runAdvance state []
    | NavalCombat ->
        let st1, convoyEvents = runNavalCombatPhase tables roll state
        runAdvance st1 convoyEvents
    | Chance ->
        let st1, chanceEvents = runChancePhase tables roll state
        runAdvance st1 chanceEvents

/// <summary>
/// Simulates one full turn, starting from whatever phase the state is in,
/// until the turn number increments or the game ends.
/// </summary>
let simulateFullTurn (tables: IRulesTables) (roll: unit -> int) (state: GameState) : Result<GameState * SimulationEvent list, string> =
    let startTurn = state.Turn.Number

    let rec loop (st: GameState) (events: SimulationEvent list) (guard: int) =
        if st.GameEnded.IsSome || st.Turn.Number > startTurn then
            Ok(st, events)
        elif guard > 200 then
            Error "Simulation guard tripped while simulating one turn"
        else
            match simulateCurrentPhase tables roll st with
            | Error msg -> Error msg
            | Ok (st', phaseEvents) -> loop st' (events @ phaseEvents) (guard + 1)

    loop state [] 0

/// <summary>
/// Simulates a fixed number of full turns, or until the game ends.
/// </summary>
let simulateTurns (tables: IRulesTables) (roll: unit -> int) (turnCount: int) (state: GameState) : Result<GameState * SimulationEvent list, string> =
    let rec loop (remaining: int) (st: GameState) (events: SimulationEvent list) =
        if remaining <= 0 || st.GameEnded.IsSome then
            Ok(st, events)
        else
            match simulateFullTurn tables roll st with
            | Error msg -> Error msg
            | Ok (st', turnEvents) -> loop (remaining - 1) st' (events @ turnEvents)

    loop turnCount state []
