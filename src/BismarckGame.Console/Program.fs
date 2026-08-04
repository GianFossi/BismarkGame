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

open BismarckGame.Core.Common
open BismarckGame.Core.SearchBoard
open BismarckGame.Core.GameState
open BismarckGame.Core.Update
open BismarckGame.Core.Scenario
open BismarckGame.Core.Dice
open BismarckGame.Core.PlayerView

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

    let apply (label: string) (cmd: Command) =
        match update tables roll cmd state with
        | Ok s' ->
            state <- s'
            printfn "  OK   %-30s %A" label cmd
        | Error msg ->
            printfn "  FAIL %-30s %s" label msg

    let advance (label: string) = apply label AdvancePhase

    let playOneTurn () =
        printfn "--- Turn %d (night=%b, C-turn=%b, visibility=%A) phase=%A ---"
            state.Turn.Number state.Turn.IsNightTurn state.Turn.IsEmergencyMovementTurn state.Turn.Visibility state.Phase

        advance "-> Visibility"
        apply "roll visibility change" RollVisibilityChange

        advance "-> ShadowDetermination"
        // British: attempt to shadow a German contact if one is already
        // known from a prior turn (none on turn 1 -- this mostly proves
        // the phase transition and the "nothing to do" path).

        advance "-> AirMovement"

        advance "-> ShipMovement"
        // German: move Bismarck and Prinz Eugen out of Bergen (F20).
        // F19 and G20 are Bergen's only two on-board neighbors per the
        // transcribed board data.
        apply "Bismarck: F20 -> F19" (MoveShip(ShipId "GER-BB-Bismarck", { Letter = 'F'; Number = 19 }))
        apply "Prinz Eugen: F20 -> F19" (MoveShip(ShipId "GER-CA-PrinzEugen", { Letter = 'F'; Number = 19 }))
        // British: move Suffolk one zone from Hvalfiord (D9) toward the
        // Denmark Strait, exercising a normal (non-breakout) move.
        apply "Suffolk: D9 -> D8" (MoveShip(ShipId "GBR-CA-Suffolk", { Letter = 'D'; Number = 8 }))

        advance "-> Search"
        apply "British search F19" (SearchZone(British, { Letter = 'F'; Number = 19 }))
        apply "German search D8" (SearchZone(German, { Letter = 'D'; Number = 8 }))

        advance "-> AirAttack"

        advance "-> NavalCombat"

        advance "-> Chance"
        apply "chance roll: Bismarck" (RollChanceForShip(ShipId "GER-BB-Bismarck"))
        apply "chance roll: Prinz Eugen" (RollChanceForShip(ShipId "GER-CA-PrinzEugen"))

        advance "-> next turn's Unit Availability"
        printfn ""

    playOneTurn ()
    playOneTurn ()

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

    0
