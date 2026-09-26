---
title: Issue traceability drift opens an issue and gates nothing
description: The source inventory is checked in the required docs job, but the issue-traceability page is checked daily and on push to main by a non-required workflow that keeps one drift issue open, because open issues change without any commit.
type: adr
status: accepted
date: 2026-09-26
decision-makers: Chase Florell
keywords: issue traceability, source inventory, drift, docs job, required checks, scheduled workflow, GitHub issues
---

# ADR-0143 — Issue traceability drift opens an issue and gates nothing

**Status:** Accepted.

## Context

Since #437, two pages must stay current:

- [`docs/source-inventory.md`](../source-inventory.md) maps every directory under `src/`;
- [`docs/issue-traceability.md`](../issue-traceability.md) lists every open issue.

Nothing enforced either one. Both had drifted for a month, and
`docs/testing-and-quality.md` claimed a check that never existed. #444 asked
for a check in the required `docs` job that fails on either kind of drift.

The source inventory depends only on the commit, so a required check can
judge it. The issue page depends on live GitHub state, which changes without
any commit:

- A pull request that says `Closes #N` has to keep #N's row while #N is open.
  The row goes stale the moment it merges, so `main` and every other open
  pull request would fail.
- Filing any issue would fail `docs` on every open pull request until some
  pull request added its row.

## Decision

- **Source inventory: a required check.** `tools/check-inventories.mjs` runs
  in the `docs` job. It fails when a directory under `src/` holding tracked
  files has no row, or a row names a directory that no longer exists. It
  takes the directories from `git ls-files`, so build output never counts. A
  directory holding only other directories needs no row.
- **Issue traceability: a drift issue, never a failed check.**
  `tools/issue-traceability.mjs --sync` runs from
  `.github/workflows/issue-traceability.yml` daily, on push to `main`, and on
  dispatch, and never on a pull request. It keeps one "Issue traceability
  drift" issue:
  - opened, with a label and milestone, when an open issue has no row or a row
    names an issue that is not open;
  - updated while the drift lasts;
  - closed once the page matches.

  Without a token it reads nothing and exits with a notice. Run locally
  without `--sync`, it reports the drift and exits 1.
- A pull request that closes an issue removes that issue's row.

## Alternatives considered

- **Both in the required `docs` job, as #444 first asked.** Rejected for the
  issue page for the reasons above.
- **A required issue check that tolerates rows the pull request closes and
  issues filed after its base.** Rejected: every exception has to be argued
  against live state, and the check would still depend on things outside the
  commit.
- **No issue check.** Rejected: that is how the page drifted for a month.

## Consequences

- A new directory under `src/` cannot merge without a row in the inventory.
- Issue-page drift is visible as an open issue within a day and blocks
  nothing. Someone still has to act on it.
- The workflow holds `issues: write` and runs only on `main`'s own triggers,
  so a fork cannot make it write.
