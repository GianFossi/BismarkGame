namespace BismarckGame.Core.Counters.Markers

/// <summary>
/// Task Force marker (rule 5.4, 2.52). The actual composition (which
/// ships), zone, and mode of a task force is mutable per-game state and
/// lives in Markers.fs's `TaskForce` record, keyed by TaskForceId — this
/// class is only the counter-identity/taxonomy entry, not the live state.
/// </summary>
type TaskForceMarker(id, nationality) =
    inherit MarkerCounter(id, nationality)
