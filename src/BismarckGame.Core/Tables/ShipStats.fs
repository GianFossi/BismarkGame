/// <summary>
/// ShipStats.fs
/// Combat statistics transcribed from the "Bismarck Hit Record Pad"
/// (Avalon Hill, 1978/79) — evasion rating, gun-section salvo strength,
/// and torpedo factors for every ship printed on the pad, not just the
/// ones in the current Basic Game scenario. Kept independent of any one
/// scenario's roster so other scenarios (e.g. a future River Plate
/// variant, which also uses Avalon Hill's Bismarck engine per The
/// Avalon Hill General vol. 16 no. 2) can reuse this table.
///
/// SIMPLIFICATION: the pad prints a full degradation sequence per gun
/// section (e.g. Bismarck's bow turrets are "7 6 5 4 3 2 1", Scharnhorst's
/// secondary is "2 2 1" — not always a simple descending-by-one run).
/// BattleBoard.GunSection only models MaxSalvo/SalvoRemaining decremented
/// by 1 per hit (see Update.fs FireInBattle), so only the leftmost
/// (highest/starting) value of each sequence is captured here. A section
/// like Scharnhorst's secondary — which the pad shows staying at
/// strength 2 for its first two hits before dropping to 1 — will
/// therefore lose one salvo of strength too early under the current
/// model. Fixing this means extending GunSection to hold the full
/// sequence, not just a max/remaining pair — noted as a follow-up, not
/// done here to avoid a type change bundled into a data transcription.
/// </summary>
module BismarckGame.Core.Tables.ShipStats

open BismarckGame.Core.Common
open BismarckGame.Core.BattleBoard

type ShipCombatStats =
    { EvasionRating: int
      /// <summary>
      /// Search Board stats (rule 2.424/2.427), transcribed from the
      /// physical Search Board Ship Counters (BGG counter-sheet photos).
      /// Every Basic Game surface ship prints "1-1" for search strength —
      /// day and night search strength are both 1 for every listed ship,
      /// no exceptions found on the sheet.
      /// </summary>
      SearchStrengthDay: int
      SearchStrengthNight: int
      /// <summary>
      /// Zones per turn (rule 2.427). 2 for most ships; the slower
      /// WWI-era British battleships (Rodney, Nelson, Ramillies, Revenge)
      /// and the old carrier Eagle print 1 instead.
      /// </summary>
      MaxSpeedZones: int
      BowMaxSalvo: int
      SternMaxSalvo: int
      SecondaryPortMaxSalvo: int
      SecondaryStarboardMaxSalvo: int
      /// <summary>
      /// None where the pad shows no torpedo value for that ship (e.g.
      /// Bismarck, King George V, Prince of Wales, Renown, North Carolina).
      /// </summary>
      TorpedoFactors: int option
      Ammunition: int
      /// <summary>
      /// Number of midships boxes on the Hit Record Pad — counted
      /// directly from a zoomed photo, cross-checked against rule 9.714's
      /// two worked examples (Bismarck = 10, Rodney = 6, both match).
      /// Everything else here is a box-count from the same photo at
      /// lower confidence than those two confirmed values.
      /// </summary>
      MaxMidshipsHits: int
      /// <summary>
      /// Fuel factor pool (rule 5.2x), read from the Hit Record Pad's
      /// FUEL boxes. LOW CONFIDENCE: the boxes are printed as a two-row
      /// grid that's hard to count exactly from a photo at an angle;
      /// only Bismarck and Tirpitz (12 each) were read from a tight,
      /// square-on crop and are reasonably solid. Every other value here
      /// is a rougher estimate from the same photo and should be
      /// re-verified against the physical pad before relying on it.
      /// None for cruisers — rule 5.21 exempts them from fuel entirely,
      /// so they don't need a value regardless of what's printed.
      /// </summary>
      FuelFactors: int option }

/// <summary>
/// Keyed by ship name (not ShipId) since this table is scenario-agnostic
/// and different scenarios may assign different ShipId strings to the
/// same historical ship.
///
/// SearchStrengthDay/Night and MaxSpeedZones come from the physical
/// Search Board Ship Counters (BGG counter-sheet photos, red/black/green
/// sections). Every ship's search strength prints "1-1" with no
/// exceptions found. MaxSpeedZones is directly confirmed as 2 for every
/// German ship and for the British/French/US ships whose counters were
/// legible in the photos; confirmed as 1 for Rodney, Nelson, Ramillies,
/// Revenge, and Eagle specifically. North Carolina, Dunkerque, Strasbourg,
/// Augusta, Algerie, Tourville, La Galissonniere, and Gloire did not
/// appear on a "1-1"-style counter in the photos reviewed — their
/// MaxSpeedZones=2 here is a default matching the majority pattern, not a
/// direct read, and should be verified if those ships matter to a future
/// scenario.
/// </summary>
let shipStats : Map<string, ShipCombatStats> =
    [ // BB and BC
      "Bismarck",       { EvasionRating = 29; SearchStrengthDay = 1; SearchStrengthNight = 1; MaxSpeedZones = 2; BowMaxSalvo = 7; SternMaxSalvo = 7; SecondaryPortMaxSalvo = 3; SecondaryStarboardMaxSalvo = 3; TorpedoFactors = None;    Ammunition = 28; MaxMidshipsHits = 10; FuelFactors = Some 12 }
      "Tirpitz",        { EvasionRating = 29; SearchStrengthDay = 1; SearchStrengthNight = 1; MaxSpeedZones = 2; BowMaxSalvo = 7; SternMaxSalvo = 7; SecondaryPortMaxSalvo = 3; SecondaryStarboardMaxSalvo = 3; TorpedoFactors = Some 4;  Ammunition = 28; MaxMidshipsHits = 10; FuelFactors = Some 12 }
      "Scharnhorst",    { EvasionRating = 32; SearchStrengthDay = 1; SearchStrengthNight = 1; MaxSpeedZones = 2; BowMaxSalvo = 5; SternMaxSalvo = 2; SecondaryPortMaxSalvo = 2; SecondaryStarboardMaxSalvo = 2; TorpedoFactors = Some 3;  Ammunition = 28; MaxMidshipsHits = 7; FuelFactors = Some 13 }
      "Gneisenau",      { EvasionRating = 32; SearchStrengthDay = 1; SearchStrengthNight = 1; MaxSpeedZones = 2; BowMaxSalvo = 5; SternMaxSalvo = 2; SecondaryPortMaxSalvo = 2; SecondaryStarboardMaxSalvo = 2; TorpedoFactors = Some 3;  Ammunition = 28; MaxMidshipsHits = 7; FuelFactors = Some 13 }
      "Admiral Scheer",  { EvasionRating = 26; SearchStrengthDay = 1; SearchStrengthNight = 1; MaxSpeedZones = 2; BowMaxSalvo = 3; SternMaxSalvo = 2; SecondaryPortMaxSalvo = 2; SecondaryStarboardMaxSalvo = 2; TorpedoFactors = Some 4;  Ammunition = 28; MaxMidshipsHits = 4; FuelFactors = Some 12 }
      "Hood",           { EvasionRating = 29; SearchStrengthDay = 1; SearchStrengthNight = 1; MaxSpeedZones = 2; BowMaxSalvo = 5; SternMaxSalvo = 5; SecondaryPortMaxSalvo = 1; SecondaryStarboardMaxSalvo = 1; TorpedoFactors = Some 2;  Ammunition = 28; MaxMidshipsHits = 6; FuelFactors = Some 11 }
      "King George V",  { EvasionRating = 29; SearchStrengthDay = 1; SearchStrengthNight = 1; MaxSpeedZones = 2; BowMaxSalvo = 7; SternMaxSalvo = 5; SecondaryPortMaxSalvo = 2; SecondaryStarboardMaxSalvo = 2; TorpedoFactors = None;    Ammunition = 28; MaxMidshipsHits = 7; FuelFactors = Some 11 }
      "Prince of Wales", { EvasionRating = 29; SearchStrengthDay = 1; SearchStrengthNight = 1; MaxSpeedZones = 2; BowMaxSalvo = 7; SternMaxSalvo = 5; SecondaryPortMaxSalvo = 2; SecondaryStarboardMaxSalvo = 2; TorpedoFactors = None;   Ammunition = 28; MaxMidshipsHits = 7; FuelFactors = Some 11 }
      "Rodney",         { EvasionRating = 21; SearchStrengthDay = 1; SearchStrengthNight = 1; MaxSpeedZones = 1; BowMaxSalvo = 8; SternMaxSalvo = 4; SecondaryPortMaxSalvo = 2; SecondaryStarboardMaxSalvo = 2; TorpedoFactors = Some 2;  Ammunition = 28; MaxMidshipsHits = 6; FuelFactors = Some 9 } // stern turret rules: see Hit Record Pad note **
      "Nelson",         { EvasionRating = 21; SearchStrengthDay = 1; SearchStrengthNight = 1; MaxSpeedZones = 1; BowMaxSalvo = 8; SternMaxSalvo = 4; SecondaryPortMaxSalvo = 2; SecondaryStarboardMaxSalvo = 2; TorpedoFactors = Some 2;  Ammunition = 28; MaxMidshipsHits = 6; FuelFactors = Some 9 }
      "Ramillies",      { EvasionRating = 19; SearchStrengthDay = 1; SearchStrengthNight = 1; MaxSpeedZones = 1; BowMaxSalvo = 5; SternMaxSalvo = 5; SecondaryPortMaxSalvo = 2; SecondaryStarboardMaxSalvo = 2; TorpedoFactors = Some 2;  Ammunition = 28; MaxMidshipsHits = 5; FuelFactors = Some 9 }
      "Revenge",        { EvasionRating = 20; SearchStrengthDay = 1; SearchStrengthNight = 1; MaxSpeedZones = 1; BowMaxSalvo = 5; SternMaxSalvo = 5; SecondaryPortMaxSalvo = 2; SecondaryStarboardMaxSalvo = 2; TorpedoFactors = Some 2;  Ammunition = 28; MaxMidshipsHits = 5; FuelFactors = Some 9 }
      "Repulse",        { EvasionRating = 28; SearchStrengthDay = 1; SearchStrengthNight = 1; MaxSpeedZones = 2; BowMaxSalvo = 5; SternMaxSalvo = 2; SecondaryPortMaxSalvo = 1; SecondaryStarboardMaxSalvo = 1; TorpedoFactors = Some 4;  Ammunition = 28; MaxMidshipsHits = 5; FuelFactors = Some 12 }
      "Renown",         { EvasionRating = 29; SearchStrengthDay = 1; SearchStrengthNight = 1; MaxSpeedZones = 2; BowMaxSalvo = 5; SternMaxSalvo = 2; SecondaryPortMaxSalvo = 1; SecondaryStarboardMaxSalvo = 1; TorpedoFactors = None;    Ammunition = 28; MaxMidshipsHits = 5; FuelFactors = Some 12 }
      "North Carolina", { EvasionRating = 28; SearchStrengthDay = 1; SearchStrengthNight = 1; MaxSpeedZones = 2; BowMaxSalvo = 10; SternMaxSalvo = 5; SecondaryPortMaxSalvo = 2; SecondaryStarboardMaxSalvo = 2; TorpedoFactors = None;   Ammunition = 28; MaxMidshipsHits = 9; FuelFactors = Some 13 }
      "Dunkerque",      { EvasionRating = 29; SearchStrengthDay = 1; SearchStrengthNight = 1; MaxSpeedZones = 2; BowMaxSalvo = 5; SternMaxSalvo = 4; SecondaryPortMaxSalvo = 2; SecondaryStarboardMaxSalvo = 2; TorpedoFactors = Some 3;  Ammunition = 28; MaxMidshipsHits = 6; FuelFactors = Some 10 } // stern: "treat as second bow turret" per note ***
      "Strasbourg",     { EvasionRating = 29; SearchStrengthDay = 1; SearchStrengthNight = 1; MaxSpeedZones = 2; BowMaxSalvo = 5; SternMaxSalvo = 4; SecondaryPortMaxSalvo = 2; SecondaryStarboardMaxSalvo = 2; TorpedoFactors = Some 3;  Ammunition = 28; MaxMidshipsHits = 6; FuelFactors = Some 10 }

      // CA and CL
      "Dorsetshire",    { EvasionRating = 32; SearchStrengthDay = 1; SearchStrengthNight = 1; MaxSpeedZones = 2; BowMaxSalvo = 3; SternMaxSalvo = 2; SecondaryPortMaxSalvo = 0; SecondaryStarboardMaxSalvo = 0; TorpedoFactors = Some 4;  Ammunition = 28; MaxMidshipsHits = 3; FuelFactors = None }
      "Norfolk",        { EvasionRating = 32; SearchStrengthDay = 1; SearchStrengthNight = 1; MaxSpeedZones = 2; BowMaxSalvo = 3; SternMaxSalvo = 2; SecondaryPortMaxSalvo = 0; SecondaryStarboardMaxSalvo = 0; TorpedoFactors = Some 4;  Ammunition = 28; MaxMidshipsHits = 3; FuelFactors = None }
      "Suffolk",        { EvasionRating = 31; SearchStrengthDay = 1; SearchStrengthNight = 1; MaxSpeedZones = 2; BowMaxSalvo = 3; SternMaxSalvo = 2; SecondaryPortMaxSalvo = 0; SecondaryStarboardMaxSalvo = 0; TorpedoFactors = Some 4;  Ammunition = 28; MaxMidshipsHits = 3; FuelFactors = None }
      "Manchester",     { EvasionRating = 32; SearchStrengthDay = 1; SearchStrengthNight = 1; MaxSpeedZones = 2; BowMaxSalvo = 3; SternMaxSalvo = 2; SecondaryPortMaxSalvo = 0; SecondaryStarboardMaxSalvo = 0; TorpedoFactors = Some 3;  Ammunition = 28; MaxMidshipsHits = 3; FuelFactors = None }
      "Birmingham",     { EvasionRating = 32; SearchStrengthDay = 1; SearchStrengthNight = 1; MaxSpeedZones = 2; BowMaxSalvo = 3; SternMaxSalvo = 2; SecondaryPortMaxSalvo = 0; SecondaryStarboardMaxSalvo = 0; TorpedoFactors = Some 3;  Ammunition = 28; MaxMidshipsHits = 3; FuelFactors = None }
      "Sheffield",      { EvasionRating = 32; SearchStrengthDay = 1; SearchStrengthNight = 1; MaxSpeedZones = 2; BowMaxSalvo = 3; SternMaxSalvo = 2; SecondaryPortMaxSalvo = 0; SecondaryStarboardMaxSalvo = 0; TorpedoFactors = Some 3;  Ammunition = 28; MaxMidshipsHits = 3; FuelFactors = None }
      "Kenya",          { EvasionRating = 33; SearchStrengthDay = 1; SearchStrengthNight = 1; MaxSpeedZones = 2; BowMaxSalvo = 3; SternMaxSalvo = 2; SecondaryPortMaxSalvo = 0; SecondaryStarboardMaxSalvo = 0; TorpedoFactors = Some 3;  Ammunition = 28; MaxMidshipsHits = 3; FuelFactors = None }
      "Edinburgh",      { EvasionRating = 32; SearchStrengthDay = 1; SearchStrengthNight = 1; MaxSpeedZones = 2; BowMaxSalvo = 3; SternMaxSalvo = 2; SecondaryPortMaxSalvo = 0; SecondaryStarboardMaxSalvo = 0; TorpedoFactors = Some 3;  Ammunition = 28; MaxMidshipsHits = 3; FuelFactors = None }
      "Hermione",       { EvasionRating = 33; SearchStrengthDay = 1; SearchStrengthNight = 1; MaxSpeedZones = 2; BowMaxSalvo = 3; SternMaxSalvo = 3; SecondaryPortMaxSalvo = 0; SecondaryStarboardMaxSalvo = 0; TorpedoFactors = Some 3;  Ammunition = 28; MaxMidshipsHits = 3; FuelFactors = None }
      "Aurora",         { EvasionRating = 32; SearchStrengthDay = 1; SearchStrengthNight = 1; MaxSpeedZones = 2; BowMaxSalvo = 2; SternMaxSalvo = 1; SecondaryPortMaxSalvo = 0; SecondaryStarboardMaxSalvo = 0; TorpedoFactors = Some 3;  Ammunition = 28; MaxMidshipsHits = 3; FuelFactors = None }
      "Arethusa",       { EvasionRating = 32; SearchStrengthDay = 1; SearchStrengthNight = 1; MaxSpeedZones = 2; BowMaxSalvo = 2; SternMaxSalvo = 1; SecondaryPortMaxSalvo = 0; SecondaryStarboardMaxSalvo = 0; TorpedoFactors = Some 3;  Ammunition = 28; MaxMidshipsHits = 3; FuelFactors = None }
      "Galatea",        { EvasionRating = 32; SearchStrengthDay = 1; SearchStrengthNight = 1; MaxSpeedZones = 2; BowMaxSalvo = 2; SternMaxSalvo = 1; SecondaryPortMaxSalvo = 0; SecondaryStarboardMaxSalvo = 0; TorpedoFactors = Some 3;  Ammunition = 28; MaxMidshipsHits = 3; FuelFactors = None }
      "Prinz Eugen",    { EvasionRating = 32; SearchStrengthDay = 1; SearchStrengthNight = 1; MaxSpeedZones = 2; BowMaxSalvo = 3; SternMaxSalvo = 2; SecondaryPortMaxSalvo = 0; SecondaryStarboardMaxSalvo = 0; TorpedoFactors = Some 6;  Ammunition = 28; MaxMidshipsHits = 3; FuelFactors = None }
      "Hipper",         { EvasionRating = 32; SearchStrengthDay = 1; SearchStrengthNight = 1; MaxSpeedZones = 2; BowMaxSalvo = 3; SternMaxSalvo = 2; SecondaryPortMaxSalvo = 0; SecondaryStarboardMaxSalvo = 0; TorpedoFactors = Some 6;  Ammunition = 28; MaxMidshipsHits = 3; FuelFactors = None }
      "Koln",           { EvasionRating = 32; SearchStrengthDay = 1; SearchStrengthNight = 1; MaxSpeedZones = 2; BowMaxSalvo = 1; SternMaxSalvo = 3; SecondaryPortMaxSalvo = 0; SecondaryStarboardMaxSalvo = 0; TorpedoFactors = Some 6;  Ammunition = 28; MaxMidshipsHits = 3; FuelFactors = None }
      "Nurnberg",       { EvasionRating = 32; SearchStrengthDay = 1; SearchStrengthNight = 1; MaxSpeedZones = 2; BowMaxSalvo = 1; SternMaxSalvo = 3; SecondaryPortMaxSalvo = 0; SecondaryStarboardMaxSalvo = 0; TorpedoFactors = Some 6;  Ammunition = 28; MaxMidshipsHits = 3; FuelFactors = None }
      "London",         { EvasionRating = 32; SearchStrengthDay = 1; SearchStrengthNight = 1; MaxSpeedZones = 2; BowMaxSalvo = 3; SternMaxSalvo = 2; SecondaryPortMaxSalvo = 0; SecondaryStarboardMaxSalvo = 0; TorpedoFactors = Some 4;  Ammunition = 28; MaxMidshipsHits = 3; FuelFactors = None }
      "Cairo",          { EvasionRating = 29; SearchStrengthDay = 1; SearchStrengthNight = 1; MaxSpeedZones = 2; BowMaxSalvo = 2; SternMaxSalvo = 1; SecondaryPortMaxSalvo = 0; SecondaryStarboardMaxSalvo = 0; TorpedoFactors = None;    Ammunition = 28; MaxMidshipsHits = 3; FuelFactors = None }
      "Exeter",         { EvasionRating = 32; SearchStrengthDay = 1; SearchStrengthNight = 1; MaxSpeedZones = 2; BowMaxSalvo = 2; SternMaxSalvo = 2; SecondaryPortMaxSalvo = 0; SecondaryStarboardMaxSalvo = 0; TorpedoFactors = Some 3;  Ammunition = 28; MaxMidshipsHits = 3; FuelFactors = None }
      "Augusta",        { EvasionRating = 32; SearchStrengthDay = 1; SearchStrengthNight = 1; MaxSpeedZones = 2; BowMaxSalvo = 3; SternMaxSalvo = 2; SecondaryPortMaxSalvo = 0; SecondaryStarboardMaxSalvo = 0; TorpedoFactors = None;    Ammunition = 28; MaxMidshipsHits = 3; FuelFactors = None }
      "Algerie",        { EvasionRating = 32; SearchStrengthDay = 1; SearchStrengthNight = 1; MaxSpeedZones = 2; BowMaxSalvo = 3; SternMaxSalvo = 2; SecondaryPortMaxSalvo = 0; SecondaryStarboardMaxSalvo = 0; TorpedoFactors = Some 2;  Ammunition = 28; MaxMidshipsHits = 3; FuelFactors = None }
      "Tourville",      { EvasionRating = 32; SearchStrengthDay = 1; SearchStrengthNight = 1; MaxSpeedZones = 2; BowMaxSalvo = 3; SternMaxSalvo = 2; SecondaryPortMaxSalvo = 0; SecondaryStarboardMaxSalvo = 0; TorpedoFactors = Some 2;  Ammunition = 28; MaxMidshipsHits = 3; FuelFactors = None }
      "La Galissonniere", { EvasionRating = 33; SearchStrengthDay = 1; SearchStrengthNight = 1; MaxSpeedZones = 2; BowMaxSalvo = 3; SternMaxSalvo = 1; SecondaryPortMaxSalvo = 0; SecondaryStarboardMaxSalvo = 0; TorpedoFactors = Some 2; Ammunition = 28; MaxMidshipsHits = 3; FuelFactors = None }
      "Gloire",         { EvasionRating = 31; SearchStrengthDay = 1; SearchStrengthNight = 1; MaxSpeedZones = 2; BowMaxSalvo = 3; SternMaxSalvo = 0; SecondaryPortMaxSalvo = 0; SecondaryStarboardMaxSalvo = 0; TorpedoFactors = Some 2;  Ammunition = 28; MaxMidshipsHits = 3; FuelFactors = None }

      // CV (carriers fire no turrets; Bow/Stern fields repurposed as 0,
      // Secondary Port/Starboard hold their anti-aircraft-ish "2/2" or
      // "1/1" values as printed — air attack/flight capacity itself is
      // tracked separately, not by this record)
      "Victorious",     { EvasionRating = 32; SearchStrengthDay = 1; SearchStrengthNight = 1; MaxSpeedZones = 2; BowMaxSalvo = 0; SternMaxSalvo = 0; SecondaryPortMaxSalvo = 2; SecondaryStarboardMaxSalvo = 2; TorpedoFactors = None; Ammunition = 0; MaxMidshipsHits = 0; FuelFactors = None }
      "Ark Royal",      { EvasionRating = 31; SearchStrengthDay = 1; SearchStrengthNight = 1; MaxSpeedZones = 2; BowMaxSalvo = 0; SternMaxSalvo = 0; SecondaryPortMaxSalvo = 2; SecondaryStarboardMaxSalvo = 2; TorpedoFactors = None; Ammunition = 0; MaxMidshipsHits = 0; FuelFactors = None }
      "Eagle",          { EvasionRating = 22; SearchStrengthDay = 1; SearchStrengthNight = 1; MaxSpeedZones = 1; BowMaxSalvo = 0; SternMaxSalvo = 0; SecondaryPortMaxSalvo = 1; SecondaryStarboardMaxSalvo = 1; TorpedoFactors = None; Ammunition = 0; MaxMidshipsHits = 0; FuelFactors = None }
      "Graf Zeppelin",  { EvasionRating = 33; SearchStrengthDay = 1; SearchStrengthNight = 1; MaxSpeedZones = 2; BowMaxSalvo = 0; SternMaxSalvo = 0; SecondaryPortMaxSalvo = 2; SecondaryStarboardMaxSalvo = 2; TorpedoFactors = None; Ammunition = 0; MaxMidshipsHits = 0; FuelFactors = None } ]
    |> Map.ofList

/// <summary>
/// Builds the four BattleBoard.GunSection records for a ship from its
/// stats, at full (undamaged) strength.
/// </summary>
let freshGunSections (stats: ShipCombatStats) : GunSection list =
    [ { Section = BowGuns; MaxSalvo = stats.BowMaxSalvo; SalvoRemaining = stats.BowMaxSalvo; CanFireBothRanges = true }
      { Section = SternGuns; MaxSalvo = stats.SternMaxSalvo; SalvoRemaining = stats.SternMaxSalvo; CanFireBothRanges = true }
      { Section = PortGuns; MaxSalvo = stats.SecondaryPortMaxSalvo; SalvoRemaining = stats.SecondaryPortMaxSalvo; CanFireBothRanges = false }
      { Section = StarboardGuns; MaxSalvo = stats.SecondaryStarboardMaxSalvo; SalvoRemaining = stats.SecondaryStarboardMaxSalvo; CanFireBothRanges = false } ]

/// <summary>
/// Rules 9.723-9.726: EVERY midships hit reduces current evasion rating
/// by an amount that depends on the ship, regardless of whether that
/// same hit also carries a separate PERMANENT reduction from the Special
/// Damage table. This is temporary — repairable via the Evasion Repair
/// Table (rule 9.728) — unlike the permanent kind.
///   Bismarck:                    -1 per midships hit (9.724)
///   All other battleships/carriers: -2 per midships hit (9.724)
///   Prinz Eugen:                 -3 per midships hit (9.725)
///   All cruisers:                -5 per midships hit (9.726)
/// </summary>
let temporaryEvasionLossPerMidshipsHit (shipName: string) (shipClass: ShipClass) : int =
    match shipName, shipClass with
    | "Bismarck", _ -> 1
    | "Prinz Eugen", _ -> 3
    | _, (Battleship | AircraftCarrier) -> 2
    | _, (HeavyCruiser | LightCruiser) -> 5
    | _, (Battlecruiser | PocketBattleship) -> 2   // not explicitly listed by the rule text found so far; grouped with "other battleships" as the closest stated category — verify if a clearer source turns up
