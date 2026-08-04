/// <summary>
/// Common.fs
/// Primitive types shared by the Search Board and Battle Board domains.
/// Source: Bismarck (Avalon Hill, 1979) Basic Game rules, sections 2.0-2.8.
/// </summary>
module BismarckGame.Core.Common

/// <summary>
/// The two sides in the Basic Game. (Rule 2.421 — US ships exist but are
/// "not used in Basic Game", so they are omitted here and can be added
/// when the Intermediate Game domain is modeled.)
/// </summary>
type Nationality =
    | British
    | German

/// <summary>
/// Ship classes actually used in the Basic Game (rule 2.422).
/// Destroyers and submarines exist as counters but are explicitly
/// "not used in Basic Game" — they belong to the Intermediate Game model.
/// </summary>
type ShipClass =
    | Battleship            // BB
    | Battlecruiser         // BC
    | PocketBattleship      // PB (Panzerschiff / "Armored Cruiser")
    | HeavyCruiser          // CA
    | LightCruiser          // CL
    | AircraftCarrier       // CV

/// <summary>
/// A ship counter has two faces: movement mode (front) or patrol mode
/// (back). Aircraft carriers have no patrol mode (rule 2.423).
/// </summary>
type ShipMode =
    | Movement
    | Patrol

/// <summary>
/// Air unit categories used in the Basic Game (rule 2.43).
/// </summary>
type AirUnitType =
    | LongRangeRecon        // LR recon — never attacks
    | TorpedoBomber
    | LevelBomber

/// <summary>
/// Bomber air units alternate between Attack and Reconnaissance mode for
/// the whole mission once airborne (rule 6.31). LR recon units alternate
/// between Movement and Patrol mode instead (rule 2.434).
/// </summary>
type AirUnitMode =
    | BomberAttack
    | BomberReconnaissance
    | ReconMovement
    | ReconPatrol

/// <summary>
/// Search capability differs between day and night turns (rule 2.424 /
/// 2.435) for both ships and air units.
/// </summary>
type SearchStrength =
    { Day: int
      Night: int }

/// <summary>
/// Identifies a specific counter instance on the board. Kept as a plain
/// string id (e.g. "GER-BB-Bismarck") rather than a GUID so it stays
/// human-readable in save files and UI bindings.
/// </summary>
type UnitId = UnitId of string

type ShipId = ShipId of string
type AirUnitId = AirUnitId of string
type TaskForceId = TaskForceId of int
