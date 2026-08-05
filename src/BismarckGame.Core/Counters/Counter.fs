namespace BismarckGame.Core.Counters

open BismarckGame.Core.Common

/// Base class for every physical game piece ("counter") printed in the
/// game — ships, air units, and the various markers (rule 2.4, "Unit
/// Counters and Markers"). Deliberately holds NO board position or
/// per-turn state (current zone, mode, fuel, hits taken...): that is
/// scenario/game-state data and lives in GameState.fs / Units.fs /
/// Markers.fs. This hierarchy only describes what KIND of piece
/// something is and the rules-fixed properties that follow from that —
/// which is exactly the part that stays valid no matter which Search
/// Board / order of battle a future scenario loads (see Scenario.fs).
/// It also exposes a stable reference for the counter's graphical
/// representation (printed face or future sprite key) so a UI can map a
/// specific piece type to its artwork without adding board-state data
/// here.
[<AbstractClass>]
type Counter(id: string, name: string, nationality: Nationality) =
    member _.Id = id
    member _.Name = name
    member _.Nationality = nationality

    /// <summary>
    /// Stable reference for the counter's graphical representation.
    /// Defaults to the counter id because the project does not yet have
    /// a separate art-asset catalog.
    /// </summary>
    member _.GraphicReference = id

    /// <summary>
    /// False for counter types the rules mark as absent from the Basic
    /// Game (destroyers, submarines, fighters — rule 2.412, 2.422, 28.0).
    /// Default true; specific leaf types override to false.
    /// </summary>
    abstract member IsBasicGameUnit: bool
    default _.IsBasicGameUnit = true
