# AGENTS.md

Generic instructions for AI coding agents (OpenAI Codex, and any tool
that reads `AGENTS.md`) working in this repository. See also
`.ai/CLAUDE.md`, `.ai/GEMINI.md`, and `.github/copilot-instructions.md`
— this file and those are kept in sync in spirit; if they ever visibly
disagree, `.ai/PROJECT_MEMORY.md` has the most recent decision.

## Project summary

F# engine implementing the *Bismarck* (Avalon Hill, 1978/79) board
wargame's Basic Game rules, transcribed from photographs of the physical
components. No clean digital rules source exists — every non-obvious
number or mechanic in this codebase should trace back to a cited rule
number or photographed component in a nearby comment.

## Setup

```
dotnet restore BismarckGame.sln
dotnet build BismarckGame.sln
```

This code has **never been successfully compiled** as of this file's
writing — expect to fix real build errors on first contact, not just
style nits.

## Test

```
dotnet test BismarckGame.Tests/BismarckGame.Tests.fsproj
```

Add tests alongside any change to `BismarckGame.Core/Update.fs` or
`BismarckGame.Core/Tables/*.fs` — see `BismarckGame.Tests/TablesTests.fs`
for the pattern of pinning specific printed table values so future edits
can't silently break them.

## Code map

- `BismarckGame.Core/Update.fs` — the only place rules are enforced
  (`Command -> GameState -> Result<GameState, string>`).
- `BismarckGame.Core/Tables/` — transcribed printed-chart data, pure
  lookups only, no state mutation.
- `BismarckGame.Core/Scenarios/BismarckBasicGame.fs` — the one concrete
  scenario's data; the engine itself must stay scenario-agnostic (see
  `Scenario.fs`'s `ScenarioDefinition`).
- `BismarckGame.Core/PlayerView.fs` — the hidden-information projection;
  a real client renders from this, never from raw `GameState`.
- `BismarckGame.Console/Program.fs` — a debugging harness, not a UI.

## Rules for changes

1. Cite a source (rule number / photographed component) for any new
   game data; mark uncertain values as uncertain in the comment.
2. Every public type/module/function needs an English
   `/// <summary>...</summary>` doc comment.
3. Prefer `Result<'T, string>` over exceptions in anything reachable
   from `Update.update`.
4. Update `README.md`'s TODO list and `.ai/PROJECT_MEMORY.md` when you
   resolve, discover, or deliberately simplify something rules-related.
5. Read `.ai/PROJECT_MEMORY.md` before assuming a design choice is
   arbitrary — most aren't.
