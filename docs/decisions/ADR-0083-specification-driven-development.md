---
status: accepted
date: 2026-09-22
decision-makers: Chase Florell
keywords: specification-driven development, SDD, Gherkin, agents, artifact chain, traceability, out of scope
---

# ADR-0083 — Specification-driven development is how this repository works

## Context

The repository already behaves this way without saying so.
[`features/README.md`](../../features/README.md) declares `/features` the
canonical target, every user-facing requirement is covered by a scenario
([ADR-0049](ADR-0049-reqnroll-for-executable-gherkin-scenarios.md)), a scenario
compiles into an executable test through Reqnroll or `playwright-bdd`
([ADR-0053](ADR-0053-ui-scenarios-execute-via-playwright-bdd.md)), an
unimplemented scenario carries `@ignore`, and an obsolete one is deleted rather
than parked. ADRs carry the rationale and never restate acceptance criteria
([ADR-0047](ADR-0047-feature-files-must-not-contradict-adrs.md)).

What is absent is the statement of *why* those rules hang together, and the
consequences that follow from taking them seriously. Most of this repository's
changes are now made by a coding agent. An agent does not ask what a sentence
meant; it builds something confident and wrong. It also does not read a
conversation from last week. Anything that is not a file in the repository did
not happen.

That makes the specification the prompt. A reviewer correcting an agent in chat
produces a correction that survives until the session ends. The same correction
written into a scenario survives the session, the branch, the reviewer, and the
model.

## Decision

Behaviour flows through an artifact chain, and every hop is a tracked file:

```
need (issue)
  → scenario            features/<area>/<area>.feature
  → supporting detail   features/<area>/README.md, docs/*.md
  → step definitions    tests/HpacSafety.Acceptance.Tests | tests/e2e/steps
  → code                src/**
```

Four rules follow, and they are the substance of this decision.

1. **The specification is written before the implementation.** When behaviour
   is being added or changed, the scenario is authored or amended first, in the
   same pull request, and the implementation is what makes it pass.

2. **A wrong behaviour is corrected in the specification, not in the chat.**
   When an implementation does the wrong thing, the fix begins by asking
   whether the scenario said the wrong thing. If it did, the scenario changes
   and the chain re-runs from there. Arguing the agent into a different answer
   leaves no artifact and does not survive the next run.

3. **The specification delta is the change.** A pull request that changes
   behaviour shows what claim changed, and its body cites the claims it
   satisfies. `Closes #<number>` and a squash-ready title are unchanged; the
   repository is configured to use the pull-request body as the commit message,
   so the delta lands in history.

4. **Out of scope is part of the specification.** A specification that says
   only what to build invites an agent to over-deliver into territory nobody
   asked for, which then has to be reviewed and removed. What not to build is
   written down where the scenarios are read.

The claim-ID scheme that makes a claim citable is decided separately in
[ADR-0084](ADR-0084-stable-claim-ids-and-a-generated-traceability-matrix.md);
the artifact that carries a correction back upstream after a bug is decided in
[ADR-0085](ADR-0085-a-lesson-flows-upstream-into-the-specification.md); the
roles that consume the chain are decided in
[ADR-0086](ADR-0086-four-role-agents-defined-in-the-repository.md).

## Consequences

- `AGENTS.md` states the chain and the four rules, so every agent and
  contributor reads them before touching the repository.
- The existing guidance in `features/README.md`, `skills/`, and the delivery
  contract is aligned to name the same chain rather than four overlapping
  descriptions of it.
- Time moves upstream. Writing a scenario costs more than it did; the
  implementation costs less, and a wrong implementation costs a scenario edit
  rather than a hotfix.
- Nothing about the product changes. This decision is about how the target is
  written and enforced, not about what the system does.

## Alternatives

- **Leave it implicit.** Rejected: the rules already exist in four places —
  `AGENTS.md`, `features/README.md`, `skills/deliver-hpac-change`, and
  `skills/test-hpac-safety` — and each states a slice. An agent reading only
  one of them gets a partial contract, and no document says the corrections go
  into the specification rather than into the conversation.
- **Adopt a specification tool or framework.** Rejected: the repository already
  has the whole chain in plain files that Git, CI, Reqnroll, and
  `playwright-bdd` understand. A tool would add a format to maintain and a
  dependency to pin without adding a link the chain lacks.
- **Write requirements as prose in `docs/` and keep Gherkin only for tests.**
  Rejected: that is the arrangement that produced the stale requirement
  documents this repository's Gherkin was adopted to replace. A scenario that
  executes cannot silently diverge from the code; a paragraph can.

## Related

- [ADR-0047](ADR-0047-feature-files-must-not-contradict-adrs.md)
- [ADR-0049](ADR-0049-reqnroll-for-executable-gherkin-scenarios.md)
- [ADR-0053](ADR-0053-ui-scenarios-execute-via-playwright-bdd.md)
- [ADR-0084](ADR-0084-stable-claim-ids-and-a-generated-traceability-matrix.md)
- [ADR-0085](ADR-0085-a-lesson-flows-upstream-into-the-specification.md)
- [ADR-0086](ADR-0086-four-role-agents-defined-in-the-repository.md)
