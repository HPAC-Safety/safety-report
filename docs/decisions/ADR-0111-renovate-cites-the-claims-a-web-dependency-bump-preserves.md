---
title: Renovate cites the claims a web dependency bump preserves
description: A Renovate pull request that moves a src/web package writes the dependency exemption itself, citing Playwright-covered claims that the same pull request's required checks re-prove, so feature-coverage passes without being relaxed.
type: adr
status: accepted
date: 2026-09-24
decision-makers: Chase Florell
keywords: Renovate, feature-coverage, exemption, dependency, claims, ADR-0090
---

# ADR-0111 — Renovate cites the claims a web dependency bump preserves

**Status:** Accepted. Applies
[ADR-0090](ADR-0090-an-exemption-cites-the-claims-it-preserves.md) to Renovate
and changes nothing in it.

## Context

`feature-coverage` counts every file under `src/**` as behavior-bearing. A
Renovate pull request that bumps a web package changes `src/web/package.json`
or `src/web/package-lock.json`. That is behavior-bearing by this definition,
and it has no scenario, so the check failed on every such pull request. #400,
a vite patch bump, is the example.

NuGet bumps never hit this, because central package management puts every
version in the root `Directory.Packages.props`, outside `src/**`.

ADR-0090 already built the right valve. The `dependency` category fails unless
only manifests and lock files changed, and `Claims preserved:` must name claim
IDs that exist in the matrix. The rule was not wrong. Renovate simply never
wrote the line.

## Decision

**Renovate writes the exemption itself.** A `packageRules` entry matching
`src/web/package.json` and `src/web/package-lock.json` appends this to the PR
body, in the exact shape `tools/feature-coverage.mjs` parses:

```
No .feature scenario needed: dependency — a web package or lock version moved; no application source file changed
Claims preserved: REQ-SUB-001, REQ-SUB-029, REQ-SUB-045, REQ-MOD-003, REQ-MOD-052, REQ-QB-076
```

**The cited claims are ones the same pull request re-proves.** Each is covered
by a Playwright scenario. A `src/web/**` change triggers the required `web` and
`e2e` checks, which run those scenarios against the bumped package before
anything merges. The citation is checked twice: once by the matrix, for
existence, and once by the browser, for truth. Between them the claims cover:

- the saved report
- paging
- upload on attach
- sign-in
- the admin report list
- question authoring

**The text is static, not templated.** A grouped update, such as Vite with
`@vitejs/plugin-react`, would otherwise repeat a per-dependency line. The diff
guard makes the reason checkable without naming the package.

## Alternatives considered

- **Skip `feature-coverage` for Renovate's pull requests.** Rejected. It exempts
  an actor rather than a change. A Renovate pull request that one day touches a
  source file would pass unexamined.
- **Remove lock files from the gate's pathspec.** Rejected. A lock file can
  change runtime behavior, and ADR-0090 says to revisit the pathspec only if the
  citation becomes a burden. Here the citation is written once, in
  configuration.
- **Add a scenario per bump.** Rejected. A patch bump adds no behavior to
  specify.

## Consequences

- A web dependency pull request passes `feature-coverage` on its own, and its
  body, which becomes the squash commit, records what it preserved.
- `git log --grep 'No .feature scenario needed: dependency'` lists every
  dependency bump alongside its citation.
- If a cited claim is ever renumbered away or deleted, the check fails loudly
  on the next bump, and `renovate.json` is where the fix goes.
- A Renovate pull request that changes anything besides a manifest or lock
  file still fails. That is intended.
