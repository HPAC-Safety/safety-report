---
title: A generated map binds every claim to its step definitions
description: tools/spec/generate-bindings.ts resolves every scenario step to the step definition its runner would bind and writes .spec/bindings.md — per claim, the files that bind it. A built claim with an unbound step fails the docs job; stale @ignore claims, unused step definitions, and ambiguous steps are listed, not failed. The specification is the authority.
type: adr
status: accepted
date: 2026-09-30
decision-makers: Chase Florell
keywords: traceability, step definitions, bindings, Reqnroll, playwright-bdd, Cucumber Expressions, specification, drift, knowledge graph, generated file, ADR-0083, ADR-0084, ADR-0088, ADR-0101, ADR-0106, ADR-0183
---

# ADR-0184 — A generated map binds every claim to its step definitions

**Status:** Accepted. Decided by the owner on 2026-09-30 in
[#710](https://github.com/HPAC-Safety/safety-report/issues/710). Amends
[ADR-0083](ADR-0083-specification-driven-development.md),
[ADR-0084](ADR-0084-stable-claim-ids-and-a-generated-traceability-matrix.md),
[ADR-0088](ADR-0088-the-matrix-carries-the-specification-into-the-graph.md),
[ADR-0101](ADR-0101-ci-regenerates-the-traceability-matrix.md),
[ADR-0106](ADR-0106-every-line-of-the-matrix-derives-from-one-source-item.md),
and [ADR-0183](ADR-0183-the-specification-lives-in-a-spec-directory.md).
Amended on 2026-09-30 by
[#711](https://github.com/HPAC-Safety/safety-report/issues/711): a large area is
grouped with `Rule:` blocks.

## Context

The traceability matrix (ADR-0084) lists every claim with its scenario, engine,
and status. Nothing linked a claim to the code that proves it:

- **The graph.** An audit of the knowledge graph after the move to `.spec/`
  found all 727 claim nodes connected only to the matrix. No node under `src/`
  or `tests/` names a claim. The graph could say a claim exists, but not which
  test proves it — the bridge ADR-0088 built stopped at the matrix.
- **The code.** Nothing checked the step definitions against the
  specification beyond a test passing. Two drifts would go unseen:
  - a step definition that no scenario uses any more;
  - a scenario still tagged `@ignore` although every step it needs is bound.

  Each runner fails an unbound step at run time. But the browser suite runs
  only `@ui and not @ignore`, Reqnroll skips `@ui` scenarios (ADR-0073), and
  no gate looked at the whole picture.

The owner's rule for this repository: the specification is the source of
truth, and the code second.

## Decision

`tools/spec/generate-bindings.ts` writes `.spec/bindings.md`, a generated file beside the
matrix.

- **The map.** For each claim, it lists the step-definition files that bind its
  steps, as relative links. The knowledge graph reads those links as edges from
  the claim to the test file.
- **How a step resolves.** It mirrors each runner rather than running it:
  - **Reqnroll**, for every claim not tagged `@ui`:
    - matches by keyword; And, But, and `*` take the keyword before them;
    - reads a pattern as a regex when it starts with `^`, ends with `$`, or
      carries a regex group, otherwise as a Cucumber Expression — Reqnroll's
      own rule;
    - anchors a regex at both ends;
    - drops a binding whose class is scoped to another feature, then prefers a
      binding scoped to this one.
  - **playwright-bdd**, for every claim tagged `@ui`: matches by text alone. A
    string is a Cucumber Expression; a regex literal keeps its flags.
  - Background steps are part of every scenario, and a Scenario Outline is
    expanded once per Examples row.
- **What fails.** A built claim — one not tagged `@ignore` — with a step no
  binding in its engine matches fails the required `docs` job. The fix is the
  step definition or the scenario, and the scenario wins: change it only when
  it said the wrong thing, never to fit the code.
- **What is listed, not failed:**
  - an `@ignore` claim whose every step is already bound (stale `@ignore`);
  - a step two bindings match (ambiguous);
  - a step definition no scenario uses.
- **It reads, never runs.** The tool is dependency-free, like the matrix,
  because `traceability.yml` runs the base branch's copy without installing
  anything. Step files are read as text and their patterns compiled as regular
  expressions. A construct it does not understand fails loudly instead of
  matching the wrong steps:
  - a Cucumber parameter type other than `{string}`, `{word}`, `{int}`, or `{}`;
  - a method-level or tag `[Scope]`;
  - a non-verbatim attribute string;
  - a template-literal step.

  A test checks the Cucumber Expression converter against the official library
  wherever the browser suite's dependencies are installed.
- **It merges like the matrix.** Every block derives from one claim, step, or
  step definition, nothing is counted across the tree, and no line numbers
  appear, so moving code does not churn the file (ADR-0106).
  - `traceability.yml` regenerates it with the matrix and the index, also when
    only a step file changes.
  - The post-merge and post-rewrite hooks and `tools/dev/ci-local.sh` regenerate it.
  - `.gitattributes` keeps the checked-out side on a conflict.
- **The rule is recorded.**
  [CON-TQ-010](../testing-and-quality.md) records it, and the generated index
  links the map.

The first run on the tree found no built claim with an unbound step and no
stale `@ignore` claim. It found 20 step definitions no scenario uses — listed
in the map, left for a follow-up issue.

## Consequences

- A claim now leads to its test files, in the map and in the knowledge graph.
- Renaming a step in a definition without its scenario, or in a scenario without
  its definition, fails `docs` before the test suites run.
- A step file change triggers the bot's regeneration, so the map stays current
  without a pull request of its own.
- A new step-definition idiom must be taught to the tool before it is used; the
  tool says so instead of guessing.

## Alternatives

- **Add the bound files to `traceability.md`.** Rejected: every step-file edit
  would churn the matrix, and it would change the block shape ADR-0106 fixes.
- **Use `@cucumber/cucumber-expressions` directly.** Rejected: the bot runs the
  base branch's tools with no install. The parity test keeps the converter
  honest instead.
- **Read the runners' own reports** (`bddgen export`, a Reqnroll binding
  report). Rejected: `bddgen export` gives no file locations, and Reqnroll's
  needs a .NET build the `docs` job does not do.
- **Fail on unused definitions and stale `@ignore` too.** Rejected by the owner
  for now: they are worth seeing, not worth blocking a pull request over.

## Related

- [ADR-0049](ADR-0049-reqnroll-for-executable-gherkin-scenarios.md)
- [ADR-0053](ADR-0053-ui-scenarios-execute-via-playwright-bdd.md)
- [ADR-0073](ADR-0073-a-ui-scenario-is-skipped-by-reqnroll-itself.md)
- [ADR-0083](ADR-0083-specification-driven-development.md)
- [ADR-0084](ADR-0084-stable-claim-ids-and-a-generated-traceability-matrix.md)
- [ADR-0088](ADR-0088-the-matrix-carries-the-specification-into-the-graph.md)
- [ADR-0106](ADR-0106-every-line-of-the-matrix-derives-from-one-source-item.md)
- [ADR-0183](ADR-0183-the-specification-lives-in-a-spec-directory.md)

## Amendment (2026-09-30, #711)

**A large area is grouped with Gherkin `Rule:` blocks, not split.** The two
largest areas held about 200 scenarios each in one flat file:
`question-bank-and-form` and `moderation-authentication-and-publication`. Each
now groups its scenarios under `Rule:` blocks inside the same file — 15 and 17
of them — following the sections of the area's README.

- **Nothing that names an area changes.** The Feature titles stay, so every
  `[Scope(Feature = …)]` binding still resolves. Claim IDs, area directories,
  and every link into the files are untouched.
- **Both runners and both generated files agree.** Reqnroll lists the same
  1,183 tests and playwright-bdd the same 401. The matrix and the step-bindings
  map regenerate byte-identical.
- **A Rule carries no tags.** Gherkin would let a tag on a Rule reach every
  scenario beneath it, but the matrix takes a claim's engine and status from
  the scenario's own tags only. So `tools/spec/generate-traceability.ts` fails a tagged
  Rule rather than let the two disagree.
- **A Rule may have its own Background**, which `tools/spec/generate-bindings.ts` adds to the
  scenarios in that Rule only.
- **Rejected:** splitting each area into new directories. It would rename areas,
  rescope four step-definition classes, and rewrite some forty links for no
  change in what is specified.
