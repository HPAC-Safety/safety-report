---
status: accepted
date: 2026-09-18
decision-makers: Chase Florell
keywords: testing, Playwright, e2e, UI, CI
---

# ADR-0045 — Every UI change ships with a Playwright test and its server-side counterpart

**Status:** Accepted

## Context

Playwright already runs in CI (`ci.yml`'s `e2e` job, `tests/e2e/`) and is
named in `docs/testing-conventions.md` as the tool for "browser journeys," but
nothing makes it a required part of a UI change — a PR that touches the web
front end can pass CI without adding or updating a Playwright test, as long as
existing journeys still pass. With the front end moving to React/TypeScript
([ADR-0043](ADR-0043-react-typescript-vite-web-front-end.md)) and the API
staying the sole source of truth for validation and authorization, a UI-only
test proves the screen renders, not that the behavior it depends on is
correct end to end.

## Decision

**A pull request that changes UI behavior includes both:**

1. a Playwright test (new or updated) exercising that behavior through the
   browser, and
2. a server-side test (xUnit/Shouldly, per
   [`docs/testing-conventions.md`](../testing-conventions.md)) covering the
   API behavior the UI depends on, when the change touches or relies on API
   behavior.

A pull request that changes only presentation with no behavioral or API
surface (copy, spacing, color tokens) needs the Playwright coverage only if no
existing journey already exercises the changed element; it does not need a
new server-side test.

## Why this choice

**Neither layer alone catches what the invariants in `AGENTS.md` require.** A
Playwright test alone would pass against a stubbed or mocked API and miss a
server-side validation regression (product invariant: "the API's validation
is authoritative regardless of what the client allowed" — already a feature
scenario). A server-side test alone misses a UI regression — a form that
silently drops a field, a focus trap, a broken locale switch — none of which
a unit test on a controller sees.

**Both, not "one or the other, reviewer's choice."** Leaving it to judgment
is how the existing "nothing enforces it" gap happened. Requiring both by
default, with the narrow presentation-only carve-out, is a rule a reviewer can
check mechanically.

## Alternatives

- **Playwright only for UI PRs.** Rejected: matches "does the screen render,"
  not "is the behavior correct," and this system's core invariants (validation
  authority, privacy boundaries, immutable questions) live server-side.
  Playwright hitting a real, seeded backend already partially covers this, but
  a targeted server-side test documents and pins the specific behavior the UI
  change depends on, which a broad e2e journey does not.
- **Leave it to reviewer judgment, as today.** Rejected: this is the status
  quo, and it is exactly what left Playwright coverage optional in practice.
- **Require Playwright coverage but not a server-side test, relying on
  existing API test coverage.** Rejected when the UI change introduces or
  changes API-facing behavior (a new field, a new validation rule, a changed
  response shape) — "existing coverage" does not exist yet for a new
  behavior.

## Consequences

- PR review (and any coding agent working in this repository) treats a UI
  behavior change with no Playwright test, or an API-facing UI change with no
  corresponding server-side test, as incomplete — same weight as a missing
  ADR for an architectural change.
- `docs/testing-conventions.md` gets a line pointing here so the rule is
  discoverable from the testing doc, not only from this ADR.
- No new tooling: `tests/e2e/` and the existing xUnit projects are what these
  tests are added to. This is a process rule, not an infrastructure change.

## Related

- [ADR-0043](ADR-0043-react-typescript-vite-web-front-end.md)
- `docs/testing-conventions.md`
- `.github/workflows/ci.yml` (`e2e` job)
