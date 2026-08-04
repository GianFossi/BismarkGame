namespace BismarckGame.Core.Counters.Markers

open BismarckGame.Core.Common
open BismarckGame.Core.Counters

/// Base for the playing-aid markers (rule 2.5) — these aren't fighting
/// units, but the manual groups them under the same "Unit Counters and
/// Markers" heading (2.4), and they're physical counters too, so they
/// get their own small hierarchy rather than being bolted onto
/// ShipCounter/AirUnitCounter. No shared behavior beyond identity exists
/// yet; this base exists so the four marker kinds have a common ancestor
/// to hang future shared logic on (e.g. a UI "counter renderer" that
/// doesn't care which marker kind it's drawing).
[<AbstractClass>]
type MarkerCounter(id: string, nationality: Nationality) =
    inherit Counter(id, id, nationality)
