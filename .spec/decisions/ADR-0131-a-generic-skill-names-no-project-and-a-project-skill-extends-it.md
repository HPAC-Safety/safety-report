---
title: A generic skill names no project, and a project skill extends it
description: Skills and role agents whose practice transfers to other projects are written generically; the rules specific to this repository move into a small project skill that extends the generic one, and a tool keeps the generic files free of project terms.
type: adr
status: accepted
date: 2026-09-25
decision-makers: Chase Florell
keywords: agents, skills, AGENTS.md, ai-author, Skillfile, reuse, generic, instructions, ADR-0037, ADR-0086, ADR-0121
---

# ADR-0131 — A generic skill names no project, and a project skill extends it

**Status:** Accepted. **Extends**
[ADR-0121](ADR-0121-a-fifth-role-maintains-the-agent-instructions.md) and
[ADR-0086](ADR-0086-four-role-agents-defined-in-the-repository.md). Amended by
[ADR-0139](ADR-0139-the-database-skills-and-agent-join-the-generic-classification.md):
`postgres-dba`, `design-ef-core-model`, and the `database-administrator` agent
are generic, and `persist-hpac-data` is now the project half of a split. The
classification table below predates them; ADR-0139 holds the current one.

## Context

Every skill under `skills/` and every role agent under `agents/` was written
for this repository. Their names, descriptions, and bodies named HPAC, the
domain (reports, reporters, pilots), this repository's tools and paths, and
specific ADR, lesson, and claim numbers. Much of what they teach —
specification-driven development, the delivery workflow, the four chain roles,
testing from scenarios, EF Core migration safety — is not specific to this
product, but another project could not install these files as they were (#492).

Some skills are about this product alone, such as the anonymization contract,
the domain model, and media handling. Making those generic would lose meaning
for no reuse.

## Decision

**Each skill and agent is one of three kinds:**

- **Generic** — the practice transfers. It names no project, product, domain
  term, repository-only path, tool this repository chose, or ADR, lesson, or
  claim number.
- **Split** — a generic skill plus a small project skill. The project skill
  names the generic one it extends, keeps its section names (and step
  numbers), and holds only this repository's commands, paths, conventions, and
  references. The generic skill opens by telling the reader to read the project
  skill too; the project skill wins where they differ.
- **Repository-specific** — its subject belongs to this product. It stays as
  it was.

**A file is made generic only where it genuinely is.** No rule is dropped: each
is made generic, moved into the project skill, or left in a
repository-specific skill.

The classification:

| Kind | Files |
|---|---|
| Generic | `clarify-requirements` (renamed from `clarify-hpac-requirements`); the agents `spec-author`, `test-writer`, `implementer`, `spec-reviewer`, `ai-author` |
| Split | `deliver-change` + `deliver-hpac-change`; `coding-conventions` + `hpac-safety-conventions`; `test-from-scenarios` + `test-hpac-safety`; `manage-ef-core-migrations` + `manage-hpac-migrations`; the five agents + `hpac-role-agents` |
| Repository-specific | `anonymize-hpac-reports`, `incident-domain-model`, `persist-hpac-data`, `handle-hpac-media`, `localize-hpac-app`, `build-hpac-web-ui`, `manage-hpac-infrastructure` |

- A split's project skill keeps the old skill's name where one existed, so
  links from ADRs, lessons, code comments, and scripts still resolve.
- `AGENTS.md`'s skill table lists each generic skill beside the project skill
  that extends it.
- `tools/check-generic-instructions.mjs` lists the generic files and fails when
  one names anything specific to this repository. The pre-commit hook runs it
  when a skill or agent is staged, and the `docs` CI job is the backstop
  ([ADR-0073](ADR-0073-a-ui-scenario-is-skipped-by-reqnroll-itself.md)).
- The generic files stay in this repository for now. Moving them to their own
  repository, installed here through `Skillfile`, is a separate task.

## Rejected alternatives

- **Genericize every skill.** The repository-specific skills would lose the
  facts that make them useful, and no other project needs them.
- **Keep the project rules in `AGENTS.md`, or in one `docs/` page per area.**
  `AGENTS.md` holds only what every task needs
  ([ADR-0037](ADR-0037-progressive-agent-instructions.md)), and a docs page is
  not loaded the way a skill is. A small project skill loads with its generic
  one and keeps the same sections.
- **Mark a generic file in its frontmatter.** A skill or agent carries exactly
  `name` and `description`
  ([ADR-0087](ADR-0087-every-markdown-file-declares-itself.md)), so the list
  lives in the checking tool.

## Consequences

- A process lesson updates the generic skill when its rule transfers, and the
  project skill when the rule names this repository's tools or paths.
- A new generic file is added to the tool's list; forgetting it only means the
  file is not checked, while a listed file that is renamed fails the check.
- Acting as a role agent means reading `hpac-role-agents` too.
