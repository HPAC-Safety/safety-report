---
title: A built claim counts only when its scenario passed in the run
description: Both engines write Cucumber Messages keyed by each scenario's claim tag; the coverage job joins them with .spec/claims.json and fails when a built claim of an engine that ran has no passing execution or any failing one. claims.json keeps the static status, Planned or Built; the per-run status is a job summary and an artifact, never committed.
type: adr
status: accepted
date: 2026-10-03
decision-makers: Chase Florell
keywords: claims, test results, Cucumber Messages, Reqnroll, playwright-bdd, coverage job, CI artifact, Built, Planned, Passing, Failing, Unexecuted, claims.json, ADR-0049, ADR-0053, ADR-0073, ADR-0193
---

# ADR-0195 — A built claim counts only when its scenario passed in the run

**Status:** Accepted. Decided by the owner on 2026-10-03 in
[#813](https://github.com/HPAC-Safety/safety-report/issues/813), part of
[#809](https://github.com/HPAC-Safety/safety-report/issues/809) (decision 2).

## Context

- A claim's status in `.spec/claims.json` and `.spec/traceability.md` came
  from its tags alone: `Covered` meant "not `@ignore`". A scenario that
  failed, was skipped at run time, or never ran — filtered out, in a project
  nobody runs, or in a job its path filter skipped — still read `Covered`.
- Nothing tied a test run back to a claim. The `.NET` job's TRX names a test
  by its scenario title and generated class, not by its tag; Playwright's
  list reporter prints titles to a log.
- A feature-file-only change (a new scenario reusing existing step
  definitions) ran no test at all: neither the .NET nor the e2e path filter
  listed `.spec/features/`.
- On 2026-10-03 every one of the 756 built claims passed: 439 in Reqnroll,
  317 in playwright-bdd. The gate starts green.

## Decision drivers

- A built claim must be proved by a run, not by a tag.
- Keyed by the claim tag, never by matching titles or generated names.
- `claims.json` stays deterministic and merge-friendly (ADR-0193): nothing
  that changes per run is committed.
- No new required check name, and runnable locally.

## Considered options

- **Read Reqnroll's TRX.** Rejected: a TRX carries no Gherkin tags, only the
  scenario title and a generated method name, so a claim would be found by
  title — which two scenarios may share, and an outline row decorates.
- **Playwright's JSON reporter.** Rejected: it carries tags, but it would be
  a second format and parser beside Reqnroll's, for the same facts.
- **Commit each claim's last result in `claims.json`.** Rejected: a result
  belongs to a run, not to the specification. It would change on every run,
  differ between branches, conflict on every merge, and record a run of one
  commit as the truth of another.
- **Judge each engine inside its own job.** Rejected: it needs no artifact
  hand-over, but no one place would report every claim, and the verdict
  would be split across two job summaries.
- **A new required `claims` job.** Rejected: a new context to add to the
  ruleset, and `coverage` already waits on both suites and reads their
  artifacts.
- **Cucumber Messages from both engines, judged in `coverage`** — chosen.

## Decision

1. **Both engines write Cucumber Messages**, the format both already
   implement: Reqnroll's `message` formatter, turned on in `ci.yml`'s `test`
   job by `REQNROLL_FORMATTERS`, and playwright-bdd's
   `cucumberReporter('message')` in `tests/e2e/playwright.config.ts`. Each
   pickle carries its scenario's tags, so an execution is keyed by its
   `@REQ-*` tag. They land in `artifacts/claims/`, gitignored.
2. **`tools/spec/check-claim-results.ts` judges a built claim** of an engine
   that ran:
   - **Passing** — every pickle (one per Examples row of an outline) ran and
     its final attempt passed; a retried attempt a later one replaced is not
     the verdict, as it is not Playwright's;
   - **Failing** — a pickle's final attempt failed, or a step was undefined,
     ambiguous, or pending;
   - **Unexecuted** — no pickle ran, or one was skipped or never reached a
     step.
   Failing or Unexecuted fails the run. A `Planned` (`@ignore`) claim is
   never judged.
3. **Which engines are judged.** Reqnroll whenever `coverage` runs, since
   `test` ran; playwright-bdd whenever `e2e` ran. An engine whose job the
   path filter skipped is reported as not run, not judged: the filter
   decided nothing it serves changed. A named engine whose results file is
   missing fails every built claim of it. `.spec/features/**` joins the .NET
   and e2e path filters, so a scenario-only change runs both suites.
4. **Where.** `test` uploads `claim-results-reqnroll` and `e2e`
   `claim-results-playwright-bdd`, both even when a test failed; `coverage`,
   already required and already waiting on both, downloads them and runs the
   gate after its coverage gate, whatever that decided. It uploads
   `claim-results`, every claim's result as JSON, and writes every claim's
   result to the job summary. Under act, the per-run share
   `tools/dev/ci-local.sh` mounts stands in for the artifacts, as it does for
   the coverage reports; `tools/dev/ci-local.sh --job coverage` or `--full`
   runs the gate locally.
5. **Static status.** `claims.json` and the matrix keep a claim's static
   status, from its tags: `Planned` (`@ignore`) or `Built` — renamed from
   `Covered`, which claimed what only a run can show. The per-run status is
   never committed.

## Consequences

- A built claim whose scenario is skipped, filtered out, or never generated
  fails `coverage`, with its ID, scenario, and feature file in the
  annotation, where before it passed silently.
- With its formatter on, Reqnroll 3.3.4 turns a scenario a hook skips at run
  time into a failed test ("Stack empty"), so such a claim fails `test`
  before `coverage` judges it; either way the build fails. The `@ui` hook
  never runs in CI, where the category filter keeps those scenarios out.
- A feature-file-only change now runs the .NET and browser suites.
- The Reqnroll results file is about 8 MB and the playwright-bdd one about
  5 MB; each is kept for seven days.
- A pull request whose `test` job fails skips `coverage`, as before; the
  failing claims are then read from the uploaded results or the test log.
- Anything that reads `"status": "Covered"` reads `"Built"`.

## Related

- [ADR-0193](ADR-0193-the-claims-are-generated-as-json-a-graph-fragment-and-one-slim-matrix.md):
  `claims.json` is the canonical claim data; unchanged but for the status
  value.
- [ADR-0049](ADR-0049-reqnroll-for-executable-gherkin-scenarios.md),
  [ADR-0053](ADR-0053-ui-scenarios-execute-via-playwright-bdd.md),
  [ADR-0073](ADR-0073-a-ui-scenario-is-skipped-by-reqnroll-itself.md): which
  engine runs which claim; unchanged.
- [ADR-0145](ADR-0145-a-pull-requests-checks-run-locally-under-act.md): the
  local gate under act.
- [CONV-001](../conventions/CONV-001-a-scenario-counts-only-in-its-own-area-and-an-ignored-one-names-its-issue.md):
  the process rules decided in the same issue.
