Last synchronized: 2026-08-06

# BismarckGame



Version v1.0.0

F# implementation of the *Bismarck* (Avalon Hill, 1978/79) Basic Game
rules — search-board movement, shadowing, search, air attack, naval
combat, and victory conditions — built as a pure functional engine
(`Command -> GameState -> Result<GameState, string>`) with a class-based
counter taxonomy, a pluggable scenario/map system, and real data
transcribed from photographs of the physical game (Search Board, Hit
Record Pad, Battleboard, and both Basic Game Tables Cards). The project
is now targeting .NET 10 and is positioned for future SVG rendering and
Blazor WebAssembly frontends.

## AI guidance files

Repository-specific AI instructions now live in [.ai/CLAUDE.md](.ai/CLAUDE.md)
and [.ai/GEMINI.md](.ai/GEMINI.md). The root-level [CLAUDE.md](CLAUDE.md)
and [GEMINI.md](GEMINI.md) files are compatibility stubs that point to
the canonical copies in [.ai](.ai) so both newer and older tools can find
them.

## Building

This project is built and tested with the .NET 10 toolchain. Use VS Code
(Ionide or C# Dev Kit with F# support) or plain `dotnet`:

```
dotnet build BismarckGame.sln
dotnet test                      # runs BismarckGame.Tests
dotnet run --project BismarckGame.Console   # two-turn play harness
```

## Project layout

- **`BismarckGame.Core`** — the engine (this is the library described
  below in detail).
- **`BismarckGame.Tests`** — xUnit test suite.
- **`BismarckGame.Console`** — a console harness (`Program.fs`) that
  references the Core DLL and plays a couple of turns for each side,
      printing what happened. It now also demonstrates XML persistence
      (options/configuration/search-map/game-status) and optional XML event
      logging for movement + command outcomes. Meant for manual
      testing/debugging, not a real UI.

### Inside `BismarckGame.Core`

- `Common.fs`, `Dice.fs` — shared primitives and the dice-rolling
  abstraction (real + deterministic-for-tests).
- `Counters/` — a class hierarchy (`Counter` → `ShipCounter` /
      `AirUnitCounter` / `MarkerCounter` → concrete leaf types like
      `Battleship`, `AircraftCarrier`, `LongRangeReconCounter`) describing
      what KIND of piece something is, the rules-fixed properties that
      follow from that, and a stable `GraphicReference` for the printed
      counter face or future sprite mapping. Deliberately holds no board
      position — see the note in `Counters/Counter.fs`.
- `SearchBoard.fs`, `BattleBoard.fs`, `Units.fs`, `Markers.fs`,
  `VictoryConditions.fs`, `GameState.fs` — the core domain model: zones,
  hexes, ship/air-unit state, markers, scoring, and the `Command` type.
- `Tables/` — every printed chart transcribed into data: `ShipStats.fs`
  (Hit Record Pad), `AirUnitStats.fs`, `ShadowTable.fs`, `ChanceTable.fs`,
  `NavalFireTables.fs`, `BomberTables.fs`, `EvasionEffects.fs`,
  `TimeAndVisibility.fs`, plus `RulesTablesImpl.fs` wiring them into the
  engine's `IRulesTables` interface.
- `Scenario.fs` — the pluggable scenario type (`ScenarioDefinition`) and
  `initializeGame`. A scenario is just data: a `SearchBoardMap`, an order
  of battle, timed reinforcements, and a damage-point schedule. Loading a
  different map/roster is a data change, not a code change.
- `Scenarios/BismarckBasicGame.fs` — the historical 1941 scenario's data,
  transcribed from photos (zone grid, ports, white-dot reference line,
  ship/air-unit roster with real stats).
- `Update.fs` — the reducer: `update tables roll command state`.
- `PlayerView.fs` — hidden-information projection (see below).
- `Validation.fs` — sanity-checks a `ScenarioDefinition` before it's
  loaded (dangling IDs, off-board zones, etc.).
- `Configuration.fs` — typed runtime options and storage/XML settings
      (including event-logging toggle and log filename/path settings).
- `Simulation.fs` — deterministic automatic driver for full-turn command
      sequencing (used by the console harness and tests).

### Inside `BismarckGame.Console`

- `Program.fs` — simulation harness + persistence demo wiring.
- `Persistence.fs` — XML stream/file read-write for:
      - `GameOptions`
      - `AppConfiguration`
      - `SearchBoardMap`
      - game status snapshots (`GameState` DTO round-trip)
- `EventLogger.fs` — XML event logging for simulation command outcomes,
      with movement-event tagging.

## Design notes

**Pluggability.** The engine (`Update.fs`) and domain types never
reference the 1941 scenario's specific data — no hardcoded zone names,
no hardcoded ship lists outside `Scenarios/BismarckBasicGame.fs`. Adding
a second scenario (a different map, a different order of battle, more or
fewer ships/air units) means writing a new file next to
`BismarckBasicGame.fs` that builds another `ScenarioDefinition` — nothing
in `Update.fs`, `GameState.fs`, or the `Tables/` resolution logic needs
to change. `SearchBoard.fs`'s `Zone.IsWhiteDot` flag and `neighbors`/
`distanceWithin` functions are similarly generic over whatever
`SearchBoardMap` is loaded.

**Two players and hidden information.** `GameState` itself is the
authoritative, omniscient state (both sides' true positions) — that's
what `Update.fs` needs to resolve rules correctly. `PlayerView.fs` is the
redaction layer: given a `GameState` and a `Nationality`, it produces a
`PlayerView` that only shows that side's own units at their true
positions, plus the *opponent's* units only where a `LocationMarker` or
successful shadow has actually revealed them. `Players.fs` wraps that
projection in the two actual Basic Game seats: the British player
controls British and allied units, the German player controls German and
allied units, and `submitCommand` rejects commands aimed at the opponent's
ships, air units, task forces, searches, reports, or score sheet. That
player-facing layer returns `Ganfoss.ROP` `Returns<'T,string>` values, so
callers can handle errors and non-fatal warnings with Railway-Oriented
Programming while the lower-level reducer remains the existing
`Result<'T,string>` rules referee.

## Known limitations / TODO

Everything below is a real gap, not a hidden one — search the codebase
for the matching comment (usually right at the relevant `match` case) for
the full explanation.

### Data confidence
- [ ] **Fuel factor pools** are solid only for Bismarck and Tirpitz (12
      each, read from a square-on close-up photo). Every other ship's
      `FuelFactors` in `Tables/ShipStats.fs` is a rougher estimate from
      the same Hit Record Pad photo at an angle — re-photograph the FUEL
      column straight-on, ideally one ship at a time, to fix this
      properly. A web search (Aug 2026) turned up no secondary source
      with exact per-ship fuel pools — BGG listings and blog posts
      discuss the game but don't reprint the Hit Record Pad numbers.
      Confidence is now machine-readable via
      `ShipStats.fuelFactorsConfidence` (`Confirmed` for Bismarck/Tirpitz,
      `Estimated` for other fuel-tracked ships).
- [ ] **`MaxMidshipsHits`** (box counts) is confirmed only for Bismarck
      (10) and Rodney (6), both cross-checked against worked examples in
      the rules text itself. The rest are box-counts from a photo at
      lower confidence. Confidence is now machine-readable via
      `ShipStats.maxMidshipsHitsConfidence` (`Confirmed` for
      Bismarck/Rodney, `Estimated` otherwise).
- [ ] The **Shadow Table's 4th column** (`Tables/ShadowTable.fs`) is
      transcribed as data and now exposed as
      `ShadowTable.CategoryUnconfirmed4`, but it still has no confirmed
      printed letter name — the ship reference list only ever uses X/Y/Z,
      never a 4th category, and the day-letter (A/B/C) print alignment on
      the physical card doesn't resolve which of the 4 visible columns it
      is by direct pixel alignment.

### Rules not yet implemented
- [x] Reinforcement combat entry (rule 9.4x) — `AttemptBattleReinforcement`
      now handles round-3 attempts, progressive die thresholds, six-hex
      entry distance, and delayed entry.
- [x] Rodney's 9.81 stern-hit conversion and King George V/Prince of
      Wales's 9.83 gun-section reduction are implemented in battle fire.
      Rodney's 9.82 cross-section salvo-halving still requires a round-level
      fire declaration so the engine knows whether both sections fired.
- [ ] High-speed shadow (rule 8.2) is only partially modeled: shadow can
      now be declared during Ship Movement after the target's first move,
      but the specific "moved through a searched zone" condition is not
      tracked yet.

The Battle Board now validates legal movement paths, bow direction, board
limits, and occupied friendly hexes. `WithdrawFromBattle` now performs the
rule 9.93 eligibility check and marks the ship for mandatory bonus movement;
`AdvanceBattleRound` resolves successful withdrawal and advances the round.
Full reinforcements and the asterisked ship-specific fire rules remain the
next implementation slice.
- [ ] Port-combat continuation from rule 12.7 is not modeled; ending a
      battle is still caller-driven via `EndNavalCombat`.

### Known correctness caveats (working as intended, but simplified)
- [ ] Huff-Duff reveals the exact zone instead of letting the German
      player choose the zone or any adjacent one (rule 10.22).
- [ ] Withdrawal from combat (`WithdrawFromBattle`) is immediate and
      automatic rather than the real multi-round bonus-move procedure
      (rule 9.9x).
- [ ] Battle Board placement uses an *approximate* board radius
      (`BattleBoard.approximateBoardRadius = 8`) — not a transcribed hex
      count from the physical board.
- [ ] `Mobilize`'s port-release logic (notes 7/8/10) treats "a German
      ship has been located" as equivalent to "Bismarck/Prinz Eugen
      confirmed to have left Bergen" — the rule text distinguishes these
      and this project conflates them.
- [ ] Convoys now have independent Search Board units that advance along
      a scenario-defined route each turn, plus Chance-phase contact
      tracking (now linked to specific convoy-unit IDs), active-escort
      screening in Naval Combat, and sinking/VP resolution (rule 12.44).
      Remaining simplifications: convoy movement is deterministic
      one-step route advancement and escort interaction is a coarse
      screen/no-screen gate rather than a full tactical escort model.
- [ ] Naval attack-eligibility rule 9.223's "unless the defender accepts"
      branch is handled as fail-closed (no explicit accept command/model).
- [ ] Fog is treated as global `'X'` visibility for search/air/naval
      combat blocking; per-zone fog creation/decay from `TriggersFog` is
      still not represented as separate state.
- [ ] Rule 12.6 is enforced for non-friendly ports represented in current
      board data (`Port owner <> German`), but neutral-port ownership is
      not a distinct type yet (only British/German in `Nationality`).

### Not started
- [ ] Intermediate Game (submarines, destroyers, convoys-with-real-position,
      more scenarios) and Advanced Game (Range Finder, Battle Maneuver
      Gauge, miniatures-style tactical resolution) — see rule 2.83 and
      the counter sheets' UB/DD/Zeppelin sections, all currently unused.
- [ ] Any actual UI (Blazor/C# per the original plan) — this repository
      is the F# core library only.
