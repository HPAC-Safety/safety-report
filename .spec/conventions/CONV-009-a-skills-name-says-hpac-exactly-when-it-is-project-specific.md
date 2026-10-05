---
title: A skill's name says hpac exactly when it is project-specific
description: Agents and skills are generic by default and a role agent has no project skill of its own; a skill whose name has hpac is project-specific, a skill without it names nothing in this repository, and a tool selects the generic files by that rule instead of a hand-kept list; no agent or skill file references a decision, lesson, convention, claim, or specification page — it names the topic, and a graphify search by keyword finds the record.
type: convention
status: accepted
date: 2026-10-04
---

# CONV-009 — A skill's name says hpac exactly when it is project-specific

## Rule

Only the `hpac-*` skills live under `skills/` here; the generic ones are
imported from agent-team (CONV-008), whose CI runs their genericity check.

- **The name is the classification.** A skill under `skills/` whose directory
  name contains `hpac` is project-specific. A skill without it is generic: it
  names no project, product, domain term, repository-unique path, tool this
  repository chose, or decision, lesson, convention, or claim number.
- **Generic by default.** An agent or skill is generic whenever its content
  would serve another project; a skill is project-specific only when it
  cannot transfer. The agents are expected to leave this repository.
- **A role agent is always generic, and has no project skill of its own.**
  This repository's detail reaches a role through the generic skills it
  preloads: a preloaded generic skill with project rules has a companion that
  names it, and `AGENTS.md` lists the pair.
  A fact that belongs to no topic skill goes in `AGENTS.md`.
- **Name the topic, never the record.** No file under `agents/` or `skills/`,
  generic or `hpac`, references a decision record, lesson, convention, claim,
  or specification page: no link, no `.spec/` path of any kind, and no
  `ADR-`, `CONV-`, `REQ-`, `CON-`, or lesson number. It names the topic in
  words a search finds. `AGENTS.md` is exempt: it links the records it cites,
  names the specification paths, and tells every agent to find a record by
  keyword with graphify.
  Paths to code and tools stay allowed in an `hpac` skill.
- **A split** is a generic skill plus a project skill. The project skill names
  the generic one it extends, keeps its section names (a project skill that
  also companions another generic skill holds that one's rules under one
  section named in its header), and holds only this repository's rules. A rule
  that transfers to any project moves to the generic half, never out of
  existence.
- **A project skill's name says what it is for**, with `hpac` in it:
  `hpac-domain-model` (renamed from `incident-domain-model`), and the
  existing `*-hpac-*` names.
- `AGENTS.md`'s skill table lists each generic skill beside the project skill
  that extends it.

### Carried from the records this supersedes

- **Three kinds of skill**: generic (transfers to any project), split (a
  generic skill plus the project skill that extends it), and
  repository-specific (its subject is this product). Making a file generic
  drops no rule: a rule naming this repository moves to the project half.
- `postgres-dba`, `design-ef-core-model`, and the `database-administrator`
  agent are generic; `persist-hpac-data` and `manage-hpac-migrations` are
  their project halves.
- **`AGENTS.md` is the always-loaded contract.** It owns the system's
  identity and data sensitivity, the product invariants, the specification's
  authority and the rule for a missing requirement, the line between agent
  skills and runtime prompts, the routing table to skills, and the minimum
  delivery contract. Every detailed procedure lives in a focused skill.

## Why

The hand-kept list of generic files in `tools/docs/check-generic-instructions.ts`
let a new generic file go unchecked until someone remembered to list it, and a
classification table in an ADR fell behind the files. A name the reader can see
and a check that follows it need no list and no table
([#847](https://github.com/HPAC-Safety/safety-report/issues/847)).

A per-role project skill tied every agent to this repository, though the owner
means to move the agents out of it; their repository detail moved to the
topic skills each role already reaches, and the per-role skill was removed
([#853](https://github.com/HPAC-Safety/safety-report/issues/853)).

The same move would leave every record link in a skill dangling, and the
records stay here. So a skill names the topic, and the agent finds the record
through `AGENTS.md` or a graphify search by keyword
([#856](https://github.com/HPAC-Safety/safety-report/issues/856)).

This convention supersedes
[ADR-0131](../decisions/ADR-0131-a-generic-skill-names-no-project-and-a-project-skill-extends-it.md),
[ADR-0139](../decisions/ADR-0139-the-database-skills-and-agent-join-the-generic-classification.md),
and
[ADR-0037](../decisions/ADR-0037-progressive-agent-instructions.md), whose
routing table had named skills that no longer exist:
they are agent-workflow rules, which are conventions
([ADR-0192](../decisions/ADR-0192-an-accepted-adr-is-immutable-and-process-rules-are-conventions.md)).
The two ADRs name `incident-domain-model` as plain text and stay as written.

## Enforced by

- The imported agents and generic skills: agent-team's CI runs the same check
  over them. Here, the local files only:
- `tools/docs/check-generic-instructions.ts`, in pre-commit and the `docs` CI
  job:
  - every `agents/*.md` and every text file of a skill whose directory has no
    `hpac` fails a line that names this product, its domain, a product role,
    a tool or provider this repository chose, its paths, or the Worker;
  - every agent and every skill text file, `hpac` included, fails a line that
    references a record: a specification path or a generated specification
    file's name, an `ADR`, `CONV`, `REQ`, or `CON` ID or a lesson number in any
    spelling, even split across a line wrap, or an old `docs/decisions` or
    `docs/lessons` path.
  A new skill is checked with no list to update; `AGENTS.md` is not scanned.
