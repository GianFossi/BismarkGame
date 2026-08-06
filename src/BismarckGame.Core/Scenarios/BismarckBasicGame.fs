/// <summary>
/// BismarckBasicGame.fs
/// Concrete scenario data for the historical 1941 hunt-for-the-Bismarck
/// Search Board, transcribed from photographs of the physical board.
/// </summary>
module BismarckGame.Core.Scenarios.BismarckBasicGame

open BismarckGame.Core.Common
open BismarckGame.Core.SearchBoard
open BismarckGame.Core.Scenario
open BismarckGame.Core.GameState
open BismarckGame.Core.VictoryConditions

/// <summary>
/// One physical row of the board: which column numbers exist (as
/// possibly-disjoint ranges, to represent land gaps), plus any ports or
/// Irish-Sea zones in that row.
/// </summary>
type private RowSpec =
    { Row: char
      ColumnRanges: (int * int) list
      Ports: (int * Nationality) list
      IrishSea: int list }

/// <summary>
/// ACCURACY NOTE (revised): re-transcribed against a sharper, straight-on
/// photo of the physical board (previous version was from an angled photo
/// with glare and is superseded). Verified row-by-row against zoomed
/// crops. Two corrections of note versus the original pass:
///   - The Irish Sea (rule 5.18) is specifically zones L20/M20/N20 — the
///     narrow strait between Eire and Great Britain — not the wider
///     range originally guessed.
///   - Board rows are NOT simple contiguous ranges once they reach the
///     British Isles: Eire and Great Britain themselves occupy grid
///     cells, so e.g. row L has sea zones at 3-17, a single zone at 20
///     (Irish Sea), and 23 (English Channel side), with 18/19/21/22
///     being land.
/// Remaining low-confidence spots: row A's exact right edge (cut off at
/// the photo edge, assumed to end at 18) and row G's right edge near
/// Shetland (assumed to reach 21 based on the earlier photo).
/// </summary>
let private rowSpecs : RowSpec list =
    [ { Row = 'A'; ColumnRanges = [ 8, 18 ];                     Ports = []; IrishSea = [] }
      { Row = 'B'; ColumnRanges = [ 7, 18 ];                     Ports = []; IrishSea = [] }
      { Row = 'C'; ColumnRanges = [ 5, 7; 11, 19 ];               Ports = [ 19, German ]; IrishSea = [] }   // gap = Iceland; C19 ~ Trondheim
      { Row = 'D'; ColumnRanges = [ 4, 7; 9, 18 ];                Ports = [ 9, British ]; IrishSea = [] }   // D09 Hvalfiord; gap = Iceland
      { Row = 'E'; ColumnRanges = [ 3, 9; 11, 18 ];               Ports = []; IrishSea = [] }               // gap = Iceland
      { Row = 'F'; ColumnRanges = [ 2, 12; 13, 20 ];              Ports = [ 20, German ]; IrishSea = [] }   // F20 Bergen; gap = Faeroe Islands
      { Row = 'G'; ColumnRanges = [ 1, 13; 14, 21 ];              Ports = []; IrishSea = [] }               // gap = Shetland Islands
      { Row = 'H'; ColumnRanges = [ 1, 22 ];                      Ports = [ 18, British ]; IrishSea = [] }  // H18 ~ Scapa Flow
      { Row = 'I'; ColumnRanges = [ 2, 22 ];                      Ports = []; IrishSea = [] }
      { Row = 'J'; ColumnRanges = [ 2, 23 ];                      Ports = [ 19, British ]; IrishSea = [] }  // J19 Clyde
      { Row = 'K'; ColumnRanges = [ 3, 23 ];                      Ports = []; IrishSea = [] }
      { Row = 'L'; ColumnRanges = [ 3, 17; 20, 20; 23, 23 ];      Ports = []; IrishSea = [ 20 ] }           // 18/19/21/22 = Eire/GB land
      { Row = 'M'; ColumnRanges = [ 4, 18; 20, 20 ];              Ports = []; IrishSea = [ 20 ] }
      { Row = 'N'; ColumnRanges = [ 4, 18; 20, 21 ];              Ports = []; IrishSea = [ 20 ] }
      { Row = 'O'; ColumnRanges = [ 5, 22 ];                      Ports = [ 22, British ]; IrishSea = [] }  // O22 Plymouth
      { Row = 'P'; ColumnRanges = [ 5, 23 ];                      Ports = [ 23, German ]; IrishSea = [] }   // P23 Brest (German-held 1941)
      { Row = 'Q'; ColumnRanges = [ 6, 25 ];                      Ports = [ 25, German ]; IrishSea = [] }   // Q25 St Nazaire
      { Row = 'R'; ColumnRanges = [ 6, 26 ];                      Ports = [ 26, German ]; IrishSea = [] }   // R26 Bordeaux
      { Row = 'S'; ColumnRanges = [ 7, 26 ];                      Ports = []; IrishSea = [] }
      { Row = 'T'; ColumnRanges = [ 7, 26 ];                      Ports = [ 24, German ]; IrishSea = [] }   // T24 Ferrol (neutral Spain, interned)
      { Row = 'U'; ColumnRanges = [ 8, 23 ];                      Ports = []; IrishSea = [] }
      { Row = 'V'; ColumnRanges = [ 8, 24 ];                      Ports = []; IrishSea = [] }
      { Row = 'W'; ColumnRanges = [ 9, 24 ];                      Ports = []; IrishSea = [] }
      { Row = 'X'; ColumnRanges = [ 9, 25 ];                      Ports = []; IrishSea = [] }               // Azores
      { Row = 'Y'; ColumnRanges = [ 10, 26 ];                     Ports = []; IrishSea = [] }
      { Row = 'Z'; ColumnRanges = [ 10, 28; 29, 29 ];             Ports = [ 29, British ]; IrishSea = [] } ] // Z29 Gibraltar

/// <summary>
/// Zones printed with a white dot on the physical board — the reference
/// line the Chance Table's General Search column keys against. Read
/// directly off a sharp, straight-on photo of the board: a diagonal run
/// from E3 down to V22 (one row + one column per step, with an extra
/// three-zone cluster at K9/K10/K11 where the line visibly pauses before
/// continuing the diagonal), then flattening to a vertical run at column
/// 22 from W22 through Z22.
/// </summary>
let private whiteDotZones : Set<GridCoordinate> =
    [ 'E', 3; 'F', 4; 'G', 5; 'H', 6; 'I', 7; 'J', 8
      'K', 9; 'K', 10; 'K', 11
      'L', 12; 'M', 13; 'N', 14; 'O', 15; 'P', 16; 'Q', 17
      'R', 18; 'S', 19; 'T', 20; 'U', 21; 'V', 22
      'W', 22; 'X', 22; 'Y', 22; 'Z', 22 ]
    |> List.map (fun (l, n) -> { Letter = l; Number = n })
    |> Set.ofList

/// <summary>
/// Expands the compact RowSpec table into a full SearchBoardMap.
/// </summary>
let private buildBoard () : SearchBoardMap =
    let zones =
        rowSpecs
        |> List.collect (fun row ->
            row.ColumnRanges
            |> List.collect (fun (lo, hi) -> [ lo .. hi ])
            |> List.map (fun col ->
                let coord = { Letter = row.Row; Number = col }
                let terrain =
                    match row.Ports |> List.tryFind (fun (c, _) -> c = col) with
                    | Some (_, nat) -> Port nat
                    | None when row.IrishSea |> List.contains col -> IrishSea
                    | None -> OpenSea
                coord,
                { Coordinate = Some coord
                  Terrain = terrain
                  // The board prints "55°" markers at the K/L row boundary
                  // (visible either side of the grid), not at row E as an
                  // earlier pass of this file guessed. Rule 11.11-11.13's
                  // exact patrol-line definition still needs the manual's
                  // Chance Table section re-read against this — row >= 'L'
                  // (south of the 55° line) is this module's best current
                  // approximation, not a confirmed rule citation.
                  IsOnBritishPatrolLine = row.Row >= 'L'
                  IsWhiteDot = whiteDotZones.Contains coord }))
        |> Map.ofList
    { Zones = zones }

/// <summary>
/// The Search Board for the historical 1941 scenario.
/// </summary>
let searchBoard = buildBoard ()

/// <summary>
/// Convoy-route proxy for Chance Table convoy results (rows 10-12): this
/// uses the scenario's patrol-line annotation sorted in reading order as
/// a best-effort stand-in until a dedicated printed convoy route
/// transcription is added.
/// </summary>
let private convoyRoutePath : GridCoordinate list =
    searchBoard.Zones
    |> Map.toSeq
    |> Seq.choose (fun (coord, zone) -> if zone.IsOnBritishPatrolLine then Some coord else None)
    |> Seq.sortBy (fun c -> c.Letter, c.Number)
    |> Seq.toList

let private convoyRouteZones : Set<GridCoordinate> = convoyRoutePath |> Set.ofList

/// <summary>
/// Five independent convoy counters for the Basic Game's 12.44 scoring
/// track; each starts at a distinct point on the route approximation.
/// </summary>
let private initialConvoyRouteIndices : int list = [ 0; 1; 2; 3; 4 ]

open BismarckGame.Core.Units
open BismarckGame.Core.Tables
open BismarckGame.Core.Tables.ShipStats

let private zone letter number : GridCoordinate = { Letter = letter; Number = number }

/// <summary>
/// Evasion rating, search strength, and max speed all come from the
/// physical counter photos now (Tables/ShipStats.fs). Fuel factors are
/// the one remaining TODO — not printed on any card/counter photographed
/// so far.
/// </summary>
let private ship id name nat cls startZone =
    let stats = shipStats.TryFind name
    let evasion = stats |> Option.map (fun s -> s.EvasionRating) |> Option.defaultValue 0
    let searchDay = stats |> Option.map (fun s -> s.SearchStrengthDay) |> Option.defaultValue 0
    let searchNight = stats |> Option.map (fun s -> s.SearchStrengthNight) |> Option.defaultValue 0
    let maxSpeed = stats |> Option.map (fun s -> s.MaxSpeedZones) |> Option.defaultValue 0
    let maxMidships = stats |> Option.map (fun s -> s.MaxMidshipsHits) |> Option.defaultValue 0
    let fuel =
        stats
        |> Option.bind (fun s -> s.FuelFactors)
        |> Option.map (fun f -> { FactorsRemaining = f; InEmergencyMovement = false })
    { Id = ShipId id
      Name = name
      Nationality = nat
      Class = cls
      EvasionRating = evasion
      MaxEvasionRating = evasion
      MaxSpeedZones = maxSpeed
      SearchStrength = { Day = searchDay; Night = searchNight }
      CanPatrol = (cls <> AircraftCarrier)
      Mode = Movement
      CurrentZone = Some startZone
      Fuel = fuel
      TaskForce = None
      IsConvoyEscort = false
      ZonesMovedThisTurn = 0
      MidshipsHits = 0
      MaxMidshipsHits = maxMidships
      PermanentEvasionLoss = 0
      IsLockedInPort = false
      IsRestrictedToPatrolUntilContact = false
      IsSunk = false }

let private air id name nat utype home startZone =
    let stats =
        AirUnitStats.stats.TryFind(nat, utype)
        |> Option.defaultValue { AirUnitStats.EnduranceRating = 0; AirUnitStats.MaxSpeedZones = 0; AirUnitStats.SearchStrengthDay = 0; AirUnitStats.SearchStrengthNight = 0 }
    { Id = AirUnitId id
      Name = name
      Nationality = nat
      UnitType = utype
      Mode = (match utype with LongRangeRecon -> ReconMovement | _ -> BomberReconnaissance)
      SearchStrength = { Day = stats.SearchStrengthDay; Night = stats.SearchStrengthNight }
      EnduranceRating = stats.EnduranceRating
      TurnsAirborne = 0
      AirAttacksLaunchedThisTurn = 0
      MaxSpeedZones = stats.MaxSpeedZones
      HomeBase = home
      CurrentZone = Some startZone
      IsAtBase = true }

/// <summary>
/// Order of Battle — German Basic Player Aid Card. Note 1 on that card:
/// on turn 1 only, Bismarck and Prinz Eugen get a breakout-move bonus (5
/// zones at 2 fuel factors, 4 zones at 1, or ≤3 zones free); this is a
/// one-time movement rule, not part of the static roster, and belongs in
/// Update.fs's MoveShip handling once fuel data is transcribed.
/// </summary>
let private germanOOB : OrderOfBattle =
    { Nationality = German
      Ships =
        [ ship "GER-BB-Bismarck" "Bismarck" German Battleship (zone 'F' 20)
          ship "GER-CA-PrinzEugen" "Prinz Eugen" German HeavyCruiser (zone 'F' 20) ]
      AirUnits =
        [ air "GER-AIR-LRRecon-Trondheim" "LR Recon" German LongRangeRecon (LandBase "Trondheim") (zone 'C' 19)
          air "GER-AIR-Bomber-Trondheim" "Level Bomber" German LevelBomber (LandBase "Trondheim") (zone 'C' 19)
          air "GER-AIR-Bomber-Bergen" "Level Bomber" German LevelBomber (LandBase "Bergen") (zone 'F' 20)
          air "GER-AIR-Bomber-Brest" "Level Bomber" German LevelBomber (LandBase "Brest") (zone 'P' 23)
          air "GER-AIR-Bomber-StNazaire" "Level Bomber" German LevelBomber (LandBase "St. Nazaire") (zone 'Q' 25)
          air "GER-AIR-LRRecon-Bordeaux" "LR Recon" German LongRangeRecon (LandBase "Bordeaux Air Base") (zone 'R' 26)
          air "GER-AIR-Bomber-Bordeaux" "Level Bomber" German LevelBomber (LandBase "Bordeaux Air Base") (zone 'R' 26) ] }

/// <summary>
/// Order of Battle — British Basic Player Aid Card. Notes 7-12 on that
/// card encode release conditions (e.g. Force H can't sortie until the
/// 4th turn after Bismarck is located) that are scenario-level triggers,
/// not static roster data — they belong in Update.fs as additional
/// MoveShip preconditions once wired up, not modeled as types here yet.
/// </summary>
let private britishOOB : OrderOfBattle =
    { Nationality = British
      Ships =
        [ ship "GBR-CA-Norfolk" "Norfolk" British HeavyCruiser (zone 'B' 7)
          ship "GBR-CA-Suffolk" "Suffolk" British HeavyCruiser (zone 'D' 9)
          ship "GBR-CL-Arethusa" "Arethusa" British LightCruiser (zone 'F' 12)
          ship "GBR-CL-Manchester" "Manchester" British LightCruiser (zone 'E' 13)
          ship "GBR-CL-Birmingham" "Birmingham" British LightCruiser (zone 'F' 14)
          ship "GBR-BC-Hood" "Hood" British Battlecruiser (zone 'G' 15)
          ship "GBR-BB-PrinceOfWales" "Prince of Wales" British Battleship (zone 'G' 15)
          ship "GBR-BB-KingGeorgeV" "King George V" British Battleship (zone 'H' 18)
          ship "GBR-CV-Victorious" "Victorious" British AircraftCarrier (zone 'H' 18)
          ship "GBR-CL-Kenya" "Kenya" British LightCruiser (zone 'H' 18)
          ship "GBR-CL-Galatea" "Galatea" British LightCruiser (zone 'H' 18)
          ship "GBR-CL-Hermione" "Hermione" British LightCruiser (zone 'H' 18)
          ship "GBR-CL-Aurora" "Aurora" British LightCruiser (zone 'H' 18)
          ship "GBR-BC-Repulse" "Repulse" British Battlecruiser (zone 'J' 19)
          ship "GBR-BB-Rodney" "Rodney" British Battleship (zone 'K' 18)
          ship "GBR-CL-Sheffield" "Sheffield" British LightCruiser (zone 'Z' 29)
          ship "GBR-CV-ArkRoyal" "Ark Royal" British AircraftCarrier (zone 'Z' 29)
          ship "GBR-BC-Renown" "Renown" British Battlecruiser (zone 'Z' 29)
          ship "GBR-CL-Edinburgh" "Edinburgh" British LightCruiser (zone 'T' 17)
          ship "GBR-BB-Ramillies" "Ramillies" British Battleship (zone 'S' 7)
          // Reinforcements (rule-driven entry timing, not turn-1 placement):
          // Revenge enters secretly, turn 1600/May24 or later, at L3 (note 13).
          // Dorsetshire enters 1600/May25 at Z20 (note 14).
          ship "GBR-BB-Revenge" "Revenge" British Battleship (zone 'L' 3)
          ship "GBR-CA-Dorsetshire" "Dorsetshire" British HeavyCruiser (zone 'Z' 20) ]
        |> List.map (fun s ->
            let lockedInPortIds =
                set [ "GBR-BB-KingGeorgeV"; "GBR-CV-Victorious"; "GBR-CL-Kenya"; "GBR-CL-Galatea"
                      "GBR-CL-Hermione"; "GBR-CL-Aurora"        // note 7: Scapa Flow task force
                      "GBR-BC-Repulse"                          // note 8: Clyde
                      "GBR-CL-Sheffield"; "GBR-CV-ArkRoyal"; "GBR-BC-Renown" ]   // note 10: Force H, Gibraltar
            let (ShipId idStr) = s.Id
            { s with
                IsConvoyEscort = (s.Id = ShipId "GBR-BB-Rodney" || s.Id = ShipId "GBR-BB-Ramillies")   // notes 9/12
                IsLockedInPort = lockedInPortIds.Contains idStr
                IsRestrictedToPatrolUntilContact = (s.Id = ShipId "GBR-CL-Edinburgh") })   // note 11
        |> List.map (fun s ->
            if s.Id = ShipId "GBR-BB-Revenge" || s.Id = ShipId "GBR-CA-Dorsetshire" then
                { s with CurrentZone = None }   // placed later — see reinforcements below
            else s)
      AirUnits =
        [ air "GBR-AIR-LRRecon-Hvalfiord" "LR Recon" British LongRangeRecon (LandBase "Hvalfiord") (zone 'D' 9)
          air "GBR-AIR-LRRecon1-ScapaFlow" "LR Recon" British LongRangeRecon (LandBase "Scapa Flow") (zone 'H' 18)
          air "GBR-AIR-LRRecon2-ScapaFlow" "LR Recon" British LongRangeRecon (LandBase "Scapa Flow") (zone 'H' 18)
          air "GBR-AIR-Bomber-ScapaFlow" "Level Bomber" British LevelBomber (LandBase "Scapa Flow") (zone 'H' 18)
          air "GBR-AIR-LRRecon1-Plymouth" "LR Recon" British LongRangeRecon (LandBase "Plymouth") (zone 'O' 22)
          air "GBR-AIR-LRRecon2-Plymouth" "LR Recon" British LongRangeRecon (LandBase "Plymouth") (zone 'O' 22)
          air "GBR-AIR-Bomber-Plymouth" "Level Bomber" British LevelBomber (LandBase "Plymouth") (zone 'O' 22)
          air "GBR-AIR-LRRecon1-Eire" "LR Recon" British LongRangeRecon (LandBase "Eire Airbase") (zone 'L' 20)
          air "GBR-AIR-LRRecon2-Eire" "LR Recon" British LongRangeRecon (LandBase "Eire Airbase") (zone 'L' 20)
          air "GBR-AIR-Bomber-Eire" "Level Bomber" British LevelBomber (LandBase "Eire Airbase") (zone 'L' 20)
          air "GBR-AIR-LRRecon-Gibraltar" "LR Recon" British LongRangeRecon (LandBase "Gibraltar") (zone 'Z' 29)
          air "GBR-AIR-Torpedo-Victorious" "Torpedo Bomber" British TorpedoBomber (CarrierBase(ShipId "GBR-CV-Victorious")) (zone 'H' 18)
          air "GBR-AIR-Torpedo1-ArkRoyal" "Torpedo Bomber" British TorpedoBomber (CarrierBase(ShipId "GBR-CV-ArkRoyal")) (zone 'Z' 29)
          air "GBR-AIR-Torpedo2-ArkRoyal" "Torpedo Bomber" British TorpedoBomber (CarrierBase(ShipId "GBR-CV-ArkRoyal")) (zone 'Z' 29)
          air "GBR-AIR-Torpedo3-ArkRoyal" "Torpedo Bomber" British TorpedoBomber (CarrierBase(ShipId "GBR-CV-ArkRoyal")) (zone 'Z' 29) ] }

let private ordersOfBattle : OrderOfBattle list = [ germanOOB; britishOOB ]

let scenario : ScenarioDefinition =
    { Id = "bismarck-1941-basic"
      Name = "Bismarck (1941) — Basic Game"
      Description = "The historical hunt for the Bismarck, May 1941. Basic Game rules only."
      SearchBoard = searchBoard
      OrdersOfBattle = ordersOfBattle
      // Rule 11.23 / the physical Time Record Track: play starts at the
      // card's printed turn 4 ("1200, Start"), not turn 1 — turns 1-3 are
      // unused reference cells before the scenario's actual start. Turn 4
      // is a 'C' (emergency-movement) turn per the card but daytime
      // (1200) — see Tables/TimeAndVisibility.fs's timeRecordTrack, entry
      // for turn 4, which independently derives the same two flags.
      FirstTurn = { Number = 4; IsNightTurn = false; IsEmergencyMovementTurn = true; Visibility = VisibilityLevel 4 }  // rule 7.15: turn 1 of play visibility is always level 4
      TurnLengthHours = 4
      DamagePoints = basicGameDamagePoints
      ConvoyRouteZones = convoyRouteZones
      ConvoyRoutePath = convoyRoutePath
      InitialConvoyRouteIndices = initialConvoyRouteIndices
      ConvoyCount = 5   // rule 12.44 scores the 1st..5th convoy explicitly.
      // British card: "13. 1600 May24 — Revenge — L3" and "14. 1600 May25
      // — Dorsetshire — Z20". Cross-referenced against the Time Record
      // Track (Tables/TimeAndVisibility.fs): 1600/May24 is turn 17,
      // 1600/May25 is turn 23. Note 13 also says the British player
      // secretly chooses when Revenge actually enters (turn 17 or later)
      // — not modeled; Revenge is placed exactly on turn 17 here.
      PendingReinforcements =
        [ 17, ShipId "GBR-BB-Revenge", zone 'L' 3
          23, ShipId "GBR-CA-Dorsetshire", zone 'Z' 20 ] }
