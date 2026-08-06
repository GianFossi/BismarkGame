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

This project has **never been compiled** — it was written in a sandbox
with no .NET toolchain, entirely by static reasoning about the F#. Build
it in VS Code (with the Ionide extension or the C# Dev Kit's F# support)
or plain `dotnet build` before trusting any of it. Expect at least a few
compile errors on the first pass given the size of the codebase; please
report them back so they can be fixed.

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
  printing what happened. Meant for manual testing/debugging, not a real
  UI.

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

**Hidden information.** `GameState` itself is the authoritative,
omniscient state (both sides' true positions) — that's what `Update.fs`
needs to resolve rules correctly. `PlayerView.fs` is the redaction layer:
given a `GameState` and a `Nationality`, it produces a `PlayerView` that
only shows that side's own units at their true positions, plus the
*opponent's* units only where a `LocationMarker` or successful shadow has
actually revealed them. This is what a real multiplayer client should
render — never the raw `GameState`.

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
- [ ] **`MaxMidshipsHits`** (box counts) is confirmed only for Bismarck
      (10) and Rodney (6), both cross-checked against worked examples in
      the rules text itself. The rest are box-counts from a photo at
      lower confidence.
- [ ] The **Shadow Table's 4th column** (`Tables/ShadowTable.fs`) is
      transcribed as data but has no confirmed letter name — the ship
      reference list only ever uses X/Y/Z, never a 4th category, and the
      day-letter (A/B/C) print alignment on the physical card doesn't
      resolve which of the 4 visible columns it is by direct pixel
      alignment. `categoryOf` doesn't expose it.

### Rules not yet implemented
- [ ] Reinforcement combat entry (rule 9.4x) — ships arriving as
      reinforcements mid-battle, the 6-hexes-away placement rule, and the
      "roll for reinforcement" die mechanic starting round 3.
- [ ] Rodney's special stern-turret rules (9.81/9.82) and King George
      V/Prince of Wales's random gun-section-disable rule (9.83) — the
      Hit Record Pad data captures the *asterisked stats* but the
      per-round resolution logic for those asterisks isn't implemented.
- [ ] High-speed shadow (rule 8.2) is only partially modeled: shadow can
      now be declared during Ship Movement after the target's first move,
      but the specific "moved through a searched zone" condition is not
      tracked yet.
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
- [ ] Convoys have no board position of their own (only a `ConvoyMarker`
      tying a marker to an escorting ship) — Chance Table convoy-location
      results are structural no-ops.
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
