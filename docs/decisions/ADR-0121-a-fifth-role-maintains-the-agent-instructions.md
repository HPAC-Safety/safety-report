---
title: A fifth role maintains the agent instructions
description: An ai-author agent owns how AGENTS.md, the project skills, and the role agents are written — direct, sectioned, each rule stated once — and may change wording but never a rule.
type: adr
status: accepted
date: 2026-09-24
decision-makers: Chase Florell
keywords: agents, skills, AGENTS.md, ai-author, instructions, style, skillfile, ADR-0037, ADR-0086
---

# ADR-0121 — A fifth role maintains the agent instructions

**Status:** Accepted. Extends
[ADR-0086](ADR-0086-four-role-agents-defined-in-the-repository.md). Amended by
[ADR-0124](ADR-0124-the-ai-author-role-may-register-its-files-and-mend-links.md):
the role may also edit a skill's `agents/*.yaml` and its `Skillfile` entries,
and mend a link its own move broke.

## Context

[ADR-0037](ADR-0037-progressive-agent-instructions.md) cut `AGENTS.md` from 691
lines by moving detail into focused skills. Since then, every lesson that
changed the process added a rule to a skill, and `AGENTS.md` grew back to 350
lines. By #432 the instruction files were about 1,700 lines. The same rules
were written out in several files, and the one line an agent needed to act on
was often in the middle of a paragraph.

Nothing owned how these files are written. Each change added to them, and no
role was responsible for keeping them short and non-repeating.

## Decision

Add a fifth role, **ai-author**, declared in `agents/ai-author.md` and installed
by `skillfile` like the other four.

- It edits `AGENTS.md`, `skills/*/SKILL.md`, and `agents/*.md`, and nothing
  else: no code, specification, ADR, lesson, or runtime prompt.
- Its file is the one home for the instruction style rules: direct, bullets
  over prose, headed sections, no fluff, each rule stated once, references
  kept.
- It may change how a rule is written, never what the rule requires. Before
  cutting anything, it lists what the file says. After rewriting, it checks
  that every item survived. It flags an obsolete rule for an owner decision
  instead of deleting it.

Like the other roles, it is a definition an operator invokes. It is not a CI
gate.

## Consequences

- `Skillfile` gains a fifth `local agent` entry. `AGENTS.md` names five roles.
- ADR-0086's four roles are unchanged. This role sits outside the
  specification chain, because it changes no behavior.
- A future rewrite of an instruction file has a written standard to be
  reviewed against.

## Alternatives

- **A skill instead of an agent.** Rejected for the reason ADR-0086 gives. A
  skill is loaded by topic and constrains nothing. "Change the wording, never
  the rule, and never touch code" describes who is acting.
- **Put the style rules in `hpac-safety-conventions`.** Rejected. Every change
  loads that skill, so every task would pay for rules that only apply when
  editing instructions.
- **A length gate in CI.** Rejected. A line budget rewards deleting rules,
  which is the one thing this role must never do.

## Related

- [ADR-0037](ADR-0037-progressive-agent-instructions.md)
- [ADR-0086](ADR-0086-four-role-agents-defined-in-the-repository.md)
- [ADR-0087](ADR-0087-every-markdown-file-declares-itself.md)
