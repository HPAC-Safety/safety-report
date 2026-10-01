---
title: The specification lives in a .spec directory
description: Everything the specification chain reads — feature areas, constraint pages, decisions, lessons, and the traceability matrix — moves into .spec/, whose README is a generated index. Every relative link is checked, and an ADR's status must agree with its status line. docs/ keeps guides only, and .gitattributes is tracked.
type: adr
status: accepted
date: 2026-09-30
decision-makers: Chase Florell
keywords: specification, .spec, directory layout, index, generated, link check, ADR status, lessons, decisions, traceability, gitattributes, drift, ADR-0083, ADR-0084, ADR-0085, ADR-0087, ADR-0091, ADR-0101, ADR-0106
---

# ADR-0183 — The specification lives in a .spec directory

**Status:** Accepted. Decided by the owner on 2026-09-30 in
[#707](https://github.com/HPAC-Safety/safety-report/issues/707). Amends the
paths in [ADR-0083](ADR-0083-specification-driven-development.md),
[ADR-0084](ADR-0084-stable-claim-ids-and-a-generated-traceability-matrix.md),
[ADR-0085](ADR-0085-a-lesson-flows-upstream-into-the-specification.md),
[ADR-0087](ADR-0087-every-markdown-file-declares-itself.md),
[ADR-0088](ADR-0088-the-matrix-carries-the-specification-into-the-graph.md),
[ADR-0090](ADR-0090-an-exemption-cites-the-claims-it-preserves.md),
[ADR-0091](ADR-0091-an-adr-number-is-verified-not-assumed.md),
[ADR-0101](ADR-0101-ci-regenerates-the-traceability-matrix.md), and
[ADR-0106](ADR-0106-every-line-of-the-matrix-derives-from-one-source-item.md). Amended by
[ADR-0184](ADR-0184-a-generated-map-binds-every-claim-to-its-step-definitions.md).

## Context

Specification-driven development (ADR-0083) makes every hop a tracked file,
but the files sat in three roots:

- `features/<area>/` held the scenarios and each area's supporting page.
- `docs/decisions/` held 174 ADRs, with no index of any kind.
- `docs/lessons/` held 39 lessons, indexed by a table kept by hand.
- Five pages carrying `CON-*` constraints sat in `docs/` beside sixteen
  how-to guides, distinguishable only by a list inside
  `tools/traceability.mjs`.
- The generated matrix was `docs/traceability.md`.

Nothing indexed the area pages: `features/README.md` linked each `.feature`
file but not the README beside it. About 2,000 relative links were checked by
nothing; the first check this change wrote found one already broken, in a
C# doc comment. And an ADR's frontmatter `status:` could disagree with its
own `**Status:**` line: four records said they were partially superseded while
declaring `accepted`.

Each of those is the same failure: a rule kept in prose, or a list kept by
hand, drifts. The rules that held were the ones a tool enforces.

## Decision

- **Layout.**

  ```
  .spec/
    README.md                 generated index
    features/<area>/          <area>.feature and README.md
    features/README.md        authority rules, product contract, guardrails
    decisions/                ADR-NNNN-*.md and README.md
    lessons/                  NNNN-*.md and README.md
    <five constraint pages>   system-overview.md, data-and-persistence.md, …
    traceability.md           generated matrix
  docs/                       guides only
  ```

- **Placement rule.** A page lives in `.spec/` when the specification chain
  reads it: it carries scenarios, `CON-*` IDs, a decision, or a lesson. A page
  that explains how — setup, deployment, conventions, status — lives in
  `docs/`. `docs/implementation-status.md` stays there: it reports progress
  against the specification, and is not part of it.
- **The name is `.spec`.** It is tooling-facing, like `.github`; GitHub renders
  it, and graphify reads dot-directories, so the specification still reaches
  the graph (ADR-0088).
- **One home for the paths.** `tools/spec-paths.mjs` exports them; every tool
  imports it. A hook or workflow, which cannot import a module, is tied to it
  by `tests/js/spec-paths.test.mjs`.
- **A generated index.** `tools/spec-index.mjs` writes `.spec/README.md`:
  - every area with its claim prefix, scenario, `@ignore`, and `@ui` counts,
    and its supporting page;
  - every constraint page with its count;
  - every decision with its status and date;
  - every lesson with its description, the claims and skills it changed, its
    issue, and its status.

  Each row derives from one file, with no whole-tree total, so branches merge
  it as they merge the matrix (ADR-0106). It replaces the hand-kept lessons
  table and the features page's index table.
- **Regenerated like the matrix.** The `docs` CI job fails a stale index.
  `traceability.yml` regenerates both generated files with the base branch's
  tools and commits them in one bot commit (ADR-0101). The post-merge and
  post-rewrite hooks regenerate both, and `tools/ci-local.sh` does what the
  bot would.
- **Every relative link is checked.** `tools/check-links.mjs` resolves every
  relative link in tracked markdown, and a C# `<see href>`, against the
  tracked tree, and checks each `#anchor` against the target's headings.
  - It runs in the `docs` CI job.
  - Pre-commit checks the staged markdown, and the whole tree when a commit
    deletes or renames a file.
  - Code fences, code spans, comments, and external URLs are skipped.
- **An ADR's status agrees with its status line.** `tools/adr-numbers.mjs`
  already reads every ADR, so the same pass fails:
  - a `status:` outside `accepted`, `partially-superseded`, `superseded`;
  - an `accepted` record whose status line says "superseded by ADR-NNNN";
  - a superseded record whose status line links nothing that replaced it;
  - a successor that does not exist.

  A lesson's `status:` is held to `accepted` or `superseded` by
  `check-frontmatter.mjs`.
- **A file left behind is refused.** `check-frontmatter.mjs` fails any tracked
  file under `docs/decisions/`, `docs/lessons/`, or `features/`. A branch not
  yet rebased past the move therefore fails with the fix named.
- **ADR numbers survive the move.** `adr-numbers.mjs --next` scans both
  `.spec/decisions` and `docs/decisions` on every remote branch, each on its
  own. It used to stop at the first ref without the directory, which after the
  move would have been every unrebased branch. The legacy path is removed once
  no remote branch carries it.
- **`.gitattributes` is tracked.** It names `merge=ours` for the two generated
  files and graphify's driver for its graph. `init-dev.sh` registers the
  `ours` driver per clone; it never did before, because the variable guarding
  that step was never set.

## Consequences

- The specification has one root and one generated index of it. "Where is the
  ADR on X" and "which areas still have planned scenarios" are answered by
  reading one file.
- A dangling link, a stale index, or an ADR contradicting its own status fails
  before commit, and again in CI, instead of rotting quietly.
- A pull request open across the move rebases, then moves any new spec file
  under `.spec/`; `check-frontmatter.mjs` says so.
- A clone that held the old untracked `.gitattributes` removes it once, to
  check out the tracked one: `rm .gitattributes && git checkout -- .gitattributes`.
  `./init-dev.sh` re-run installs the hooks and the merge driver.
- The rewrite edited historical ADR and lesson text only where it named a
  moved path, so every reference still resolves.

## Alternatives

- **A visible `spec/` directory.** Rejected: the owner chose the dot-directory
  convention this repository already uses for tool-facing roots.
- **Numbered prose specifications (`docs/specs/00-overview.md`, …)**, as in
  common Claude Code guides. Rejected: tagged Gherkin with stable claim IDs and
  `CON-*` constraints is already stronger, and generates its own matrix.
- **A hand-written index.** Rejected: the lessons table it replaces is the
  evidence that one drifts.
- **Leave the constraint pages in `docs/`.** Rejected: they carry the `CON-*`
  IDs the matrix is built from; they are specification, not guides.
- **Move `docs/implementation-status.md` too.** Rejected: it is a status
  report, written by hand as work lands.
- **Mark only the ADR index regions as generated inside the hand-written
  READMEs.** Rejected: mixing prose and generated rows in one file invites an
  edit the generator then throws away.

## Related

- [ADR-0047](ADR-0047-feature-files-must-not-contradict-adrs.md)
- [ADR-0083](ADR-0083-specification-driven-development.md)
- [ADR-0084](ADR-0084-stable-claim-ids-and-a-generated-traceability-matrix.md)
- [ADR-0085](ADR-0085-a-lesson-flows-upstream-into-the-specification.md)
- [ADR-0087](ADR-0087-every-markdown-file-declares-itself.md)
- [ADR-0088](ADR-0088-the-matrix-carries-the-specification-into-the-graph.md)
- [ADR-0091](ADR-0091-an-adr-number-is-verified-not-assumed.md)
- [ADR-0101](ADR-0101-ci-regenerates-the-traceability-matrix.md)
- [ADR-0106](ADR-0106-every-line-of-the-matrix-derives-from-one-source-item.md)
- [ADR-0182](ADR-0182-a-role-agent-declares-its-model-and-effort.md)

## Amendment (2026-09-30, ADR-0184)

`tools/spec-paths.mjs` also exports `BINDINGS` (`.spec/bindings.md`) and the two step-definition roots, `REQNROLL_STEPS` and `PLAYWRIGHT_STEPS`. ([ADR-0184](ADR-0184-a-generated-map-binds-every-claim-to-its-step-definitions.md))
