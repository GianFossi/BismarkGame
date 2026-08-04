namespace BismarckGame.Core.Counters.Markers

/// <summary>
/// Shadow marker (rule 2.53, 8.1). Live state (zone, shadowing/shadowed
/// unit) lives in Markers.fs's `ShadowMarker` record.
/// </summary>
type ShadowMarker(id, nationality) =
    inherit MarkerCounter(id, nationality)
