/// <summary>Public rule decisions introduced by the published errata.</summary>
module BismarckGame.Core.ErrataRules

open BismarckGame.Core.BattleBoard
open BismarckGame.Core.Common

/// <summary>Assigns a section's available salvoes to one target, then applies rule 9.651.</summary>
let assignAndHalveSalvoes (available: int) (distance: int) : int =
    if available <= 0 then 0
    elif distance > 3 then available / 2
    else available

/// <summary>Returns true only when the target is in the hex immediately beyond the bow or stern (errata 9.716).</summary>
let isDirectBowOrStern (shooter: HexCoord) (target: HexCoord) (facing: HexSide) : bool * bool =
    let bow = hexNeighbor shooter facing
    let stern = hexNeighbor shooter facing.Opposite
    target = bow, target = stern

/// <summary>Applies 9.717: a hit fills an unmarked secondary box on that side before becoming midships damage.</summary>
let resolveSecondaryHit (portMarked: int) (starboardMarked: int) (portCapacity: int) (starboardCapacity: int) (side: GunSectionType) : int * int * bool =
    match side with
    | PortGuns when portMarked < portCapacity -> portMarked + 1, starboardMarked, false
    | StarboardGuns when starboardMarked < starboardCapacity -> portMarked, starboardMarked + 1, false
    | _ -> portMarked, starboardMarked, true

/// <summary>Whether a task force may combine air attacks under errata 27.52.</summary>
let mayCombineTaskForceAirAttack (sameTaskForce: bool) (allCarrierBased: bool) : bool =
    sameTaskForce && allCarrierBased

/// <summary>Fastest non-carrier evasion used by an attacking task force (errata 9.222).</summary>
let taskForceEvasion (ships: (ShipClass * int) list) : int option =
    ships
    |> List.choose (fun (shipClass, evasion) -> if shipClass = AircraftCarrier then None else Some evasion)
    |> List.sortDescending
    |> List.tryHead
