/// <summary>
/// VictoryConditions.fs
/// Basic Game end conditions and victory point schedule (rules 12.1-12.7).
/// Point VALUES below are transcribed from the manual where legible;
/// a few (marked TODO) were garbled in OCR/text extraction and must be
/// verified against the printed Victory Point Schedule before use.
/// </summary>
module BismarckGame.Core.VictoryConditions

open BismarckGame.Core.Common

/// <summary>
/// Rule 12.1: the game ends when one of these occurs.
/// </summary>
type GameEndCondition =
    | BismarckSunk
    | BismarckReturnsToPort
    | TimeRunsOut

/// <summary>
/// Discrete scoring events, each worth a fixed number of victory points.
/// Kept as data (event -> points) rather than hard-coded match arms so the
/// schedule can be loaded/edited without recompiling.
/// </summary>
type BritishVictoryEvent =
    | SankBismarck
    | SankPrinzEugen
    | MidshipsHitOnBismarckOrPrinzEugen   // "at least one midships hit" — 2 pts (rule 12.31 area)

type GermanVictoryEvent =
    | SankVictorious                       // 24 points (rule 12.41)
    | SankArkRoyal                         // 20 points (rule 12.41)
    | SankRenownOrRepulse                  // 10 points each (rule 12.41)
    | SankRevengeOrRamillies               // 8 points each (rule 12.41)
    | SankHeavyCruiser                     // 6 points (rule 12.41)
    | SankLightCruiser                     // 4 points (rule 12.41)
    | DestroyedConvoyNumber of int          // 1st..5th convoy, escalating 6/6/8/10/12 (rule 12.44)
    | BismarckReachedPortOnTurn of mayDate: int  // sliding scale 12/10/8/6/4 by date (rule 12.3x)
    | BismarckAtSeaAtEndLowEvasion          // evasion 21 or less at game end — 6 pts (rule 12.34)
    | PrinzEugenAtSeaAtEndLowEvasion        // 2 pts (rule 12.34)
    | BritishAirUnitLost                    // -2 pts penalty to British, credited to German score bookkeeping
    | GermanAirUnitLost                     // -2 pts (rule 12.35)
    | NoConvoySunk                          // 4 pts consolation if German fails entirely (rule 12.36)

/// <summary>
/// Points awarded for damage inflicted on ships still afloat at game end
/// (rule 12.5) — separate from sinking bonuses above.
/// </summary>
type DamagePoints =
    { PerMidshipsHitOnBattleshipOrCarrier: int  // 2 points (rule 12.5)
      PerMidshipsHitOnCruiser: int }             // 1 point (rule 12.5)

let basicGameDamagePoints =
    { PerMidshipsHitOnBattleshipOrCarrier = 2
      PerMidshipsHitOnCruiser = 1 }

/// <summary>
/// Running score for one side.
/// </summary>
type VictoryScore =
    { Nationality: Nationality
      Points: int
      Events: (string * int) list }   // audit trail: event label * points awarded

/// <summary>
/// Minimal per-ship facts needed to score rule 12.5 damage points and
/// detect sinkings — kept as a plain record instead of taking a
/// GameState/ShipCounter directly so this module doesn't have to depend
/// on Units.fs/GameState.fs (avoiding a cycle, since GameState.fs already
/// depends on this module for GameEndCondition/VictoryScore).
/// </summary>
type ShipOutcome =
    { Name: string
      Nationality: Nationality
      Class: ShipClass
      IsSunk: bool
      MidshipsHits: int }

/// <summary>
/// Rule 12.5: 2 points per midships hit on a battleship/battlecruiser/
/// carrier still afloat, 1 point per midships hit on a cruiser. Sunk
/// ships are scored via the sinking-bonus lists (SankBismarck etc.), not
/// this — a sunk ship's residual MidshipsHits isn't double-counted here.
/// </summary>
let private damagePointsFor (points: DamagePoints) (ship: ShipOutcome) : int =
    if ship.IsSunk then
        0
    else
        match ship.Class with
        | Battleship | Battlecruiser | AircraftCarrier -> points.PerMidshipsHitOnBattleshipOrCarrier * ship.MidshipsHits
        | HeavyCruiser | LightCruiser | PocketBattleship -> points.PerMidshipsHitOnCruiser * ship.MidshipsHits

/// <summary>
/// Evaluates final/running scores for both sides from a snapshot of ship
/// outcomes. This only implements rule 12.5 (damage points on ships still
/// afloat) plus sinking Bismarck/Prinz Eugen (rule 12.31-ish) — the full
/// German sinking-bonus schedule (SankVictorious=24, etc.), the sliding
/// Bismarck-reached-port scale, and the convoy/air-unit-loss adjustments
/// are typed above (BritishVictoryEvent/GermanVictoryEvent) but NOT yet
/// wired into this function: several of those point values are marked
/// TODO in this file's header because the source photo was garbled there,
/// and scoring them with unverified numbers would be worse than not
/// scoring them at all. Extending this function is a data-completion
/// task, not a design change, once those numbers are confirmed.
/// </summary>
let evaluate (damagePoints: DamagePoints) (shipOutcomes: ShipOutcome list) : VictoryScore list =
    let scoreFor nat =
        let events =
            [ for ship in shipOutcomes do
                if ship.Nationality <> nat then   // score against the OTHER side's ships
                    if ship.IsSunk && ship.Name = "Bismarck" then
                        yield "Sank Bismarck", 0   // TODO: exact point value not confirmed (rule 12.31 area)
                    elif ship.IsSunk && ship.Name = "Prinz Eugen" then
                        yield "Sank Prinz Eugen", 0   // TODO: exact point value not confirmed
                    else
                        let pts = damagePointsFor damagePoints ship
                        if pts > 0 then yield $"Damage on {ship.Name}", pts ]
        { Nationality = nat
          Points = events |> List.sumBy snd
          Events = events }
    [ scoreFor British; scoreFor German ]

/// <summary>
/// Rule 12.1 end-condition detection from the same ship-outcome snapshot
/// plus the current turn number. `finishTurn` is the scenario's "Finish"
/// turn (34 for the historical Basic Game — see
/// Tables/TimeAndVisibility.fs's TimeTrackEntry.IsFinishTurn) rather than
/// a hardcoded constant, so a longer/shorter variant scenario still works.
/// </summary>
let checkGameEnd (finishTurn: int) (currentTurn: int) (bismarckInPort: bool) (shipOutcomes: ShipOutcome list) : GameEndCondition option =
    let bismarckSunk = shipOutcomes |> List.exists (fun s -> s.Name = "Bismarck" && s.IsSunk)
    if bismarckSunk then Some BismarckSunk
    elif bismarckInPort then Some BismarckReturnsToPort
    elif currentTurn >= finishTurn then Some TimeRunsOut
    else None
