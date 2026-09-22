---
status: accepted
date: 2026-09-22
decision-makers: Chase Florell
keywords: editorconfig, formatting, accessibility, indentation, tabs
---

# ADR-0075 — Tabs, not spaces, for indentation

**Status:** Accepted.

## Context

The repository indented with spaces: two for most files, four for C#,
`.csproj`, `.props`, and `.targets`, pinned in `.editorconfig`. A space is a
fixed visual width for every reader. A screen-magnifier or low-vision user who
widens their editor's rendered tab stop cannot get the same relief from a run
of space characters — the indent stays whatever width it was typed at. A tab
character carries no width of its own; each reader's editor renders it at
whatever stop they've set, so the same file reads comfortably at a narrow
stop for one person and a wide one for another, without changing a single
byte of the file.

## Decision

`.editorconfig`'s `[*]` section sets `indent_style = tab`. `tab_width` is set
per group to match the widths already on screen — `2` for the general
section, `4` for `[*.{cs,csproj,props,targets}]` — so nothing shifts visually
for anyone who hasn't changed their own tab-stop setting. `indent_size` is
left unset where a tab now stands in for it, since a tab is one character
however wide it renders.

### Two exceptions, both syntactic, not stylistic

- **YAML** (`*.yml`, `*.yaml`): indentation is significant to the format, and
  the YAML spec forbids tab characters in indentation outright. A tab here is
  a parse error, not a style choice.
- **Gherkin** (`*.feature`): the parser Reqnroll uses rejects a tab in a
  feature file's indentation the same way.

Both keep `indent_style = space` with their existing width. Terraform,
covered by `terraform fmt`, is unaffected either way — that formatter always
writes spaces and has no tab mode, so there is nothing for `.editorconfig` to
override there.

### Scope of this change

This ADR and the accompanying reformat cover C#, `.csproj`/`.props`/
`.targets`, and the general `.editorconfig` default. Markdown, JSON,
Terraform, and the `src/web` static site were not reformatted here — each has
its own tooling (or none) and its own migration cost, and forcing tabs onto a
`terraform fmt`-owned file would just have the formatter fight this decision
on every run. Bringing another file family under `indent_style = tab` is a
follow-up, done through whatever formatter already owns that family, not a
retroactive amendment to this record.
