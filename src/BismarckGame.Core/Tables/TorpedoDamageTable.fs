/// <summary>Torpedo Damage Table from the Intermediate Player Aid card.</summary>
module BismarckGame.Core.Tables.TorpedoDamageTable

open BismarckGame.Core.Common

/// <summary>Target categories printed on the Torpedo Damage Table.</summary>
type TargetType = Cruiser | Battleship | Aircraft

/// <summary>Damage produced by one successful torpedo hit.</summary>
type Damage =
    | Miss
    | Midships of count: int * evasionReduction: int option
    | Sunk

/// <summary>Resolves the two-dice Torpedo Hit Table used by British destroyer attacks.</summary>
let torpedoHit (evasionRating: int) (diceSum: int) : Result<bool, string> =
    if diceSum < 2 || diceSum > 12 then Error "Torpedo Hit Table requires a 2-12 dice sum"
    else
        let threshold =
            match evasionRating with
            | n when n <= 5 -> 10
            | n when n <= 10 -> 8
            | n when n <= 15 -> 7
            | n when n <= 20 -> 6
            | n when n <= 25 -> 5
            | n when n <= 30 -> 4
            | _ -> 3
        Ok(diceSum <= threshold)

let private cruiser =
    [ 1, Midships(1, Some 5); 2, Midships(1, Some 10); 3, Midships(2, Some 15)
      4, Midships(2, Some 20); 5, Sunk; 6, Sunk ] |> Map.ofList

let private battleship =
    [ 1, Midships(1, None); 2, Midships(1, Some 4); 3, Midships(1, Some 6)
      4, Midships(1, Some 20); 5, Midships(2, Some 8); 6, Midships(2, Some 10) ] |> Map.ofList

let private aircraft =
    [ 1, Midships(1, None); 2, Midships(1, Some 4); 3, Midships(1, Some 8)
      4, Midships(2, Some 12); 5, Midships(2, Some 16); 6, Sunk ] |> Map.ofList

/// <summary>Maps a Basic/Intermediate ship class to the printed target category.</summary>
let targetType = function
    | BismarckGame.Core.Common.AircraftCarrier -> TargetType.Aircraft
    | BismarckGame.Core.Common.Battleship | BismarckGame.Core.Common.Battlecruiser -> TargetType.Battleship
    | BismarckGame.Core.Common.PocketBattleship | BismarckGame.Core.Common.HeavyCruiser | BismarckGame.Core.Common.LightCruiser -> TargetType.Cruiser

/// <summary>
/// Resolves the printed table. Rolls outside 1-6 are rejected. The starred
/// targets take one midships hit and half the printed evasion reduction;
/// Bismarck and Tirpitz treat the result as a miss.
/// </summary>
let resolve (targetName: string) (targetClass: ShipClass) (dieRoll: int) : Result<Damage, string> =
    if dieRoll < 1 || dieRoll > 6 then Error "Torpedo Damage Table requires a 1-6 die roll"
    elif targetName = "Bismarck" || targetName = "Tirpitz" then Ok Miss
    else
        let raw =
            match targetType targetClass with
            | TargetType.Cruiser -> cruiser.[dieRoll]
            | TargetType.Battleship -> battleship.[dieRoll]
            | TargetType.Aircraft -> aircraft.[dieRoll]
        match targetName, raw with
        | ("Prince of Wales" | "King George V" | "North Carolina"), Midships(_, reduction) ->
            Midships(1, reduction |> Option.map (fun value -> value / 2)) |> Ok
        | _, result -> Ok result
