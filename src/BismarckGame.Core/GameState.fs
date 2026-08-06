/// <summary>
/// GameState.fs
/// Turn structure and the top-level Basic Game state that ties together
/// the Search Board, Battle Board, units, markers and victory tracking.
/// Source: rule 4.0 (Sequence of Play) and 11.2x (Time Record Track).
/// </summary>
module BismarckGame.Core.GameState

open BismarckGame.Core.Common
open BismarckGame.Core.SearchBoard
open BismarckGame.Core.Units
open BismarckGame.Core.Markers
open BismarckGame.Core.BattleBoard
open BismarckGame.Core.VictoryConditions

/// <summary>
/// The nine phases every turn goes through, in order (rule 4.1-4.9).
/// </summary>
type Phase =
    | UnitAvailability      // 4.1
    | Visibility            // 4.2
    | ShadowDetermination   // 4.3
    | AirMovement           // 4.4
    | ShipMovement          // 4.5
    | Search                // 4.6
    | AirAttack             // 4.7
    /// <summary>Intermediate torpedo attack phase between air attack and naval combat.</summary>
    | TorpedoAttack         // 22.2 / 23.3
    | NavalCombat           // 4.8
    | Chance                // 4.9

/// <summary>
/// Each turn represents four hours of real time (rule 1.2). Night and
/// 'C' (emergency-movement) status are independent — a turn can be both
/// at once (e.g. the historical turn labeled "2000" partway through the
/// scenario is both night and 'C' — see Tables/TimeAndVisibility.fs's
/// TimeTrackEntry, which is what actually derives these two flags at
/// runtime). An earlier version of this type used a single 3-way
/// DayTurn|NightTurn|EmergencyTurn enum, which could not represent that
/// overlap and was a real correctness bug, not just a modeling nicety.
/// </summary>
type GameTurn =
    { /// Matches the physical Time Record Track's own numbering (1-42;
      /// <summary>
      /// play starts at printed turn 4 = "1200, Start" and ends at
      /// printed turn 34 = "1200, Finish" — see rule 11.23 and
      /// Tables/TimeAndVisibility.fs). NOT a from-1 engine-internal count.
      /// </summary>
      Number: int
      IsNightTurn: bool
      IsEmergencyMovementTurn: bool
      Visibility: VisibilityLevel }

/// <summary>
/// Everything one player controls.
/// </summary>
type PlayerState =
    { Nationality: Nationality
      Ships: Map<ShipId, ShipCounter>
      AirUnits: Map<AirUnitId, AirUnitCounter>
      TaskForces: Map<TaskForceId, TaskForce>
      ConvoyEscorts: ConvoyMarker list
      Score: VictoryScore }

/// <summary>
/// The complete Basic Game state at any point in play.
///
/// NOTE on hidden information (rule 2.2): each player's ship/air-unit
/// positions are secret. This type models the *authoritative* full state
/// (as an engine/server would hold it); a UI-facing "player view" that
/// redacts the opponent's unrevealed positions belongs in a separate
/// projection, not in the core domain, so the domain stays a single
/// source of truth for the FSE-style update function.
/// </summary>
type GameState =
    { Turn: GameTurn
      Phase: Phase
      SearchBoard: SearchBoardMap
      /// <summary>Zones currently affected by local fog (rule 7.31).</summary>
      FogZones: Set<GridCoordinate>
      /// <summary>
      /// Scenario-defined convoy route zones used by Chance Table convoy
      /// outcomes (rows 10-12 on the Basic Game Tables Card).
      /// </summary>
      ConvoyRouteZones: Set<GridCoordinate>
      /// <summary>
      /// Ordered convoy route path used to move independent convoy units.
      /// </summary>
      ConvoyRoutePath: GridCoordinate list
      /// <summary>
      /// Independent convoy units on the Search Board.
      /// </summary>
      ConvoyUnits: ConvoyUnit list
      /// <summary>
      /// Convoy contacts the German player has located so far.
      /// </summary>
      ConvoyContacts: ConvoyContactMarker list
      /// <summary>
      /// Total number of convoy targets available in the scenario.
      /// Rule 12.44's schedule explicitly lists the 1st..5th convoy.
      /// </summary>
      ConvoysAvailable: int
      /// <summary>
      /// Running count of convoys sunk by the German side.
      /// </summary>
      ConvoysSunkByGerman: int
      Players: Map<Nationality, PlayerState>
      ShadowMarkers: ShadowMarker list
      LocationMarkers: LocationMarker list
      /// <summary>
      /// Zero, one, or two simultaneous Battle Board actions in progress
      /// (rule 9.26: Bismarck and Prinz Eugen can be engaged separately).
      /// </summary>
      ActiveBattles: BattleBoardState list
      /// <summary>
      /// First turn any German ship was revealed (SearchZone or
      /// RollChanceForShip creating a LocationMarker) — several British
      /// Order of Battle notes (7/8/9/10/12) gate release-from-port /
      /// convoy-escort mobilization on this. Simplification: the notes
      /// distinguish "Bismarck located" from "Bismarck confirmed to have
      /// left Bergen"; this single field conflates them (see Update.fs's
      /// Mobilize handler doc comment).
      /// </summary>
      GermanLocatedTurn: int option
      /// <summary>
      /// Ships not yet on the board, entering at a specific turn/zone
      /// (Scenario.ScenarioDefinition.PendingReinforcements). Consumed
      /// (removed from this list) by AdvancePhase as turns pass it.
      /// </summary>
      PendingReinforcements: (int * ShipId * GridCoordinate) list
      GameEnded: GameEndCondition option }

/// <summary>
/// Commands a player can issue — the input to the eventual
/// `update: Command -> GameState -> GameState` reducer, kept here only as
/// a type sketch since the logic itself comes after the domain model is
/// agreed on.
/// </summary>
type Command =
    | MoveShip of ShipId * GridCoordinate
    | MoveAirUnit of AirUnitId * GridCoordinate
    /// <summary>Returns an air unit to its home base and starts its mandatory one-turn refit (rule 6.41-6.42).</summary>
    | ReturnAirUnitToBase of AirUnitId
    | SetShipMode of ShipId * ShipMode
    | SetAirUnitMode of AirUnitId * AirUnitMode
    | FormTaskForce of Nationality * ShipId list
    | BreakTaskForce of TaskForceId * ShipId
    | DeclareShadow of shadower: UnitId * target: UnitId
    | LaunchAirAttack of AirUnitId * targetShip: ShipId
    | InitiateNavalCombat of GridCoordinate * attacker: Nationality
    | FireInBattle of FireOrder
    /// <summary>Resolves the damage roll after a successful torpedo hit (rules 19.7 and 23.54).</summary>
    | ResolveTorpedoDamage of battleId: int * target: ShipId * dieRoll: int
    /// <summary>British torpedo salvo: rolls two dice for the hit table, then one damage die (errata 23.53).</summary>
    | ResolveBritishTorpedoSalvo of battleId: int * target: ShipId
    /// <summary>Launches one or more torpedo salvoes and consumes them from the firing ship.</summary>
    | LaunchTorpedoSalvo of battleId: int * firer: ShipId * target: ShipId * salvoes: int
    /// <summary>Resolves one defensive-fire opportunity against a torpedo attacker.</summary>
    | ResolveDefensiveFire of battleId: int * defender: ShipId * attacker: ShipId
    /// <summary>Replenishes torpedo salvoes while refueling in a friendly port.</summary>
    | ReplenishTorpedoes of ShipId
    /// <summary>
    /// Battle Board movement: moves `hexesMoved` hexes to `destination`,
    /// changing facing to `newFacing` after `directionChanges` turns.
    /// Validated against Tables/EvasionEffects.battleBoardMovementOptions
    /// for the ship's CURRENT (possibly damaged) evasion rating — see
    /// BattleShipState.EvasionRating, not the Search Board copy.
    /// </summary>
    | MoveShipInBattle of ShipId * hexesMoved: int * directionChanges: int * destination: HexCoord * newFacing: HexSide
    /// <summary>
    /// Rule 2.62 / 9.9x: a ship may attempt to disengage from an ongoing
    /// naval combat action. Simplification: this immediately removes the
    /// ship from the battle (treated as a successful withdrawal) — the
    /// rules' actual withdrawal-attempt resolution (opposed evasion
    /// check, being intercepted) isn't modeled.
    /// </summary>
    | WithdrawFromBattle of ShipId
    /// <summary>
    /// Ends a naval combat action: syncs each surviving ship's damage
    /// (MidshipsHits, EvasionRating) back to its Search Board ShipCounter,
    /// marks sunk ships IsSunk there too, and removes the battle from
    /// ActiveBattles.
    /// </summary>
    | EndNavalCombat of battleId: int
    /// <summary>
    /// Completes the current naval combat round: withdrawing ships make
    /// their mandatory bonus move when possible, ships that are clear of
    /// all eligible enemies leave the action, and the round number advances.
    /// Rules 9.31 and 9.94-9.97.
    /// </summary>
    | AdvanceBattleRound of battleId: int
    /// <summary>
    /// Attempts to bring a qualified ship from the battle's Search Board
    /// zone into combat. From round 3 onward, the first attempt succeeds on
    /// a 1 and each later attempt receives the additional -1 modifier
    /// required by rule 9.41.
    /// </summary>
    | AttemptBattleReinforcement of battleId: int * shipId: ShipId
    /// <summary>Attempts one atomic reinforcement roll for every ship in a task force (rule 9.4x).</summary>
    | AttemptTaskForceReinforcement of battleId: int * taskForceId: TaskForceId
    /// <summary>
    /// Rule 4.2 / Visibility Phase: rolls 2d6 against the Visibility
    /// Change Table (Tables/TimeAndVisibility.fs) and applies the shift.
    /// Skipped on turn 1 per the Sequence of Play card ("Skip 2A on the
    /// first turn") — Update.fs enforces that, not the caller.
    /// </summary>
    | RollVisibilityChange
    /// <summary>
    /// Rule 6.0/7.22: deterministic zone search — compares the searching
    /// side's search strength (day or night depending on the turn, plus
    /// any inherent coastal bonus, rule 7.27) against the current
    /// visibility level. BOTH sides do this in the Basic Game (not just
    /// British — rule 7.22 has the German player search too, after the
    /// British player's search is complete).
    /// </summary>
    | SearchZone of searcher: Nationality * GridCoordinate
    /// <summary>
    /// Rule 4.9 / Chance Table: rolled once per German ship (Bismarck,
    /// Prinz Eugen) during the Chance Phase. Which General Search column
    /// (A/B/C) applies is computed from the ship's position against the
    /// board's white-dot line and coastal zones (SearchBoard.fs) — no
    /// longer a caller-supplied input.
    /// </summary>
    | RollChanceForShip of ShipId
    /// <summary>Resolves a Huff-Duff result using the German player's chosen current or adjacent zone (rule 10.22).</summary>
    | ResolveHuffDuff of ShipId * GridCoordinate
    /// <summary>
    /// Resolve a convoy attack in the Naval Combat phase against a convoy
    /// contact in `zone`. This awards German VP per rule 12.44's convoy
    /// schedule.
    /// </summary>
    | AttackConvoy of attacker: ShipId * zone: GridCoordinate
    /// <summary>
    /// Releases a ship from a scenario-specific restriction (port lock,
    /// convoy escort, or patrol lock — British Order of Battle notes
    /// 7/8/9/10/11/12). Update.fs checks the specific condition for
    /// whichever restriction the named ship actually has; requesting this
    /// for a ship with no active restriction is a no-op, not an error.
    /// </summary>
    | Mobilize of ShipId
    | AdvancePhase
