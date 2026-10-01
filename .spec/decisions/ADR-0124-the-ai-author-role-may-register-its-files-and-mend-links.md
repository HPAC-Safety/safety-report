---
title: The ai-author role may register its files and mend links
description: The instruction-file maintainer may also edit a skill's agents/*.yaml and the Skillfile entries for the files it owns, and fix a link in any page that its own move broke.
type: adr
status: accepted
date: 2026-09-25
decision-makers: Chase Florell
keywords: agents, ai-author, skills, Skillfile, instructions, ADR-0121
---

# ADR-0124 — The ai-author role may register its files and mend links

**Status:** Accepted. **Amends**
[ADR-0121](ADR-0121-a-fifth-role-maintains-the-agent-instructions.md), which
said the role edits `AGENTS.md`, `skills/*/SKILL.md`, and `agents/*.md` "and
nothing else".

## Context

The audit in #437 found that `agents/ai-author.md` gives the role three more
permissions than ADR-0121 names:
- a skill's `agents/*.yaml`;
- the `Skillfile` entries for the files it maintains;
- fixing a link in a `docs/**` page that a move broke.

Each is part of maintaining instruction files:
- **Registration.** A skill or agent is only installed once `Skillfile` lists
  it (ADR-0086).
- **Metadata.** A skill's `agents/*.yaml` is that skill's own metadata.
- **Links.** Moving or renaming an instruction file breaks every link to it.

## Decision

**ADR-0121's scope stands, plus three edits:**
- a skill's `agents/*.yaml`;
- the `Skillfile` entries for the files the role maintains;
- a link in any page that the role's own move or rename broke, and nothing
  else in that page.

It still never edits code, specification, ADRs, lessons, runtime prompts, or
generated `.claude/` copies. It still may change how a rule is written, never
what the rule requires.

## Rejected alternatives

- **Narrowing the agent to ADR-0121's list.** A move would then leave broken
  links for another role to find, and a new skill could not be registered by
  the role that wrote it.

## Consequences

- `agents/ai-author.md` needs no change: it already states this scope.
