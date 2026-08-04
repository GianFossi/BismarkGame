namespace BismarckGame.Core.Counters.Markers

/// <summary>
/// Location marker (rule 2.54) — tracks a located-but-not-fully-identified
/// enemy ship's position and general type. Live state lives in Markers.fs's
/// `LocationMarker` record.
/// </summary>
type LocationMarker(id, nationality) =
    inherit MarkerCounter(id, nationality)
