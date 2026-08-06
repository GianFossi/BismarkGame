/// <summary>Printed Submarine Attack and Defensive Fire tables, rules 22.0 and 23.0.</summary>
module BismarckGame.Core.Tables.SubmarineAttackTable

/// <summary>Anti-submarine-strength column on the printed chart.</summary>
type AntiSubmarineBand = Zero | OneToTwo | ThreeToFour | FiveToSix | SevenToEight | NineToTen | ElevenToTwelve | ThirteenToFourteen | FifteenToSixteen | SeventeenPlus

/// <summary>One submarine-table result: salvoes and elimination stars.</summary>
type AttackResult = { Salvoes: int; EliminationStars: int }

let private bands = [ Zero; OneToTwo; ThreeToFour; FiveToSix; SevenToEight; NineToTen; ElevenToTwelve; ThirteenToFourteen; FifteenToSixteen; SeventeenPlus ]
let private row values = List.zip bands values |> List.map (fun (b, (salvoes, stars)) -> b, { Salvoes = salvoes; EliminationStars = stars }) |> Map.ofList

// Transcribed from the supplied Phase 8 player-aid image. Stars are data:
// rule note 1 rolls once per star and removes one submarine for each six.
let private table =
    [ [ (1,0);(1,0);(1,0);(1,0);(0,0);(0,1);(0,0);(0,1);(0,2);(0,1) ]
      [ (2,0);(1,0);(1,0);(1,0);(1,1);(0,1);(0,1);(0,1);(0,2);(0,2) ]
      [ (2,0);(2,0);(1,0);(1,1);(1,1);(1,1);(0,1);(0,2);(0,2);(0,2) ]
      [ (3,0);(2,0);(2,1);(1,1);(1,1);(1,1);(1,2);(0,1);(0,2);(0,2) ]
      [ (4,0);(3,1);(2,1);(2,1);(1,2);(1,2);(1,2);(1,2);(0,2);(0,2) ]
      [ (5,1);(4,1);(3,1);(2,1);(2,2);(1,2);(1,2);(1,2);(1,2);(0,3) ]
      [ (6,2);(5,1);(4,1);(3,2);(2,2);(2,2);(1,3);(1,3);(1,3);(1,3) ] ]
    |> List.map row

/// <summary>Maps anti-submarine strength to the printed column.</summary>
let band strength =
    match strength with
    | n when n <= 0 -> Zero | 1 | 2 -> OneToTwo | 3 | 4 -> ThreeToFour
    | 5 | 6 -> FiveToSix | 7 | 8 -> SevenToEight | 9 | 10 -> NineToTen
    | 11 | 12 -> ElevenToTwelve | 13 | 14 -> ThirteenToFourteen
    | 15 | 16 -> FifteenToSixteen | _ -> SeventeenPlus

/// <summary>Looks up the printed result, applying the note for prior losses.</summary>
let resolve dieRoll antiSubStrength submarinesPreviouslyLost =
    if dieRoll < 0 || dieRoll > 6 then Error "Submarine Attack Table die roll must be 0-6"
    else Ok table.[max 0 (dieRoll - submarinesPreviouslyLost)].[band antiSubStrength]

/// <summary>Defensive-fire result from the printed 2d6 table.</summary>
type DefensiveFireResult = LoseOneDestroyer of torpedoLost: bool | Miss

/// <summary>Applies the day/night bands and the starred-result die.</summary>
let resolveDefensiveFireAt isNight diceSum torpedoLossDie =
    if diceSum < 2 || diceSum > 12 then Error "Defensive Fire requires a 2-12 dice sum"
    elif (isNight && diceSum <= 3) || ((not isNight) && diceSum <= 6) then Ok (LoseOneDestroyer (torpedoLossDie >= 1 && torpedoLossDie <= 4))
    else Ok Miss

/// <summary>Daytime convenience lookup for callers that do not need the star die.</summary>
let resolveDefensiveFire diceSum = resolveDefensiveFireAt false diceSum 6
