/// <summary>
/// Players.fs
/// Multiplayer-facing helpers for the two Basic Game sides. GameState
/// remains the authoritative full-state referee; this module gives a UI
/// or server a small API that binds a human player to one side, returns
/// only that player's redacted PlayerView, and rejects commands aimed at
/// the opponent's units.
/// </summary>
module BismarckGame.Core.Players

open ROP
open BismarckGame.Core.Common
open BismarckGame.Core.GameState
open BismarckGame.Core.PlayerView
open BismarckGame.Core.Update

/// <summary>
/// Player-facing operation result: a Railway-Oriented result that carries
/// warnings alongside successful values and errors on failed branches.
/// </summary>
type PlayerReturns<'T> = Returns<'T, string>

/// <summary>
/// Human-facing player role in the Basic Game.
/// </summary>
type PlayerRole =
    | BritishPlayer
    | GermanPlayer

/// <summary>
/// One player's stable game seat. Allies are controlled by the owning
/// Basic Game side because the Basic Game model only distinguishes
/// British and German nationalities (rule 2.421).
/// </summary>
type PlayerSeat =
    { Role: PlayerRole
      Side: Nationality
      DisplayName: string }

/// <summary>
/// The two seats present in every Basic Game session.
/// </summary>
type PlayerRoster =
    { British: PlayerSeat
      German: PlayerSeat }

/// <summary>
/// Per-player information suitable for rendering a private screen:
/// redacted map/unit state plus the player's own report and score.
/// </summary>
type PlayerDashboard =
    { Seat: PlayerSeat
      View: PlayerView
      Reports: string list }

/// <summary>
/// Creates the standard two-player roster: British/Commonwealth side and
/// German/Axis side.
/// </summary>
let createRoster () : PlayerRoster =
    { British = { Role = BritishPlayer; Side = British; DisplayName = "British player" }
      German = { Role = GermanPlayer; Side = German; DisplayName = "German player" } }

/// <summary>
/// Gets the seat for a nationality.
/// </summary>
let seatFor (roster: PlayerRoster) (side: Nationality) : PlayerSeat =
    match side with
    | British -> roster.British
    | German -> roster.German

let private tryFindShipOwner (state: GameState) shipId =
    state.Players
    |> Seq.tryPick (fun kvp ->
        if kvp.Value.Ships.ContainsKey shipId then Some kvp.Key else None)

let private tryFindAirUnitOwner (state: GameState) airUnitId =
    state.Players
    |> Seq.tryPick (fun kvp ->
        if kvp.Value.AirUnits.ContainsKey airUnitId then Some kvp.Key else None)

let private tryFindTaskForceOwner (state: GameState) taskForceId =
    state.Players
    |> Seq.tryPick (fun kvp ->
        if kvp.Value.TaskForces.ContainsKey taskForceId then Some kvp.Key else None)

let private tryFindUnitOwner (state: GameState) (UnitId id) =
    match tryFindShipOwner state (ShipId id) with
    | Some owner -> Some owner
    | None -> tryFindAirUnitOwner state (AirUnitId id)

let private ok value : PlayerReturns<'T> =
    Success(value, [])

let private fail error : PlayerReturns<'T> =
    Failure [ error ]

let private ofEngineResult (result: Result<'T, string>) : PlayerReturns<'T> =
    match result with
    | Ok value -> ok value
    | Error error -> fail error

let private resultOfOption error value =
    match value with
    | Some x -> ok x
    | None -> fail error

let private commandController (state: GameState) (command: Command) : PlayerReturns<Nationality option> =
    match command with
    | MoveShip(shipId, _)
    | SetShipMode(shipId, _)
    | RollChanceForShip shipId
    | AttackConvoy(shipId, _)
    | WithdrawFromBattle shipId
    | Mobilize shipId ->
        tryFindShipOwner state shipId
        |> Option.map Some
        |> resultOfOption $"No ship {shipId} exists in this game"

    | MoveAirUnit(airUnitId, _)
    | SetAirUnitMode(airUnitId, _)
    | LaunchAirAttack(airUnitId, _) ->
        tryFindAirUnitOwner state airUnitId
        |> Option.map Some
        |> resultOfOption $"No air unit {airUnitId} exists in this game"

    | FormTaskForce(side, _)
    | SearchZone(side, _)
    | InitiateNavalCombat(_, side) ->
        ok(Some side)

    | BreakTaskForce(taskForceId, _) ->
        tryFindTaskForceOwner state taskForceId
        |> Option.map Some
        |> resultOfOption $"No task force {taskForceId} exists in this game"

    | DeclareShadow(shadower, _) ->
        tryFindUnitOwner state shadower
        |> Option.map Some
        |> resultOfOption $"No shadowing unit {shadower} exists in this game"

    | FireInBattle order ->
        tryFindShipOwner state order.Firer
        |> Option.map Some
        |> resultOfOption $"No firing ship {order.Firer} exists in this game"

    | EndNavalCombat battleId ->
        match state.ActiveBattles |> List.tryFind (fun b -> b.Id = battleId) with
        | None -> fail $"No active battle {battleId}"
        | Some battle ->
            let sides =
                battle.Ships
                |> Map.toSeq
                |> Seq.choose (fun (shipId, _) -> tryFindShipOwner state shipId)
                |> Set.ofSeq

            if sides.IsEmpty then fail $"Battle {battleId} has no ships controlled by either player"
            elif sides.Count = 1 then ok(Some(Set.minElement sides))
            else ok None

    | MoveShipInBattle(shipId, _, _, _, _) ->
        tryFindShipOwner state shipId
        |> Option.map Some
        |> resultOfOption $"No ship {shipId} exists in this game"

    | RollVisibilityChange
    | AdvancePhase ->
        ok None

/// <summary>
/// Returns true when the player is allowed to submit the command. Commands
/// that belong to a specific side must match the seat's side; shared
/// housekeeping commands such as AdvancePhase are accepted for either
/// player.
/// </summary>
let canSubmit (state: GameState) (seat: PlayerSeat) (command: Command) : PlayerReturns<unit> =
    match commandController state command with
    | Failure errors -> Failure errors
    | Success(None, warnings) -> Success((), warnings)
    | Success(Some owner, warnings) when owner = seat.Side -> Success((), warnings)
    | Success(Some owner, warnings) ->
        Failure(warnings @ [ $"{seat.DisplayName} controls {seat.Side} units and cannot issue a {owner} command" ])

/// <summary>
/// Applies a command on behalf of one player after ownership validation.
/// </summary>
let submitCommand tables roll (seat: PlayerSeat) (command: Command) (state: GameState) : PlayerReturns<GameState> =
    match canSubmit state seat command with
    | Failure errors -> Failure errors
    | Success((), warnings) ->
        match ofEngineResult (update tables roll command state) with
        | Success(updated, updateWarnings) -> Success(updated, warnings @ updateWarnings)
        | Failure errors -> Failure(warnings @ errors)

/// <summary>
/// Builds the private dashboard for one player: their redacted PlayerView
/// plus their own score-sheet events as report lines.
/// </summary>
let dashboard (state: GameState) (seat: PlayerSeat) : PlayerDashboard =
    let view = project state seat.Side
    let reports =
        view.OwnScore.Events
        |> List.rev
        |> List.map (fun (text, points) ->
            let signedPoints = if points > 0 then $"+{points}" else string points
            $"{text} ({signedPoints} VP)")

    { Seat = seat; View = view; Reports = reports }
