---
title: The database skills and agent join the generic classification
description: postgres-dba, design-ef-core-model, and the database-administrator agent are generic; persist-hpac-data becomes the project half of a split; the sixth role agent needs no decision beyond ADR-0086 and ADR-0121.
type: adr
status: accepted
date: 2026-09-26
decision-makers: Chase Florell
keywords: agents, skills, database-administrator, postgres-dba, design-ef-core-model, persist-hpac-data, manage-hpac-migrations, generic, ADR-0086, ADR-0121, ADR-0131
---

# ADR-0139 — The database skills and agent join the generic classification

**Status:** Accepted. **Amends**
[ADR-0131](ADR-0131-a-generic-skill-names-no-project-and-a-project-skill-extends-it.md)'s
classification table, which predates the files #498 added.

## Context

ADR-0131 classifies every skill and role agent as generic, split, or
repository-specific. #498 then added three generic files:

- the `postgres-dba` skill;
- the `design-ef-core-model` skill;
- the `database-administrator` agent.

It also made `persist-hpac-data` extend `design-ef-core-model`, and made
`manage-hpac-migrations` extend `postgres-dba` for its schema conventions.
ADR-0131's table still lists `persist-hpac-data` as repository-specific and
names none of the new files. `tools/check-generic-instructions.mjs` already
lists all three as generic.

## Decision

**ADR-0131's three kinds and its rules stand. Its table now reads:**

| Kind | Files |
|---|---|
| Generic | `clarify-requirements`, `postgres-dba`, `design-ef-core-model`; the agents `spec-author`, `test-writer`, `implementer`, `spec-reviewer`, `ai-author`, `database-administrator` |
| Split | `deliver-change` + `deliver-hpac-change`; `coding-conventions` + `hpac-safety-conventions`; `test-from-scenarios` + `test-hpac-safety`; `manage-ef-core-migrations` + `manage-hpac-migrations`; `postgres-dba` + `manage-hpac-migrations`; `design-ef-core-model` + `persist-hpac-data`; the six agents + `hpac-role-agents` |
| Repository-specific | `anonymize-hpac-reports`, `incident-domain-model`, `handle-hpac-media`, `localize-hpac-app`, `build-hpac-web-ui`, `manage-hpac-infrastructure` |

- `manage-hpac-migrations` is the project half of two splits: it extends
  `manage-ef-core-migrations` for how a migration is written and applied, and
  `postgres-dba` for schema conventions. It wins over both.
- `persist-hpac-data` keeps its name, so existing links still resolve.

**The sixth role agent needs nothing beyond ADR-0086 and ADR-0121.**

- ADR-0086 already provides for more roles: "a manifest entry and a file, not
  a redesign."
- Like `ai-author` (ADR-0121), `database-administrator` sits outside the
  specification chain. It is an operator-invoked definition, not a CI gate, and
  no chain role hands it an artifact.
- It changes no product rule. Its constraints in this repository — tiny keys,
  no physical deletion, no row content in an audit — come from `AGENTS.md`
  and the ADRs they already cite, and `hpac-role-agents` points it at them.

## Rejected alternatives

- **Edit ADR-0131's table in place.** An ADR is a historical record of what
  was decided when; the amendment records when and why the table grew.
- **A separate ADR for the sixth role.** It would restate ADR-0086's
  consequence and ADR-0121's precedent with no new trade-off.

## Consequences

- `AGENTS.md` already names six role agents and lists `postgres-dba` and
  `design-ef-core-model` beside their project skills; nothing there changes.
- A future generic skill or agent is added to
  `tools/check-generic-instructions.mjs` and to this classification, by an
  amendment like this one.
