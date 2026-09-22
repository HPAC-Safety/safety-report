---
status: proposed
date: 2026-09-22
decision-makers: Chase Florell
keywords: lessons, bug fixes, specification drift, postmortem, docs/lessons
---

# ADR-0085 — A lesson flows upstream into the specification

**Status:** Proposed. Accepted when [#289](https://github.com/HPAC-Safety/safety-report/issues/289)
lands and `docs/lessons/` exists.

## Context

A bug is usually a specification defect wearing implementation clothes. The
code did something nobody wanted, which means either no claim covered the case
or a claim covered it wrongly. Fixing only the code leaves that gap exactly
where it was, and the gap is what produces the same bug again a quarter later.

This repository has an ADR for a durable architectural decision and a scenario
for a user-facing requirement. Neither fits an ordinary bug. Writing an ADR for
every fix would devalue the ADR series; adding a scenario is often right but
says nothing about *why* the claim was missing, so the next person makes the
same class of mistake in a different area.

Two fixes already demonstrate the shape. The `@ui` scenarios that failed on a
fresh clone
([ADR-0073](ADR-0073-a-ui-scenario-is-skipped-by-reqnroll-itself.md)) were a
guard that existed only where a CI flag was typed. The French translations
that could be silently overwritten
([ADR-0070](ADR-0070-a-hand-edited-french-value-is-a-recorded-correction.md)) were
provenance recorded over the wrong half of a pair. Both produced an ADR because
both happened to warrant one. A fix that does not warrant an ADR currently
produces nothing.

## Decision

A bug fix that reveals a specification gap records a **lesson** under
`docs/lessons/`, one file per lesson, named `NNNN-kebab-slug.md`, with
frontmatter carrying the date, the issue, and a status, and a body stating four
things:

- **Symptom** — what was observed, in the terms it was observed in.
- **Root cause** — why it happened, not which line was edited.
- **Spec delta** — what changed upstream: the claim that was added or
  corrected, the ADR written, or the guard moved.
- **Scenario** — the claim ID that now proves it, or an explicit statement that
  no scenario can.

A lesson is read on a design pass, not only when someone goes looking for it:
`AGENTS.md` and the delivery contract point at `docs/lessons/` alongside
`/features` and the ADRs.

A lesson does not replace an ADR or a scenario. When a fix also makes a durable
architectural decision it still gets an ADR, and the lesson cites it. When a
fix changes user-facing behaviour it still gets a scenario, and the lesson
cites the claim ID.

## Consequences

- A fix that today ends at a green build now ends at a written cause.
- `docs/lessons/` accumulates the repository's failure modes in one place, in a
  form an agent reads before designing rather than after breaking something.
- Two lessons are backfilled from the record that already exists (#219 and
  #215) to establish the form; the rest arrive as bugs are fixed. History is
  not audited retroactively for lessons that were never written down.
- A lesson that turns out to be wrong is corrected in place or marked
  superseded in its frontmatter status, like an ADR.

## Alternatives

- **Fold lessons into ADRs as a lesson-flavoured record.** Rejected: an ADR is
  a decision with rejected alternatives. Most lessons record no decision at
  all — they record a cause — and filing them as ADRs would make the series
  unreadable as a record of architecture.
- **Keep lessons in issue comments and pull-request threads.** Rejected: that
  is where they already are, and nothing reads them. A file in the repository
  is read by every agent that opens the design pass; a closed thread is not.
- **A single `docs/lessons.md` accumulating entries.** Rejected: one file per
  lesson diffs cleanly, carries its own frontmatter and status, and can be
  cited by name from an ADR, a claim, or a commit.
- **Write nothing and rely on tests.** Rejected: the test proves the specific
  bug is gone. It does not tell the next agent that a guard typed into CI is
  not a guard.

## Related

- [ADR-0070](ADR-0070-a-hand-edited-french-value-is-a-recorded-correction.md)
- [ADR-0073](ADR-0073-a-ui-scenario-is-skipped-by-reqnroll-itself.md)
- [ADR-0083](ADR-0083-specification-driven-development.md)
- [ADR-0084](ADR-0084-stable-claim-ids-and-a-generated-traceability-matrix.md)
