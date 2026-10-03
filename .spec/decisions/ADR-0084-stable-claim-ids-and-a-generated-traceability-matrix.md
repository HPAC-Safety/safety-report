---
title: A claim has a stable ID, and the traceability matrix is generated
description: Every scenario carries one stable claim ID as a tag, every normative docs constraint carries a CON id naming what verifies it, and .spec/traceability.md is generated from both.
type: adr
status: partially-superseded
date: 2026-09-22
decision-makers: Chase Florell
keywords: traceability, claim IDs, Gherkin tags, generated documentation, CI gate, drift
---

# ADR-0084 — A claim has a stable ID, and the traceability matrix is generated

**Status:** Accepted. Amended by
[ADR-0101](ADR-0101-ci-regenerates-the-traceability-matrix.md): CI
regenerates the matrix onto a same-repo pull request, and a branch must be up
to date to merge. Amended by
[ADR-0106](ADR-0106-every-line-of-the-matrix-derives-from-one-source-item.md):
the matrix carries no totals and one block per item, so git can merge it. Paths amended by
[ADR-0183](ADR-0183-the-specification-lives-in-a-spec-directory.md). Amended by
[ADR-0184](ADR-0184-a-generated-map-binds-every-claim-to-its-step-definitions.md).
Partially superseded by
[ADR-0191](ADR-0191-each-rule-is-stated-once-and-no-status-page-is-written-by-hand.md):
the two narrative pages are no longer kept by hand.

## Context

[ADR-0083](ADR-0083-specification-driven-development.md) makes the
specification the thing a change is judged against. Judging against it requires
naming a piece of it.

Today a claim is identified by its scenario's prose name. That name is not
stable — improving the wording of a scenario silently renames the claim — and
it is not citable: an ADR cannot say which claim it changes, a pull request
cannot say which claims it satisfies, a review finding cannot point at the
claim a diff exceeded, and an issue cannot list the claims that close it.

The traceability that exists is narrative and hand-written:
[`docs/implementation-status.md`](https://github.com/HPAC-Safety/safety-report/blob/46af5841/docs/implementation-status.md) and
[`docs/issue-traceability.md`](../../docs/issue-traceability.md). Both are valuable as
an audit of where main stands against the target, and both are maintained by a
person remembering to maintain them.

## Decision

**Every scenario carries exactly one stable claim ID, as a Gherkin tag.**

The form is `@REQ-<AREA>-<NNN>`, three digits, one tag per `Scenario` or
`Scenario Outline`; an outline's `Examples` rows share their outline's ID. Area
prefixes are `REQ-AI`, `REQ-DOM`, `REQ-MED`, `REQ-MOD`, `REQ-QB`, `REQ-SUB`,
`REQ-TF`, and `REQ-WLD`, one per folder under `.spec/features/`.

**An ID is never reused and never renumbered.** A deleted scenario leaves a
gap. Rewording a scenario keeps its ID; a scenario asserting genuinely
different behaviour is a new claim with a new ID. This is what makes a claim
citable from a commit that is already in history.

**A normative constraint in a canonical `docs/` page carries a `CON-<PAGE>-<NNN>`
ID** and names what verifies it: one or more `REQ-*` IDs, or
`none — <reason>` when no scenario can verify it. Those pages hold claims that
are real and not expressible as a scenario — a schema constraint, a deployment
topology, a testing obligation — and leaving them anonymous would make the
matrix quietly incomplete rather than visibly incomplete.

**The matrix is generated from the artifacts, committed, and drift-checked.**
`tools/spec/generate-traceability.ts` reads the feature files and the `CON-*` claims and
writes `.spec/traceability.md`. It is dependency-free, like every other tool in
`tools/`: the grammar it consumes is two line shapes, and
`tools/gherkin/verify.ts` has already proved with the official Cucumber parser
that the files are valid Gherkin, so the generator never has to be the thing
that discovers a syntax error. CI regenerates it and fails on a difference, in
the same shape as the existing skill-install and `dotnet format` drift checks.
The generator exits non-zero on a duplicate ID, a malformed ID, a scenario with
no ID, or a `Verified by:` naming an ID that does not exist.

Tags are inert to both runners: Reqnroll turns a tag into an xUnit trait and
`playwright-bdd` into a Playwright tag, so `--filter "Category!=ui"` and
`tags: "@ui and not @ignore"` are unaffected.

## Consequences

- A claim can be cited anywhere: in an ADR, an issue, a pull-request body, a
  review finding, a lesson, or a commit message.
- `.spec/traceability.md` is generated and never hand-edited; it joins the
  generated-files table in [`docs/agent-workflow.md`](../../docs/agent-workflow.md).
- A scenario added without an ID fails the build rather than quietly escaping
  the matrix.
- `docs/implementation-status.md` and `docs/issue-traceability.md` remain as
  the narrative audit. The generated matrix answers "is every claim accounted
  for"; those pages answer "how far is main from the target".
- Renaming a scenario is now cheap and renumbering is forbidden, which is the
  opposite of the pressure prose names create.

## Alternatives

- **No IDs; rely on scenario names.** Rejected: the scenario-to-test link is
  already mechanical, but the human-to-claim link is not. A finding that says
  "this exceeds the claim about skipped answers" cannot be checked; one that
  says `REQ-SUB-005` can.
- **Compute the matrix in CI and never commit it.** Rejected on this
  repository's own evidence: ADR-0073 records what happens when a guard exists
  only where the command line is typed. A contributor must be able to read the
  matrix from a clean clone.
- **Put IDs in a separate registry file mapping IDs to scenarios.** Rejected: a
  second file to keep in step with the first is the drift this decision exists
  to remove. The tag lives on the scenario it names.
- **Number the claims in the scenario name instead of a tag.** Rejected: a name
  is displayed in test output and read aloud in review; a tag is structured
  data both runners already parse, and it survives a wording change.

## Related

- [ADR-0049](ADR-0049-reqnroll-for-executable-gherkin-scenarios.md)
- [ADR-0053](ADR-0053-ui-scenarios-execute-via-playwright-bdd.md)
- [ADR-0073](ADR-0073-a-ui-scenario-is-skipped-by-reqnroll-itself.md)
- [ADR-0083](ADR-0083-specification-driven-development.md)

## Amendment (2026-09-30)

The matrix is `.spec/traceability.md`, and the canonical constraint pages are the five `.spec/*.md` pages listed in `tools/spec/spec-paths.ts`. A generated `.spec/README.md` indexes the areas, constraint pages, decisions, and lessons beside it. ([ADR-0183](ADR-0183-the-specification-lives-in-a-spec-directory.md))

## Amendment (2026-09-30, ADR-0184)

The generated specification files are three: the matrix, the index (`.spec/README.md`), and the step-bindings map (`.spec/bindings.md`), which lists the step-definition files that bind each claim. ([ADR-0184](ADR-0184-a-generated-map-binds-every-claim-to-its-step-definitions.md))
