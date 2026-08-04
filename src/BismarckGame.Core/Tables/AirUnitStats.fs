/// <summary>
/// AirUnitStats.fs
/// Air unit stats transcribed from the physical air unit counters (BGG
/// counter-sheet photos, brown/gray sections). Every Basic Game air unit
/// prints "2-1" for Endurance-MaxSpeed — Endurance 2 turns, 1 zone/turn —
/// with no exceptions found; only Search Strength differs, and it
/// differs by nationality + role rather than by individual unit.
/// </summary>
module BismarckGame.Core.Tables.AirUnitStats

open BismarckGame.Core.Common

type AirUnitCombatStats =
    { EnduranceRating: int
      MaxSpeedZones: int
      SearchStrengthDay: int
      SearchStrengthNight: int }

let private uniform day night =
    { EnduranceRating = 2; MaxSpeedZones = 1; SearchStrengthDay = day; SearchStrengthNight = night }

/// <summary>
/// Keyed by (Nationality, AirUnitType). Carrier-based vs land-based
/// bombers of the same nationality+type were not observed to differ on
/// the counters photographed, EXCEPT that British bombers split into two
/// distinct roles the counters print separately — Torpedo Bomber and a
/// second (level/dive) bomber role — both using AirUnitType.LevelBomber
/// in Common.fs today since that type doesn't yet distinguish a separate
/// "dive bomber" case. TorpedoBomber below is that distinctly-printed
/// role; LevelBomber covers the other British carrier bomber counters.
/// </summary>
let stats : Map<Nationality * AirUnitType, AirUnitCombatStats> =
    [ (British, LongRangeRecon), uniform 5 6   // Eire, Plymouth, Scapa, Hvalfiord, Gibraltar
      (British, TorpedoBomber), uniform 1 8    // Victorious, Ark Royal
      (British, LevelBomber),   uniform 1 5    // Victorious, Ark Royal, Eagle, Scapa
      (German, LongRangeRecon), uniform 4 7    // Bordeaux, Trondheim
      (German, TorpedoBomber),  uniform 2 6    // rule 2.432: Basic Game German bombers share one
      (German, LevelBomber),    uniform 2 6 ]   // counter type covering both silhouettes — same stats
    |> Map.ofList
