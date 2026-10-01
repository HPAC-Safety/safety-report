---
title: A role agent declares its model and effort
description: Every agents/*.md carries name, description, model, and effort, and may carry the other keys Claude Code reads on an agent. Judgement roles run opus at high effort; build roles run sonnet at medium. An agent file is brief: a mission line, Read first, Produce, Refuse.
type: adr
status: accepted
date: 2026-09-30
decision-makers: Chase Florell
keywords: agents, role agents, frontmatter, model, effort, reasoning effort, orchestration, subagent, skillfile, ADR-0086, ADR-0087
---

# ADR-0182 — A role agent declares its model and effort

**Status:** Accepted. Decided by the owner on 2026-09-30 in
[#706](https://github.com/HPAC-Safety/safety-report/issues/706). Amends
[ADR-0086](ADR-0086-four-role-agents-defined-in-the-repository.md) and
[ADR-0087](ADR-0087-every-markdown-file-declares-itself.md), which held an
agent's frontmatter to `name` and `description`.

## Context

The six role agents under `agents/` are installed by `skillfile`, which copies
each file verbatim to `.claude/agents/<name>.md`. ADR-0086 gave them "the
upstream-standard `name` and `description` frontmatter", and ADR-0087 made
that the whole allowed shape; `tools/check-frontmatter.mjs` enforces it.

Claude Code now reads more than that from an agent's frontmatter: `model`,
`effort`, `tools`, `disallowedTools`, `permissionMode`, `maxTurns`, `skills`,
`memory`, `isolation`, and `background`. A subagent spawned by role takes its
model and reasoning effort from that definition unless the caller overrides
the model for one call.

Without those keys an orchestrator spawning `spec-reviewer` cannot see whether
the role was meant for the deepest model or the cheapest, and every role runs
on whatever the session happens to use. The choice is a property of the role
— how widely it must read and how much it must decide — so it belongs with the
role, reviewable in a pull request.

The agent files had also grown wordy: the same rule explained twice, rationale
restated beside a link.

## Decision

- **Every `agents/*.md` carries four keys**: `name`, `description`, `model`,
  `effort`.
  - `model` is `sonnet`, `opus`, `haiku`, `inherit`, or a full `claude-…`
    model ID.
  - `effort` is `low`, `medium`, `high`, `max`, or a positive integer.
- **It may also carry** the other keys Claude Code reads on an agent —
  `tools`, `disallowedTools`, `permissionMode`, `maxTurns`, `skills`,
  `memory`, `isolation`, `background` — and nothing else.
- **The assignment**:
  - judgement roles, which read widely and decide, run `opus` at `high`:
    `spec-author`, `spec-reviewer`, `database-administrator`;
  - build roles, which execute a bounded brief, run `sonnet` at `medium`:
    `implementer`, `test-writer`, `ai-author`.

  The table and its reason live in the `hpac-role-agents` skill. The agents
  stay generic (ADR-0131); a model name is not repository-specific.
- **An agent file is brief**: one mission line, then `## Read first`,
  `## Produce`, `## Refuse` (`spec-reviewer` adds `## Look for` and
  `## Report`; `ai-author` keeps its style rules, which are their one home).
  Every rule survives; only wording shrinks.
- `tools/check-frontmatter.mjs` requires the four keys on an agent, accepts
  the optional ones, checks `model` and `effort` against the values above, and
  refuses any other key. Skills keep `name` and `description`.

## Consequences

- `skillfile install` puts the model and effort in front of Claude Code with no
  further step; an orchestrator spawning a role by name gets them.
- Changing a role's model is a one-line, reviewable diff.
- A key Claude Code later adds is refused until this list grows; that is a
  deliberate, small amendment rather than an open door.

## Alternatives

- **Leave the choice to the orchestrator's prompt.** Rejected: invisible to
  `skillfile` and to review, and re-decided on every spawn.
- **Restrict each role's tools now.** Deferred by the owner: each role's
  `## Refuse` already says what it may not do, and a tool list is a separate
  decision.
- **Every role on the deepest model.** Rejected: the build roles execute a
  brief that a judgement role already settled; the cost buys nothing.

## Related

- [ADR-0086](ADR-0086-four-role-agents-defined-in-the-repository.md)
- [ADR-0087](ADR-0087-every-markdown-file-declares-itself.md)
- [ADR-0121](ADR-0121-a-fifth-role-maintains-the-agent-instructions.md)
- [ADR-0131](ADR-0131-a-generic-skill-names-no-project-and-a-project-skill-extends-it.md)
