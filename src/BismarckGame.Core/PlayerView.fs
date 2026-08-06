/// <summary>
/// PlayerView.fs
/// GameState is deliberately omniscient (rule 2.2: each side's positions
/// are secret from the other, but Update.fs needs the true state to
/// referee correctly). This module is the redaction layer a real
/// multiplayer client should render FROM — never the raw GameState. Given
/// a GameState and which side is looking, it produces a PlayerView that
/// shows that side's own units at their true positions and the
/// opponent's units ONLY where a LocationMarker (from search or Chance
/// Table) or an active shadow has actually revealed them, and even then
/// only what the rules say gets revealed (rule 7.23: general type, not
/// name; not exact ship identity).
/// </summary>
module BismarckGame.Core.PlayerView

open BismarckGame.Core.Common
open BismarckGame.Core.SearchBoard
open BismarckGame.Core.Units
open BismarckGame.Core.Markers
open BismarckGame.Core.BattleBoard
open BismarckGame.Core.GameState
open BismarckGame.Core.VictoryConditions

/// <summary>
/// What the viewer knows about one opposing contact — deliberately NOT
/// the opponent's real ShipCounter (that would leak name, exact evasion,
/// fuel, etc. that rule 7.23/2.54 never reveal to a searching player).
/// </summary>
type RevealedContact =
    { Zone: GridCoordinate
      /// <summary>
      /// Some when search/Chance revealed the specific class (rule
      /// 7.23's "battleship, aircraft carrier or cruiser"); None for a
      /// contact whose type hasn't been pinned down (e.g. Huff-Duff,
      /// rule 10.22, which reveals only a zone, not a type).
      /// </summary>
      ShipClass: ShipClass option
      /// <summary>
      /// True if the viewer currently has an active, successfully-held
      /// shadow on this contact (a ShadowMarker in this zone shadowed by
      /// one of the viewer's own units).
      /// </summary>
      IsShadowed: bool }

/// <summary>
/// A naval combat action the viewer is party to. Battles the viewer has
/// no ship in at all aren't included — rule 2.2's secrecy extends to
/// "which zones have active combat" just as much as ship positions
/// (the opponent could infer a lot from battle existence alone).
/// </summary>
type VisibleBattle =
    { BattleId: int
      OwnShips: BattleShipState list
      /// <summary>Salvoes launched by the viewer in this round; enemy values are never exposed.</summary>
      OwnTorpedoSalvosFired: (ShipId * int) list
      /// <summary>
      /// Same redaction as RevealedContact: enemy ships in a battle the
      /// viewer IS party to are still only shown by class, not full
      /// stats, until this project has a reason to think the rules
      /// reveal more once combat starts (they likely do — 9.17 has the
      /// defender place counters "face down" until damage forces a
      /// reveal — but that nuance isn't modeled yet; see README TODO).
      /// </summary>
      EnemyShipZonesAndClasses: (HexCoord * ShipClass) list
      Round: int }

type PlayerView =
    { Viewer: Nationality
      Turn: GameTurn
      Phase: Phase
      OwnShips: ShipCounter list
      OwnAirUnits: AirUnitCounter list
      OwnTaskForces: TaskForce list
      OwnConvoyEscorts: ConvoyMarker list
      OwnScore: VictoryScore
      /// <summary>
      /// Contacts on the enemy the viewer has actually located — derived
      /// from LocationMarkers owned by the OPPONENT (LocationMarker.Owner
      /// is the nationality of the tracked ship, so "opponent-owned
      /// markers" are exactly the enemy contacts this side has found).
      /// </summary>
      RevealedEnemyContacts: RevealedContact list
      /// <summary>
      /// Only the viewer's OWN shadow attempts (ShadowingUnit belongs to
      /// the viewer) — rule 8.15: "the shadowing player does not have to
      /// reveal... to the opponent" that a shadow even exists.
      /// </summary>
      OwnShadows: ShadowMarker list
      VisibleBattles: VisibleBattle list
      GameEnded: GameEndCondition option }

/// <summary>
/// Builds the view for one side. Deliberately does NOT expose the
/// opponent's PlayerState, ActiveBattles the viewer isn't in, or the
/// opponent's VictoryScore (real play doesn't show your opponent's
/// running total on their scoresheet either) — there's no "just trust me
/// and don't render this part" field to misuse; the type itself doesn't
/// carry that data.
/// </summary>
let project (state: GameState) (viewer: Nationality) : PlayerView =
    let opponent = if viewer = British then German else British
    let ownPlayer = state.Players.TryFind viewer

    let revealedContacts =
        state.LocationMarkers
        |> List.filter (fun m -> m.Owner = opponent)
        |> List.map (fun m ->
            let isShadowed =
                state.ShadowMarkers
                |> List.exists (fun sm ->
                    sm.Zone = m.Zone
                    && (let (UnitId shadowerId) = sm.ShadowingUnit
                        match ownPlayer with
                        | Some p -> p.Ships.ContainsKey(ShipId shadowerId) || p.AirUnits.ContainsKey(AirUnitId shadowerId)
                        | None -> false))
            { Zone = m.Zone; ShipClass = m.RevealedShipClass; IsShadowed = isShadowed })

    let ownShadows =
        state.ShadowMarkers
        |> List.filter (fun sm ->
            let (UnitId shadowerId) = sm.ShadowingUnit
            match ownPlayer with
            | Some p -> p.Ships.ContainsKey(ShipId shadowerId) || p.AirUnits.ContainsKey(AirUnitId shadowerId)
            | None -> false)

    let ownShipIds =
        ownPlayer |> Option.map (fun p -> p.Ships |> Map.toSeq |> Seq.map fst |> Set.ofSeq) |> Option.defaultValue Set.empty

    let visibleBattles =
        state.ActiveBattles
        |> List.filter (fun b -> b.Ships |> Map.toSeq |> Seq.exists (fun (sid, _) -> ownShipIds.Contains sid))
        |> List.map (fun b ->
            let ownBattleShips = b.Ships |> Map.toList |> List.filter (fun (sid, _) -> ownShipIds.Contains sid) |> List.map snd
            let enemyBattleShips =
                b.Ships
                |> Map.toList
                |> List.filter (fun (sid, _) -> not (ownShipIds.Contains sid))
                |> List.map (fun (_, bs) -> bs.Position, bs.Class)
            let ownTorpedoes = b.TorpedoSalvosFired |> Map.toList |> List.filter (fun (sid, _) -> ownShipIds.Contains sid)
            { BattleId = b.Id; OwnShips = ownBattleShips; OwnTorpedoSalvosFired = ownTorpedoes; EnemyShipZonesAndClasses = enemyBattleShips; Round = b.Round })

    { Viewer = viewer
      Turn = state.Turn
      Phase = state.Phase
      OwnShips = ownPlayer |> Option.map (fun p -> p.Ships |> Map.toList |> List.map snd) |> Option.defaultValue []
      OwnAirUnits = ownPlayer |> Option.map (fun p -> p.AirUnits |> Map.toList |> List.map snd) |> Option.defaultValue []
      OwnTaskForces = ownPlayer |> Option.map (fun p -> p.TaskForces |> Map.toList |> List.map snd) |> Option.defaultValue []
      OwnConvoyEscorts = ownPlayer |> Option.map (fun p -> p.ConvoyEscorts) |> Option.defaultValue []
      OwnScore = ownPlayer |> Option.map (fun p -> p.Score) |> Option.defaultValue { Nationality = viewer; Points = 0; Events = [] }
      RevealedEnemyContacts = revealedContacts
      OwnShadows = ownShadows
      VisibleBattles = visibleBattles
      GameEnded = state.GameEnded }
