# Lessons

A bug is usually a specification defect wearing implementation clothes. The code
did something nobody wanted, which means either no claim covered the case or a
claim covered it wrongly. Fixing only the code leaves that gap exactly where it
was, and the gap is what produces the same bug again next quarter
([ADR-0085](../decisions/ADR-0085-a-lesson-flows-upstream-into-the-specification.md)).

A lesson records what the specification should have said. It is read on a design
pass, alongside `/features` and the ADRs — not looked up after something breaks.

## When to write one

A bug fix that reveals a specification gap writes a lesson, in the same pull
request as the fix. A fix that reveals nothing — a typo, a dependency bump, a
rename — does not.

A lesson does not replace an ADR or a scenario. A fix that also makes a durable
architectural decision still gets an ADR, and the lesson cites it. A fix that
changes user-facing behavior still gets a scenario, and the lesson cites its
claim ID.

## The shape

One file per lesson, `NNNN-kebab-slug.md`, numbered in the order they are
written. Frontmatter carries the date, the issue, and a status. The body states
four things and stops:

- **Symptom** — what was observed, in the terms it was observed in.
- **Root cause** — why it happened, not which line was edited.
- **Spec delta** — what changed upstream: the claim added or corrected, the ADR
  written, the guard moved.
- **Scenario** — the claim ID that now proves it, or an explicit statement that
  no scenario can.

A lesson that turns out to be wrong is corrected in place, or marked superseded
in its frontmatter status, like an ADR.

## Index

| Lesson | What it cost us |
|---|---|
| [0001 — A guard that lives only in CI is not a guard](0001-a-guard-that-lives-only-in-ci-is-not-a-guard.md) | 38 failing tests on every fresh clone, invisible to CI |
| [0002 — Provenance that hashes only one side of a pair](0002-provenance-that-hashes-only-one-side-of-a-pair.md) | Hand-written French silently overwritten by the translator |
