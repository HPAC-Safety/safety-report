---
title: A fresh navigation starts at the top, and only a return restores
description: Every page starts at its top when the reader navigates to it afresh, through one router-level reset on a push or replace that changes the path; Back, Forward, and query-string-only changes are left alone. A report list restores its saved results and position only on a return to the same history entry, never on a fresh visit.
type: adr
status: accepted
date: 2026-09-29
decision-makers: Chase Florell
keywords: scroll, scroll restoration, navigation, React Router, BrowserRouter, back button, history entry, sessionStorage, infinite scroll, public feed, Manage reports, REQ-WLD-032, REQ-MOD-178, REQ-MOD-179, REQ-MOD-082, ADR-0155, #670
---

# ADR-0173 — A fresh navigation starts at the top, and only a return restores

**Status:** Accepted. This ADR **amends**
[ADR-0155](ADR-0155-infinite-scroll-replaces-load-more-on-both-report-lists.md):
a report list still restores its accumulated results and scroll position on
a back-button return, but only on a return to the history entry it was built
in. It never restores on a fresh visit.

## Context

Opening a page could land the reader at the bottom (#670). There were two
causes:

- The web app uses a plain `<BrowserRouter>`, which does not touch the scroll
  position when the route changes. The next page opened wherever the previous
  one was scrolled. Following a footer link from the bottom of one page
  landed at the bottom of the next.
- The report lists' shared hook restored its `sessionStorage` state on any
  visit that found saved state. ADR-0155 intended that for a back-button
  return. The saved position is recorded as each page of results finishes
  loading, and infinite scroll loads pages as the reader nears the end. So a
  fresh visit to a list read earlier in the tab jumped to the bottom and
  showed stale results without fetching.

The owner decided (2026-09-29) that the fix is global: every page starts at
its top on a fresh navigation, with no page opting in or out.

## Decision

- **One reset at the router.** A component rendered once beside the routes
  scrolls to the top, before paint, when the path changes on a push or a
  replace. It does nothing:
  - on Back or Forward (a "POP", which is also how a page first loads), where
    the browser or a report list restores the earlier position;
  - on a change to the query string alone (the search box's `?q=`, a status
    filter), which keeps the reader in place on the same page;
  - on a link to an in-page `#anchor`, which the browser scrolls to.
- **A report list restores only on a return to its own history entry.** The
  saved state records React Router's `location.key` for the entry it was
  built in, and restores only when the current entry has that key. A fresh
  visit is a new entry, so it loads the first page and starts at the top.
- **The first entry of a page load is told apart by the page load.** React
  Router names the first entry of every page load `default`. On its own, that
  key cannot tell a Back return to that entry (restore) from typing the
  list's address again in a new page load (start afresh). So the saved state
  also records a per-page-load token. It restores from an earlier page load
  only when the browser reports that page load as a reload or a Back/Forward
  arrival (`PerformanceNavigationTiming.type`), never as a typed or linked
  navigation.

## Considered options

- **Move to React Router's data router for its `<ScrollRestoration>`.**
  `<ScrollRestoration>` works only under `createBrowserRouter`/`RouterProvider`.
  Adopting it means rewriting every route definition and the admin route
  guard around loaders, a large change for one behavior. It would also still
  leave the report lists' own restoration to reconcile with it.
- **Reset in each page's own effect.** That is not global. Any page that
  forgot the reset would still open wherever the last page was scrolled.
- **Reset on every location change, query string included.** Typing in the
  public feed's search box updates `?q=` on every search. A reset there would
  pull the reader away from what they are reading.
- **Clear a list's saved state whenever the reader leaves it.** A Back return
  would then lose the results and position that ADR-0155 and REQ-MOD-082
  promise.
- **Restore by navigation type alone (`POP`).** A page's first load is also a
  `POP`, so typing a list's address again would still restore a stale list.
  So would a reload of any other page.

## Consequences

- Every page, public and admin, opens at its top when reached by a link. A
  new route needs nothing to get this.
- A report list read earlier in the same tab is fetched again when opened
  afresh, which is one more first-page request than before.
- Saved state written before this change has no entry key, so it is never
  restored and is overwritten on the next visit.
- The Back-restored position is still the one recorded when the last page of
  results loaded, not where the reader left. Recording it on leave is a
  separate change (#670, out of scope).

## Related

- [ADR-0155](ADR-0155-infinite-scroll-replaces-load-more-on-both-report-lists.md) — amended.
- [#670](https://github.com/HPAC-Safety/safety-report/issues/670), [#572](https://github.com/HPAC-Safety/safety-report/issues/572).
