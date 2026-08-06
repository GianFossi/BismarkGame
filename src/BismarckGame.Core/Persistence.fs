module BismarckGame.Core.Persistence

open System
open System.IO
open System.Text
open System.Xml
open System.Xml.Serialization
open BismarckGame.Core.Common
open BismarckGame.Core.SearchBoard
open BismarckGame.Core.Markers
open BismarckGame.Core.Units
open BismarckGame.Core.BattleBoard
open BismarckGame.Core.GameState
open BismarckGame.Core.VictoryConditions
open BismarckGame.Core.Configuration
open BismarckGame.Core.Scenario

[<CLIMutable>]
type ScoreEventDto =
    { Label: string
      Points: int }

[<CLIMutable>]
type FuelDto =
    { HasFuel: bool
      FactorsRemaining: int
      InEmergencyMovement: bool }

[<CLIMutable>]
type ShipStatusDto =
    { Id: string
      Nationality: string
      CurrentZone: string
      Mode: string
      EvasionRating: int
      MidshipsHits: int
      PermanentEvasionLoss: int
      ZonesMovedThisTurn: int
      IsSunk: bool
      IsConvoyEscort: bool
      IsLockedInPort: bool
      IsRestrictedToPatrolUntilContact: bool
      Fuel: FuelDto }

[<CLIMutable>]
type AirStatusDto =
    { Id: string
      Nationality: string
      CurrentZone: string
      Mode: string
      TurnsAirborne: int
      AirAttacksLaunchedThisTurn: int
      IsAtBase: bool
      IsSunkLikeMissing: bool }

[<CLIMutable>]
type ConvoyContactDto =
    { Zone: string
      ConvoyId: int
      Discoverer: string
      Source: string
      TurnLocated: int }

[<CLIMutable>]
type ConvoyUnitDto =
    { Id: int
      Zone: string
      RouteIndex: int
      Direction: string
      IsSunk: bool }

[<CLIMutable>]
type LocationMarkerDto =
    { Zone: string
      HasRevealedShipClass: bool
      RevealedShipClass: string
      Owner: string }

[<CLIMutable>]
type ShadowMarkerDto =
    { Zone: string
      ShadowingUnitId: string
      ShadowedUnitId: string }

[<CLIMutable>]
type BattleGunSectionDto =
    { Section: string
      MaxSalvo: int
      SalvoRemaining: int
      CanFireBothRanges: bool }

[<CLIMutable>]
type BattleShipDto =
    { ShipId: string
      Name: string
      Class: string
      Q: int
      R: int
      S: int
      Facing: string
      SecondaryHits: int
      EvasionRating: int
      MidshipsHits: int
      MaxMidshipsHits: int
      PermanentEvasionLoss: int
      IsWithdrawing: bool
      IsSunk: bool
      GunSections: BattleGunSectionDto array }

[<CLIMutable>]
type BattleStatusDto =
    { Id: int
      Round: int
      Ships: BattleShipDto array }

[<CLIMutable>]
type PlayerScoreDto =
    { Nationality: string
      Points: int
      Events: ScoreEventDto array }

[<CLIMutable>]
type SearchZoneDto = { Key: string; HasCoordinate: bool; Coordinate: string; TerrainKind: string; TerrainOwner: string; IsOnBritishPatrolLine: bool; IsWhiteDot: bool }

[<CLIMutable>]
type SearchBoardMapDto = { Zones: SearchZoneDto array }

[<CLIMutable>]
type GameStatusDto =
    { TurnNumber: int
      IsNightTurn: bool
      IsEmergencyMovementTurn: bool
      Visibility: int
      Phase: string
      GermanLocatedTurn: int
      ConvoysAvailable: int
      ConvoysSunkByGerman: int
      ConvoyContacts: ConvoyContactDto array
      ConvoyUnits: ConvoyUnitDto array
      ShadowMarkers: ShadowMarkerDto array
      LocationMarkers: LocationMarkerDto array
      ActiveBattles: BattleStatusDto array
      Scores: PlayerScoreDto array
      Ships: ShipStatusDto array
      AirUnits: AirStatusDto array
      GameEnded: string }

let private serializer<'T> = XmlSerializer(typeof<'T>)

let private toEncoding (name: string) =
    try Encoding.GetEncoding(name)
    with _ -> Encoding.UTF8

let private xmlWriterSettings (opts: XmlPersistenceOptions) =
    XmlWriterSettings(Indent = opts.IndentOutput, OmitXmlDeclaration = opts.OmitXmlDeclaration, Encoding = toEncoding opts.EncodingName)

let writeXmlToStream (opts: XmlPersistenceOptions) (stream: Stream) (value: 'T) : unit =
    let s = serializer<'T>
    use writer = XmlWriter.Create(stream, xmlWriterSettings opts)
    s.Serialize(writer, value)

let readXmlFromStream<'T> (stream: Stream) : 'T =
    let s = serializer<'T>
    s.Deserialize(stream) :?> 'T

let writeXmlToFile (opts: XmlPersistenceOptions) (filePath: string) (value: 'T) : unit =
    let dir = Path.GetDirectoryName(filePath)
    if not (String.IsNullOrWhiteSpace(dir)) then
        Directory.CreateDirectory(dir) |> ignore
    use fs = File.Create(filePath)
    writeXmlToStream opts fs value

let readXmlFromFile<'T> (filePath: string) : 'T =
    use fs = File.OpenRead(filePath)
    readXmlFromStream<'T> fs

let saveGameOptionsToFile (opts: XmlPersistenceOptions) (filePath: string) (value: GameOptions) : unit =
    writeXmlToFile opts filePath value

let loadGameOptionsFromFile (filePath: string) : GameOptions =
    readXmlFromFile<GameOptions> filePath

let saveConfigurationToFile (opts: XmlPersistenceOptions) (filePath: string) (value: AppConfiguration) : unit =
    writeXmlToFile opts filePath value

let loadConfigurationFromFile (filePath: string) : AppConfiguration =
    readXmlFromFile<AppConfiguration> filePath

let private terrainToDto (terrain: TerrainFeature) : string * string =
    match terrain with
    | OpenSea -> "OpenSea", ""
    | IrishSea -> "IrishSea", ""
    | RestrictedEntry -> "RestrictedEntry", ""
    | BordeauxAirBase -> "BordeauxAirBase", ""
    | Port nat ->
        let owner = match nat with | British -> "British" | German -> "German"
        "Port", owner

let private terrainFromDto (kind: string) (owner: string) : TerrainFeature =
    match kind with
    | "Port" ->
        let nat = if String.Equals(owner, "German", StringComparison.OrdinalIgnoreCase) then German else British
        Port nat
    | "IrishSea" -> IrishSea
    | "RestrictedEntry" -> RestrictedEntry
    | "BordeauxAirBase" -> BordeauxAirBase
    | _ -> OpenSea

let toSearchBoardMapDto (map: SearchBoardMap) : SearchBoardMapDto =
    let zones =
        map.Zones
        |> Map.toArray
        |> Array.map (fun (key, zone) ->
            let terrainKind, terrainOwner = terrainToDto zone.Terrain
            { Key = key.ToString()
              HasCoordinate = zone.Coordinate.IsSome
              Coordinate = zone.Coordinate |> Option.map (fun c -> c.ToString()) |> Option.defaultValue ""
              TerrainKind = terrainKind
              TerrainOwner = terrainOwner
              IsOnBritishPatrolLine = zone.IsOnBritishPatrolLine
              IsWhiteDot = zone.IsWhiteDot })
    { Zones = zones }

let fromSearchBoardMapDto (dto: SearchBoardMapDto) : SearchBoardMap =
    let parseCoordinate (text: string) : GridCoordinate option =
        if String.IsNullOrWhiteSpace(text) then
            None
        else
            let t = text.Trim().ToUpperInvariant()
            if t.Length < 2 then
                None
            else
                let letter = t.[0]
                let mutable n = 0
                if Int32.TryParse(t.Substring(1), &n) then
                    Some { Letter = letter; Number = n }
                else
                    None

    let zones =
        dto.Zones
        |> Array.toList
        |> List.choose (fun z ->
            parseCoordinate z.Key
            |> Option.map (fun key ->
                let coord =
                    if z.HasCoordinate then parseCoordinate z.Coordinate
                    else None
                let zone : Zone =
                    { Coordinate = coord
                      Terrain = terrainFromDto z.TerrainKind z.TerrainOwner
                      IsOnBritishPatrolLine = z.IsOnBritishPatrolLine
                      IsWhiteDot = z.IsWhiteDot }
                key, zone))
        |> Map.ofList
    { Zones = zones }

let saveSearchMapToFile (opts: XmlPersistenceOptions) (filePath: string) (map: SearchBoardMap) : unit =
    let dto = toSearchBoardMapDto map
    writeXmlToFile opts filePath dto

let loadSearchMapFromFile (filePath: string) : SearchBoardMap =
    let dto = readXmlFromFile<SearchBoardMapDto> filePath
    fromSearchBoardMapDto dto

let private zoneToString (z: GridCoordinate option) =
    match z with
    | Some c -> c.ToString()
    | None -> ""

let private parseZone (text: string) : GridCoordinate option =
    if String.IsNullOrWhiteSpace(text) then
        None
    else
        let t = text.Trim().ToUpperInvariant()
        if t.Length < 2 then
            None
        else
            let letter = t.[0]
            let mutable n = 0
            if Int32.TryParse(t.Substring(1), &n) then
                Some { Letter = letter; Number = n }
            else
                None

let private nationalityToString = function | British -> "British" | German -> "German"
let private parseNationality (text: string) = if String.Equals(text, "German", StringComparison.OrdinalIgnoreCase) then German else British

let private shipModeToString = function | Movement -> "Movement" | Patrol -> "Patrol"
let private parseShipMode (text: string) = if String.Equals(text, "Patrol", StringComparison.OrdinalIgnoreCase) then Patrol else Movement

let private airModeToString = function
    | BomberAttack -> "BomberAttack"
    | BomberReconnaissance -> "BomberReconnaissance"
    | ReconMovement -> "ReconMovement"
    | ReconPatrol -> "ReconPatrol"

let private parseAirMode (text: string) =
    match text with
    | "BomberAttack" -> BomberAttack
    | "BomberReconnaissance" -> BomberReconnaissance
    | "ReconPatrol" -> ReconPatrol
    | _ -> ReconMovement

let private headingToString = function
    | North -> "North" | NorthEast -> "NorthEast" | East -> "East" | SouthEast -> "SouthEast"
    | South -> "South" | SouthWest -> "SouthWest" | West -> "West" | NorthWest -> "NorthWest"

let private parseHeading (text: string) =
    match text with
    | "North" -> North
    | "NorthEast" -> NorthEast
    | "SouthEast" -> SouthEast
    | "South" -> South
    | "SouthWest" -> SouthWest
    | "West" -> West
    | "NorthWest" -> NorthWest
    | _ -> East

let private shipClassToString = function
    | Battleship -> "Battleship"
    | Battlecruiser -> "Battlecruiser"
    | PocketBattleship -> "PocketBattleship"
    | HeavyCruiser -> "HeavyCruiser"
    | LightCruiser -> "LightCruiser"
    | AircraftCarrier -> "AircraftCarrier"

let private parseShipClass (text: string) =
    match text with
    | "Battlecruiser" -> Battlecruiser
    | "PocketBattleship" -> PocketBattleship
    | "HeavyCruiser" -> HeavyCruiser
    | "LightCruiser" -> LightCruiser
    | "AircraftCarrier" -> AircraftCarrier
    | _ -> Battleship

let private phaseToString = function
    | UnitAvailability -> "UnitAvailability"
    | Visibility -> "Visibility"
    | ShadowDetermination -> "ShadowDetermination"
    | AirMovement -> "AirMovement"
    | ShipMovement -> "ShipMovement"
    | Search -> "Search"
    | AirAttack -> "AirAttack"
    | NavalCombat -> "NavalCombat"
    | Chance -> "Chance"

let private parsePhase (text: string) =
    match text with
    | "Visibility" -> Visibility
    | "ShadowDetermination" -> ShadowDetermination
    | "AirMovement" -> AirMovement
    | "ShipMovement" -> ShipMovement
    | "Search" -> Search
    | "AirAttack" -> AirAttack
    | "NavalCombat" -> NavalCombat
    | "Chance" -> Chance
    | _ -> UnitAvailability

let private gameEndToString = function
    | Some BismarckSunk -> "BismarckSunk"
    | Some BismarckReturnsToPort -> "BismarckReturnsToPort"
    | Some TimeRunsOut -> "TimeRunsOut"
    | None -> ""

let private parseGameEnd (text: string) =
    match text with
    | "BismarckSunk" -> Some BismarckSunk
    | "BismarckReturnsToPort" -> Some BismarckReturnsToPort
    | "TimeRunsOut" -> Some TimeRunsOut
    | _ -> None

let private gunSectionToString = function
    | BowGuns -> "BowGuns"
    | SternGuns -> "SternGuns"
    | PortGuns -> "PortGuns"
    | StarboardGuns -> "StarboardGuns"

let private parseGunSection (text: string) =
    match text with
    | "SternGuns" -> SternGuns
    | "PortGuns" -> PortGuns
    | "StarboardGuns" -> StarboardGuns
    | _ -> BowGuns

let private hexSideToString = function
    | HexN -> "HexN" | HexNE -> "HexNE" | HexSE -> "HexSE"
    | HexS -> "HexS" | HexSW -> "HexSW" | HexNW -> "HexNW"

let private parseHexSide (text: string) =
    match text with
    | "HexNE" -> HexNE
    | "HexSE" -> HexSE
    | "HexS" -> HexS
    | "HexSW" -> HexSW
    | "HexNW" -> HexNW
    | _ -> HexN

let private contactSourceToString = function
    | ChanceOnRoute -> "ChanceOnRoute"
    | ChanceNearRoute -> "ChanceNearRoute"
    | ChanceAdjacentToRoute -> "ChanceAdjacentToRoute"

let private parseContactSource (text: string) =
    match text with
    | "ChanceNearRoute" -> ChanceNearRoute
    | "ChanceAdjacentToRoute" -> ChanceAdjacentToRoute
    | _ -> ChanceOnRoute

let private scoreEventsToDto (events: (string * int) list) =
    events |> List.map (fun (l, p) -> { Label = l; Points = p }) |> List.toArray

let private scoreEventsFromDto (events: ScoreEventDto array) =
    events |> Array.toList |> List.map (fun e -> e.Label, e.Points)

let captureGameStatus (state: GameState) : GameStatusDto =
    let ships =
        state.Players
        |> Map.toList
        |> List.collect (fun (_, p) ->
            p.Ships
            |> Map.toList
            |> List.map (fun (_, s) ->
                let fuel : FuelDto =
                    match s.Fuel with
                    | Some f -> { HasFuel = true; FactorsRemaining = f.FactorsRemaining; InEmergencyMovement = f.InEmergencyMovement }
                    | None -> { HasFuel = false; FactorsRemaining = 0; InEmergencyMovement = false }
                ({ Id = let (ShipId v) = s.Id in v
                   Nationality = nationalityToString s.Nationality
                   CurrentZone = zoneToString s.CurrentZone
                   Mode = shipModeToString s.Mode
                   EvasionRating = s.EvasionRating
                   MidshipsHits = s.MidshipsHits
                   PermanentEvasionLoss = s.PermanentEvasionLoss
                   ZonesMovedThisTurn = s.ZonesMovedThisTurn
                   IsSunk = s.IsSunk
                   IsConvoyEscort = s.IsConvoyEscort
                   IsLockedInPort = s.IsLockedInPort
                   IsRestrictedToPatrolUntilContact = s.IsRestrictedToPatrolUntilContact
                   Fuel = fuel } : ShipStatusDto)))
        |> List.toArray

    let airUnits =
        state.Players
        |> Map.toList
        |> List.collect (fun (_, p) ->
            p.AirUnits
            |> Map.toList
            |> List.map (fun (_, a) ->
                let id = let (AirUnitId v) = a.Id in v
                let dto : AirStatusDto =
                    { Id = id
                      Nationality = nationalityToString a.Nationality
                      CurrentZone = zoneToString a.CurrentZone
                      Mode = airModeToString a.Mode
                      TurnsAirborne = a.TurnsAirborne
                      AirAttacksLaunchedThisTurn = a.AirAttacksLaunchedThisTurn
                      IsAtBase = a.IsAtBase
                      IsSunkLikeMissing = false }
                dto))
        |> List.toArray

    let contacts =
        state.ConvoyContacts
        |> List.map (fun c ->
            let dto : ConvoyContactDto =
                { Zone = c.Zone.ToString()
                  ConvoyId = (c.ConvoyId |> Option.defaultValue 0)
                  Discoverer = nationalityToString c.Discoverer
                  Source = contactSourceToString c.Source
                  TurnLocated = c.TurnLocated }
            dto)
        |> List.toArray

    let convoys =
        state.ConvoyUnits
        |> List.map (fun c ->
            let dto : ConvoyUnitDto =
                { Id = c.Id
                  Zone = c.Zone.ToString()
                  RouteIndex = c.RouteIndex
                  Direction = headingToString c.Direction
                  IsSunk = c.IsSunk }
            dto)
        |> List.toArray

    let locationMarkers =
        state.LocationMarkers
        |> List.map (fun m ->
            let dto : LocationMarkerDto =
                { Zone = m.Zone.ToString()
                  HasRevealedShipClass = m.RevealedShipClass.IsSome
                  RevealedShipClass = m.RevealedShipClass |> Option.map shipClassToString |> Option.defaultValue ""
                  Owner = nationalityToString m.Owner }
            dto)
        |> List.toArray

    let shadowMarkers =
        state.ShadowMarkers
        |> List.map (fun m ->
            let (UnitId s1) = m.ShadowingUnit
            let (UnitId s2) = m.ShadowedUnit
            let dto : ShadowMarkerDto =
                { Zone = m.Zone.ToString(); ShadowingUnitId = s1; ShadowedUnitId = s2 }
            dto)
        |> List.toArray

    let battles =
        state.ActiveBattles
        |> List.map (fun b ->
            let ships =
                b.Ships
                |> Map.toList
                |> List.map (fun (_, s) ->
                    let sections =
                        s.GunSections
                        |> List.map (fun g ->
                            ({ Section = gunSectionToString g.Section
                               MaxSalvo = g.MaxSalvo
                               SalvoRemaining = g.SalvoRemaining
                               CanFireBothRanges = g.CanFireBothRanges } : BattleGunSectionDto))
                        |> List.toArray
                    ({ ShipId = let (ShipId v) = s.ShipId in v
                       Name = s.Name
                       Class = shipClassToString s.Class
                       Q = s.Position.Q
                       R = s.Position.R
                       S = s.Position.S
                       Facing = hexSideToString s.Facing
                       SecondaryHits = s.SecondaryHits
                       EvasionRating = s.EvasionRating
                       MidshipsHits = s.MidshipsHits
                       MaxMidshipsHits = s.MaxMidshipsHits
                       PermanentEvasionLoss = s.PermanentEvasionLoss
                       IsWithdrawing = s.IsWithdrawing
                       IsSunk = s.IsSunk
                       GunSections = sections } : BattleShipDto))
                |> List.toArray
            ({ Id = b.Id; Round = b.Round; Ships = ships } : BattleStatusDto))
        |> List.toArray

    let scores =
        state.Players
        |> Map.toList
        |> List.map (fun (_, p) ->
            ({ Nationality = nationalityToString p.Nationality
               Points = p.Score.Points
               Events = scoreEventsToDto p.Score.Events } : PlayerScoreDto))
        |> List.toArray

    { TurnNumber = state.Turn.Number
      IsNightTurn = state.Turn.IsNightTurn
      IsEmergencyMovementTurn = state.Turn.IsEmergencyMovementTurn
      Visibility = let (VisibilityLevel v) = state.Turn.Visibility in v
      Phase = phaseToString state.Phase
      GermanLocatedTurn = state.GermanLocatedTurn |> Option.defaultValue 0
      ConvoysAvailable = state.ConvoysAvailable
      ConvoysSunkByGerman = state.ConvoysSunkByGerman
      ConvoyContacts = contacts
      ConvoyUnits = convoys
      ShadowMarkers = shadowMarkers
      LocationMarkers = locationMarkers
      ActiveBattles = battles
      Scores = scores
      Ships = ships
      AirUnits = airUnits
      GameEnded = gameEndToString state.GameEnded }

let applyGameStatus (snapshot: GameStatusDto) (state: GameState) : GameState =
    let withShips =
        snapshot.Ships
        |> Array.fold (fun (players: Map<Nationality, PlayerState>) (dto: ShipStatusDto) ->
            let sid = ShipId dto.Id
            let nat = parseNationality dto.Nationality
            match players.TryFind nat with
            | None -> players
            | Some p ->
                let current =
                    match p.Ships.TryFind sid with
                    | Some s -> s
                    | None ->
                        { Id = sid
                          Name = dto.Id
                          Nationality = nat
                          Class = Battleship
                          EvasionRating = dto.EvasionRating
                          MaxEvasionRating = dto.EvasionRating
                          MaxSpeedZones = 0
                          SearchStrength = { Day = 0; Night = 0 }
                          CanPatrol = true
                          Mode = parseShipMode dto.Mode
                          CurrentZone = parseZone dto.CurrentZone
                          Fuel = None
                          TaskForce = None
                          IsConvoyEscort = dto.IsConvoyEscort
                          ZonesMovedThisTurn = dto.ZonesMovedThisTurn
                          MidshipsHits = dto.MidshipsHits
                          MaxMidshipsHits = dto.MidshipsHits
                          PermanentEvasionLoss = dto.PermanentEvasionLoss
                          IsLockedInPort = dto.IsLockedInPort
                          IsRestrictedToPatrolUntilContact = dto.IsRestrictedToPatrolUntilContact
                          IsSunk = dto.IsSunk }
                let updated =
                    { current with
                        CurrentZone = parseZone dto.CurrentZone
                        Mode = parseShipMode dto.Mode
                        EvasionRating = dto.EvasionRating
                        MidshipsHits = dto.MidshipsHits
                        PermanentEvasionLoss = dto.PermanentEvasionLoss
                        ZonesMovedThisTurn = dto.ZonesMovedThisTurn
                        IsSunk = dto.IsSunk
                        IsConvoyEscort = dto.IsConvoyEscort
                        IsLockedInPort = dto.IsLockedInPort
                        IsRestrictedToPatrolUntilContact = dto.IsRestrictedToPatrolUntilContact
                        Fuel = if dto.Fuel.HasFuel then Some { FactorsRemaining = dto.Fuel.FactorsRemaining; InEmergencyMovement = dto.Fuel.InEmergencyMovement } else None }
                players.Add(nat, { p with Ships = p.Ships.Add(sid, updated) }))
            state.Players

    let withAir =
        snapshot.AirUnits
        |> Array.fold (fun (players: Map<Nationality, PlayerState>) (dto: AirStatusDto) ->
            let aid = AirUnitId dto.Id
            let nat = parseNationality dto.Nationality
            match players.TryFind nat with
            | None -> players
            | Some p ->
                let current =
                    match p.AirUnits.TryFind aid with
                    | Some a -> a
                    | None ->
                        { Id = aid
                          Name = dto.Id
                          Nationality = nat
                          UnitType = LongRangeRecon
                          Mode = parseAirMode dto.Mode
                          SearchStrength = { Day = 0; Night = 0 }
                          EnduranceRating = 0
                          TurnsAirborne = dto.TurnsAirborne
                          AirAttacksLaunchedThisTurn = dto.AirAttacksLaunchedThisTurn
                          MaxSpeedZones = 0
                          HomeBase = LandBase "Restored"
                          CurrentZone = parseZone dto.CurrentZone
                          IsAtBase = dto.IsAtBase }
                let updated =
                    { current with
                        CurrentZone = parseZone dto.CurrentZone
                        Mode = parseAirMode dto.Mode
                        TurnsAirborne = dto.TurnsAirborne
                        AirAttacksLaunchedThisTurn = dto.AirAttacksLaunchedThisTurn
                        IsAtBase = dto.IsAtBase }
                players.Add(nat, { p with AirUnits = p.AirUnits.Add(aid, updated) }))
            withShips

    let scoredPlayers =
        snapshot.Scores
        |> Array.fold (fun (players: Map<Nationality, PlayerState>) (s: PlayerScoreDto) ->
            let nat = parseNationality s.Nationality
            match players.TryFind nat with
            | Some p ->
                players.Add(nat, { p with Score = { p.Score with Points = s.Points; Events = scoreEventsFromDto s.Events } })
            | None -> players)
            withAir

    let contacts =
        snapshot.ConvoyContacts
        |> Array.toList
        |> List.choose (fun c ->
            parseZone c.Zone
            |> Option.map (fun z ->
                let marker : ConvoyContactMarker =
                    { Zone = z
                      ConvoyId = if c.ConvoyId <= 0 then None else Some c.ConvoyId
                      Discoverer = parseNationality c.Discoverer
                      Source = parseContactSource c.Source
                      TurnLocated = c.TurnLocated }
                marker))

    let convoys =
        snapshot.ConvoyUnits
        |> Array.toList
        |> List.choose (fun c ->
            parseZone c.Zone
            |> Option.map (fun z ->
                let convoy : ConvoyUnit =
                    { Id = c.Id
                      Zone = z
                      RouteIndex = c.RouteIndex
                      Direction = parseHeading c.Direction
                      IsSunk = c.IsSunk }
                convoy))

    let locationMarkers =
        snapshot.LocationMarkers
        |> Array.toList
        |> List.choose (fun m ->
            parseZone m.Zone
            |> Option.map (fun z ->
                let marker : LocationMarker =
                    { Zone = z
                      RevealedShipClass = if m.HasRevealedShipClass then Some(parseShipClass m.RevealedShipClass) else None
                      Owner = parseNationality m.Owner }
                marker))

    let shadowMarkers =
        snapshot.ShadowMarkers
        |> Array.toList
        |> List.choose (fun m ->
            parseZone m.Zone
            |> Option.map (fun z ->
                let marker : ShadowMarker =
                    { Zone = z
                      ShadowingUnit = UnitId m.ShadowingUnitId
                      ShadowedUnit = UnitId m.ShadowedUnitId }
                marker))

    let activeBattles : BattleBoardState list =
        snapshot.ActiveBattles
        |> Array.toList
        |> List.map (fun b ->
            let ships : Map<ShipId, BattleShipState> =
                b.Ships
                |> Array.toList
                |> List.map (fun s ->
                    let gunSections : GunSection list =
                        s.GunSections
                        |> Array.toList
                        |> List.map (fun g ->
                            ({ Section = parseGunSection g.Section
                               MaxSalvo = g.MaxSalvo
                               SalvoRemaining = g.SalvoRemaining
                               CanFireBothRanges = g.CanFireBothRanges } : GunSection))
                    let sid = ShipId s.ShipId
                    sid,
                    ({ ShipId = sid
                       Name = s.Name
                       Class = parseShipClass s.Class
                       Position = { Q = s.Q; R = s.R; S = s.S }
                       Facing = parseHexSide s.Facing
                       GunSections = gunSections
                       SecondaryHits = s.SecondaryHits
                       EvasionRating = s.EvasionRating
                       MidshipsHits = s.MidshipsHits
                       MaxMidshipsHits = s.MaxMidshipsHits
                       PermanentEvasionLoss = s.PermanentEvasionLoss
                       IsWithdrawing = s.IsWithdrawing
                       IsSunk = s.IsSunk } : BattleShipState))
                |> Map.ofList
            ({ Id = b.Id; Round = b.Round; Ships = ships } : BattleBoardState))

    { state with
        Turn =
            { Number = snapshot.TurnNumber
              IsNightTurn = snapshot.IsNightTurn
              IsEmergencyMovementTurn = snapshot.IsEmergencyMovementTurn
              Visibility = VisibilityLevel snapshot.Visibility }
        Phase = parsePhase snapshot.Phase
        Players = scoredPlayers
        ConvoysAvailable = snapshot.ConvoysAvailable
        ConvoysSunkByGerman = snapshot.ConvoysSunkByGerman
        ConvoyContacts = contacts
        ConvoyUnits = convoys
        LocationMarkers = locationMarkers
        ShadowMarkers = shadowMarkers
        ActiveBattles = activeBattles
        GermanLocatedTurn = if snapshot.GermanLocatedTurn <= 0 then None else Some snapshot.GermanLocatedTurn
        GameEnded = parseGameEnd snapshot.GameEnded }

let saveGameStatusToFile (opts: XmlPersistenceOptions) (filePath: string) (state: GameState) : unit =
    captureGameStatus state |> writeXmlToFile opts filePath

let loadGameStatusFromFile (filePath: string) (state: GameState) : GameState =
    let snapshot = readXmlFromFile<GameStatusDto> filePath
    applyGameStatus snapshot state

let loadGameStatusFromFileWithScenario (filePath: string) (scenario: ScenarioDefinition) : GameState =
    let baseState = initializeGame scenario
    loadGameStatusFromFile filePath baseState
