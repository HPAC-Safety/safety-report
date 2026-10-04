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

- **Frontmatter** keeps `name` (the role, never renamed: it routes),
  `description`, `model`, `effort`, and where needed `tools` and `isolation`.
  It adds `skills`, a list of **generic skills only**.
  - The `description` opens with the team role, then the persona name in
    parentheses: "The team's security engineer (Kyle). …". It then says what
    the agent does and when to pick it over its neighbours.
  - A role that edits files preloads `deliver-change`: the builders, Kevin,
    Jennifer, Emily, and Jane. A reviewer does not.
- **The body** is who, what, and what it leaves to others; its exact sections
  are in the generic
  [`write-agent-instructions`](../../skills/write-agent-instructions/SKILL.md)
  skill.
- **Every refusal stays in the agent file.** A skill constrains nothing; a
  role is defined by what it refuses.
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

### What still holds

Not restated; each stays as its record or skill says.

- The frontmatter keys an agent may carry, checked by
  `tools/docs/check-frontmatter.ts`, and that each declares its model and
  effort, now with the reason in the team table above
  ([ADR-0182](../decisions/ADR-0182-a-role-agent-declares-its-model-and-effort.md)).
- ai-author's scope: it edits instruction files and their manifest entries,
  never code, specification, or runtime prompts, and changes a rule's wording,
  never the rule
  ([ADR-0121](../decisions/ADR-0121-a-fifth-role-maintains-the-agent-instructions.md),
  [ADR-0124](../decisions/ADR-0124-the-ai-author-role-may-register-its-files-and-mend-links.md)).
- The split of the retired implementer into `backend`, `ux`, and
  `infrastructure`, the chain from spec-author through test-writer and a
  builder to spec-reviewer, and the adversary taking the privacy review from the spec-reviewer
  ([ADR-0197](../decisions/ADR-0197-backend-and-ux-replace-the-implementer-and-the-adversary-takes-privacy-review.md)).
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
  skill names nothing specific to this repository.
- Nothing checks the body's three sections or the guardrails; the
  `ai-author` role and review hold them.
