namespace BismarckGame.Core.Counters.Markers

/// <summary>
/// Convoy escort marker (rule 2.51). Live state (which ship, direction
/// of travel) lives in Markers.fs's `ConvoyMarker` record.
/// </summary>
type ConvoyMarker(id, nationality) =
    inherit MarkerCounter(id, nationality)
