# ADR-0001 — Agent-agnostic configuration via AGENTS.md and skillfile

**Status:** Accepted for agent configuration. `/features` is now the product-design
authority; `AGENTS.md` routes contributors to it.

## Context

This system is built primarily by AI agents. Claude is the primary tool, but the
project must not be locked to it — switching to Codex, Copilot, or Cursor should
be a configuration change, not a migration.

Instruction files are tool-specific by convention (`CLAUDE.md`,
`.github/copilot-instructions.md`, `.cursor/rules/`), which normally means
duplicated, drifting copies of the same content.

## Decision

`AGENTS.md` at the repository root is the single canonical instruction file.
Every tool-specific path is a committed **symlink** to it.

Skills and agents are managed declaratively with
[`skillfile`](https://github.com/eljulians/skillfile): sources live under
`skills/` and `agents/`, `Skillfile.lock` pins upstream revisions, and
`skillfile install` generates `.claude/`, which is gitignored.

## Alternatives

- **Generated copies checked by CI.** Works on Windows without configuration,
  but puts duplicated content in the repository and invites edits to the wrong
  copy.
- **`AGENTS.md` alone, no symlinks.** Claude reads it natively; other tools may
  not.

## Consequences

- One file to edit; no drift possible.
- Windows contributors need `core.symlinks=true` and Developer Mode. CI asserts
  the symlinks resolve, so a broken checkout fails loudly rather than silently
  giving an agent no instructions.
- `.claude/` is reproducible from the lockfile, so it is never committed.
- Local `Skillfile` entries need **explicit names** — every source file is
  `SKILL.md`, so name inference collapses them all into one.
