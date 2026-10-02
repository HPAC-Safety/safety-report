---
title: The coverage baseline walks back to the newest run that ran coverage
description: Main's newest green push run does not always carry a coverage-report artifact, since the coverage job is skipped on a docs-, infra-, or workflow-only change; a shared script walks back to the newest run that did, so the ratchet stops silently sitting out.
type: adr
status: accepted
date: 2026-09-28
decision-makers: Chase Florell
keywords: coverage baseline, ratchet, gh run list, artifacts, ci.yml, ci-local.sh, ADR-0014, ADR-0147
---

# ADR-0165 — The coverage baseline walks back to the newest run that ran coverage

**Status:** Accepted. Narrows
[ADR-0014](ADR-0014-coverage-gate.md) (decision 3: which run supplies the
baseline) and the coverage section of
[ADR-0147](ADR-0147-pull-requests-merge-through-a-merge-queue.md). Neither
decision changes: the baseline is still only ever a `push` run on `main`
(ADR-0014, ADR-0147) - this only changes *which* one.

## Context

Every pull request's coverage comment read "No main baseline was available,
so the ratchet did not run." (#589). The `coverage` job is skipped whenever a
change touches no .NET or e2e code (`changes`'s filter), so `main`'s newest
successful `push` run is frequently one that never ran coverage and so never
uploaded a `coverage-report` artifact. `ci.yml`'s "Fetch the main baseline"
step, and the identical lookup in `tools/dev/ci-local.sh`, took only that newest
run (`gh run list --limit 1`) and gave up the moment it lacked the artifact -
even though an earlier green run has a perfectly good one, well inside the
artifact's retention window.

Eight consecutive `main` push runs showed the pattern: the three newest
skipped `coverage` entirely (docs/infra changes), and the next five all
carried a live artifact.

## Decision

**Walk back.** `tools/coverage/find-coverage-baseline.mjs` is now the one place either
caller finds the baseline:

- List successful `push` runs of `CI` on `main`, newest first
  (`gh run list --branch main --event push --status success --limit 20`).
- For each, in order, check whether it carries a non-expired `coverage-report`
  artifact (`gh api repos/{owner}/{repo}/actions/runs/{id}/artifacts`). The
  first one that does is the baseline.
- Each candidate's event, branch, and conclusion are asserted again in the
  script rather than trusted from `gh run list`'s own filters - a
  merge-group run (branch `gh-readonly-queue/main/*`) or a fork pull request
  whose branch happens to be named `main` must never qualify, exactly as
  ADR-0147 already requires for the single-run lookup it replaces.
- Giving up after `--limit` runs (20, generous enough to cross several
  docs-only merges in a row) is not a failure: it is today's outcome, applied
  further back - a visible notice, ratchet skipped, the floor still gates.
- `ci.yml`'s "Fetch the main baseline" step and `tools/dev/ci-local.sh` both call
  the script and nothing else, so they can never pick a different run.
- **The coverage comment now names the baseline run** it used
  (`coverage-gate.mjs --baseline-run-id`), so a reader can open the exact run
  the ratchet compared against.
- **No retention change.** The `coverage-report` artifact stays at the
  repository default; walking back further only reaches runs already inside
  that window.

**Rejected: force `coverage` to run on every `main` push**, regardless of
`changes`. That was the fast, one-line fix, and directly against the reason
`changes` exists: keeping a docs- or infra-only merge cheap. It would trade
one problem (a stale baseline) for the one `changes` was written to prevent.

**Rejected: widen the artifact retention instead.** Retention was never the
constraint - the artifacts eight runs back were all still live. The problem
was that the lookup stopped at the first (wrong) run, not that the right run
had expired.

## Consequences

- A pull request opened after a run of docs-, infra-, or workflow-only
  merges gets a real baseline again, and the ratchet runs.
- Two `gh` calls become up to `1 + limit`: one `run list`, plus one
  `artifacts` lookup per candidate walked past. In the ordinary case (the
  newest run ran coverage) this is unchanged from before.
- `tools/coverage/find-coverage-baseline.mjs`'s selection logic
  (`isEligible`, `selectBaselineRun`) is unit-tested directly; a CLI-level
  test stubs `gh` the same way `tools/dev/ci-local.sh`'s own tests already stub
  `gh` and `act`.
