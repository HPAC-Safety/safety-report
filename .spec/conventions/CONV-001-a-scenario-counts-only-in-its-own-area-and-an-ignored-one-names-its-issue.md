---
title: A scenario counts only in its own area, and an ignored one names its open issue
description: feature-coverage counts a changed scenario, or a cited claim, only when it belongs to an area the changed code maps to in .spec/area-paths.json, and a whitespace or comment edit counts as no change; a scenario may merge @ignore ahead of its code only while it names the open issue that will build it.
type: convention
status: accepted
date: 2026-10-03
---

# CONV-001 — A scenario counts only in its own area, and an ignored one names its open issue

## Rule

### Relevance

- `.spec/area-paths.json` maps every behavior-bearing path — everything under
  `src/`, and every e2e `.ts` that is not a step definition — to the feature
  areas whose scenarios describe it. `every` lists the paths that belong to
  every area: composition roots, shared primitives, persistence, and build
  files.
- A step definition is not listed: it belongs to the areas of the claims it
  binds, read from `.spec/claims.json`. A helper in a step directory that
  binds nothing belongs to every area its engine serves.
- A new behavior-bearing file joins the map in the pull request that adds it;
  a moved one moves its glob. The `docs` job fails an unmapped file, a glob
  that matches nothing, a key that is not a feature area, and a feature area
  with no path (`node tools/spec/check-area-paths.ts`).
- `feature-coverage` passes a behavior change only when:
  - a claim's scenario text changed — its tags, title, description, steps,
    tables, examples, or a Background it runs after — in an area the changed
    code or step definitions map to; whitespace, comments, and feature or
    Rule descriptions change no scenario; or
  - its exemption is well formed (ADR-0090) and **every** claim it cites
    belongs to one of those areas.
- A change is judged against the areas of its files that are **not** under
  `every`. A file under `every` adds no area beside them, so touching
  `Program.cs` never makes an unrelated scenario count; only when every
  changed file is under `every` is the change judged against all areas.
- A file that truly serves every area goes under `every`, not into each
  area's list. A file that serves a few areas is listed under each.

### `@ignore` may lead

- A scenario may merge tagged `@ignore` before its code exists, so the
  specification can lead the implementation.
- It then carries exactly one `@issue-<N>` tag beside `@ignore`, naming the
  open issue that will build it:

  ```gherkin
  @REQ-MED-061 @ignore @issue-842
  Scenario: A video longer than ten minutes is refused
  ```

- The pull request that builds it removes `@ignore` and the `@issue-<N>` tag,
  binds its steps, and closes the issue. A pull request that closes an issue
  an `@ignore` claim still names fails; build the claim there, or move its
  tag to the open issue that will.
- Two issues, two pull requests, when the specification leads:
  1. the specification issue's pull request adds the scenario as
     `@ignore @issue-<N>`, where N is a separate, open implementation issue,
     and closes the specification issue;
  2. the implementation issue's pull request builds the claim, removes both
     tags, and closes issue N.
  The specification pull request never names N in a closing keyword.
- **A closed owner fails every pull request.** The check reads every `@ignore`
  claim in the tree, not only the changed ones, so an `@issue-<N>` whose issue
  is closed by hand, outside a pull request, fails `feature-coverage` on every
  pull request and queued commit until someone reopens N, builds the claim,
  or moves its tag to an open issue. That is deliberate (owner decision,
  2026-10-03): a scenario nobody owns is fixed at once, not discovered later.
- A GitHub outage or rate limit fails the check with a clear error naming the
  issue it could not read; re-run it.
- `@ignore` still means "not built yet", never "no longer true": a superseded
  scenario is deleted.

## Why

- `feature-coverage` passed a behavior change alongside any edit to any
  `.feature` file — an unrelated area, or whitespace — and accepted any
  existing claim in an exemption, related or not (#809 audit, #813).
- No scenario on `main` was `@ignore`: the specification never sat ahead of
  the code. A scenario may now lead, but never without an owner; a closed
  issue would leave an `@ignore` scenario nobody is building (#813).
- The tag form keeps the owner on the scenario's own tag line, where
  `.spec/claims.json` already records every tag, so a tool reads it without
  parsing comments.
- The gate that proves a built claim passes is an architecture decision, in
  [ADR-0195](../decisions/ADR-0195-a-built-claim-counts-only-when-its-scenario-passed-in-the-run.md).

## Enforced by

- `node tools/spec/check-area-paths.ts` — the `docs` job.
- `node tools/spec/check-feature-coverage-diff.ts` — the `feature-coverage`
  job, on a pull request and on each queued commit.
- `node tools/spec/check-ignored-claims.ts` — the `feature-coverage` job: every
  `@ignore` claim names one issue, it is an open issue, and this change does
  not close it.
