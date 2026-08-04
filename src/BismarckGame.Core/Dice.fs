/// <summary>
/// Dice.fs
/// Dice-rolling utilities. Every resolution table in this project (Naval
/// Fire, Shadow, Chance) takes its randomness through a plain `unit -> int`
/// function rather than calling System.Random directly — this module is
/// where that function actually comes from in real play. Kept separate
/// from System.Random so a deterministic roller (fixed sequence) can
/// stand in for tests without touching Update.fs or the Tables modules.
/// </summary>
module BismarckGame.Core.Dice

open System

/// <summary>
/// A source of six-sided die rolls (1-6). An interface rather than a bare
/// function so a single instance can be threaded through a whole game
/// (one shared System.Random, not a fresh one per call — reusing System.Random
/// per call on a fast loop biases results, since it defaults to a
/// time-based seed and rapid re-construction can repeat seeds).
/// </summary>
type IDiceRoller =
    /// <summary>
    /// Rolls one six-sided die. Returns 1-6.
    /// </summary>
    abstract Roll: unit -> int

/// <summary>
/// Wraps a System.Random instance as an IDiceRoller.
/// </summary>
let ofRandom (rng: Random) : IDiceRoller =
    { new IDiceRoller with
        member _.Roll() = rng.Next(1, 7) }   // Random.Next upper bound is exclusive

/// <summary>
/// A real random roller. Pass a seed for reproducible play-throughs
/// (e.g. replaying a turn while debugging); omit it for actual play.
/// </summary>
let create (seed: int option) : IDiceRoller =
    match seed with
    | Some s -> ofRandom (Random(s))
    | None -> ofRandom (Random())

/// <summary>
/// A deterministic roller that returns a fixed sequence of values, then
/// raises once exhausted — for tests that need to force a specific
/// outcome (e.g. "prove a 12 on the Special Damage A-range table sinks a
/// non-capital ship"). Raising on exhaustion is deliberate: a test that
/// runs past its scripted rolls has a bug worth surfacing, not a reason
/// to silently wrap around.
/// </summary>
let ofSequence (values: int list) : IDiceRoller =
    let queue = System.Collections.Generic.Queue<int>(values)
    { new IDiceRoller with
        member _.Roll() =
            if queue.Count = 0 then
                failwith "Dice.ofSequence: sequence exhausted — the test rolled more times than it scripted for"
            queue.Dequeue() }

/// <summary>
/// Adapts an IDiceRoller to the plain `unit -> int` shape that
/// Update.update and IRulesTables expect.
/// </summary>
let asRollFn (dice: IDiceRoller) : unit -> int = dice.Roll

/// <summary>
/// Convenience: roll `n` dice at once, e.g. `rollN dice 2` for a 2d6 roll
/// as a list rather than a pre-summed int (useful when a table needs the
/// individual dice, not just the sum — none of the tables transcribed so
/// far do, but rules text for some wargames' fumble/critical tables do).
/// </summary>
let rollN (dice: IDiceRoller) (n: int) : int list =
    [ for _ in 1 .. n -> dice.Roll() ]

/// <summary>
/// Rolls `n` dice and sums them — e.g. `rollSum dice 2` for the 2d6 sum
/// every table in Tables/ (Naval Fire, Special Damage, Chance) actually
/// keys on.
/// </summary>
let rollSum (dice: IDiceRoller) (n: int) : int =
    rollN dice n |> List.sum
