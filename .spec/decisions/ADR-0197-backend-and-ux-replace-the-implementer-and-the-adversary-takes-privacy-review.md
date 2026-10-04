---
title: Backend and ux replace the implementer, and the adversary takes privacy review
description: The implementer role is retired and replaced by backend (everything but the web UI) and ux (the web UI), each carrying the chain contract; the adversary takes security and privacy hunting from the spec-reviewer. The chain is spec-author, test-writer, backend or ux, spec-reviewer.
type: adr
status: accepted
date: 2026-10-04
decision-makers: Chase Florell
keywords: agents, roles, implementer, backend, ux, adversary, critic, spec-reviewer, chain, ADR-0086
---

# ADR-0197 — Backend and ux replace the implementer, and the adversary takes privacy review

**Status:** Accepted. Decided by Chase Florell on 2026-10-04 in
[#842](https://github.com/HPAC-Safety/safety-report/issues/842). Supersedes
[ADR-0086](ADR-0086-four-role-agents-defined-in-the-repository.md). Amends
[ADR-0182](ADR-0182-a-role-agent-declares-its-model-and-effort.md)'s model
values and assignment.

## Context

[ADR-0086](ADR-0086-four-role-agents-defined-in-the-repository.md) declared
four chain roles: spec-author, test-writer, implementer, and spec-reviewer.
The implementer was one role for all production code, so it overlapped any
domain split, and the spec-reviewer both judged a diff against claims and hunted
privacy leaks, a second lens in one agent. Issue #842 adds a critic (a plan's
challenger) and an adversary (a change's attacker); each overlap is better
split into the agent it belongs to.

## Decision drivers

- Each agent owns one artifact and one lens; none repeats another's job.
- The chain contract (a failing test, the cited claims, nothing else) stays.

## Considered options

- **Keep the implementer beside backend and ux.** Rejected: three builders
  with overlapping scope.
- **Leave privacy hunting in the spec-reviewer.** Rejected: it duplicates the
  adversary's security and correctness lens.
- **Retire the implementer; split by domain; move privacy to the adversary** —
  chosen.

## Decision

- The `implementer` agent is retired. `backend` owns everything that is not the
  web UI (API, Worker, scripts, infrastructure, CI); `ux` owns the web UI; the
  database-administrator keeps the schema. Each carries the chain contract: make
  the failing test pass against the cited claims and nothing else, and never
  write the specification.
- The chain is spec-author → test-writer → backend | ux → spec-reviewer. Each
  step still trusts only the artifact from the step before it.
- The spec-reviewer keeps claims, ADRs, scope, exemptions, and record rules.
  The adversary owns correctness bugs, security, privacy leaks, contract
  violations, and missing tests.
- The critic judges a plan before it is built; the spec-reviewer judges a diff
  after it is built.
- **Models** (amends ADR-0182): `model` may also be `fable`. The assignment
  gains two judgement roles that depart from its `opus` at `high` default:
  - `critic` runs `opus` at `medium`: its loop is capped at one pass and one
    recheck, and every finding cites its evidence, so it needs no open-ended
    reasoning;
  - `adversary` runs `fable` at `high`: the hardest-to-see bugs and holes
    justify the strongest model, and it is read-only.

  `backend` and `ux` are build roles, `sonnet` at `medium`, as the implementer
  was.

## Consequences

- Every reference to the implementer is updated; `agents/implementer.md` is
  deleted.
- Older ADRs that name the implementer are history and stay as written.
- When to run the critic and the adversary:
  [CONV-007](../conventions/CONV-007-a-plan-meets-the-critic-and-a-change-meets-the-adversary.md).

## Related

- [ADR-0086](ADR-0086-four-role-agents-defined-in-the-repository.md),
  [ADR-0121](ADR-0121-a-fifth-role-maintains-the-agent-instructions.md),
  [ADR-0182](ADR-0182-a-role-agent-declares-its-model-and-effort.md).
