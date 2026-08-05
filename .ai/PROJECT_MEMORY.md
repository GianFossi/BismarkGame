# Project Memory — BismarckGame

A running journal of what has been done on this project and why, written
for AI assistants (and the human maintainer) picking this up in a fresh
session. This is not a changelog of commits — it's a record of
*reasoning*: which photo produced which number, which rule text resolved
which ambiguity, and which design choices were made deliberately versus
which are placeholders.

**Append to this file as the project continues. Don't rewrite or delete
past entries** — if something described here turns out to be wrong,
add a correction entry, don't erase the record of the mistake; future
sessions benefit from knowing what was tried and revised.

---

## 1. Origin and sources

The project implements the Basic Game rules of *Bismarck* (Jack Greene
Jr., Avalon Hill, 1978/79 — a "thorough update" of the original 1962
Avalon Hill game of the same name). There is no clean digital rules
reference. Every game-data value in this codebase traces back to one of:

- **A PDF of the 1979 rules manual**, first found via Scribd
  (`623262298-Bismarck-Rules-1979.pdf`, converted to text with
  `pdftotext -layout`) and later cross-checked against a cleaner OCR
  found at `http://www.hexagonia.com/rules/Bismarck.pdf` (this second
  source filled a real gap — Section 5.2, Fuel Allotment, was
  essentially unreadable in the Scribd copy and came entirely from the
  hexagonia PDF).
- **Photographs taken by the user** of physical game components:
  the Search Board (multiple photos, different angles/sharpness — the
  sharper later photos corrected several errors from the first pass),
  the Battle Board (hex grid + printed Naval Fire/Special Damage
  tables), the Bismarck Hit Record Pad (ship stats: evasion, gun
  salvoes, midships boxes, fuel boxes, torpedo factors), the German and
  British Basic Player Aid Cards (Time Record Track, Visibility Track,
  Order of Battle with starting zones and release-condition notes), the
  Basic Game Tables Card (Sequence of Play, Shadow Table, Chance Table,
  Evasion Repair Table, Movement-vs-Evasion tables), and physical
  counter sheets sourced from BoardGameGeek image pages (delivered as
  saved `.mhtml` files, which had to be parsed with Python's `email`
  module to extract the embedded base64 images before they could be
  viewed).
- **One web search** (August 2026) looking for a secondary source on
  per-ship fuel-factor pool sizes, since the Hit Record Pad photo could
  only be read confidently for two ships. It found general
  discussion/retrospectives of the game (Wikipedia, BGG listings, a
  "Bismarck Tactical Values" blog analysis) but no reprint of the actual
  Hit Record Pad fuel numbers. That gap is still open — see README.md.

## 2. Chronology of major milestones

1. **Domain model v1** — `Common.fs`, `SearchBoard.fs`, `Units.fs`,
   `Markers.fs`, `BattleBoard.fs`, `VictoryConditions.fs`, `GameState.fs`
   sketched from the rules text alone, before any board photo was
   available. Adjacency was originally modeled as a hand-authored list
   per zone; later replaced with a computed `neighbors` function once it
   became clear the board is a regular orthogonal grid (irregular only
   at its edges/land gaps), not an irregular graph.

2. **First Search Board transcription** — from an angled, somewhat blurry
   photo. Adjacency, port locations, and the Irish Sea zones were
   estimated. This pass had real errors, corrected in milestone 4
   below once sharper photos arrived — notably the Irish Sea was
   originally guessed as a wide band of zones; it's actually just three
   zones (L20/M20/N20), the narrow strait between Eire and Great
   Britain.

3. **Basic Game tables (first pass)**: Naval Fire and Special Damage
   tables transcribed from a clear photo of the physical Battle Board
   (both sides' printed tables are numerically identical — one table
   serves both nationalities). This gave `Tables/NavalFireTables.fs`
   real, high-confidence data essentially from the start.

4. **Search Board re-transcription** from sharper, straight-on photos.
   Corrected the Irish Sea zones, fixed several row column-ranges that
   were cut off or distorted in the first photo's angle, and discovered
   the board's "white dot" reference line (a diagonal run of 24 marked
   zones from E3 to Z22, with a 3-zone widening at K9/K10/K11) used by
   the Chance Table's General Search column selection (rule text: "in or
   within two zones of a zone with a white dot"). Distinguished this
   line from the separately-printed "Atlantic Convoy" (row H, horizontal
   dashes) and "Africa Convoy" (long diagonal dashes) routes, which are
   Intermediate Game features and not adjacency- or search-related.

5. **Basic Game Tables Card photographed**: Shadow Table, Chance Table,
   Evasion Repair Table, and the two Evasion-Level-vs-Movement tables
   (Search Board and Battle Board) all transcribed with real values.
   The Shadow Table has one unresolved wrinkle: the printed grid shows 4
   result columns with a clean "lose contact at die 3/4/5/6" staircase,
   but the day-letter (A/B/C) header row is printed visually offset from
   the night-letter (X/Y/Z) row (apparently because the "DAY (arrow)"
   prefix text pushes it right), and the reference list of which ship
   uses which category only ever uses X/Y/Z, never a 4th letter. The
   4th column's identity was never resolved — see README.md.

6. **Hit Record Pad photographed and read closely**: real evasion
   ratings, gun-section salvo counts (bow/stern/port/starboard), and
   torpedo factors for all 28 ships on the pad (not just the Basic Game
   roster — kept as a scenario-agnostic table). Two additional passes
   over the same photo later extracted MaxMidshipsHits (box counts,
   confirmed against rule 9.714's two worked examples: Bismarck=10,
   Rodney=6) and FuelFactors (box counts, confirmed only for Bismarck
   and Tirpitz=12 each from a tight square-on crop; everything else is a
   lower-confidence estimate from the same photo at an angle).

7. **Counter sheets** (BoardGameGeek `.mhtml` pages + two directly
   embedded images) gave real Search Strength (uniformly "1-1" for every
   Basic Game surface ship, no exceptions) and MaxSpeedZones (2 for
   almost every ship; 1 specifically for Rodney, Nelson, Ramillies,
   Revenge, Eagle) — filling in fields that had been TODO=0 placeholders
   until this point. Also revealed the full unit taxonomy (ship classes,
   air unit types, markers, and their front/back counter faces), which
   became the basis of the `Counters/` class hierarchy.

8. **`Counters/` class hierarchy added** — an F# class-based taxonomy
   (`Counter` -> `ShipCounter`/`AirUnitCounter`/`MarkerCounter` -> concrete
   leaf types) requested explicitly to use real inheritance, kept
   deliberately free of board-position state so it stays valid
   regardless of which scenario/map is loaded. This is descriptive
   taxonomy, NOT the mutable simulation state — that's still
   `Units.fs`/`Markers.fs`/`GameState.fs`'s records. The two systems
   are related but intentionally not merged.

9. **Engine built incrementally in `Update.fs`** across many sessions:
   phase sequencing, movement legality, task forces, shadowing (wired to
   the real Shadow Table only after fixing an interface signature
   mismatch — the original `IRulesTables.ResolveShadow` took evasion
   ints, but the real table keys off the shadowing unit's NAME), naval
   fire, air attack (wired to `Tables/BomberTables.fs`), Search Phase
   (discovered mid-project that rule 7.22 has the GERMAN player also
   search deterministically, not just the British — an early version of
   `SearchZone` was wrongly British-only), Chance Phase, victory
   condition detection, port-lock/convoy-escort release conditions
   (British Order of Battle notes 7-12), timed reinforcements (Revenge,
   Dorsetshire), and Battle Board hex placement (defender-center,
   attacker rolls an edge — rule 9.28).

10. **A second read of the hexagonia.com rules PDF** (prompted by the
    user asking to research fuel data, since the first Scribd PDF's
    Section 5.2 was unreadable) revealed the full fuel-allotment rules
    (5.21-5.29) AND several damage-modeling rules that had been
    oversimplified up to that point:
    - Ships sink by filling every midships box (rule 9.714), not only
      via the Naval Fire table's explicit "Sunk" result — added
      `MaxMidshipsHits` and a fill-the-track sink check.
    - EVERY midships hit reduces evasion by a class-specific amount
      (Bismarck -1, other BB/CV -2, Prinz Eugen -3, cruisers -5; rules
      9.723-9.726), separate from and in addition to any PERMANENT
      reduction a Special Damage table result specifies (rule 9.722).
      Added `PermanentEvasionLoss` to distinguish repairable (temporary)
      from unrepairable (permanent) evasion damage.
    - Evasion repair is only attemptable in a turn the ship moved <=1
      zone (rule 9.728) — an unconditional-every-turn repair was wrong.
    - Rule 7.22: the German player ALSO performs deterministic zone
      search (see milestone 9) — found while re-reading for fuel.
    - Real fuel-cost rules per rule 5.2x, including the actual breakout
      bonus cost structure (turn 1: zones 1-3 free, 4th zone = 1 factor,
      5th = another factor — rule 5.28), replacing an earlier version
      that granted the larger zone allowance without any fuel cost.
    - Discovered the Time Record Track's real numbering: play starts at
      the card's printed turn 4 ("1200, Start"), not turn 1 (rule
      11.23) — `GameTurn.Number` was renumbered to match the physical
      card instead of an internal from-1 count.
    - Discovered turns can be simultaneously night AND a 'C'
      (emergency-movement) turn (e.g. printed turn 12) — the original
      `TurnLabel` type was a single 3-way enum (Day/Night/Emergency)
      that couldn't represent that overlap. Replaced with two
      independent booleans (`IsNightTurn`, `IsEmergencyMovementTurn`) on
      `GameTurn`, both derived from `Tables/TimeAndVisibility.fs`'s
      per-turn data rather than guessed.

11. **Testing/tooling pass** (most recent): `Validation.fs` (scenario
    sanity-checking — dangling ids, off-board coordinates),
    `PlayerView.fs` (hidden-information projection for real multiplayer
    — `GameState` itself stays omniscient/authoritative, this is the
    redaction layer a client should actually render from), an xUnit test
    project (`BismarckGame.Tests`) covering the data tables and the
    engine's movement/fuel/search/phase logic against a small synthetic
    3x3 test board (kept independent of the ~200-line real 1941 roster
    so unit tests are easy to reason about by hand), and a console
    harness project (`BismarckGame.Console`) that plays two turns
    end-to-end for debugging. Full VS Code tooling
    (`.vscode/tasks.json`/`launch.json`/`settings.json`), `.editorconfig`,
    `cspell.json`, git hygiene files, and this AI-instructions structure
    were added in the same pass. XML `<summary>` doc-comment wrapping
    was applied mechanically across all 48 `.fs` files (a Python script
    wrapped existing `///` comment blocks that precede a
    type/module/let/member/DU-case/record-field in `<summary>` tags,
    rather than hand-writing thousands of new doc comments) — spot-check
    a few files after any further large-scale edit to make sure new code
    still gets proper doc comments, since the mechanical pass only ran
    once.

  12. **Correction note** — the earlier note in `Update.fs`'s module
    summary that Search Table / Chance Table / Air Attack were not yet
    wired is stale. The current reducer already handles `SearchZone`,
    `RollChanceForShip`, and `LaunchAirAttack`; the flowcharts in
    `docs/flowcharts/` are the canonical read-through for those
    branches. Keep future doc updates aligned with the actual reducer,
    not the pre-flowchart summary text.

## 3. Deliberate simplifications (not bugs)

See README.md for the full, actively-maintained list. A few WHYs that
are easy to lose over time:

- **Battle Board placement uses an unmeasured `approximateBoardRadius`**
  (8 rings) — nobody has counted the physical board's actual hex extent
  from a photo; this is a functional stand-in so placement logic has
  *some* bound, not a transcribed fact.
- **`PlayerView`'s battle redaction shows enemy ship class but not full
  stats even mid-combat** — rule 9.17 mentions the defender places
  counters "face down" until damage forces a reveal, which suggests the
  real information rules during combat are richer than what's modeled;
  this project picked the simpler "always show class, never show name/
  stats" rule uniformly rather than model the face-down/reveal nuance,
  and says so in `PlayerView.fs`'s doc comment.
- **`nearBritishOrIrishCoast` (Chance Table column selection) uses
  "is a friendly Port zone" as a proxy** for "is a coastal zone" (rule
  7.27 names specific geographic features — Faeroe/Shetland/Irish
  coast/GB coast/Hvalfiord — not just the 5 port zones). This
  under-counts true coastal zones but doesn't fabricate strength in
  zones with no coastal feature at all.

## 4. Open questions worth resurfacing if new source material appears

- A full-resolution, straight-on photo of the Hit Record Pad's FUEL
  column, ideally one ship at a time, would let the fuel-pool estimates
  in `Tables/ShipStats.fs` be corrected from "estimate" to "confirmed"
  for every ship, not just Bismarck/Tirpitz.
- A clean, well-lit, straight-on photo of the Shadow Table specifically
  would resolve the 4th-column identity question in
  `Tables/ShadowTable.fs`.
- A photo of the physical Battle Board's actual edge/hex count would let
  `BattleBoard.approximateBoardRadius` become a real transcribed value.
- Rules 9.221-9.224 (naval combat attack eligibility), 9.16 (air attack
  frequency limits), 9.4x (reinforcement combat entry), 9.81-9.84
  (Rodney/KGV/PoW/cruiser special fire rules) are all TEXT the project
  already has (from the hexagonia.com PDF) but hasn't yet turned into
  `Update.fs` logic — this is implementation backlog, not a missing
  source.
