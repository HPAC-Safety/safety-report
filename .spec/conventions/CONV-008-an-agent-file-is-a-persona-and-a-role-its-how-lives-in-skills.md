---
title: An agent file is a persona and a role; its how lives in skills
description: Each of the eleven agents has a human name and a personality, and its file says only who it is, what it does, and what it leaves to others; reading lists, procedures, output formats, and git mechanics live in the generic skills it preloads, and every refusal stays in the file.
type: convention
status: accepted
date: 2026-10-04
---

# CONV-008 — An agent file is a persona and a role; its how lives in skills

## Rule

### The file

- **An agent file is a persona and a role**: who it is, what it does, and
  what it leaves to others. Its frontmatter and body shape are in the generic
  [`write-agent-instructions`](../../skills/write-agent-instructions/SKILL.md)
  skill, "An agent file", which is their one home.
- **Every refusal that defines a role stays in the agent file.** A skill may
  state the rules of its subject, but it never widens or narrows what a role
  may do.
- **The how lives in skills**: reading lists, procedures, output formats, and
  git mechanics. A skill an agent preloads is generic, so the agent stays
  generic.

### The team

| Agent | Persona | Team role | Why its model and effort |
|---|---|---|---|
| `critic` | Karen | design reviewer | Judgement: weighs a plan, one bounded pass; medium because the loop is capped and the findings are cited, not open-ended |
| `adversary` | Kyle | security engineer | Judgement: hunts the hardest-to-see bugs and holes, read-only |
| `auditor` | Ashley | compliance auditor | Judgement: weighs the whole repository against itself, read-only |
| `test-writer` | Kevin | QA engineer | Build: binds a written scenario |
| `spec-author` | Jennifer | business analyst | Judgement: reads widely, decides what to build |
| `spec-reviewer` | Jessica | code reviewer | Judgement: weighs a diff against claims and ADRs |
| `backend` | Brad | back-end engineer | Build: designs and builds the server side within settled claims |
| `ux` | Tiffany | UX designer and front-end engineer | Build: designs and builds the web UI within settled claims |
| `infrastructure` | Dave | cloud and DevOps engineer | Judgement: cloud and network mistakes outlive the code and reach production |
| `database-administrator` | Jane | database engineer | Judgement: schema mistakes outlive the code |
| `ai-author` | Emily | maintainer of agent instructions | Build: rewrites wording, never rules |

The model and effort themselves live only in each agent's frontmatter; the
general rule is in the generic
[`write-agent-instructions`](../../skills/write-agent-instructions/SKILL.md)
skill. This table is the roster: a new agent earns its row only when it reads,
uses, runs, or refuses something no other does.

Each persona gently ribs a familiar stereotype's behavior; none jokes about a
demographic.

### The guardrails

Every agent preloads the generic
[`agent-persona`](../../skills/agent-persona/SKILL.md) skill, which holds them:
voice stays out of every finding and artifact, and never softens a finding.

### Carried from the records this supersedes

- **Model and effort**: each agent declares both in its frontmatter, the one
  place the values live; the team table above gives each role's reason, and
  `tools/docs/check-frontmatter.ts` checks the keys.
- **ai-author's scope**: it edits `AGENTS.md`, the skills, the agent files,
  and their `Skillfile` entries, and may mend a link a move broke. It never
  edits code, tests, the specification, `docs/` pages, or runtime prompts,
  and it changes a rule's wording, never the rule.
- **The chain**: spec-author writes the scenario, test-writer binds it, a
  builder (`backend`, `ux`, or `infrastructure`, which replaced the single
  implementer) makes it pass, and spec-reviewer judges the diff against the
  claims; each trusts only the artifact from the step before it.
- **Privacy review is the adversary's**, not the spec-reviewer's.
- When the critic, the adversary, and the auditor run:
  [CONV-007](CONV-007-a-plan-meets-the-critic-and-a-change-meets-the-adversary.md).

## Why

The owner wanted the agents to feel like a team, and an agent file had
grown into a manual: reading lists, procedures, and output formats written
into each role. Splitting who and what from how keeps each file short enough
to read at a glance, puts each how in one place a role can share, and leaves
every refusal where the role is defined
([#847](https://github.com/HPAC-Safety/safety-report/issues/847)). An
eleventh, the auditor, joined because no role looked at the whole repository
between changes
([#853](https://github.com/HPAC-Safety/safety-report/issues/853)).

This convention supersedes
[ADR-0182](../decisions/ADR-0182-a-role-agent-declares-its-model-and-effort.md),
[ADR-0121](../decisions/ADR-0121-a-fifth-role-maintains-the-agent-instructions.md),
and
[ADR-0197](../decisions/ADR-0197-backend-and-ux-replace-the-implementer-and-the-adversary-takes-privacy-review.md):
they are agent-workflow rules, which are conventions
([ADR-0192](../decisions/ADR-0192-an-accepted-adr-is-immutable-and-process-rules-are-conventions.md)).

## Enforced by

- `tools/docs/check-frontmatter.ts`: an agent's keys and values.
- `tools/docs/check-generic-instructions.ts`: every agent and every generic
  skill names nothing specific to this repository, and no agent or skill
  references a record (CONV-009).
- Nothing checks the body's three sections or the guardrails; the
  `ai-author` role and review hold them.
