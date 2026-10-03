---
title: Lessons
description: What a lesson records, its three kinds and what each owes, when to write one, and where the generated index of lessons lives.
type: guide
---

# Lessons

A bug is usually a specification defect wearing implementation clothes. The code
did something nobody wanted, which means either no claim covered the case or a
claim covered it wrongly. Fixing only the code leaves that gap exactly where it
was, and the gap is what produces the same bug again next quarter
([ADR-0085](../decisions/ADR-0085-a-lesson-flows-upstream-into-the-specification.md)).

A lesson records what the specification should have said. It is read on a design
pass, alongside `.spec/features` and the ADRs — not looked up after something breaks.

## When to write one

A bug fix that reveals a specification gap writes a lesson, in the same pull
request as the fix. A fix that reveals nothing — a typo, a dependency bump, a
rename — does not.

A lesson does not replace an ADR or a scenario. A fix that also makes a durable
architectural decision still gets an ADR, and the lesson cites it. A fix that
changes user-facing behavior still gets a scenario, and the lesson cites its
claim ID.

## Three kinds, and what each one owes

Every lesson declares its `kind` in frontmatter
([ADR-0192](../decisions/ADR-0192-an-accepted-adr-is-immutable-and-process-rules-are-conventions.md)).

**A product lesson** (`kind: product`) is about what the system does. Its
remedy is a claim: a scenario is added or corrected, and the lesson's
**Spec delta** names the `REQ-` or `CON-` ID that now proves it. It does not
change a skill — restating product behavior in a skill creates a second place
for it to drift from `.spec/features`.

**A process lesson** (`kind: process`) is about how we work: tooling, CI,
hooks, conventions, the delivery workflow, what an agent is expected to do. No
scenario can prove it, so it has no claim to add. Its remedy is a **skill** —
the one that would have prevented it — or a
[convention](../conventions/README.md), updated in the same pull request as the
lesson, and its **Skill** section names it. The skill states the general rule;
the lesson keeps the incident.

An agent reads the skills before it starts. It does not read this index looking
for a mistake it has not made yet, which is why a process lesson that stops
here is a story rather than a rule
([ADR-0085](../decisions/ADR-0085-a-lesson-flows-upstream-into-the-specification.md)).

**An incident** (`kind: incident`) is an operational postmortem — a deploy, an
account, a provider failed in running the system. It records what happened and
why, and owes only that. When it did change a skill, its **Skill** section
names it; never invent a rule just to give it one.

## The shape

One file per lesson, `NNNN-kebab-slug.md`, numbered in the order they are
written. Frontmatter carries the date, the issue, a status (`accepted` or
`superseded`), and the kind. The body has these sections, in this order, and no
others; anything more is a `###` under one of them:

- **Symptom** — what was observed, in the terms it was observed in.
- **Root cause** — why it happened, not which line was edited.
- **Spec delta** — what changed upstream: the claim added or corrected, the ADR
  written, the guard moved.
- **Scenario** — the claim ID that now proves it, or an explicit statement that
  no scenario can.
- **Skill** — for a process lesson, the skill or convention that now carries
  the general rule and what it says. A product lesson writes "None — the claim
  is the remedy."

| Kind | Required sections | And |
|---|---|---|
| `product` | all five | Spec delta names a `REQ-` or `CON-` ID |
| `process` | all five | Skill names a skill (`` `skill-name` ``) or a convention (`CONV-NNN`) that exists |
| `incident` | Symptom, Root cause | may name a skill it changed |

`node tools/spec/check-records.ts` checks all of it, in the pre-commit hook and
the `docs` job.

A lesson that turns out to be wrong is corrected in place, or marked superseded
in its frontmatter status. Unlike an ADR, a lesson may be edited.

## Index

Every lesson, newest first, with what it cost and what it changed upstream, is
listed in the generated [specification index](../README.md#lessons). Its
"What it cost us" column is the lesson's frontmatter `description`, its
"Remedy" column the claims under `## Scenario` and the skills under `## Skill`,
and its "Kind" column the frontmatter `kind` — so write those carefully; there
is no table to fill in by hand
([ADR-0183](../decisions/ADR-0183-the-specification-lives-in-a-spec-directory.md)).
