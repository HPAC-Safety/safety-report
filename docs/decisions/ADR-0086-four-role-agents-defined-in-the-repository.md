---
title: Four roles are defined as repository agents
description: Four roles — spec author, test writer, implementer, and reviewer — are declared as agents under agents/ and installed by skillfile.
type: adr
status: accepted
date: 2026-09-22
decision-makers: Chase Florell
keywords: agents, skillfile, roles, spec author, test writer, implementer, reviewer
---

# ADR-0086 — Four roles are defined as repository agents

**Status:** Accepted. Extended by
[ADR-0121](ADR-0121-a-fifth-role-maintains-the-agent-instructions.md), which
adds a fifth role outside the specification chain. Extended by
[ADR-0131](ADR-0131-a-generic-skill-names-no-project-and-a-project-skill-extends-it.md),
which makes the role agents generic and moves this repository's specifics into
the `hpac-role-agents` skill.

## Context

[ADR-0001](ADR-0001-repository-and-agent-configuration.md) established that
agent configuration is declared in the repository and installed by `skillfile`,
and the manifest already supports `agent` entries beside `skill` entries.
`.claude/agents/` is created, gitignored, and covered by the `agent-config`
job's no-drift check. Nothing has ever been declared there.

Twelve project-owned skills carry what this repository knows. A skill is
knowledge: it is loaded when a topic is in play, and it does not constrain what
the reader may do with it. What is missing is the constraint —
[ADR-0083](ADR-0083-specification-driven-development.md) describes a chain in
which each step trusts the artifact from the step before it, and nothing in the
repository expresses "you are writing tests now, and you may not write the
implementation".

Running every step in one conversation collapses the chain. The implementer
sees the reasoning that produced the scenario instead of only the scenario, so
it satisfies the intent it overheard rather than the claim that was written.
The reviewer, holding the whole history, judges by taste — which is where
humans disagree and agents invent.

## Decision

Four roles are declared as agents under `agents/`, source-controlled and
installed to `.claude/agents/` by `skillfile`:

- **spec-author** — turns a need into scenarios carrying new claim IDs, and
  states what is out of scope. Writes specification files. Writes no code.
- **test-writer** — turns a claim into failing step definitions, and removes
  `@ignore` only once a binding exists. Writes test files. Writes no
  production code.
- **implementer** — makes the failing test pass against the cited claims and
  nothing else. Writes production code.
- **spec-reviewer** — judges a diff against the cited claims and the ADRs.
  Scope creep and untraced behaviour are findings. Findings are returned as
  specification deltas or test deltas, never as taste.

Each agent declares what it may read, what it may write, and what it must
refuse. Each carries the upstream-standard `name` and `description`
frontmatter that Claude Code and `skillfile` already expect, so no
repository-specific key is injected into a file a third-party tool parses.

The agents are definitions an operator invokes. They are not wired into CI and
they are not a pipeline; the repository's gates remain the enforcement, and a
role is a way of holding a change to one job at a time.

## Consequences

- `Skillfile` gains four `local agent` entries; `skillfile install` populates
  `.claude/agents/`, and the existing no-drift step covers them with no change
  to CI.
- The skills are unchanged in purpose. A skill is knowledge the roles consume;
  an agent is the role.
- A contributor who never invokes an agent is unaffected. Nothing in the
  delivery contract requires one.
- Adding a fifth role later is a manifest entry and a file, not a redesign.

## Alternatives

- **Add a reviewer agent only.** Rejected: the reviewer is the most obviously
  valuable role, but it is also the one that works least well alone. Reviewing
  against claims assumes the claims were authored deliberately and the tests
  were written from them, which is what the other three roles produce.
- **Express the roles as skills.** Rejected: a skill is loaded by topic and
  constrains nothing. "Do not write production code" is a property of who is
  acting, not of what the subject is.
- **Keep the roles in prose in `AGENTS.md`.** Rejected: prose describing a role
  is read by whoever is already doing everything. A declared agent is invoked
  with only its own instructions and only the artifact it was handed.
- **Vendor role definitions from an upstream registry.** Rejected: the roles
  are defined by this repository's chain — its claim IDs, its two test runners,
  its delivery contract. An upstream definition would have to be rewritten to
  be correct here, which is the same work without ownership.

## Related

- [ADR-0001](ADR-0001-repository-and-agent-configuration.md)
- [ADR-0037](ADR-0037-progressive-agent-instructions.md)
- [ADR-0083](ADR-0083-specification-driven-development.md)
- [ADR-0084](ADR-0084-stable-claim-ids-and-a-generated-traceability-matrix.md)
