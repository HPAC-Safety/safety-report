---
title: Diagrams in Markdown are Mermaid, not images
description: "Every diagram inside a Markdown file in this repository is Mermaid, written as a fenced mermaid ` code block."
type: adr
status: accepted
date: 2026-09-18
decision-makers: Chase Florell
keywords: Mermaid, diagrams, documentation, ADRs
---

# ADR-0046 — Diagrams in Markdown are Mermaid, not images

**Status:** Accepted

## Context

`AGENTS.md` already lists "Mermaid for diagrams" alongside Shouldly and
Given/When/Then as a repository-wide convention, and every ADR and spec doc
that has needed a diagram so far (ADR-0031, ADR-0040, ADR-0042, ADR-0044,
`docs/infrastructure-and-operations.md`) already uses a fenced ` ```mermaid `
block. Nothing had written the rule down as its own decision with the
reasoning behind it, so it was easy to treat as a style preference rather than
a requirement, and there was nowhere to point a reviewer who sees a PNG
architecture diagram land in a PR.

## Decision

**Every diagram inside a Markdown file in this repository is Mermaid**,
written as a fenced ` ```mermaid ` code block. No pasted image (PNG/SVG/JPEG),
no linked external diagramming tool (draw.io, Lucidchart, Excalidraw export),
and no ASCII-art diagram in a code fence, for any diagram that a Mermaid
diagram type can express (flowchart, sequence, ER, state, class, Gantt).

## Why this choice

**Diffable and reviewable in a pull request.** A Mermaid block is text; a
change to a diagram shows up as a line diff like any other content change. An
embedded image shows up as an opaque binary change a reviewer cannot inspect
without opening it separately, and a link to an external tool shows up as no
diff at all until someone follows the link.

**No external tool, no export step, no asset to commit.** Consistent with
this repository's existing bias against build-time or authoring-time
dependencies on external tools ([ADR-0023](ADR-0023-pinned-and-vendored-web-assets.md)'s
reasoning about self-hosted, committed inputs applies in spirit here too,
though this ADR is about authoring convention, not a build pin).

**Renders natively on GitHub and in most Markdown tooling.** No plugin, no
build step, no broken image link when an external host disappears.

**Agent-writable and agent-readable.** This codebase is written primarily by
agents (the same reasoning ADR-0006 gave for Tailwind and ADR-0043 gives for
React); Mermaid's syntax is plain text an agent can write and revise directly,
where a binary diagram requires a human or a separate tool in the loop.

## Alternatives

- **Whatever the author has on hand (screenshots, exported images).**
  Rejected: not diffable, not renderable everywhere, and it is the status quo
  this ADR exists to stop.
- **PlantUML.** Comparable expressiveness, but needs a rendering
  service/plugin GitHub does not support natively; Mermaid renders directly in
  GitHub's Markdown viewer with no extra step.
- **ASCII-art diagrams in a plain code fence.** No rendering, no diagram
  types beyond what fits in a monospace grid, and every edit is a manual
  re-alignment. Rejected.

## Consequences

- A PR review that sees a pasted image or an external diagram link where a
  Mermaid diagram would work treats it the same as a missing ADR or missing
  test — a gap to flag before merge, not a style nitpick.
- Diagram types Mermaid cannot express (a detailed UI mockup, a photograph, a
  screenshot of an actual error) are unaffected — this ADR only covers
  diagrams a diagramming syntax can represent.
- `AGENTS.md`'s existing "Mermaid for diagrams" line now points here for the
  reasoning.

## Related

- `AGENTS.md`
- ADR-0031, ADR-0040, ADR-0042, ADR-0044 — existing Mermaid usage this ADR
  formalizes
