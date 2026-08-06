/// <summary>
/// Units.fs
/// Ship and air unit counters as they exist on the Search Board.
/// Source: rules 2.42, 2.43, 5.x, 6.x.
/// </summary>
module BismarckGame.Core.Units

open BismarckGame.Core.Common
open BismarckGame.Core.SearchBoard

/// <summary>
/// A ship counter (rule 2.42). Properties are the union of everything
/// printed on the counter plus the mutable state needed to play a turn.
/// </summary>
type ShipCounter =
    { Id: ShipId
      Name: string
      Nationality: Nationality
      Class: ShipClass
      /// <summary>
      /// Ship's maximum speed in knots — used to resolve shadowing/
      /// withdrawal attempts (rule 2.426). NOT the same as MaxSpeedZones.
      /// </summary>
      EvasionRating: int
      /// <summary>
      /// Undamaged evasion rating (Tables/ShipStats.fs) — the ceiling the
      /// Evasion Repair Table (rule: "attempt to repair temporary evasion
      /// rating damage") restores toward; EvasionRating itself is what
      /// combat damage reduces and repair increases, always clamped to
      /// this ceiling.
      /// </summary>
      MaxEvasionRating: int
      /// <summary>
      /// Greatest number of zones the ship can move in one turn (0, 1 or 2)
      /// (rule 2.427).
      /// </summary>
      MaxSpeedZones: int
      SearchStrength: SearchStrength
      /// <summary>
      /// Aircraft carriers have no patrol face (rule 2.423).
      /// </summary>
      CanPatrol: bool
      Mode: ShipMode
      CurrentZone: GridCoordinate option
      /// <summary>
      /// Only tracked for battleships/carriers with MaxSpeedZones = 2;
      /// cruisers are fuel-exempt in the Basic Game (rule 5.21).
      /// </summary>
      Fuel: FuelState option
      /// <summary>Unspent torpedo salvoes; zero for ships without torpedoes.</summary>
      TorpedoesRemaining: int
      TaskForce: TaskForceId option
      IsConvoyEscort: bool
      /// <summary>
      /// Zones already moved this turn (reset at Unit Availability phase).
      /// Needed because max speed is derived from CURRENT evasion rating
      /// (Tables/EvasionEffects.fs), not a fixed per-ship constant — a
      /// damaged ship's allowance can shrink mid-game, so "did this ship
      /// already use its move(s)" has to be tracked, not just checked
      /// against a static field.
      /// </summary>
      ZonesMovedThisTurn: int
      /// <summary>
      /// Persistent midships-hit count, synced from BattleBoard.BattleShipState
      /// when a naval combat action concludes (Update.fs's EndNavalCombat) —
      /// this is what Victory Conditions rule 12.5 ("damage inflicted on
      /// ships still afloat") actually scores from.
      /// </summary>
      MidshipsHits: int
      /// <summary>
      /// Number of midships boxes on the Hit Record Pad — a ship sinks
      /// when MidshipsHits reaches this (rule 9.714: "a ship is not sunk
      /// until every midships box has been marked out"), independent of
      /// the Special Damage table's explicit "Sunk" result. Best-effort
      /// box count from a photo of the pad — see Tables/ShipStats.fs.
      /// </summary>
      MaxMidshipsHits: int
      /// <summary>
      /// Rule 9.722: some hit results say "reduce evasion rating by N"
      /// — that reduction is PERMANENT and never repaired. Tracked
      /// separately from EvasionRating (which the Evasion Repair Table
      /// restores) so repair can correctly cap at MaxEvasionRating minus
      /// this, not at the full MaxEvasionRating.
      /// </summary>
      PermanentEvasionLoss: int
      /// <summary>
      /// British Order of Battle notes 7/8/10: some ships may not leave
      /// port until a release condition is met (Bismarck/Prinz Eugen
      /// leaving Bergen for KGV's task force and Repulse; the 4th turn
      /// after Bismarck is located for Force H). True at scenario start
      /// for those specific ships; cleared by the Mobilize command once
      /// the caller has verified the condition (Update.fs checks it
      /// again independently — see Mobilize's handler).
      /// </summary>
      IsLockedInPort: bool
      /// <summary>
      /// British Order of Battle note 11 (Edinburgh): must stay on patrol
      /// until a German ship is discovered within 10 zones. Separate from
      /// IsLockedInPort because the release condition is different
      /// (distance-based, not turn-count-based) and from ShipMode.Patrol
      /// because that's the printed counter face (movement vs patrol
      /// silhouette), not a scenario-specific restriction.
      /// </summary>
      IsRestrictedToPatrolUntilContact: bool
      IsSunk: bool }

/// <summary>
/// Where an air unit operates from (rule 2.438).
/// </summary>
type HomeBase =
    | CarrierBase of ShipId
    | LandBase of name: string

/// <summary>
/// An air unit counter (rule 2.43).
/// </summary>
type AirUnitCounter =
    { Id: AirUnitId
      Name: string
      Nationality: Nationality
      UnitType: AirUnitType
      Mode: AirUnitMode
      SearchStrength: SearchStrength
      /// <summary>
      /// Number of turns the unit can remain airborne (rule 2.436).
      /// </summary>
      EnduranceRating: int
      /// <summary>
      /// Turns already spent aloft this sortie — compared against
      /// EnduranceRating to force a return-to-base (rule 6.2x).
      /// </summary>
      TurnsAirborne: int
      /// <summary>
      /// Rule 9.16: attacks launched in the current turn. British bombers
      /// may attack at most twice per day turn; German bombers at most
      /// once per day turn; no air attacks at night.
      /// </summary>
      AirAttacksLaunchedThisTurn: int
      MaxSpeedZones: int
      HomeBase: HomeBase
      CurrentZone: GridCoordinate option
      /// <summary>
      /// True while resting/refitting at base after a sortie (rule 6.14).
      /// </summary>
      IsAtBase: bool }

    /// <summary>
    /// LR recon units can never attack (rule 2.434); only bombers in
    /// BomberAttack mode can (rule 6.31).
    /// </summary>
    member this.CanAttack =
        match this.UnitType, this.Mode with
        | (TorpedoBomber | LevelBomber), BomberAttack -> true
        | _ -> false
