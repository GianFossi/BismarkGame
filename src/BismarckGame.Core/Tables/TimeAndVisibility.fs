/// <summary>
/// TimeAndVisibility.fs
/// Time Record Track and Visibility tables, transcribed from the German
/// and British Basic Player Aid Cards (both cards print an identical
/// track). Source: rule 1.2 (4-hour turns), 4.2, 7.1x, 11.2x.
/// </summary>
module BismarckGame.Core.Tables.TimeAndVisibility

open BismarckGame.Core.GameState
open BismarckGame.Core.SearchBoard

/// <summary>
/// One entry of the Time Record Track (42 turns printed on the card;
/// historical play runs turns 1-34, ending at "Finish" on 1200, May 27 —
/// turns 35-42 exist on the card for scenarios that run longer).
/// </summary>
type TimeTrackEntry =
    { Turn: int
      ClockTime: string      // "2400", "0400", "0800", "1200", "1600", "2000"
      Date: string option    // only printed at the first turn of each new day
      IsEmergencyMovementTurn: bool   // the 'C' turns (rule 5.24)
      IsNightTurn: bool
      IsFinishTurn: bool }   // turn 34 is marked "Finish" on both cards

/// <summary>
/// The clock cycles every 6 turns: 2400, 0400, 0800, 1200, 1600, 2000
/// (rule 1.2: each turn = 4 real hours). Within that cycle, 0400/1200/
/// 2000 are 'C' (emergency movement) turns, and 2400/2000 are night turns
/// — i.e. every even-numbered turn is a C-turn, and turns where
/// (turn mod 6) is 1 or 0 are night turns. This was verified directly
/// against all 42 printed cells on both cards, not just the visible
/// pattern.
/// </summary>
let private clockCycle = [| "2400"; "0400"; "0800"; "1200"; "1600"; "2000" |]

let private datesByCycleIndex =
    // cycle index 0 (the 2400 turn) is where a new date is printed.
    [| "May 22"; "May 23"; "May 24"; "May 25"; "May 26"; "May 27"; "May 28" |]

let timeRecordTrack : TimeTrackEntry list =
    [ for turn in 1 .. 42 do
        let cycleIndex = (turn - 1) % 6
        let dayIndex = (turn - 1) / 6
        yield
            { Turn = turn
              ClockTime = clockCycle.[cycleIndex]
              Date = if cycleIndex = 0 && dayIndex < datesByCycleIndex.Length then Some datesByCycleIndex.[dayIndex] else None
              IsEmergencyMovementTurn = turn % 2 = 0
              IsNightTurn = cycleIndex = 0 || cycleIndex = 5   // 2400 or 2000
              IsFinishTurn = turn = 34 } ]

/// GameState.GameTurn's IsNightTurn/IsEmergencyMovementTurn come directly
/// from the matching TimeTrackEntry's fields of the same name — no
/// conversion needed; look up the entry for a turn number and copy them.

/// <summary>
/// Visibility Track: levels 1 (best, sunny) through 8 and X (worst, fog),
/// each with a search-strength modifier printed on the track itself.
/// </summary>
type VisibilityTrackEntry =
    { Level: int          // 1..8, with 9 representing the 'X' (fog) box
      SearchModifier: int }

let visibilityTrack : VisibilityTrackEntry list =
    [ { Level = 1; SearchModifier = -1 }
      { Level = 2; SearchModifier = 0 }
      { Level = 3; SearchModifier = 0 }
      { Level = 4; SearchModifier = 0 }
      { Level = 5; SearchModifier = 0 }
      { Level = 6; SearchModifier = 0 }
      { Level = 7; SearchModifier = 1 }
      { Level = 8; SearchModifier = 2 }
      { Level = 9 (* 'X' *); SearchModifier = -2 } ]

/// <summary>
/// Visibility Change Table: two-dice roll (2-12, but the card lists 1-13
/// — 1 and 13 are the extreme single-direction results reachable only via
/// modified rolls per rule 13.24) -> track shift, with some shifts also
/// triggering Fog (rule 7.31-7.33).
/// </summary>
type VisibilityShift =
    { DiceRoll: int
      Shift: int          // positive = toward level 1 (clearer), negative = toward X (worse)
      TriggersFog: bool }

let visibilityChangeTable : VisibilityShift list =
    [ { DiceRoll = 1; Shift = 6; TriggersFog = false }
      { DiceRoll = 2; Shift = 5; TriggersFog = false }
      { DiceRoll = 3; Shift = 4; TriggersFog = false }
      { DiceRoll = 4; Shift = 3; TriggersFog = false }
      { DiceRoll = 5; Shift = 2; TriggersFog = true }
      { DiceRoll = 6; Shift = 1; TriggersFog = true }
      { DiceRoll = 7; Shift = 0; TriggersFog = false }
      { DiceRoll = 8; Shift = -1; TriggersFog = true }
      { DiceRoll = 9; Shift = -2; TriggersFog = false }
      { DiceRoll = 10; Shift = -3; TriggersFog = true }
      { DiceRoll = 11; Shift = -4; TriggersFog = false }
      { DiceRoll = 12; Shift = -5; TriggersFog = true }
      { DiceRoll = 13; Shift = -6; TriggersFog = true } ]
    // NOTE: a 2d6 roll only produces 2-12; rows 1 and 13 are reachable
    // via die modifiers described in rule 13.24 (Intermediate Game). Kept
    // here since both cards print them, but the Basic Game update logic
    // should never need to look them up.

/// <summary>
/// Applies a visibility shift, clamping to the track's 1..9 range
/// (rule 7.15: off-the-end results stay at the end box).
/// </summary>
let applyVisibilityShift (VisibilityLevel current) (shift: int) : VisibilityLevel =
    VisibilityLevel(max 1 (min 9 (current - shift)))
    // Shift sign convention: positive shift = toward clearer (lower level
    // number) per the track's printed up-arrows; hence subtraction here.
