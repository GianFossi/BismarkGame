module BismarckGame.Tests.TestHelpers

open BismarckGame.Core.Common
open BismarckGame.Core.SearchBoard
open BismarckGame.Core.Markers
open BismarckGame.Core.Units
open BismarckGame.Core.GameState
open BismarckGame.Core.Dice
open BismarckGame.Core.Update

/// <summary>
/// A small 3x3 synthetic board (A1..C3), independent of the real 1941
/// scenario so engine tests aren't coupled to the full historical roster
/// and can reason about adjacency by hand. A1 is a German port, C3 a
/// British port, B2 is Irish Sea (blocked to German ships, rule 5.18).
/// </summary>
let testBoard () : SearchBoardMap =
    let mk letter number terrain =
        let coord = { Letter = letter; Number = number }
        coord, { Coordinate = Some coord; Terrain = terrain; IsOnBritishPatrolLine = false; IsWhiteDot = false }
    [ mk 'A' 1 (Port German)
      mk 'A' 2 OpenSea
      mk 'A' 3 OpenSea
      mk 'B' 1 OpenSea
      mk 'B' 2 IrishSea
      mk 'B' 3 OpenSea
      mk 'C' 1 OpenSea
      mk 'C' 2 OpenSea
      mk 'C' 3 (Port British) ]
    |> Map.ofList
    |> fun zones -> { Zones = zones }

let coord letter number : GridCoordinate = { Letter = letter; Number = number }

/// <summary>
/// Builds a ship with reasonable defaults; override individual fields
/// with `{ testShip ... with Field = value }` in a test as needed.
/// </summary>
let testShip (id: string) (name: string) (nat: Nationality) (cls: ShipClass) (zone: GridCoordinate) : ShipCounter =
    { Id = ShipId id
      Name = name
      Nationality = nat
      Class = cls
      EvasionRating = 29
      MaxEvasionRating = 29
      MaxSpeedZones = 2
      SearchStrength = { Day = 1; Night = 1 }
      CanPatrol = (cls <> AircraftCarrier)
      Mode = Movement
      CurrentZone = Some zone
      Fuel = None
      TaskForce = None
      IsConvoyEscort = false
      ZonesMovedThisTurn = 0
      MidshipsHits = 0
      MaxMidshipsHits = 10
      PermanentEvasionLoss = 0
      IsLockedInPort = false
      IsRestrictedToPatrolUntilContact = false
      IsSunk = false }

let testAirUnit (id: string) (name: string) (nat: Nationality) (utype: AirUnitType) (zone: GridCoordinate) : AirUnitCounter =
    { Id = AirUnitId id
      Name = name
      Nationality = nat
      UnitType = utype
      Mode = (match utype with LongRangeRecon -> ReconMovement | _ -> BomberAttack)
      SearchStrength = { Day = 5; Night = 6 }
      EnduranceRating = 2
      TurnsAirborne = 0
      MaxSpeedZones = 1
      HomeBase = LandBase "TestBase"
      CurrentZone = Some zone
      IsAtBase = true }

/// <summary>
/// A minimal two-side GameState over `testBoard()`, with one German ship
/// at A1 and one British ship at C3, positioned at the start of the Ship
/// Movement phase (the phase most movement/fuel tests care about).
/// </summary>
let testState () : GameState =
    let board = testBoard ()
    let german = testShip "GER-1" "TestBismarck" German Battleship (coord 'A' 1)
    let british = testShip "GBR-1" "TestCruiser" British HeavyCruiser (coord 'C' 3)
    { Turn = { Number = 4; IsNightTurn = false; IsEmergencyMovementTurn = true; Visibility = VisibilityLevel 4 }
      Phase = ShipMovement
      SearchBoard = board
      ConvoyRouteZones = Set.ofList [ coord 'C' 3 ]
      ConvoyRoutePath = [ coord 'A' 1; coord 'A' 2; coord 'A' 3; coord 'B' 3; coord 'C' 3 ]
      ConvoyUnits = [ { Id = 1; Zone = coord 'C' 3; RouteIndex = 4; Direction = East; IsSunk = false } ]
      ConvoyContacts = []
      ConvoysAvailable = 5
      ConvoysSunkByGerman = 0
      Players =
        [ German,
          { Nationality = German
            Ships = Map.ofList [ german.Id, german ]
            AirUnits = Map.empty
            TaskForces = Map.empty
            ConvoyEscorts = []
            Score = { Nationality = German; Points = 0; Events = [] } }
          British,
          { Nationality = British
            Ships = Map.ofList [ british.Id, british ]
            AirUnits = Map.empty
            TaskForces = Map.empty
            ConvoyEscorts = []
            Score = { Nationality = British; Points = 0; Events = [] } } ]
        |> Map.ofList
      ShadowMarkers = []
      LocationMarkers = []
      ActiveBattles = []
      GermanLocatedTurn = None
      PendingReinforcements = []
      GameEnded = None }

/// <summary>
/// A dummy IRulesTables that never needs to resolve anything for tests
/// that don't touch combat/shadow — raises loudly if actually called, so
/// a test that unexpectedly hits it fails clearly instead of silently
/// returning a made-up result.
/// </summary>
let unusedTables : IRulesTables =
    { new IRulesTables with
        member _.ResolveShadow(_, _, _, _) = failwith "unusedTables.ResolveShadow was called — this test needs a real table or a fixed dice sequence"
        member _.ResolveNavalFire(_, _) = failwith "unusedTables.ResolveNavalFire was called — this test needs a real table or a fixed dice sequence" }

/// <summary>
/// A roll function that always returns the same value — for tests that
/// need `roll` to exist but don't care what it returns.
/// </summary>
let constantRoll (n: int) : unit -> int = fun () -> n
