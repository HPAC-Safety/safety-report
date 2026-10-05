---
title: The role agents and generic skills live in agent-team, and nothing is pinned here
description: The eleven role agents and the generic skills they preload live in the public ChaseFlorell/agent-team repository, installed per user into ~/.claude; this repository keeps AGENTS.md, the symlinks, and the hpac-* project skills, and pins nothing from agent-team.
type: adr
status: accepted
date: 2026-10-04
decision-makers: Chase Florell
keywords: agents, skills, agent-team, skillfile, AGENTS.md, symlinks, ai-author, ADR-0001, ADR-0124
---

# ADR-0199 — The role agents and generic skills live in agent-team, and nothing is pinned here

**Status:** Accepted. Decided by Chase Florell on 2026-10-04 in
[#862](https://github.com/HPAC-Safety/safety-report/issues/862). Supersedes
[ADR-0001](ADR-0001-repository-and-agent-configuration.md) and
[ADR-0124](ADR-0124-the-ai-author-role-may-register-its-files-and-mend-links.md).

## Context

[ADR-0001](ADR-0001-repository-and-agent-configuration.md) said skill and
agent sources live under `skills/` and `agents/` here.
[ADR-0124](ADR-0124-the-ai-author-role-may-register-its-files-and-mend-links.md)
gave the `ai-author` role edit rights over `agents/*.md` here. The role agents
and the generic skills they preload name nothing specific to this repository,
and other projects can use them, so they moved to the public
[agent-team](https://github.com/ChaseFlorell/agent-team) repository.

Claude Code resolves a personal skill over a project skill, but a project agent
over a personal one. A project pin of the moved files plus a user's global
install would therefore load some from each source, and the versions would
skew.

## Decision drivers

- One home per file, with no copy to drift.
- No version skew between a pin and a global install.
- A generic file never names this repository.

## Considered options

- **Keep the agents and generic skills here.** Rejected: they serve any
  project, and a second project would copy them.
- **Pin them from agent-team through the `Skillfile`.** Rejected: with a
  global install present, Claude Code takes the personal skill but the project
  agent, so the two sources skew.
- **Pin the agents only.** Rejected: the generic skills the agents preload
  would still come from the global install, so an agent and its skills skew.
- **Move them to agent-team, install per user, pin nothing** — chosen.

## Decision

- **The eleven role agents and the generic skills they preload live in
  agent-team**, with the same layout (`agents/<name>.md`,
  `skills/<name>/SKILL.md`). A user clones it and runs its `skillfile install`,
  which installs into `~/.claude`. They are optional here.
- **This repository pins nothing from agent-team.** Its `Skillfile` lists the
  `hpac-*` project skills and the upstream skills it already used, and
  `Skillfile.lock` pins those.
- **A change to a moved file is a pull request to agent-team**; each user
  re-runs its `skillfile install`. The genericity guard for those files runs in
  agent-team's CI.
- **What of ADR-0001 still holds:**
  - `AGENTS.md` at the repository root is the single canonical instruction
    file, and every tool-specific path is a committed symlink to it;
  - `skillfile` manages the `hpac-*` project skills and the upstream skills,
    their sources live under `skills/`, and `skillfile install` generates
    `.claude/`, which is gitignored;
  - a local `Skillfile` entry has an explicit name.
- **What of ADR-0124 moves:** the `ai-author` role's scope, including a skill's
  `agents/*.yaml`, the manifest entries for the files it maintains, and a link
  its own move broke, lives in the role's agent file in agent-team. Here it
  covers `AGENTS.md`, the `hpac-*` skills, and their `Skillfile` entries.

## Consequences

- A contributor without agent-team installed still builds and tests the
  repository; the role agents are a convenience.
- A change to a role or a generic skill is made in agent-team, not here.
- `check-generic-instructions.ts` here keeps only the record-reference check
  over the `hpac-*` skills, and fails a skill whose name lacks `hpac`
  ([CONV-009](../conventions/CONV-009-a-skills-name-says-hpac-exactly-when-it-is-project-specific.md)).
- The team's layout and roster are in
  [CONV-008](../conventions/CONV-008-an-agent-file-is-a-persona-and-a-role-its-how-lives-in-skills.md).

## Related

- [ADR-0086](ADR-0086-four-role-agents-defined-in-the-repository.md) and
  [ADR-0121](ADR-0121-a-fifth-role-maintains-the-agent-instructions.md), the
  earlier role decisions.
