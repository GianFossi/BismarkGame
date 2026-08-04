/// <summary>
/// EvasionEffects.fs
/// "Evasion Repair Table" and "Effect of Current Evasion Level on Search
/// and Tactical Movement", transcribed from the Basic Game Tables Card.
///
/// IMPORTANT DISCOVERY versus the rest of this codebase so far: a ship's
/// maximum movement — on BOTH the Search Board and the Battle Board — is
/// not a fixed per-ship stat. It's derived from the ship's CURRENT
/// evasion rating (which starts at ShipStats.EvasionRating and drops when
/// Special Damage results say "reduce evasion rating by N", see
/// NavalFireTables.fs). This means Units.ShipCounter.MaxSpeedZones and
/// BattleBoard.BattleShipState's movement handling should ultimately be
/// computed via `searchBoardMaxSpeed`/`battleBoardMovementOptions` below
/// rather than stored as a static field — Update.fs's MoveShip still
/// checks adjacency only and doesn't yet enforce a per-turn zone budget
/// against this table; wiring that up is the natural next step.
/// </summary>
module BismarckGame.Core.Tables.EvasionEffects

/// <summary>
/// Die roll (repairing temporary evasion damage, rule: "both players
/// attempt to repair temporary evasion rating damage" in Ship Movement
/// Phase) -> evasion factors restored.
/// </summary>
let evasionRepairTable : Map<int, int> =
    [ 1, 0; 2, 0; 3, 2; 4, 4; 5, 6; 6, 8 ] |> Map.ofList

/// <summary>
/// Search Board: current evasion rating -> max zones per turn.
/// "Emergency Movement" means one zone, but only on 'C' turns (see
/// Tables/TimeAndVisibility.fs's IsEmergencyMovementTurn).
/// </summary>
type SearchBoardSpeed =
    | Speed0
    | EmergencyMovementOnly   // 1 zone, C turns only
    | Speed1
    | Speed2

let searchBoardMaxSpeed (currentEvasionRating: int) : SearchBoardSpeed =
    match currentEvasionRating with
    | e when e <= 6 -> Speed0
    | e when e <= 15 -> EmergencyMovementOnly
    | e when e <= 24 -> Speed1
    | _ -> Speed2

/// <summary>
/// Battle Board: current evasion rating -> the set of (hexes-moved,
/// direction-changes) trade-off pairs the ship may choose between this
/// round (rule 9.53's Combat Movement Chart). E.g. evasion 18-24 offers
/// (0 hexes, 2 turns) OR (1 hex, 1 turn) OR (2 hexes, 0 turns).
/// </summary>
let battleBoardMovementOptions (currentEvasionRating: int) : (int * int) list =
    match currentEvasionRating with
    | 0 -> [ 0, 0 ]
    | e when e <= 3 -> [ 0, 1 ]
    | e when e <= 10 -> [ 1, 1 ]
    | e when e <= 17 -> [ 0, 2; 1, 1 ]
    | e when e <= 24 -> [ 0, 2; 1, 1; 2, 0 ]
    | e when e <= 29 -> [ 0, 3; 1, 2; 2, 1 ]
    | _ -> [ 1, 3; 2, 2 ]
