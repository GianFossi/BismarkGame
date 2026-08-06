/// <summary>
/// Update.fs
/// The engine: `update` applies one Command to a GameState and returns
/// either the new state or an Error naming the rule that blocks it.
///
/// SCOPE: movement legality (now including the evasion-derived speed
/// limit from Tables/EvasionEffects.fs), phase sequencing, task force
/// composition, battle-board setup, Search, Chance, and Air Attack are
/// all implemented from the rules text and wired to the transcribed
/// tables. Shadow and Naval Fire resolution are routed through
/// IRulesTables (see Tables/RulesTablesImpl.fs). The remaining gaps are
/// the specific rule exceptions called out in README.md, not the phase
/// handlers themselves.
/// </summary>
module BismarckGame.Core.Update

open BismarckGame.Core.Common
open BismarckGame.Core.SearchBoard
open BismarckGame.Core.Units
open BismarckGame.Core.Markers
open BismarckGame.Core.BattleBoard
open BismarckGame.Core.GameState
open BismarckGame.Core.Tables.EvasionEffects
open BismarckGame.Core.VictoryConditions

/// <summary>
/// Abstraction over the printed resolution tables. A different
/// implementation can be swapped in per game level (rule 22.25 notes the
/// Intermediate Game uses a separate Shadow Table from the Basic Game).
/// `rollTwoDice` returns a fresh 2d6 sum (2-12) each call — ResolveNavalFire
/// may call it twice, since a "consult special damage" main-table result
/// requires a second roll against the Special Damage sub-table.
/// </summary>
type IRulesTables =
    /// <summary>
    /// `shadowerName` is looked up against the printed Shadow Table
    /// reference list (Tables/ShadowTable.fs's categoryOf); ships/units
    /// not on that list can't shadow (Update.fs checks this before ever
    /// calling ResolveShadow, so implementations can assume the name is
    /// valid). `targetMoving2Zones` is rule modification 1, computed from
    /// the target's ZonesMovedThisTurn.
    /// </summary>
    abstract ResolveShadow: shadowerName: string * visibilityLevel: int * targetMoving2Zones: bool * roll: int -> bool
    abstract ResolveNavalFire: order: FireOrder * rollTwoDice: (unit -> int) -> FireResult

/// <summary>
/// The nine Basic Game phases in order (rule 4.1-4.9).
/// </summary>
let private phaseOrder =
    [ UnitAvailability; Visibility; ShadowDetermination; AirMovement
      ShipMovement; Search; AirAttack; TorpedoAttack; NavalCombat; Chance ]

let private nextPhase (p: Phase) =
    let i = phaseOrder |> List.findIndex ((=) p)
    phaseOrder.[(i + 1) % phaseOrder.Length]

// --- lookup helpers ----------------------------------------------------

let private tryFindShip (state: GameState) (id: ShipId) =
    state.Players
    |> Map.toSeq
    |> Seq.tryPick (fun (_, p) -> p.Ships.TryFind id |> Option.map (fun s -> p.Nationality, s))

let private tryFindAirUnit (state: GameState) (id: AirUnitId) =
    state.Players
    |> Map.toSeq
    |> Seq.tryPick (fun (_, p) -> p.AirUnits.TryFind id |> Option.map (fun a -> p.Nationality, a))

let private tryFindUnit (state: GameState) (UnitId id) =
    match tryFindShip state (ShipId id) with
    | Some (nat, ship) -> Some(nat, Choice1Of2 ship)
    | None ->
        match tryFindAirUnit state (AirUnitId id) with
        | Some (nat, air) -> Some(nat, Choice2Of2 air)
        | None -> None

let private updatePlayer (state: GameState) (nat: Nationality) (f: PlayerState -> PlayerState) =
    { state with Players = state.Players.Add(nat, f state.Players.[nat]) }

let private updateShip (state: GameState) (nat: Nationality) (shipId: ShipId) (f: ShipCounter -> ShipCounter) =
    updatePlayer state nat (fun p -> { p with Ships = p.Ships.Add(shipId, f p.Ships.[shipId]) })

let private updateAirUnit (state: GameState) (nat: Nationality) (id: AirUnitId) (f: AirUnitCounter -> AirUnitCounter) =
    updatePlayer state nat (fun p -> { p with AirUnits = p.AirUnits.Add(id, f p.AirUnits.[id]) })

// --- movement legality (rule 5.1x, 5.18) --------------------------------

/// <summary>
/// True if `dest` may be entered by a ship of this nationality: exists on
/// the map, has a grid-coordinate (rule 5.18 excludes coordinate-less
/// partial zones), and isn't off-limits to this side (German ships may
/// never enter the Irish Sea or a British port, rule 5.18).
/// </summary>
let private canEnterZone (map: SearchBoardMap) (nat: Nationality) (dest: GridCoordinate) =
    match map.TryFind dest with
    | None -> false
    | Some zone ->
        match zone.Coordinate with
        | None -> false
        | Some _ ->
            match nat, zone.Terrain with
            | German, IrishSea -> false
            | German, Port owner when owner <> German -> false
            | _ -> true

let private isFogTurn (turn: GameTurn) =
    let (VisibilityLevel vis) = turn.Visibility
    vis >= 9

let private isFogZone (state: GameState) (zone: GridCoordinate) =
    isFogTurn state.Turn || state.FogZones.Contains zone

let private isAdjacent (map: SearchBoardMap) (from_: GridCoordinate) (dest: GridCoordinate) =
    neighbors map from_ |> List.contains dest

let private nearestConvoyRouteZoneWithin
    (map: SearchBoardMap)
    (routeZones: Set<GridCoordinate>)
    (maxDistance: int)
    (origin: GridCoordinate)
    : GridCoordinate option =
    routeZones
    |> Seq.choose (fun rz ->
        distanceWithin map maxDistance origin rz
        |> Option.map (fun d -> d, rz))
    |> Seq.sortBy (fun (d, rz) -> d, rz.Letter, rz.Number)
    |> Seq.tryHead
    |> Option.map snd

let private nearestLiveConvoyWithin
    (map: SearchBoardMap)
    (maxDistance: int)
    (origin: GridCoordinate)
    (convoys: ConvoyUnit list)
    : ConvoyUnit option =
    convoys
    |> List.filter (fun c -> not c.IsSunk)
    |> List.choose (fun c ->
        distanceWithin map maxDistance origin c.Zone
        |> Option.map (fun d -> d, c))
    |> List.sortBy (fun (d, c) -> d, c.RouteIndex, c.Id)
    |> List.tryHead
    |> Option.map snd

let private addOrReplaceConvoyContact
    (state: GameState)
    (convoyId: int)
    (zone: GridCoordinate)
    (discoverer: Nationality)
    (source: ConvoyContactSource)
    : GameState =
    let keepExisting =
        state.ConvoyContacts
        |> List.filter (fun c -> not (c.ConvoyId = Some convoyId && c.Discoverer = discoverer))
    { state with
        ConvoyContacts =
            { Zone = zone
              ConvoyId = Some convoyId
              Discoverer = discoverer
              Source = source
              TurnLocated = state.Turn.Number }
            :: keepExisting }

let private headingFromTo (a: GridCoordinate) (b: GridCoordinate) : Heading =
    let dy = int b.Letter - int a.Letter
    let dx = b.Number - a.Number
    let sy = if dy = 0 then 0 elif dy > 0 then 1 else -1
    let sx = if dx = 0 then 0 elif dx > 0 then 1 else -1
    match sy, sx with
    | -1, 0 -> North
    | -1, 1 -> NorthEast
    | 0, 1 -> East
    | 1, 1 -> SouthEast
    | 1, 0 -> South
    | 1, -1 -> SouthWest
    | 0, -1 -> West
    | -1, -1 -> NorthWest
    | _ -> East

let private moveConvoysAlongRoute (state: GameState) : GameState =
    let route = state.ConvoyRoutePath |> List.toArray
    if route.Length = 0 then
        state
    else
        let movedConvoys, movedPairs, movedZonePairs =
            state.ConvoyUnits
            |> List.map (fun c ->
                if c.IsSunk || c.RouteIndex >= route.Length - 1 then
                    c, None, None
                else
                    let nextIndex = c.RouteIndex + 1
                    let nextZone = route.[nextIndex]
                    let moved =
                        { c with
                            RouteIndex = nextIndex
                            Zone = nextZone
                            Direction = headingFromTo c.Zone nextZone }
                    moved, Some(c.Id, nextZone), Some(c.Zone, nextZone))
            |> List.fold (fun (cs, ids, zones) (c, idMove, zoneMove) -> c :: cs, idMove :: ids, zoneMove :: zones) ([], [], [])

        let movedConvoys = List.rev movedConvoys
        let movedPairs = List.rev movedPairs
        let movedZonePairs = List.rev movedZonePairs

        let movedById =
            movedPairs
            |> List.choose id
            |> Map.ofList

        let movedByZone =
            movedZonePairs
            |> List.choose id
            |> Map.ofList

        let movedContacts =
            state.ConvoyContacts
            |> List.map (fun contact ->
                match contact.ConvoyId with
                | Some convoyId ->
                    match movedById.TryFind convoyId with
                    | Some newZone -> { contact with Zone = newZone }
                    | None -> contact
                | None ->
                    match movedByZone.TryFind contact.Zone with
                    | Some newZone -> { contact with Zone = newZone }
                    | None -> contact)

        { state with
             ConvoyUnits = movedConvoys
             ConvoyContacts = movedContacts }

let private applyFinalScores (state: GameState) : GameState =
    match state.GameEnded with
    | None -> state
    | Some _ ->
        if state.Players.[German].Score.Events |> List.exists (fun (label, _) -> label = "Final game bonuses") then
            state
        else
            let allShips = state.Players |> Map.toList |> List.collect (fun (_, p) -> p.Ships |> Map.toList |> List.map snd)
            let inGermanPort name =
                allShips
                |> List.tryFind (fun s -> s.Name = name)
                |> Option.exists (fun s -> s.CurrentZone |> Option.bind state.SearchBoard.TryFind |> Option.exists (fun z -> match z.Terrain with Port German -> true | _ -> false))
            let bismarck = allShips |> List.tryFind (fun s -> s.Name = "Bismarck")
            let prinz = allShips |> List.tryFind (fun s -> s.Name = "Prinz Eugen")
            let portBonus =
                match bismarck with
                | Some s when inGermanPort "Bismarck" -> match state.Turn.Number with | n when n <= 10 -> 14 | n when n <= 16 -> 12 | n when n <= 22 -> 10 | n when n <= 28 -> 8 | _ -> 4
                | _ -> 0
            let lowEvasion =
                (match bismarck with Some s when not s.IsSunk && s.CurrentZone.IsSome && s.EvasionRating <= 21 -> 6 | _ -> 0)
                + (match prinz with Some s when not s.IsSunk && s.CurrentZone.IsSome && s.EvasionRating <= 21 -> 2 | _ -> 0)
            let airLoss = (state.Players.[German].AirUnits |> Map.toList |> List.filter (fun (_, a) -> not a.IsAtBase) |> List.length) * -2
            let convoyBonus = if state.ConvoysSunkByGerman = 0 then 4 else 0
            let bonus = portBonus + lowEvasion + airLoss + convoyBonus
            let score = state.Players.[German].Score
            let score' = { score with Points = score.Points + bonus; Events = ("Final game bonuses", bonus) :: score.Events }
            let britishPortBonus =
                (match bismarck with Some s when inGermanPort "Bismarck" && s.MidshipsHits > 0 -> 6 | _ -> 0)
                + (match prinz with Some s when inGermanPort "Prinz Eugen" && s.MidshipsHits > 0 -> 2 | _ -> 0)
            let british = state.Players.[British].Score
            let british' = { british with Points = british.Points + britishPortBonus; Events = ("Ships returned to port", britishPortBonus) :: british.Events }
            { state with Players = state.Players.Add(German, { state.Players.[German] with Score = score' }).Add(British, { state.Players.[British] with Score = british' }) }

let private convoySinkingPoints (convoyNumber: int) : int =
    // Rule 12.44 convoy VP schedule (1st..5th): 6/6/8/10/12.
    // For any out-of-range value, clamp to the nearest listed band.
    match convoyNumber with
    | n when n <= 1 -> 6
    | 2 -> 6
    | 3 -> 8
    | 4 -> 10
    | _ -> 12
// --- battle board placement (rule 9.28) -----------------------------------

/// <summary>
/// Approximate the hexes along one edge of the board for the given
/// direction, spreading out from the far point in that direction along
/// the two neighboring directions. Exact only to the extent
/// `approximateBoardRadius` (BattleBoard.fs) matches the real board —
/// flagged there as an estimate, not a transcribed hex count.
/// </summary>
let private edgeHexes (edge: BoardEdge) : HexCoord list =
    let dir = edge.ToHexSide().Offset
    let far = { Q = dir.Q * approximateBoardRadius; R = dir.R * approximateBoardRadius; S = dir.S * approximateBoardRadius }
    let perp = edge.ToHexSide().RotateClockwise.Offset
    [ for i in -3 .. 3 -> { Q = far.Q + perp.Q * i; R = far.R + perp.R * i; S = far.S + perp.S * i } ]
    |> List.filter isOnBoard

/// <summary>
/// Rule 9.281: no more than two friendly ships per hex. Cycles through
/// the given hex list two ships at a time; if there are more ships than
/// 2*hexes.Length, the last hex absorbs the overflow rather than crashing.
/// </summary>
let private placeTwoPerHex (hexes: HexCoord list) (shipIds: ShipId list) : Map<ShipId, HexCoord> =
    if hexes.IsEmpty then
        shipIds |> List.map (fun sid -> sid, HexCoord.Zero) |> Map.ofList
    else
        shipIds
        |> List.mapi (fun i sid -> sid, hexes.[min (i / 2) (hexes.Length - 1)])
        |> Map.ofList

let private edgeFromRoll (n: int) : BoardEdge =
    match ((n - 1) % 6) + 1 with
    | 1 -> Edge1 | 2 -> Edge2 | 3 -> Edge3 | 4 -> Edge4 | 5 -> Edge5 | _ -> Edge6

/// <summary>
/// Computes the fuel factor cost of one more zone of movement this turn,
/// or an Error if the move isn't legal given remaining fuel and whether
/// this is a 'C' turn. Returns Ok None for ships that don't track fuel
/// (cruisers, rule 5.21) and Ok (Some cost) otherwise — cost is 0 when
/// the move is free under the current rule (5.22/5.25's "at least one
/// factor left" and 'C'-turn cases).
/// </summary>
let private fuelCost (ship: ShipCounter) (isCTurn: bool) (isBreakoutBonusShip: bool) : Result<int option, string> =
    match ship.Fuel with
    | None -> Ok None
    | Some fuel ->
        let zoneIndex = ship.ZonesMovedThisTurn   // 0 = about to move the 1st zone this turn, etc.
        if isBreakoutBonusShip then
            // Rule 5.28: zones 1-3 free; the 4th zone costs 1 factor, the
            // 5th costs another (total 2 for a full 5-zone breakout).
            if zoneIndex < 3 then Ok(Some 0)
            elif fuel.FactorsRemaining > 0 then Ok(Some 1)
            else Error "No fuel remaining for the breakout bonus's 4th/5th zone (rule 5.28)"
        elif ship.MaxSpeedZones = 2 then
            if zoneIndex = 0 then
                // First zone: free as long as any fuel remains (5.22); once
                // exhausted, even the first zone needs a 'C' turn (5.23/5.24).
                if fuel.FactorsRemaining > 0 then Ok(Some 0)
                elif isCTurn then Ok(Some 0)
                else Error $"{ship.Name} has no fuel left — it may only move on 'C' turns until it's a first zone again (rule 5.23/5.24)"
            else
                // Second zone: always costs 1 factor (5.21); with no fuel,
                // a second zone is simply not possible (emergency movement
                // is one zone per 'C' turn, never two).
                if fuel.FactorsRemaining > 0 then Ok(Some 1)
                else Error $"{ship.Name} has no fuel left for a second zone this turn (rule 5.21)"
        else
            // MaxSpeedZones = 1 ship (Rodney, Nelson, Ramillies, Revenge —
            // rule 5.25): costs 1 factor per zone in a non-'C' turn; free
            // (as emergency movement) on a 'C' turn.
            if isCTurn then Ok(Some 0)
            elif fuel.FactorsRemaining > 0 then Ok(Some 1)
            else Error $"{ship.Name} has no fuel left — it may only move on 'C' turns until refueled (rule 5.25)"

let private searchStrengthFor (useNight: bool) (s: ShipCounter) =
    if useNight then s.SearchStrength.Night else s.SearchStrength.Day

let private airSearchStrengthFor (useNight: bool) (a: AirUnitCounter) =
    if useNight then a.SearchStrength.Night else a.SearchStrength.Day

// --- the reducer ---------------------------------------------------------

/// <summary>
/// Applies one command. `roll` supplies dice values (kept as a parameter,
/// not System.Random directly, so tests can inject fixed sequences) —
/// see Dice.fs for the real (Dice.create) and deterministic
/// (Dice.ofSequence) sources, adapted via Dice.asRollFn.
/// </summary>
let rec update (tables: IRulesTables) (roll: unit -> int) (cmd: Command) (state: GameState) : Result<GameState, string> =
    match cmd with

    | SearchZone (searcher, zone) ->
        if state.Phase <> Search then
            Error "Search is performed in the Search phase (rule 4.6)"
        elif isFogZone state zone then
            Error "Search is blocked in fog ('X' visibility, rule 7.3x)"
        else
            match state.Players.TryFind searcher with
            | None -> Error $"No {searcher} player in this scenario"
            | Some p ->
                // Rule 2.424: day/night search strength depends on
                // whether it's a day or night turn — now read directly
                // from GameTurn.IsNightTurn (a turn can independently be
                // night and/or a 'C' turn; see GameState.fs's doc comment
                // on why this used to be a bug when it was a single
                // 3-way label).
                let useNight = state.Turn.IsNightTurn
                let shipsHere = p.Ships |> Map.toList |> List.map snd |> List.filter (fun s -> s.CurrentZone = Some zone)
                let airHere = p.AirUnits |> Map.toList |> List.map snd |> List.filter (fun a -> a.CurrentZone = Some zone)
                // Rule 7.27: inherent coastal search strength of 4 (3 at
                // night) in certain coastal zones — Faeroe/Shetland/
                // Ireland/GB coast/Hvalfiord for the British, Norway/
                // France coast for the Germans. The board data doesn't
                // mark exact coastline zones, so this uses "is a friendly
                // Port zone" as a proxy — it will under-count true coastal
                // zones that aren't themselves a named port, but won't
                // fabricate a strength in zones with no coastal feature.
                let coastalBonus =
                    match state.SearchBoard.TryFind zone with
                    | Some z ->
                        match z.Terrain with
                        | Port owner when owner = searcher -> if useNight then 3 else 4
                        | _ -> 0
                    | None -> 0
                let capacity =
                    let taskForceMax =
                        p.TaskForces
                        |> Map.toList
                        |> List.map snd
                        |> List.filter (fun tf -> tf.Zone = zone && tf.Mode = Patrol)
                        |> List.map (fun tf ->
                            tf.Ships
                            |> List.choose p.Ships.TryFind
                            |> List.map (searchStrengthFor useNight)
                            |> (fun xs -> if xs.IsEmpty then 0 else List.max xs))
                        |> (fun xs -> if xs.IsEmpty then 0 else List.max xs)
                    let independentShipMax =
                        shipsHere
                        |> List.filter (fun s -> s.TaskForce.IsNone)
                        |> List.map (searchStrengthFor useNight)
                        |> (fun xs -> if xs.IsEmpty then 0 else List.max xs)
                    let shipMax = max taskForceMax independentShipMax
                    let airMax = airHere |> List.map (airSearchStrengthFor useNight) |> (fun xs -> if xs.IsEmpty then 0 else List.max xs)
                    List.max [ shipMax; airMax; coastalBonus ]
                let (VisibilityLevel visLevel) = state.Turn.Visibility
                if capacity < visLevel then
                    Ok state   // insufficient search capacity — not an error, just no reveal (rule 6.0)
                else
                    // Reveal every located-enemy ship in the zone: one
                    // LocationMarker per opposing ship present, replacing
                    // any existing marker for that ship in that zone.
                    let opponent = if searcher = British then German else British
                    let enemyShipsHere =
                        state.Players.TryFind opponent
                        |> Option.map (fun op -> op.Ships |> Map.toList |> List.map snd |> List.filter (fun s -> s.CurrentZone = Some zone))
                        |> Option.defaultValue []
                    let newMarkers =
                        enemyShipsHere
                        |> List.map (fun s -> { Zone = zone; RevealedShipClass = Some s.Class; Owner = opponent })
                    let keepExisting =
                        state.LocationMarkers
                        |> List.filter (fun m -> not (m.Zone = zone && m.Owner = opponent))
                    let germanLocatedTurn' =
                        if opponent = German && not newMarkers.IsEmpty && state.GermanLocatedTurn.IsNone then Some state.Turn.Number
                        else state.GermanLocatedTurn
                    Ok { state with LocationMarkers = keepExisting @ newMarkers; GermanLocatedTurn = germanLocatedTurn' }

    | ResolveHuffDuff (shipId, chosenZone) ->
        if state.Phase <> Chance then Error "Huff-Duff is resolved in the Chance phase (rule 4.9)"
        else
            match tryFindShip state shipId with
            | Some (German, ship) ->
                match ship.CurrentZone with
                | Some current when chosenZone = current || isAdjacent state.SearchBoard current chosenZone ->
                    let marker = { Zone = chosenZone; RevealedShipClass = None; Owner = German }
                    Ok { state with LocationMarkers = marker :: state.LocationMarkers; GermanLocatedTurn = state.GermanLocatedTurn |> Option.orElse (Some state.Turn.Number) }
                | Some _ -> Error "Huff-Duff must name the ship's current zone or an adjacent zone (rule 10.22)"
                | None -> Error "Ship has no position to resolve Huff-Duff"
            | Some (British, _) -> Error "Only German ships use the Basic Game Chance Table"
            | None -> Error $"Unknown ship {shipId}"

    | RollChanceForShip shipId ->
        if state.Phase <> Chance then
            Error "Chance rolls happen in the Chance phase (rule 4.9)"
        else
            match tryFindShip state shipId with
            | None -> Error $"Unknown ship {shipId}"
            | Some (German, ship) ->
                match ship.CurrentZone with
                | None -> Error "Ship has no position to reveal"
                | Some zone ->
                    let nearWhiteDot = nearWhiteDotBelowRowD state.SearchBoard zone
                    let nearCoast = nearBritishOrIrishCoast state.SearchBoard zone
                    let column = BismarckGame.Core.Tables.ChanceTable.column nearWhiteDot nearCoast
                    let diceSum = roll () + roll ()
                    let result = BismarckGame.Core.Tables.ChanceTable.resolve diceSum column
                    match result with
                    | BismarckGame.Core.Tables.ChanceTable.HuffDuff ->
                        Error "Huff-Duff requires ResolveHuffDuff with the German player's chosen zone (rule 10.22)"
                    | BismarckGame.Core.Tables.ChanceTable.GeneralSearchThreshold threshold ->
                        let (VisibilityLevel visLevel) = state.Turn.Visibility
                        if visLevel <= threshold then
                            let marker = { Zone = zone; RevealedShipClass = Some ship.Class; Owner = German }
                            Ok { state with
                                    LocationMarkers = marker :: state.LocationMarkers
                                    GermanLocatedTurn = state.GermanLocatedTurn |> Option.orElse (Some state.Turn.Number) }
                        else
                            Ok state
                    | BismarckGame.Core.Tables.ChanceTable.NoSearchPossible -> Ok state
                    | BismarckGame.Core.Tables.ChanceTable.ConvoyLocatedOnRoute ->
                        match state.ConvoyUnits |> List.tryFind (fun c -> not c.IsSunk && c.Zone = zone) with
                        | Some convoy -> Ok(addOrReplaceConvoyContact state convoy.Id convoy.Zone German ChanceOnRoute)
                        | None -> Ok state
                    | BismarckGame.Core.Tables.ChanceTable.ConvoyLocatedNearRoute ->
                        // Card text: "on patrol, within 2 zones of a
                        // convoy route".
                        if ship.Mode <> Patrol then
                            Ok state
                        else
                            match nearestLiveConvoyWithin state.SearchBoard 2 zone state.ConvoyUnits with
                            | Some convoy -> Ok(addOrReplaceConvoyContact state convoy.Id convoy.Zone German ChanceNearRoute)
                            | None -> Ok state
                    | BismarckGame.Core.Tables.ChanceTable.ConvoyLocatedAdjacentToRoute ->
                        // Card text: "one zone away from route".
                        match nearestLiveConvoyWithin state.SearchBoard 1 zone state.ConvoyUnits with
                        | Some convoy -> Ok(addOrReplaceConvoyContact state convoy.Id convoy.Zone German ChanceAdjacentToRoute)
                        | None -> Ok state
            | Some (British, _) -> Error "Only German ships are rolled on the Chance Table in the Basic Game (rule 4.9)"

    | AttackConvoy (attacker, zone) ->
        if state.Phase <> NavalCombat then
            Error "Convoy attacks are resolved in the Naval Combat phase (rule 4.8 / 12.44)"
        else
            match tryFindShip state attacker with
            | None -> Error $"Unknown ship {attacker}"
            | Some (nat, ship) when nat <> German -> Error "Only German ships can sink convoys for rule 12.44 scoring"
            | Some (_, ship) when ship.IsSunk -> Error "Sunk ships cannot attack convoys"
            | Some (_, ship) ->
                match ship.CurrentZone with
                | None -> Error "Attacking ship is off-board"
                | Some shipZone when shipZone <> zone -> Error "Attacking ship must be in the convoy-contact zone"
                | Some _ ->
                    let contact =
                        state.ConvoyContacts
                        |> List.tryFind (fun c -> c.Zone = zone && c.Discoverer = German)
                    let liveConvoyAtZone =
                        match contact with
                        | Some c ->
                            match c.ConvoyId with
                            | Some convoyId -> state.ConvoyUnits |> List.tryFind (fun cu -> cu.Id = convoyId && not cu.IsSunk && cu.Zone = zone)
                            | None -> state.ConvoyUnits |> List.tryFind (fun cu -> not cu.IsSunk && cu.Zone = zone)
                        | None -> None
                    let britishEscortsInZone =
                        state.Players
                        |> Map.tryFind British
                        |> Option.map (fun p ->
                            p.Ships
                            |> Map.toList
                            |> List.map snd
                            |> List.filter (fun s -> s.IsConvoyEscort && not s.IsSunk && s.CurrentZone = Some zone))
                        |> Option.defaultValue []
                    if contact.IsNone then
                        Error "No German convoy contact exists in that zone"
                    elif liveConvoyAtZone.IsNone then
                        Error "No live convoy is currently in that zone"
                    elif not britishEscortsInZone.IsEmpty then
                        Error "Convoy is screened by active escort ships; resolve naval combat with escorts first"
                    elif state.ConvoysSunkByGerman >= state.ConvoysAvailable then
                        Error "All scenario convoys have already been sunk"
                    else
                        let convoy = liveConvoyAtZone.Value
                        let sunkNumber = state.ConvoysSunkByGerman + 1
                        let points = convoySinkingPoints sunkNumber
                        let contacts' =
                            state.ConvoyContacts
                            |> List.filter (fun c -> not (c.Discoverer = German && (c.ConvoyId = Some convoy.Id || (c.ConvoyId.IsNone && c.Zone = zone))))
                        let convoys' =
                            state.ConvoyUnits
                            |> List.map (fun c -> if c.Id = convoy.Id then { c with IsSunk = true } else c)
                        let germanScore = state.Players.[German].Score
                        let germanScore' =
                            { germanScore with
                                Points = germanScore.Points + points
                                Events = ($"Sank convoy unit {convoy.Id} (#{sunkNumber}) at {zone}", points) :: germanScore.Events }
                        let state' =
                            { state with
                                ConvoyContacts = contacts'
                                ConvoyUnits = convoys'
                                ConvoysSunkByGerman = sunkNumber }
                        Ok(updatePlayer state' German (fun p -> { p with Score = germanScore' }))

    | AdvancePhase ->
        if state.Phase = Chance then
            // New turn (rule 4.9 end-of-turn -> 4.1 Unit Availability):
            // reset each ship's per-turn movement budget, and reset air
            // units that are sitting at base (rule 1C, "return air units
            // available from rest and refit to their home base" — this
            // project doesn't model a multi-turn rest cycle, just clears
            // the airborne counter once a unit is back, which is what
            // makes it available to fly again).
            let newTurnNumber = state.Turn.Number + 1
            let newTurnFlags =
                BismarckGame.Core.Tables.TimeAndVisibility.timeRecordTrack
                |> List.tryFind (fun e -> e.Turn = newTurnNumber)
            let resetPlayers =
                state.Players
                |> Map.map (fun _ p ->
                    { p with
                        Ships = p.Ships |> Map.map (fun _ s -> { s with ZonesMovedThisTurn = 0 })
                        AirUnits =
                            p.AirUnits
                            |> Map.map (fun _ a ->
                                let a' = if a.IsAtBase then { a with TurnsAirborne = 0 } else a
                                { a' with AirAttacksLaunchedThisTurn = 0 }) })
            // Reinforcements due this turn (rule: Order of Battle
            // "Reinforcements — Starting Location").
            let due, notYetDue = state.PendingReinforcements |> List.partition (fun (t, _, _) -> t = newTurnNumber)
            let playersWithReinforcements =
                due
                |> List.fold
                    (fun players (_, shipId, zone) ->
                        match tryFindShip { state with Players = players } shipId with
                        | None -> players
                        | Some (nat, _) ->
                            let p = players |> Map.find nat
                            let p' = { p with Ships = p.Ships.Add(shipId, { p.Ships.[shipId] with CurrentZone = Some zone }) }
                            players |> Map.add nat p')
                    resetPlayers
            let advancedState =
                { state with
                    Phase = UnitAvailability
                    Turn =
                        { state.Turn with
                            Number = newTurnNumber
                            IsNightTurn = newTurnFlags |> Option.map (fun e -> e.IsNightTurn) |> Option.defaultValue state.Turn.IsNightTurn
                            IsEmergencyMovementTurn = newTurnFlags |> Option.map (fun e -> e.IsEmergencyMovementTurn) |> Option.defaultValue state.Turn.IsEmergencyMovementTurn }
                    Players = playersWithReinforcements
                    PendingReinforcements = notYetDue
                    GameEnded =
                        let allShips =
                            state.Players
                            |> Map.toList
                            |> List.collect (fun (_, p) -> p.Ships |> Map.toList |> List.map snd)
                        let outcomes =
                            allShips
                            |> List.map (fun s ->
                                { ShipOutcome.Name = s.Name
                                  Nationality = s.Nationality
                                  Class = s.Class
                                  IsSunk = s.IsSunk
                                  MidshipsHits = s.MidshipsHits })
                        let bismarckInPort =
                            allShips
                            |> List.exists (fun s ->
                                s.Name = "Bismarck" && not s.IsSunk
                                && (match s.CurrentZone |> Option.bind state.SearchBoard.TryFind with
                                    | Some z -> (match z.Terrain with Port German -> true | _ -> false)
                                    | None -> false))
                        let finishTurn = 34   // Time Record Track's printed "Finish" turn — see Tables/TimeAndVisibility.fs
                        checkGameEnd finishTurn newTurnNumber bismarckInPort outcomes }
            Ok (advancedState |> moveConvoysAlongRoute |> applyFinalScores)
        elif state.Phase = ShipMovement then
            // Rule 9.728: "In any turn in which a ship moves either one
            // zone or not at all... it may attempt to repair lost evasion
            // rating factors." A ship that moved 2 zones this turn gets no
            // repair attempt. The repair ceiling is MaxEvasionRating minus
            // any PERMANENT loss (rule 9.722) — permanent damage never
            // repairs, only the temporary component does.
            let repairedPlayers =
                state.Players
                |> Map.map (fun _ p ->
                    { p with
                        Ships =
                            p.Ships
                            |> Map.map (fun _ s ->
                                let ceiling = s.MaxEvasionRating - s.PermanentEvasionLoss
                                if s.ZonesMovedThisTurn <= 1 && s.EvasionRating < ceiling then
                                    let inPort =
                                        s.CurrentZone
                                        |> Option.bind state.SearchBoard.TryFind
                                        |> Option.exists (fun z ->
                                            match z.Terrain with
                                            | Port owner -> owner = s.Nationality
                                            | _ -> false)
                                    let repaired =
                                        if inPort then 2
                                        else BismarckGame.Core.Tables.EvasionEffects.evasionRepairTable.TryFind(roll ()) |> Option.defaultValue 0
                                    { s with EvasionRating = min ceiling (s.EvasionRating + repaired) }
                                else s) })
            Ok { state with Phase = nextPhase state.Phase; Players = repairedPlayers }
        else
            Ok { state with Phase = nextPhase state.Phase }

    | SetShipMode (shipId, mode) ->
        match tryFindShip state shipId with
        | None -> Error $"Unknown ship {shipId}"
        | Some (nat, ship) ->
            if not ship.CanPatrol && mode = Patrol then
                Error "Aircraft carriers have no patrol mode (rule 2.423)"
            elif ship.IsRestrictedToPatrolUntilContact && mode = Movement then
                Error $"{ship.Name} must remain on patrol until a German ship is discovered within 10 zones (Mobilize command)"
            elif state.Phase <> Visibility then
                Error "Mode changes must be made in the Visibility Phase, before any ship moves (rule 5.32)"
            else
                Ok(updateShip state nat shipId (fun s -> { s with Mode = mode }))

    | SetAirUnitMode (unitId, mode) ->
        match tryFindAirUnit state unitId with
        | None -> Error $"Unknown air unit {unitId}"
        | Some (nat, au) ->
            if state.Phase <> Visibility then
                Error "Air unit mode changes must be made in the Visibility Phase (rule 6.31)"
            else
                Ok(updateAirUnit state nat unitId (fun s -> { s with Mode = mode }))

    | MoveShip (shipId, dest) ->
        match tryFindShip state shipId with
        | None -> Error $"Unknown ship {shipId}"
        | Some (nat, ship) ->
            if state.Phase <> ShipMovement && state.Phase <> ShadowDetermination then
                Error "Ships may only move in the Shadow Determination or Ship Movement phase (rule 4.3/4.5)"
            elif ship.Mode = Patrol then
                Error "A ship in patrol mode cannot move (rule 5.33)"
            elif ship.IsLockedInPort then
                Error $"{ship.Name} may not leave port yet — see the scenario's release notes (Mobilize command)"
            else
                match ship.CurrentZone with
                | None -> Error "Ship must be placed on the board first (Unit Availability phase, rule 4.1)"
                | Some current ->
                    if not (isAdjacent state.SearchBoard current dest) then
                        Error "Destination zone is not adjacent to the current zone (rule 5.18)"
                    elif not (canEnterZone state.SearchBoard nat dest) then
                        Error "This side may not enter that zone (rule 5.18)"
                    else
                        // Max zones this turn is derived from CURRENT evasion
                        // rating (Tables/EvasionEffects.fs), not a fixed
                        // per-ship constant — combat damage that reduces
                        // evasion (Special Damage table) shrinks this as the
                        // game goes on.
                        //
                        // EXCEPTION — rule 5.28 (German Basic Player Aid
                        // Card, Order of Battle note 1): on the first turn
                        // of play only (card turn 4 — see GameTurn.Number's
                        // doc comment), Bismarck and Prinz Eugen get a
                        // breakout-move bonus of up to 5 zones total this
                        // turn, at a fuel cost handled by `fuelCost` below
                        // (free for zones 1-3, 1 factor for the 4th, 1 more
                        // for the 5th).
                        let isBreakoutBonusShip =
                            state.Turn.Number = 4 && nat = German && (ship.Name = "Bismarck" || ship.Name = "Prinz Eugen")
                        let allowedThisTurn =
                            if isBreakoutBonusShip then
                                5
                            else
                                match searchBoardMaxSpeed ship.EvasionRating with
                                | Speed0 -> 0
                                | EmergencyMovementOnly -> if state.Turn.IsEmergencyMovementTurn then 1 else 0
                                | Speed1 -> 1
                                | Speed2 -> 2
                        if ship.ZonesMovedThisTurn >= allowedThisTurn then
                            Error $"Ship has no movement left this turn (evasion rating {ship.EvasionRating} allows {allowedThisTurn} zone(s) — see Tables/EvasionEffects.fs)"
                        else
                            let isCTurn = state.Turn.IsEmergencyMovementTurn
                            match fuelCost ship isCTurn isBreakoutBonusShip with
                            | Error msg -> Error msg
                            | Ok fuelDeduction ->
                                let ship' =
                                    { ship with
                                        CurrentZone = Some dest
                                        ZonesMovedThisTurn = ship.ZonesMovedThisTurn + 1
                                        Fuel =
                                            match ship.Fuel, fuelDeduction with
                                            | Some fuel, Some cost ->
                                                let remaining = max 0 (fuel.FactorsRemaining - cost)
                                                Some { FactorsRemaining = remaining; InEmergencyMovement = (remaining = 0) }
                                            | _ -> ship.Fuel }
                                Ok(updateShip state nat shipId (fun _ -> ship'))

    | MoveAirUnit (unitId, dest) ->
        match tryFindAirUnit state unitId with
        | None -> Error $"Unknown air unit {unitId}"
        | Some (nat, au) ->
            if state.Phase <> AirMovement then
                Error "Air units may only move in the Air Movement phase (rule 4.4)"
            elif au.IsAtBase && au.TurnsAirborne < 0 then
                Error "Air unit is resting/refitting and cannot launch this turn (rule 6.42)"
            else
                let au' =
                    { au with
                        CurrentZone = Some dest
                        TurnsAirborne = au.TurnsAirborne + 1
                        IsAtBase = false }
                if au'.TurnsAirborne > au.EnduranceRating then
                    Error "Air unit has exceeded its endurance rating — must return to base instead (rule 6.2x)"
                else
                    Ok(updateAirUnit state nat unitId (fun _ -> au'))

    | ReturnAirUnitToBase unitId ->
        match tryFindAirUnit state unitId with
        | None -> Error $"Unknown air unit {unitId}"
        | Some (nat, au) ->
            if state.Phase <> AirAttack then Error "Air units return after the Air Attack phase (rule 4.7)"
            elif au.IsAtBase then Error "Air unit is already at base"
            else Ok(updateAirUnit state nat unitId (fun a -> { a with CurrentZone = None; IsAtBase = true; TurnsAirborne = -1 }))

    | FormTaskForce (nat, shipIds) ->
        if state.Phase <> Visibility then
            Error "Task forces may only be formed in the Visibility phase (rule 5.41)"
        else
            let player = state.Players.[nat]
            let ships = shipIds |> List.choose player.Ships.TryFind
            let zones = ships |> List.choose (fun s -> s.CurrentZone) |> List.distinct
            match zones with
            | [ singleZone ] when ships.Length = shipIds.Length ->
                let tfId = TaskForceId(player.TaskForces.Count + 1)
                let tf =
                    { Id = tfId
                      Nationality = nat
                      Ships = shipIds
                      Zone = singleZone
                      Mode = Movement }
                let playerWithShips =
                    shipIds
                    |> List.fold
                        (fun (p: PlayerState) sid ->
                            { p with Ships = p.Ships.Add(sid, { p.Ships.[sid] with TaskForce = Some tfId }) })
                        player
                Ok(updatePlayer state nat (fun _ -> { playerWithShips with TaskForces = playerWithShips.TaskForces.Add(tfId, tf) }))
            | [] -> Error "No matching ships found to form a task force"
            | _ -> Error "All ships in a task force must occupy the same zone (rule 5.41)"

    | BreakTaskForce (tfId, shipId) ->
        if state.Phase <> Visibility then
            Error "Ships may only break from task force in the Visibility phase, before any ship moves (rule 5.44)"
        else
            match tryFindShip state shipId with
            | None -> Error $"Unknown ship {shipId}"
            | Some (nat, _) ->
                let player = state.Players.[nat]
                match player.TaskForces.TryFind tfId with
                | None -> Error $"Unknown task force {tfId}"
                | Some tf ->
                    let remaining = tf.Ships |> List.filter ((<>) shipId)
                    let taskForces' =
                        if remaining.IsEmpty then player.TaskForces.Remove tfId
                        else player.TaskForces.Add(tfId, { tf with Ships = remaining })
                    let ships' = player.Ships.Add(shipId, { player.Ships.[shipId] with TaskForce = None })
                    Ok(updatePlayer state nat (fun _ -> { player with Ships = ships'; TaskForces = taskForces' }))

    | DeclareShadow (UnitId shadowerId, UnitId targetId) ->
        if state.Phase <> ShadowDetermination && state.Phase <> ShipMovement then
            Error "Shadowing is declared in Shadow Determination, or as high-speed shadow during Ship Movement (rules 4.3/8.2)"
        else
            match tryFindUnit state (UnitId shadowerId), tryFindShip state (ShipId targetId) with
            | Some (_, shadower), Some (_, target) ->
                if state.Phase = ShipMovement && target.ZonesMovedThisTurn < 1 then
                    Error "High-speed shadow may only be attempted after the target's first move (rule 8.2)"
                else
                    let shadowerZone, shadowerNameForTable, evasionCheckResult =
                        match shadower with
                        | Choice1Of2 ship ->
                            ship.CurrentZone, ship.Name,
                            (if ship.EvasionRating < target.EvasionRating then
                                 Error "The shadowing ship's evasion rating must be equal to or greater than the target's (rule 4.3)"
                             else Ok ())
                        | Choice2Of2 air ->
                            match air.UnitType, air.CurrentZone with
                            | LongRangeRecon, z -> z, "Br. LR Recon", Ok ()
                            | _, _ -> None, air.Name, Error "Only LR Recon air units may shadow (rule 8.13)"
                    match evasionCheckResult with
                    | Error msg -> Error msg
                    | Ok () ->
                        match shadowerZone, target.CurrentZone with
                        | Some shadowerPos, Some targetPos when shadowerPos = targetPos ->
                            match BismarckGame.Core.Tables.ShadowTable.categoryOf.TryFind shadowerNameForTable with
                            | None -> Error $"{shadowerNameForTable} is not on the printed Shadow Table reference list and cannot shadow"
                            | Some _ ->
                                let (VisibilityLevel visLevel) = state.Turn.Visibility
                                let targetMoved2Zones = target.ZonesMovedThisTurn >= 2
                                let succeeded = tables.ResolveShadow(shadowerNameForTable, visLevel, targetMoved2Zones, roll ())
                                if succeeded then
                                    let marker = { Zone = shadowerPos; ShadowingUnit = UnitId shadowerId; ShadowedUnit = UnitId targetId }
                                    Ok { state with ShadowMarkers = marker :: state.ShadowMarkers }
                                else
                                    Ok state   // failed attempt — no marker, no error (rule 8.1)
                        | Some _, Some _ -> Error "Shadower and target must occupy the same zone to declare shadow (rule 8.1)"
                        | None, _ -> Error "Shadowing unit has no position"
                        | _, None -> Error "Target ship has no position"
            | _ -> Error "Could not find shadowing unit and/or target ship"

    | RollVisibilityChange ->
        if state.Phase <> Visibility then
            Error "Visibility changes are rolled in the Visibility phase (rule 4.2)"
        elif state.Turn.Number = 4 then
            Ok state   // Sequence of Play card: "Skip 2A on the first turn" — not an error, just a no-op (first turn of play is card turn 4, see GameTurn.Number's doc comment)
        else
            let diceSum = roll () + roll ()
            match BismarckGame.Core.Tables.TimeAndVisibility.visibilityChangeTable |> List.tryFind (fun s -> s.DiceRoll = diceSum) with
            | None -> Error $"No Visibility Change Table entry for roll {diceSum}"
            | Some shift ->
                let newLevel = BismarckGame.Core.Tables.TimeAndVisibility.applyVisibilityShift state.Turn.Visibility shift.Shift
                let occupiedZones =
                    state.Players
                    |> Map.toList
                    |> List.collect (fun (_, p) -> p.Ships |> Map.toList |> List.choose (fun (_, s) -> s.CurrentZone))
                    |> Set.ofList
                let fogZones = if shift.TriggersFog then occupiedZones else Set.empty
                Ok { state with Turn = { state.Turn with Visibility = newLevel }; FogZones = fogZones }

    | Mobilize shipId ->
        match tryFindShip state shipId with
        | None -> Error $"Unknown ship {shipId}"
        | Some (nat, ship) ->
            if ship.IsLockedInPort then
                // Notes 7/8 (KGV group, Repulse): released the turn AFTER
                // a German ship is located. Note 10 (Force H): released on
                // the 4th turn after. Distinguished by name set, since
                // that's what the card itself does (different notes for
                // different ship groups) rather than a modeled property.
                let isForceH = [ "Sheffield"; "Ark Royal"; "Renown" ] |> List.contains ship.Name
                match state.GermanLocatedTurn with
                | None -> Error "No German ship has been located yet (rule: Order of Battle notes 7/8/10)"
                | Some locatedTurn ->
                    let releaseTurn = if isForceH then locatedTurn + 4 else locatedTurn + 1
                    if state.Turn.Number >= releaseTurn then
                        Ok(updateShip state nat shipId (fun s -> { s with IsLockedInPort = false }))
                    else
                        Error $"{ship.Name} is released on turn {releaseTurn}, not yet (current turn {state.Turn.Number})"
            elif ship.IsConvoyEscort then
                // Notes 9/12 (Rodney, Ramillies): released once ANY German
                // ship has been located; German player gets 1 v.p.
                match state.GermanLocatedTurn with
                | None -> Error "No German ship has been located yet (rule: Order of Battle notes 9/12)"
                | Some _ ->
                    let state' = updateShip state nat shipId (fun s -> { s with IsConvoyEscort = false })
                    let germanScore = state'.Players.[German].Score
                    let germanScore' = { germanScore with Points = germanScore.Points + 1; Events = ("Mobilized " + ship.Name, 1) :: germanScore.Events }
                    Ok(updatePlayer state' German (fun p -> { p with Score = germanScore' }))
            elif ship.IsRestrictedToPatrolUntilContact then
                // Note 11 (Edinburgh): released once a German ship is
                // located within 10 zones of its current position.
                match ship.CurrentZone with
                | None -> Error "Ship has no position"
                | Some zone ->
                    let germanNear =
                        state.LocationMarkers
                        |> List.filter (fun m -> m.Owner = German)
                        |> List.exists (fun m -> (distanceWithin state.SearchBoard 10 zone m.Zone).IsSome)
                    if germanNear then
                        Ok(updateShip state nat shipId (fun s -> { s with IsRestrictedToPatrolUntilContact = false }))
                    else
                        Error "No German ship has been discovered within 10 zones yet (rule: Order of Battle note 11)"
            else
                Ok state   // no active restriction on this ship — no-op, not an error

    | WithdrawFromBattle shipId ->
        match state.ActiveBattles |> List.tryFind (fun b -> b.Ships.ContainsKey shipId) with
        | None -> Error "Ship is not in an active battle"
        | Some battle ->
            let ship = battle.Ships.[shipId]
            let enemies = battle.Ships |> Map.toList |> List.map snd |> List.filter (fun s -> s.ShipId <> shipId && not s.IsSunk)
            let canWithdraw =
                enemies
                |> List.filter (fun e -> ship.Position.DistanceTo e.Position <= 6)
                |> List.forall (fun e -> ship.EvasionRating > e.EvasionRating)
            if not canWithdraw then
                Error "A ship may withdraw only when its evasion exceeds every same-or-larger enemy type within six hexes (rule 9.93)"
            else
                let battle' = { battle with Ships = battle.Ships.Add(shipId, { ship with IsWithdrawing = true }) }
                Ok { state with ActiveBattles = state.ActiveBattles |> List.map (fun b -> if b.Id = battle.Id then battle' else b) }

    | AdvanceBattleRound battleId ->
        match state.ActiveBattles |> List.tryFind (fun b -> b.Id = battleId) with
        | None -> Error $"No active battle with id {battleId}"
        | Some battle ->
            let live = battle.Ships |> Map.toList |> List.map snd |> List.filter (fun s -> not s.IsSunk)
            let withdrawn = live |> List.filter (fun s -> s.IsWithdrawing)
            let opponentsOf ship = live |> List.filter (fun other -> other.ShipId <> ship.ShipId && other.Class <> ship.Class)
            let occupied = battle.Ships |> Map.toList |> List.map snd |> List.map (fun s -> s.Position) |> Set.ofList
            let moveWithdrawing ship =
                let candidates =
                    [ ship.Facing; ship.Facing.RotateClockwise; ship.Facing.RotateCounterclockwise ]
                    |> List.map (fun side -> hexNeighbor ship.Position side)
                    |> List.filter (fun p -> isOnBoard p && not (occupied.Contains p))
                    |> List.filter (fun p -> opponentsOf ship |> List.forall (fun enemy -> p.DistanceTo enemy.Position >= ship.Position.DistanceTo enemy.Position))
                candidates
                |> List.sortByDescending (fun p -> opponentsOf ship |> List.map (fun e -> p.DistanceTo e.Position) |> List.sortDescending |> List.tryHead |> Option.defaultValue 0)
                |> List.tryHead
                |> Option.defaultValue ship.Position
            let moved =
                battle.Ships
                |> Map.map (fun _ ship -> if ship.IsWithdrawing then { ship with Position = moveWithdrawing ship } else ship)
            let afterMove = { battle with Ships = moved }
            let successfullyWithdrawn =
                afterMove.Ships
                |> Map.toList
                |> List.map snd
                |> List.filter (fun ship -> ship.IsWithdrawing && opponentsOf ship |> List.forall (fun enemy -> ship.Position.DistanceTo enemy.Position > 6))
                |> List.map (fun ship -> ship.ShipId)
                |> Set.ofList
            let remaining = afterMove.Ships |> Map.filter (fun id _ -> not (successfullyWithdrawn.Contains id))
            let battle' = { afterMove with Ships = remaining; Round = afterMove.Round + 1; TorpedoSalvosFired = Map.empty; TorpedoTargets = Map.empty; DefensiveFireResolved = Set.empty; SpecialFireChecked = Set.empty; FiredOrders = Set.empty }
            let liveSides =
                battle'.Ships
                |> Map.toList
                |> List.choose (fun (sid, ship) -> if ship.IsSunk then None else tryFindShip state sid |> Option.map fst)
                |> Set.ofList
            if liveSides.Count <= 1 then
                let synced =
                    battle'.Ships
                    |> Map.toList
                    |> List.fold (fun st (sid, bs) ->
                        match tryFindShip st sid with
                        | Some (nat, _) -> updateShip st nat sid (fun s -> { s with EvasionRating = bs.EvasionRating; MidshipsHits = bs.MidshipsHits; PermanentEvasionLoss = bs.PermanentEvasionLoss; IsSunk = bs.IsSunk })
                        | None -> st) state
                Ok { synced with ActiveBattles = synced.ActiveBattles |> List.filter (fun b -> b.Id <> battleId) }
            else
                Ok { state with ActiveBattles = state.ActiveBattles |> List.map (fun b -> if b.Id = battleId then battle' else b) }

    | AttemptBattleReinforcement (battleId, shipId) ->
        match state.ActiveBattles |> List.tryFind (fun b -> b.Id = battleId) with
        | None -> Error $"No active battle with id {battleId}"
        | Some battle when battle.Round < 3 ->
            Error "Reinforcement rolls begin after the third combat round (rule 9.41)"
        | Some battle ->
            match tryFindShip state shipId with
            | None -> Error $"Unknown ship {shipId}"
            | Some (nat, ship) ->
                let alreadyInBattle = battle.Ships.ContainsKey shipId
                let inZone = ship.CurrentZone = Some battle.Zone
                let enemyShips =
                    battle.Ships
                    |> Map.toList
                    |> List.map snd
                    |> List.filter (fun s -> not s.IsSunk && s.Class <> ship.Class)
                if alreadyInBattle then
                    Error "A ship already engaged in this action cannot reinforce it"
                elif not inZone then
                    Error "The ship must be in the same Search Board zone as the naval action"
                elif enemyShips.IsEmpty then
                    Error "A reinforcement requires at least one live enemy ship in the naval action"
                elif ship.Class = AircraftCarrier then
                    Error "A reinforcement must be able to attack an enemy ship; aircraft carriers cannot initiate naval combat"
                else
                    let attempts = battle.ReinforcementAttempts |> Map.tryFind shipId |> Option.defaultValue 0
                    let success = roll () <= min 6 (attempts + 1)
                    let attempts' = battle.ReinforcementAttempts |> Map.add shipId (attempts + 1)
                    if not success then
                        let battle' = { battle with ReinforcementAttempts = attempts' }
                        Ok { state with ActiveBattles = state.ActiveBattles |> List.map (fun b -> if b.Id = battleId then battle' else b) }
                    else
                        let edge = edgeFromRoll (roll ())
                        let candidatePositions = edgeHexes edge
                        let position =
                            candidatePositions
                            |> List.tryFind (fun p -> enemyShips |> List.forall (fun e -> p.DistanceTo e.Position >= 6))
                        match position with
                        | None -> Error "No legal entry hex is at least six hexes from every enemy ship (rule 9.42)"
                        | Some position ->
                            let gunSections =
                                BismarckGame.Core.Tables.ShipStats.shipStats.TryFind ship.Name
                                |> Option.map BismarckGame.Core.Tables.ShipStats.freshGunSections
                                |> Option.defaultValue []
                            let battleShip =
                                { ShipId = ship.Id
                                  Name = ship.Name
                                  Class = ship.Class
                                  Position = position
                                  Facing = edge.ToHexSide().Opposite
                                  GunSections = gunSections
                                  SecondaryHits = 0
                                  EvasionRating = ship.EvasionRating
                                  MidshipsHits = ship.MidshipsHits
                                  MaxMidshipsHits = ship.MaxMidshipsHits
                                  PermanentEvasionLoss = ship.PermanentEvasionLoss
                                  IsWithdrawing = false
                                  IsSunk = false }
                            let battle' = { battle with Ships = battle.Ships.Add(shipId, battleShip); ReinforcementAttempts = attempts' }
                            Ok { state with ActiveBattles = state.ActiveBattles |> List.map (fun b -> if b.Id = battleId then battle' else b) }

    | AttemptTaskForceReinforcement (battleId, taskForceId) ->
        match state.ActiveBattles |> List.tryFind (fun b -> b.Id = battleId), state.Players |> Map.toList |> List.tryPick (fun (nat, player) -> player.TaskForces.TryFind taskForceId |> Option.map (fun tf -> nat, tf)) with
        | None, _ -> Error $"No active battle with id {battleId}"
        | _, None -> Error $"Unknown task force {taskForceId}"
        | Some battle, Some (nat, taskForce) ->
            let members = taskForce.Ships |> List.choose (fun sid -> state.Players.[nat].Ships.TryFind sid)
            let enemies = battle.Ships |> Map.toList |> List.map snd |> List.filter (fun s -> not s.IsSunk && s.Class <> AircraftCarrier)
            let eligible =
                members.Length = taskForce.Ships.Length
                && battle.Round >= 3
                && not enemies.IsEmpty
                && (members |> List.forall (fun ship -> ship.CurrentZone = Some battle.Zone && ship.Class <> AircraftCarrier && not (battle.Ships.ContainsKey ship.Id)))
            if not eligible then
                Error "The complete task force is not eligible to reinforce this battle"
            else
                let attempts = battle.TaskForceReinforcementAttempts |> Map.tryFind taskForceId |> Option.defaultValue 0
                let attempts' = battle.TaskForceReinforcementAttempts |> Map.add taskForceId (attempts + 1)
                if roll () > min 6 (attempts + 1) then
                    let failedBattle = { battle with TaskForceReinforcementAttempts = attempts' }
                    Ok { state with ActiveBattles = state.ActiveBattles |> List.map (fun b -> if b.Id = battleId then failedBattle else b) }
                else
                    let edge = edgeFromRoll (roll ())
                    let positions = edgeHexes edge |> List.filter (fun p -> enemies |> List.forall (fun e -> p.DistanceTo e.Position >= 6))
                    if positions.Length < members.Length then
                        Error "No legal entry edge can place the complete task force six hexes from every enemy"
                    else
                        let newShips =
                            members
                            |> List.mapi (fun index ship ->
                                let gunSections =
                                    BismarckGame.Core.Tables.ShipStats.shipStats.TryFind ship.Name
                                    |> Option.map BismarckGame.Core.Tables.ShipStats.freshGunSections
                                    |> Option.defaultValue []
                                ship.Id,
                                { ShipId = ship.Id
                                  Name = ship.Name
                                  Class = ship.Class
                                  Position = positions.[index]
                                  Facing = edge.ToHexSide().Opposite
                                  GunSections = gunSections
                                  SecondaryHits = 0
                                  EvasionRating = ship.EvasionRating
                                  MidshipsHits = ship.MidshipsHits
                                  MaxMidshipsHits = ship.MaxMidshipsHits
                                  PermanentEvasionLoss = ship.PermanentEvasionLoss
                                  IsWithdrawing = false
                                  IsSunk = false })
                            |> Map.ofList
                        let successfulBattle = { battle with Ships = (newShips |> Map.fold (fun acc id ship -> acc.Add(id, ship)) battle.Ships); TaskForceReinforcementAttempts = attempts' }
                        Ok { state with ActiveBattles = state.ActiveBattles |> List.map (fun b -> if b.Id = battleId then successfulBattle else b) }

    | ResolveTorpedoDamage (battleId, targetId, dieRoll) ->
        match state.ActiveBattles |> List.tryFind (fun b -> b.Id = battleId) with
        | None -> Error $"No active battle with id {battleId}"
        | Some battle ->
            match battle.Ships.TryFind targetId with
            | None -> Error "Torpedo target is not in this battle"
            | Some target ->
                match BismarckGame.Core.Tables.TorpedoDamageTable.resolve target.Name target.Class dieRoll with
                | Error message -> Error message
                | Ok BismarckGame.Core.Tables.TorpedoDamageTable.Miss -> Ok state
                | Ok BismarckGame.Core.Tables.TorpedoDamageTable.Sunk ->
                    let updated = { target with IsSunk = true }
                    let battle' = { battle with Ships = battle.Ships.Add(targetId, updated) }
                    Ok { state with ActiveBattles = state.ActiveBattles |> List.map (fun b -> if b.Id = battleId then battle' else b) }
                | Ok (BismarckGame.Core.Tables.TorpedoDamageTable.Midships(count, evasionReduction)) ->
                    let temporaryLoss = count * BismarckGame.Core.Tables.ShipStats.temporaryEvasionLossPerMidshipsHit target.Name target.Class
                    let specialLoss = evasionReduction |> Option.defaultValue 0
                    let hits = target.MidshipsHits + count
                    let updated =
                        { target with
                            MidshipsHits = hits
                            EvasionRating = max 0 (target.EvasionRating - temporaryLoss - specialLoss)
                            IsSunk = target.IsSunk || (target.MaxMidshipsHits > 0 && hits >= target.MaxMidshipsHits) }
                    let battle' = { battle with Ships = battle.Ships.Add(targetId, updated) }
                    Ok { state with ActiveBattles = state.ActiveBattles |> List.map (fun b -> if b.Id = battleId then battle' else b) }

    | ResolveBritishTorpedoSalvo (battleId, targetId) ->
        match state.ActiveBattles |> List.tryFind (fun b -> b.Id = battleId) with
        | None -> Error $"No active battle with id {battleId}"
        | Some battle ->
            let pendingFirer = battle.TorpedoTargets |> Map.tryFindKey (fun _ target -> target = targetId)
            let pending = pendingFirer |> Option.bind (fun firer -> battle.TorpedoSalvosFired |> Map.tryFind firer) |> Option.defaultValue 0
            if pending <= 0 then
                Error "No pending torpedo salvo is assigned to this target"
            else
                match battle.Ships.TryFind targetId with
                | None -> Error "Torpedo target is not in this battle"
                | Some target ->
                    let hit = BismarckGame.Core.Tables.TorpedoDamageTable.torpedoHit target.EvasionRating (roll () + roll ())
                    match hit with
                    | Error message -> Error message
                    | Ok false ->
                        let fired = battle.TorpedoSalvosFired.Add(pendingFirer.Value, pending - 1)
                        let battle' = { battle with TorpedoSalvosFired = fired }
                        Ok { state with ActiveBattles = state.ActiveBattles |> List.map (fun b -> if b.Id = battleId then battle' else b) }
                    | Ok true ->
                        let damageRoll = roll ()
                        let fired = battle.TorpedoSalvosFired.Add(pendingFirer.Value, pending - 1)
                        let state' = { state with ActiveBattles = state.ActiveBattles |> List.map (fun b -> if b.Id = battleId then { b with TorpedoSalvosFired = fired } else b) }
                        update tables roll (ResolveTorpedoDamage(battleId, targetId, damageRoll)) state'

    | LaunchTorpedoSalvo (battleId, firerId, targetId, salvoes) ->
        if state.Phase <> TorpedoAttack then
            Error "Torpedo salvos are launched in the Torpedo phase (rule 4.7 / 22.0)"
        elif salvoes <= 0 then
            Error "A torpedo launch must contain at least one salvo"
        else
            match state.ActiveBattles |> List.tryFind (fun b -> b.Id = battleId), tryFindShip state firerId, tryFindShip state targetId with
            | None, _, _ -> Error $"No active battle with id {battleId}"
            | _, None, _ -> Error $"Unknown firing ship {firerId}"
            | _, _, None -> Error $"Unknown target ship {targetId}"
            | Some battle, Some (firerNat, firer), Some (targetNat, target) ->
                if firerNat = targetNat then Error "A torpedo may not target a friendly ship"
                elif firer.IsSunk || target.IsSunk then Error "Sunk ships cannot launch or receive torpedoes"
                elif not (battle.Ships.ContainsKey firerId) || not (battle.Ships.ContainsKey targetId) then Error "Both ships must be in the same battle"
                elif firer.TorpedoesRemaining < salvoes then Error $"{firer.Name} has only {firer.TorpedoesRemaining} torpedo salvoes remaining"
                else
                    let fired = battle.TorpedoSalvosFired |> Map.tryFind firerId |> Option.defaultValue 0
                    let battle' = { battle with TorpedoSalvosFired = battle.TorpedoSalvosFired.Add(firerId, fired + salvoes); TorpedoTargets = battle.TorpedoTargets.Add(firerId, targetId) }
                    let state' = updateShip state firerNat firerId (fun s -> { s with TorpedoesRemaining = s.TorpedoesRemaining - salvoes })
                    let launched = { state' with ActiveBattles = state'.ActiveBattles |> List.map (fun b -> if b.Id = battleId then battle' else b) }
                    // Rule 23.0: defensive fire is not an optional second phase
                    // for the defender; it is resolved immediately against the
                    // launching flotilla. The explicit command remains useful
                    // for replay/import of an already declared opportunity.
                    update tables roll (ResolveDefensiveFire(battleId, targetId, firerId)) launched

    | ResolveDefensiveFire (battleId, defenderId, attackerId) ->
        if state.Phase <> TorpedoAttack then
            Error "Defensive fire is resolved in the Torpedo phase (rule 23.0)"
        else
            match state.ActiveBattles |> List.tryFind (fun b -> b.Id = battleId) with
            | None -> Error $"No active battle with id {battleId}"
            | Some battle when not (battle.Ships.ContainsKey defenderId && battle.Ships.ContainsKey attackerId) -> Error "Both defensive-fire ships must be in the battle"
            | Some battle when battle.DefensiveFireResolved.Contains defenderId -> Ok state
            | Some battle ->
                let fired = battle.TorpedoSalvosFired |> Map.tryFind attackerId |> Option.defaultValue 0
                let remaining =
                    match BismarckGame.Core.Tables.SubmarineAttackTable.resolveDefensiveFireAt state.Turn.IsNightTurn (roll () + roll ()) (roll ()) with
                    | Ok (BismarckGame.Core.Tables.SubmarineAttackTable.LoseOneDestroyer true) when fired > 0 -> fired - 1
                    | _ -> fired
                let battle' = { battle with TorpedoSalvosFired = battle.TorpedoSalvosFired.Add(attackerId, remaining); DefensiveFireResolved = battle.DefensiveFireResolved.Add defenderId }
                Ok { state with ActiveBattles = state.ActiveBattles |> List.map (fun b -> if b.Id = battleId then battle' else b) }

    | ReplenishTorpedoes shipId ->
        match tryFindShip state shipId with
        | None -> Error $"Unknown ship {shipId}"
        | Some (nat, ship) ->
            let inFriendlyPort =
                ship.CurrentZone
                |> Option.bind state.SearchBoard.TryFind
                |> Option.exists (fun z -> match z.Terrain with | Port owner -> owner = nat | _ -> false)
            if not inFriendlyPort then Error "Torpedoes can be replenished only in a friendly port"
            else
                let maximum = Tables.ShipStats.shipStats |> Map.tryFind ship.Name |> Option.bind (fun s -> s.TorpedoFactors) |> Option.defaultValue 0
                Ok (updateShip state nat shipId (fun s -> { s with TorpedoesRemaining = maximum }))

    | EndNavalCombat battleId ->
        match state.ActiveBattles |> List.tryFind (fun b -> b.Id = battleId) with
        | None -> Error $"No active battle with id {battleId}"
        | Some battle ->
            let liveSides =
                battle.Ships
                |> Map.toList
                |> List.choose (fun (sid, ship) -> if ship.IsSunk then None else tryFindShip state sid |> Option.map fst)
                |> Set.ofList
            if liveSides.Count > 1 then
                Error "A naval combat with live opposing ships may end only after a round resolves or all ships disengage (rules 9.32/12.7)"
            else
                let state' =
                    battle.Ships
                    |> Map.toList
                    |> List.fold
                        (fun st (shipId, bship) ->
                            match tryFindShip st shipId with
                            | None -> st
                            | Some (nat, _) ->
                                updateShip st nat shipId (fun s ->
                                    { s with
                                        EvasionRating = bship.EvasionRating
                                        MidshipsHits = bship.MidshipsHits
                                        PermanentEvasionLoss = bship.PermanentEvasionLoss
                                        IsSunk = bship.IsSunk }))
                        state
                Ok { state' with ActiveBattles = state'.ActiveBattles |> List.filter (fun b -> b.Id <> battleId) }

    | MoveShipInBattle (shipId, hexesMoved, directionChanges, destination, newFacing) ->
        match state.ActiveBattles |> List.tryFind (fun b -> b.Ships.ContainsKey shipId) with
        | None -> Error "Ship is not in an active battle"
        | Some battle ->
            let bship = battle.Ships.[shipId]
            let allowedPairs = battleBoardMovementOptions bship.EvasionRating
            if not (allowedPairs |> List.contains (hexesMoved, directionChanges)) then
                Error $"({hexesMoved} hexes, {directionChanges} turns) is not an allowed move at evasion rating {bship.EvasionRating} — see Tables/EvasionEffects.battleBoardMovementOptions"
            elif bship.Position.DistanceTo destination <> hexesMoved then
                Error "Destination hex distance doesn't match the declared hexesMoved"
            elif not (isOnBoard destination) then
                Error "Destination hex is off the board"
            elif bship.IsSunk then
                Error "A sunk ship cannot move on the Battle Board"
            else
                let occupied =
                    battle.Ships
                    |> Map.toList
                    |> List.choose (fun (id, s) -> if id = shipId || s.IsSunk then None else Some s.Position)
                    |> Set.ofList
                let paths = legalMovementPaths occupied bship hexesMoved directionChanges
                if not (paths |> List.exists (fun path -> List.last path = destination)) then
                    Error "The requested move violates bow direction, turn count, board limits, or occupied-hex blocking (rules 9.55-9.57)"
                else
                    let bship' = { bship with Position = destination; Facing = newFacing }
                    let battle' = { battle with Ships = battle.Ships.Add(shipId, bship') }
                    Ok { state with ActiveBattles = state.ActiveBattles |> List.map (fun b -> if b.Id = battle.Id then battle' else b) }

    | LaunchAirAttack (unitId, targetShip) ->
        match tryFindAirUnit state unitId, tryFindShip state targetShip with
        | None, _ -> Error $"Unknown air unit {unitId}"
        | _, None -> Error $"Unknown target ship {targetShip}"
        | Some (attackerNat, au), Some (targetNat, ship) ->
            if state.Phase <> AirAttack then
                Error "Air attacks are launched in the Air Attack phase (rule 4.7)"
            elif ship.CurrentZone |> Option.exists (isFogZone state) then
                Error "Air attacks are blocked in fog ('X' visibility, rule 7.3x)"
            elif state.Turn.IsNightTurn then
                Error "Air attacks are not allowed at night (rule 9.16)"
            elif not au.CanAttack then
                Error "This air unit cannot attack — wrong type or not in attack mode (rule 2.434/6.31)"
            elif au.CurrentZone <> ship.CurrentZone then
                Error "The air unit must be in the same zone as the target (rule 6.31)"
            else
                let maxAttacksThisTurn = if attackerNat = British then 2 else 1
                if au.AirAttacksLaunchedThisTurn >= maxAttacksThisTurn then
                    Error $"This unit has already launched {maxAttacksThisTurn} air attack(s) this day turn (rule 9.16)"
                else
                    let isBattleshipOrBattlecruiser = (ship.Class = Battleship || ship.Class = Battlecruiser)
                    let effects =
                        match attackerNat, au.UnitType with
                        | British, TorpedoBomber ->
                            BismarckGame.Core.Tables.BomberTables.britishTorpedoBomber.TryFind(roll () + roll ())
                            |> Option.defaultValue [ BismarckGame.Core.Tables.BomberTables.BMiss ]
                        | British, LevelBomber ->
                            BismarckGame.Core.Tables.BomberTables.resolveBritishDiveLevelBomber isBattleshipOrBattlecruiser (fun () -> roll () + roll ())
                        | German, _ ->
                            BismarckGame.Core.Tables.BomberTables.germanBomber.TryFind(roll () + roll ())
                            |> Option.defaultValue [ BismarckGame.Core.Tables.BomberTables.BMiss ]
                        | _ -> [ BismarckGame.Core.Tables.BomberTables.BMiss ]
                    let applyEffect (s: ShipCounter) effect =
                        match effect with
                        | BismarckGame.Core.Tables.BomberTables.BMiss -> s
                        | BismarckGame.Core.Tables.BomberTables.BMidships (count, permanentReduction) ->
                            let temporaryLoss = count * BismarckGame.Core.Tables.ShipStats.temporaryEvasionLossPerMidshipsHit s.Name s.Class
                            let permanentLoss = permanentReduction |> Option.defaultValue 0
                            let midshipsHits' = s.MidshipsHits + count
                            { s with
                                MidshipsHits = midshipsHits'
                                PermanentEvasionLoss = s.PermanentEvasionLoss + permanentLoss
                                EvasionRating = max 0 (s.EvasionRating - temporaryLoss - permanentLoss)
                                IsSunk = s.IsSunk || (s.MaxMidshipsHits > 0 && midshipsHits' >= s.MaxMidshipsHits) }
                        | BismarckGame.Core.Tables.BomberTables.BSecondary _ -> s   // no Search-Board-side field for this yet, see FireInBattle's same note
                        | BismarckGame.Core.Tables.BomberTables.BSection _ -> s     // Search Board ships don't carry GunSection state — only Battle Board ones do
                    let ship' = effects |> List.fold applyEffect ship
                    let state' = updateShip state targetNat targetShip (fun _ -> ship')
                    Ok(updateAirUnit state' attackerNat unitId (fun a -> { a with AirAttacksLaunchedThisTurn = a.AirAttacksLaunchedThisTurn + 1 }))

    | InitiateNavalCombat (zone, attacker) ->
        if state.Phase <> NavalCombat then
            Error "Naval combat may only be initiated in the Naval Combat phase (rule 4.8)"
        elif isFogZone state zone then
            Error "Naval combat is blocked in fog ('X' visibility, rule 7.3x)"
        else
            let shipsInZone =
                state.Players
                |> Map.toList
                |> List.collect (fun (_, p) -> p.Ships |> Map.toList |> List.map snd)
                |> List.filter (fun s -> s.CurrentZone = Some zone)
            let sidesPresent = shipsInZone |> List.map (fun s -> s.Nationality) |> List.distinct
            if sidesPresent.Length < 2 then
                Error "Naval combat requires located ships of both sides in the same zone (rule 4.8)"
            elif not (sidesPresent |> List.contains attacker) then
                Error $"{attacker} has no ships in this zone to attack with"
            else
                // Rule 9.28: defender's ships go in/around the center hex;
                // the attacker rolls one die for which edge to enter from.
                let attackerShips, defenderShips = shipsInZone |> List.partition (fun s -> s.Nationality = attacker)
                let attackerCanInitiate = attackerShips |> List.exists (fun s -> s.Class <> AircraftCarrier)
                if not attackerCanInitiate then
                    Error "Aircraft carriers cannot initiate naval combat attacks (rule 9.221)"
                else
                    let attackerMaxEvasion = attackerShips |> List.map (fun s -> s.EvasionRating) |> List.max
                    let defenderMaxEvasion = defenderShips |> List.map (fun s -> s.EvasionRating) |> List.max
                    if attackerMaxEvasion < defenderMaxEvasion then
                        Error "A slower attacker cannot force combat against a faster target unless the defender accepts (rule 9.223)"
                    else
                        let defenderShipIds = defenderShips |> List.map (fun s -> s.Id) |> Set.ofList
                        // Errata 41.5 removes the former restriction that a
                        // shadowing ship could attack only its shadow target.
                        let edge = edgeFromRoll (roll ())
                        let defenderHexes = HexCoord.Zero :: (List.map (fun (h: HexSide) -> hexNeighbor HexCoord.Zero h) [ HexN; HexNE; HexSE; HexS; HexSW; HexNW ])
                        let attackerHexes = edgeHexes edge
                        let defenderPlacement = placeTwoPerHex defenderHexes (defenderShips |> List.map (fun s -> s.Id))
                        let attackerPlacement = placeTwoPerHex attackerHexes (attackerShips |> List.map (fun s -> s.Id))
                            // Rule 9.282: bow must point toward a hex side, not a
                            // corner — both sides face a cardinal HexSide. Defender
                            // faces outward toward the attacker's edge; attacker faces
                            // back toward center. Approximate but always legal (never
                            // a corner) since HexSide only has the six valid values.
                        let defenderFacing = edge.ToHexSide()
                        let attackerFacing = edge.ToHexSide().Opposite
                        let battleShips =
                                shipsInZone
                                |> List.map (fun s ->
                                    let stats = BismarckGame.Core.Tables.ShipStats.shipStats.TryFind s.Name
                                    let gunSections =
                                        match stats with
                                        | Some st -> BismarckGame.Core.Tables.ShipStats.freshGunSections st
                                        | None -> []
                                    let isDefender = s.Nationality <> attacker
                                    let position =
                                        (if isDefender then defenderPlacement else attackerPlacement)
                                        |> Map.tryFind s.Id
                                        |> Option.defaultValue HexCoord.Zero
                                    s.Id,
                                    { ShipId = s.Id
                                      Name = s.Name
                                      Class = s.Class
                                      Position = position
                                      Facing = (if isDefender then defenderFacing else attackerFacing)
                                      GunSections = gunSections
                                      SecondaryHits = 0
                                      EvasionRating = s.EvasionRating
                                      MidshipsHits = s.MidshipsHits
                                      MaxMidshipsHits = s.MaxMidshipsHits
                                      PermanentEvasionLoss = s.PermanentEvasionLoss
                                      IsWithdrawing = false
                                      IsSunk = false })
                                |> Map.ofList
                        let newId = if state.ActiveBattles.IsEmpty then 1 else (state.ActiveBattles |> List.map (fun b -> b.Id) |> List.max) + 1
                        Ok { state with ActiveBattles = { Id = newId; Zone = zone; Ships = battleShips; Round = 1; ReinforcementAttempts = Map.empty; TaskForceReinforcementAttempts = Map.empty; TorpedoSalvosFired = Map.empty; TorpedoTargets = Map.empty; DefensiveFireResolved = Set.empty; SpecialFireChecked = Set.empty; FiredOrders = Set.empty } :: state.ActiveBattles }

    | FireInBattle order ->
        match state.ActiveBattles |> List.tryFind (fun b -> b.Ships.ContainsKey order.Firer) with
        | None -> Error "No active battle contains the firing ship"
        | Some battle ->
            match battle.Ships.TryFind order.Target with
            | None -> Error "Target ship is not in this battle"
            | Some targetShip ->
                let firstSpecialFire = not (battle.SpecialFireChecked.Contains order.Firer)
                let battleWithJam =
                    match battle.Ships.TryFind order.Firer with
                    | Some firer when firstSpecialFire && (firer.Name = "King George V" || firer.Name = "Prince of Wales") && (order.Section = BowGuns || order.Section = SternGuns) && roll () >= 4 ->
                        let jammedSection = if roll () <= 3 then BowGuns else SternGuns
                        let ships' =
                            battle.Ships.Add(order.Firer,
                                { firer with
                                    GunSections = firer.GunSections |> List.map (fun g -> if g.Section = jammedSection then { g with SalvoRemaining = g.SalvoRemaining / 2 } else g) })
                        { battle with Ships = ships'; SpecialFireChecked = battle.SpecialFireChecked.Add order.Firer }
                    | _ -> { battle with SpecialFireChecked = battle.SpecialFireChecked.Add order.Firer }
                let firedSections = battleWithJam.FiredOrders
                let isRodneyCrossFire =
                    let hasBow = firedSections.Contains((order.Firer, BowGuns, RangeA))
                    targetShip.Name = "Rodney" && order.Section = SternGuns && order.Range = RangeA && hasBow
                let effectiveOrder =
                    if isRodneyCrossFire then { order with SalvoesFired = max 1 (order.SalvoesFired / 2) }
                    else order
                let result = tables.ResolveNavalFire(effectiveOrder, fun () -> roll () + roll ())
                let result =
                    if targetShip.Name = "Rodney" then
                        match result, order.Aspect with
                        | HitSection SternGuns, SternOn -> HitSecondary
                        | HitSection SternGuns, Broadside -> if roll () <= 2 then HitSection BowGuns else HitSection SternGuns
                        | other, _ -> other
                    else result
                let targetShip' =
                    match result with
                    | Miss -> targetShip
                    | Sunk -> { targetShip with IsSunk = true }
                    | HitMidships (count, permanentReduction) ->
                        let temporaryLoss = count * BismarckGame.Core.Tables.ShipStats.temporaryEvasionLossPerMidshipsHit targetShip.Name targetShip.Class
                        let permanentLoss = permanentReduction |> Option.defaultValue 0
                        let midshipsHits' = targetShip.MidshipsHits + count
                        { targetShip with
                            MidshipsHits = midshipsHits'
                            PermanentEvasionLoss = targetShip.PermanentEvasionLoss + permanentLoss
                            EvasionRating = max 0 (targetShip.EvasionRating - temporaryLoss - permanentLoss)
                            IsSunk = targetShip.IsSunk || (targetShip.MaxMidshipsHits > 0 && midshipsHits' >= targetShip.MaxMidshipsHits) }
                    | HitSecondary ->
                        { targetShip with SecondaryHits = targetShip.SecondaryHits + 1 }
                    | HitSection sectionType ->
                        { targetShip with
                            GunSections =
                                targetShip.GunSections
                                |> List.map (fun gs ->
                                    if gs.Section = sectionType then
                                        { gs with SalvoRemaining = max 0 (gs.SalvoRemaining - 1) }
                                    else gs) }
                let battle' =
                    { battleWithJam with
                        Ships = battleWithJam.Ships.Add(order.Target, targetShip')
                        FiredOrders = battleWithJam.FiredOrders.Add(order.Firer, order.Section, order.Range) }
                let battles' =
                    state.ActiveBattles |> List.map (fun b -> if b.Id = battle.Id then battle' else b)
                Ok { state with ActiveBattles = battles' }
