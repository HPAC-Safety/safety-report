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

- **Keep the implementer beside backend and ux.** Rejected: builders with
  overlapping scope.
- **Builders that only make a failing test pass.** Rejected: nobody would own
  the design of an API, a screen, or a network, and it would be improvised.
- **backend also owns infrastructure.** Rejected: the cloud and network need
  different expertise and carry a different blast radius (production,
  exposure, cost), as the schema does for the database-administrator.
- **Leave privacy hunting in the spec-reviewer.** Rejected: it duplicates the
  adversary's security and correctness lens.
- **Retire the implementer; split by domain; move privacy to the adversary** —
  chosen.

## Decision

- The `implementer` agent is retired. Three builders replace it, and each
  **designs and builds** its domain, not only makes a test green:
  - `backend`: API shape, Worker pipeline, data flow, error handling, scripts,
    and CI workflows;
  - `ux`: interaction design, layout, components, accessibility, and localized
    copy in the web UI;
  - `infrastructure`: cloud, network, compute, storage, DNS, secrets, backups,
    and deployment, as infrastructure code. The owner still applies to and
    promotes production.

  The database-administrator keeps the schema. Each builder owns the unit,
  integration, or component tests of what it builds; the test-writer owns the
  acceptance step definitions. Each still builds only what cited claims
  describe, sends a design plan to the critic, records a significant choice as
  an ADR, and never writes the specification.
- The chain is spec-author → test-writer → backend | ux | infrastructure →
  spec-reviewer. Each step still trusts only the artifact from the step before
  it.
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

  `backend` and `ux` run `sonnet` at `medium`, as the implementer did: the
  claims bound their design. `infrastructure` runs `opus` at `high`, the
  judgement default: its mistakes outlive the code, as the schema's do.

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
