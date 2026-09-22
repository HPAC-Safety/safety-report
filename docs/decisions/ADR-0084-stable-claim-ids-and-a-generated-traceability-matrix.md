---
status: proposed
date: 2026-09-22
decision-makers: Chase Florell
keywords: traceability, claim IDs, Gherkin tags, generated documentation, CI gate, drift
---

# ADR-0084 — A claim has a stable ID, and the traceability matrix is generated

**Status:** Proposed. Accepted when [#286](https://github.com/HPAC-Safety/safety-report/issues/286)
lands and every scenario carries its ID; until then the feature files do not
yet assert what this record decides.

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
[`docs/implementation-status.md`](../implementation-status.md) and
[`docs/issue-traceability.md`](../issue-traceability.md). Both are valuable as
an audit of where main stands against the target, and both are maintained by a
person remembering to maintain them.

## Decision

**Every scenario carries exactly one stable claim ID, as a Gherkin tag.**

The form is `@REQ-<AREA>-<NNN>`, three digits, one tag per `Scenario` or
`Scenario Outline`; an outline's `Examples` rows share their outline's ID. Area
prefixes are `REQ-AI`, `REQ-DOM`, `REQ-MED`, `REQ-MOD`, `REQ-QB`, `REQ-SUB`,
`REQ-TF`, and `REQ-WLD`, one per folder under `features/`.

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
`tools/traceability.mjs` reads the feature files and the `CON-*` claims and
writes `docs/traceability.md`. CI regenerates it and fails on a difference, in
the same shape as the existing skill-install and `dotnet format` drift checks.
The generator exits non-zero on a duplicate ID, a malformed ID, a scenario with
no ID, or a `Verified by:` naming an ID that does not exist.

Tags are inert to both runners: Reqnroll turns a tag into an xUnit trait and
`playwright-bdd` into a Playwright tag, so `--filter "Category!=ui"` and
`tags: "@ui and not @ignore"` are unaffected.

## Consequences

- A claim can be cited anywhere: in an ADR, an issue, a pull-request body, a
  review finding, a lesson, or a commit message.
- `docs/traceability.md` is generated and never hand-edited; it joins the
  generated-files table in [`docs/agent-workflow.md`](../agent-workflow.md).
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
