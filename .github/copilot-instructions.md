Last synchronized: 2026-08-06

# GitHub Copilot instructions for BismarckGame



This repository is an F# implementation of the *Bismarck* (Avalon Hill,
1978/79) board wargame's Basic Game rules. Read `README.md` first for the
architecture overview, then `.ai/PROJECT_MEMORY.md` for the detailed
history of how the rules were transcribed and why specific design
choices were made — a lot of non-obvious decisions in this codebase
trace back to a specific photo of a specific printed table, and that
context matters when you're asked to extend or fix something.

## Ground rules for suggestions in this repo

1. **Always verify with the toolchain.** This repository builds/tests on
  .NET 10, but treat compiler/test output as authoritative over comments
  or stale docs when they disagree.
2. **Every rule-derived number or table must cite its source** in a
   comment — the manual section (e.g. "rule 9.714"), or the photographed
   component ("Hit Record Pad", "Search Board Ship Counter"). Do not
   invent plausible-looking game data. If a value is uncertain, say so
   explicitly in a comment rather than presenting a guess as fact — see
   `Tables/ShipStats.fs` for the established pattern (confirmed vs.
   estimated values, both clearly labeled).
3. **`Update.fs` is the only place game rules get enforced.** Domain
   types (`GameState.fs`, `Units.fs`, `BattleBoard.fs`, etc.) should stay
   free of rule logic — they're data shapes. `Tables/` holds transcribed
   chart data and pure lookup functions, not stateful logic.
4. **Prefer `Result<'T, string>` over exceptions** for anything reachable
   from `Update.update`. Exceptions are for genuine programming-error
   invariant violations only (see `unusedTables` in
   `BismarckGame.Tests/TestHelpers.fs` for the one deliberate
   `failwith`-on-purpose pattern in this codebase).
5. **XML doc comments (`/// <summary>...</summary>`) are required** on
   every public type, module, DU case group, record, and function. Write
   them in English. If you can't explain a design choice in the doc
   comment without hedging, that's a signal the choice needs a TODO note
   in `README.md` instead of confident-sounding prose.
6. **Never assume a scenario detail is universal.** The engine
   (`Update.fs`, `GameState.fs`) must stay agnostic of the 1941
   historical scenario's specifics — ship names, zone coordinates, board
   size. Scenario-specific data belongs in `Scenarios/*.fs`, referenced
   through `Scenario.ScenarioDefinition`, not hardcoded into the engine.
7. **Hidden information**: never suggest code that has a real multiplayer
   client read directly from `GameState`. `PlayerView.project` is the
   only sanctioned way to expose state to a specific side.
8. When adding a new resolution table or scenario data file, **add or
   update the matching test** in `BismarckGame.Tests/` — see
   `TablesTests.fs` for the pattern of pinning specific printed values.

## F# golden rules for this repository

These rules apply when editing F# code in the engine or other F# modules.

- Make immutability the default. Prefer records with copy/update syntax
  and avoid `mutable` except for tightly scoped local loops.
- Keep the core pure. No hidden I/O, hidden randomness, or
  exception-based control flow in the domain engine.
- Use strong types and discriminated unions for domain concepts instead of
  primitive-heavy or boolean-flagged models.
- Keep rule enforcement in `Update.fs` and keep data modules focused on
  facts and lookups rather than logic.
- Prefer small composable functions and modules over class-heavy designs.
  Pass dependencies in as parameters rather than reaching to the
  environment.
- Use `Result<'T, string>` or a richer domain error model for rule and
  validation failures. Preserve source citations and documented
  simplifications when touching game data.

## C# golden rules for UI and integration code

These rules apply when editing C# code for a frontend, service layer, or
interop boundary.

- Keep UI and integration code thin. Put business rules in the F# core;
  let C# orchestrate and present.
- Favor explicit state flow, nullable-aware code, and clear async/cancel
  handling.
- Separate presentation, view-model, and service concerns. Avoid mixing
  rendering logic with domain logic.
- Prefer dependency injection for services and infrastructure while keeping
  the actual rules logic in the F# core rather than duplicating it in C#.

## What NOT to do

- Don't "helpfully" fill in the uncertain fuel-factor values in
  `Tables/ShipStats.fs` with round numbers to make the table look more
  complete — see `README.md`'s TODO list for what's confirmed vs.
  estimated, and leave the distinction visible.
- Don't collapse the `IsNightTurn` / `IsEmergencyMovementTurn` two-flag
  design in `GameState.GameTurn` back into a single enum — that was a
  real bug fix (a turn can be both simultaneously), not a stylistic
  choice.
- Don't add a UI or a `Program.fs` outside `BismarckGame.Console` without
  checking `.ai/PROJECT_MEMORY.md` — a Blazor/C# UI layer is planned but
  not started, and there may be newer decisions about it than this file.
