---
name: hpac-safety-conventions
description: Cross-cutting repository conventions for HPAC safety-report, including Mermaid diagrams, .NET boundaries, date/time types, runtime prompts, generated artifacts, and focused-skill routing. Use before writing or reviewing any code, test, documentation, or diagram in this repository.
---

# Apply the repository baseline

Read `AGENTS.md` first. Its safety invariants outrank this skill and every task.
Then load the more focused skill named in its routing table.

## Write diagrams and code

- Use Mermaid for every diagram in Markdown. Never use ASCII or box-drawing art.
- Target .NET 10 with file-scoped namespaces, nullable enabled, and warnings as
  errors.
- Keep `HpacSafety.Core` dependency-free. Declare ports there and implement
  external integrations in `HpacSafety.Infrastructure`.
- Put third-party production libraries behind owned ports and adapters. Do not
  wrap the .NET BCL, test-only libraries, or EF Core's `DbContext`/`DbSet`.
- Use `DateOnly` for dates, `TimeOnly` for local wall-clock times, and
  `DateTimeOffset` for instants. Never use `DateTime`; it is banned by the
  repository analyzer. Convert vendor `DateTime` values inside their adapter.
- Store domain values as invariant codes and localize only at the edge.

Load upstream `ddd`, `dotnet-best-practices`, and `csharp-async` as applicable.
Load [`solid-principles`](../solid-principles/SKILL.md) for boundaries and
[`gang-of-four-patterns`](../gang-of-four-patterns/SKILL.md) before adding
indirection.

## Keep runtime prompts separate

Treat `prompts/` as versioned product assets sent to the model processing real
reports. Bump a prompt version instead of editing one in place. Keep agent
instructions under `skills/`; never move redaction policy out of `prompts/`.

## Regenerate owned artifacts

Never hand-edit:

| Artifact | Owner |
|---|---|
| `docs/form-spec.md` | `tools/extract-typeform.py` |
| `locales/fr-CA.json` | `tools/translate-locale.mjs` in CI |
| `.claude/skills/`, `.claude/agents/` | `skillfile install` |
| `src/web/styles/site.css` | `tools/build-css.sh` |

Load [`deliver-hpac-change`](../deliver-hpac-change/SKILL.md) before committing
or opening a pull request.
