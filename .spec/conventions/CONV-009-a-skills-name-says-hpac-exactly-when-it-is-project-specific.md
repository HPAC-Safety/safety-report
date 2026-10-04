---
title: A skill's name says hpac exactly when it is project-specific
description: Agents and skills are generic by default and a role agent has no project skill of its own; a skill whose name has hpac is project-specific, a skill without it names nothing in this repository, and a tool selects the generic files by that rule instead of a hand-kept list.
type: convention
status: accepted
date: 2026-10-04
---

# CONV-009 — A skill's name says hpac exactly when it is project-specific

## Rule

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

### What still holds

Not restated; each stays as its record says.

- The three kinds, generic, split, and repository-specific, with no rule
  dropped when a file is made generic, and a split's project skill keeping its
  generic skill's section names
  ([ADR-0131](../decisions/ADR-0131-a-generic-skill-names-no-project-and-a-project-skill-extends-it.md)).
- `postgres-dba`, `design-ef-core-model`, and the `database-administrator`
  agent as generic, with `persist-hpac-data` and `manage-hpac-migrations` as
  their project halves
  ([ADR-0139](../decisions/ADR-0139-the-database-skills-and-agent-join-the-generic-classification.md)).

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

This convention supersedes
[ADR-0131](../decisions/ADR-0131-a-generic-skill-names-no-project-and-a-project-skill-extends-it.md)
and
[ADR-0139](../decisions/ADR-0139-the-database-skills-and-agent-join-the-generic-classification.md):
they are agent-workflow rules, which are conventions
([ADR-0192](../decisions/ADR-0192-an-accepted-adr-is-immutable-and-process-rules-are-conventions.md)).
The two ADRs name `incident-domain-model` as plain text and stay as written.

## Enforced by

- `tools/docs/check-generic-instructions.ts` selects every `agents/*.md` and
  every `skills/*/SKILL.md` whose directory has no `hpac`, and fails a line
  that names this product, its domain, its paths, a decision, lesson,
  convention, or claim number, or the Worker. A new generic skill is checked
  with no list to update.
