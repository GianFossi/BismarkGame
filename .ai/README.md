# `.ai/`

This folder holds context for AI coding assistants working on this
repository, kept separate from the tool-specific root files
(`AGENTS.md`, `.github/copilot-instructions.md`) so there's one shared
place for anything not specific to a single tool. The tool-specific
instructions now live in `.ai/CLAUDE.md` and `.ai/GEMINI.md`.

- **`PROJECT_MEMORY.md`** — the project's narrative history: what was
  transcribed from which photo/PDF, what design decisions were made and
  why, what's confirmed vs. estimated. Read this before making any
  change that touches game rules or data — most non-obvious choices in
  this codebase have a reason recorded there. Append to it as the
  project continues; don't rewrite past entries.

If you add tool-specific instruction files in the future (e.g. a
`.cursor/rules` file, a `.windsurfrules` file), point them at
`PROJECT_MEMORY.md` and `.github/copilot-instructions.md` rather than
duplicating their content, so the project's conventions don't drift out
of sync across tools. For Claude/Gemini, the repo-local instruction files
are `.ai/CLAUDE.md` and `.ai/GEMINI.md`.
