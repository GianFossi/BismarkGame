# CLAUDE.md

Instructions for Claude Code and Claude working in this repository.

## What this project is

This repository contains an F# rules engine for the *Bismarck* board
wargame. The engine is reducer-oriented and the current solution targets
.NET 10. The game data, scenario definitions, and printed-table
transcriptions live under [src](src), with the main engine code in
[src/BismarckGame.Core](src/BismarckGame.Core).

## Start here

1. [README.md](README.md) — architecture, build commands, and the
   authoritative TODO list.
2. [.ai/PROJECT_MEMORY.md](.ai/PROJECT_MEMORY.md) — the project journal
   for source notes, design decisions, and unresolved ambiguities.
3. [.github/copilot-instructions.md](.github/copilot-instructions.md) —
   the repo-wide coding rules that apply to every AI tool.

## Working conventions

- Verify with the real toolchain before claiming anything works. Use:
  - `dotnet restore BismarckGame.sln`
  - `dotnet build BismarckGame.sln`
  - `dotnet test src/BismarckGame.Tests/BismarckGame.Tests.fsproj`
  - `dotnet run --project src/BismarckGame.Console/BismarckGame.Console.fsproj`
- Treat the printed game components as the source of truth for data.
  When adding or changing a value, add a comment naming the rule number
  or the photographed component. If a value is uncertain, say so
  explicitly instead of presenting it as a fact.
- Keep rules enforcement in [src/BismarckGame.Core/Update.fs](src/BismarckGame.Core/Update.fs).
  Domain types and table modules should stay data-oriented rather than
  containing rule logic.
- Prefer `Result<'T, string>` over exceptions in update paths. Reserve
  exceptions for genuine programming errors.
- Keep the engine scenario-agnostic. Scenario-specific data belongs in
  [src/BismarckGame.Core/Scenarios](src/BismarckGame.Core/Scenarios), not
  in the core engine.
- Expose player-visible state through
  [src/BismarckGame.Core/PlayerView.fs](src/BismarckGame.Core/PlayerView.fs)
  rather than reading raw state directly in client-facing code.
- Add or update tests whenever changing rules, tables, or scenario data.
  The regression suite in [src/BismarckGame.Tests](src/BismarckGame.Tests)
  is the primary guardrail.
- Update [.ai/PROJECT_MEMORY.md](.ai/PROJECT_MEMORY.md) when a change
  introduces a new source note, resolves an ambiguity, or changes a
  design decision that future sessions will need to understand.

## F# golden rules for this repository

These instructions apply only when editing F# code in this repository,
especially the engine in [src/BismarckGame.Core](src/BismarckGame.Core).

- Prefer immutability by default. Use records with copy/update syntax,
  and avoid `mutable` except for a tightly scoped local loop.
- Keep the domain core pure. Avoid I/O, hidden randomness, and
  exceptions for control flow inside the engine.
- Use strong types instead of primitives where possible. Prefer single-case
  discriminated unions for domain identifiers and keep rule-derived values
  clearly typed and documented.
- Keep rule enforcement inside [src/BismarckGame.Core/Update.fs](src/BismarckGame.Core/Update.fs).
  Table modules and domain types should stay data-oriented rather than
  embedding game logic.
- Use `Result<'T, string>` or a richer domain error model for update-path
  failures; avoid ad-hoc exception-based control flow.
- Prefer small composable functions and modules over class-heavy designs.
  If a function needs a dependency, pass it in as a parameter rather than
  reaching out to the environment.
- Keep the engine scenario-agnostic and testable without mocks. Add or
  update regression tests whenever rules, tables, or scenario data change.
- Preserve documented simplifications unless the user explicitly asks to
  implement the missing rule.

## C# golden rules for UI and integration code

These instructions apply when adding or editing C# code for any future
WPF or Blazor frontend, service layer, or interop boundary.

- Keep UI and integration code thin. Put business rules in the F# core;
  let C# code orchestrate, validate inputs, and present results.
- Favor explicit state flow over hidden side effects. Prefer immutable
  view-model state and clear event/message transitions.
- Use nullable reference types and avoid `null`-based control flow in new
  code. Convert nulls at the boundary as soon as possible.
- Keep async and cancellation explicit. UI code should not block the
  thread or hide long-running work behind fire-and-forget calls.
- Separate presentation concerns from domain concerns. Views, view-models,
  and services should each own a narrow responsibility.
- Use dependency injection for services and infrastructure, but keep the
  actual rules logic in the F# core rather than duplicating it in C#.
- Prefer small, testable components. If a class is hard to test, it is
  probably doing too much or coupling itself to the UI.

## Editing expectations

- Preserve documented simplifications unless the user explicitly asks to
  implement the missing rule.
- Add English XML doc comments to public F# types, modules, records, and
  functions.
- If a change is substantial, explain the intent clearly so future
  sessions can follow the reasoning without re-deriving it.
