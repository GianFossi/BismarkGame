/// <summary>
/// Markers.fs
/// Search Board playing-aid markers (rule 2.5): task forces, convoy escort,
/// shadow tracking, and located-enemy location markers.
/// </summary>
module BismarckGame.Core.Markers

open BismarckGame.Core.Common
open BismarckGame.Core.SearchBoard

/// <summary>
/// Compass-ish heading used only to record a convoy's direction of
/// movement on the board (rule 2.51 — "an arrow indicates the direction
/// the convoy is moving").
/// </summary>
type Heading =
    | North | NorthEast | East | SouthEast
    | South | SouthWest | West | NorthWest

/// <summary>
/// Two or more ships combined to move/fight as one unit (rule 5.4).
/// Represented on the board by a single numbered counter (rule 5.43).
/// </summary>
type TaskForce =
    { Id: TaskForceId
      Nationality: Nationality
      Ships: ShipId list
      Zone: GridCoordinate
      /// <summary>
      /// A task force patrols at the search capability of any one member
      /// ship (rule 5.45); it uses the slowest ship's speed to move
      /// (rule 5.44).
      /// </summary>
      Mode: ShipMode }

/// <summary>
/// Marks a ship on convoy-escort duty (rule 2.51). Two British battleships
/// start the Basic Game assigned to this role.
/// </summary>
type ConvoyMarker =
    { EscortedShip: ShipId
      Direction: Heading }

/// <summary>
/// Placed in every zone containing a shadowed ship/task force so both
/// players can track ongoing shadow attempts (rule 2.53, 8.1).
/// </summary>
type ShadowMarker =
    { Zone: GridCoordinate
      ShadowingUnit: UnitId
      ShadowedUnit: UnitId }

/// <summary>
/// Tracks the position of an enemy ship that has been located by search,
/// without revealing its exact identity — only its general type symbol
/// (rule 2.54).
/// </summary>
type LocationMarker =
    { Zone: GridCoordinate
      RevealedShipClass: ShipClass option   // None if only "unknown contact"
      Owner: Nationality }                   // whose ship this marker tracks
