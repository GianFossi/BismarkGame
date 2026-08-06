Last synchronized: 2026-08-06

# GEMINI.md



Instructions for Gemini CLI / Code Assist working in this repository.

This project is an F# rules engine for the *Bismarck* board wargame.
The implementation is transcribed from photographs of the physical game
components, so tracing a number or mechanic back to its source is a
hard requirement here rather than a style preference.

## Before editing

1. Read [README.md](README.md) for the architecture, build commands, and
   the current TODO list.
2. Read [.ai/PROJECT_MEMORY.md](.ai/PROJECT_MEMORY.md) for the project
   history and the rationale behind non-obvious design decisions.
3. Follow [.github/copilot-instructions.md](.github/copilot-instructions.md)
   for the concrete coding rules that apply to this repository.

## Working rules

- Verify with the actual toolchain before assuming anything works:
  - `dotnet restore BismarckGame.sln`
  - `dotnet build BismarckGame.sln`
  - `dotnet test src/BismarckGame.Tests/BismarckGame.Tests.fsproj`
- Cite the source for any new or changed game data. If a value is
  uncertain, mark that explicitly in the code comment.
- Keep rules handling in [src/BismarckGame.Core/Update.fs](src/BismarckGame.Core/Update.fs).
  Keep data modules and types free of rule logic.
- Keep the engine scenario-agnostic; scenario data belongs in
  [src/BismarckGame.Core/Scenarios](src/BismarckGame.Core/Scenarios).
- Keep integration-side persistence/logging effects in
  [src/BismarckGame.Console/Persistence.fs](src/BismarckGame.Console/Persistence.fs)
  and [src/BismarckGame.Console/EventLogger.fs](src/BismarckGame.Console/EventLogger.fs),
  not in the core reducer.
- Prefer `Result<'T, string>` in update paths and add English XML docs to
  public F# symbols.
- Add or update tests for changes in rules, tables, or scenario data.
- Append notes to [.ai/PROJECT_MEMORY.md](.ai/PROJECT_MEMORY.md) when a
  change introduces important context for future sessions.

## F# golden rules

Apply these rules when editing F# code in this repository.

- Make immutability the default. Prefer records with copy/update syntax,
  and avoid `mutable` except in a tightly scoped local loop.
- Keep the core functional. Pure functions should be free of I/O and
  hidden side effects; let the boundary handle effects.
- Use strong types and discriminated unions rather than primitive-heavy
  or boolean-flagged models.
- Use `Result` or a richer domain error model for validation and rule
  failures; avoid exception-based control flow in the engine.
- Keep modules and functions composable and small. Prefer passing
  dependencies as parameters over reaching across the system.
- Prefer the F# core as the single authority for game rules and state
  transitions; avoid duplicating rules in higher layers.

## C# golden rules

Apply these rules when editing C# code for UI, services, or interop.

- Keep the UI layer thin and presentation-focused. Put rules in the F#
  core rather than in C# view classes.
- Favor explicit state flow and nullable-aware code. Avoid `null`-based
  control flow and hidden side effects.
- Keep async and cancellation explicit, especially in UI and service code.
- Separate responsibilities clearly between views, view-models, services,
  and infrastructure adapters.
- Prefer small, testable classes and dependency injection over large
  monolithic implementations.
