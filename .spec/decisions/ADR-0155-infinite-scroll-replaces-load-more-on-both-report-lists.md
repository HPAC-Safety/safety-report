---
title: Infinite scroll replaces Load more on both report lists
description: The public feed and Manage reports both auto-load their next keyset page as the reader nears the end; the fallback control stays visually hidden until keyboard focus and a Retry control appears only on failure, with a polite announcement of newly loaded items throughout. The admin list gains its first server-side keyset paging, using the same ID-only opaque cursor as the public feed with a true ordinal tie-break, and the browser restores the same accumulated results and scroll position on a back-button return instead of reloading the first page.
type: adr
status: accepted
date: 2026-09-27
decision-makers: Chase Florell
keywords: infinite scroll, keyset pagination, admin report list, admin_report_queue, public feed, IntersectionObserver, back button, scroll restoration, REQ-MOD-129, REQ-MOD-126, REQ-MOD-127, REQ-MOD-128, #572, #570
---

# ADR-0155 — Infinite scroll replaces Load more on both report lists

## Status

Accepted.

Amended by
[ADR-0173](ADR-0173-a-fresh-navigation-starts-at-the-top-and-only-a-return-restores.md):
a list restores its saved results and position only on a return to the
history entry it was built in, never on a fresh visit.

## Context

Both report lists paged one screen at a time: the public feed
(`/reports`) with `Older reports` / `Newest reports` links that put the
keyset cursor in the address bar (REQ-MOD-082, pre-#572), and Manage reports
(`/admin/reports`) with no paging at all — every live report came back in one
response (REQ-MOD-030). Neither matches how a reader scrolls a feed today,
and the admin list's unpaged response only stayed cheap because HPAC receives
a modest number of reports a year; it does not stay cheap indefinitely.

#570 ([ADR-0153](ADR-0153-the-public-feed-sorts-by-submission-time.md))
changed the public feed's sort key to `submitted_at` and its cursor to carry
only the last page's report ID, with the API resolving that ID's
`submitted_at` server-side. This decision reuses that exact cursor shape for
the admin list and replaces both lists' paging control with infinite scroll.

## Decision

- **Both lists auto-load their next page** as the reader nears the end of
  the list, through an `IntersectionObserver` sentinel placed after the last
  item. While auto-load is working, nothing is visibly shown for it.
- **The fallback control ("Load more") is hidden until it is needed.** It
  sits in the DOM at all times a further page might exist — never removed,
  so it is reachable by Tab — but is visually hidden (an `sr-only`-style
  rule) unless it holds keyboard focus (`:focus-visible`), so a sighted
  mouse user never sees a control auto-load already makes unnecessary, while
  a keyboard or screen-reader visitor can still tab to it and activate it.
  Once a page fails to load, the control becomes visible unconditionally and
  reads "Retry" — a failure is exactly the moment a sighted visitor also
  needs it.
- **A shared hook** (`useInfiniteReportList`, `src/web/src/hooks/`) and a
  shared status component (`InfiniteScrollStatus`,
  `src/web/src/components/`) back both lists, so the sentinel, the fallback
  button, the retry state, and the `aria-live="polite"` announcement of how
  many items just loaded are one implementation, not two.
- **De-duplication by item ID.** Because a cursor naming an item no longer
  in the list (unpublished, deleted, or filtered out since) restarts that
  list from the top rather than failing (REQ-MOD-037, and REQ-MOD-129
  below), the hook filters every appended page against the IDs already
  shown, so a restart-from-top can never repeat a row.
- **The admin report list gains server-side keyset paging** (REQ-MOD-129):
  it had none before. `GET /api/admin/reports` now accepts `after` and
  returns `{ items, next }` in place of a bare array, ordered by
  `submitted_at` desc, a tie broken by report ID, working with the existing
  `filter` query parameter. Its cursor is ID-only, in the same opaque
  base64url shape as the public feed's (ADR-0153): never a timestamp, looked
  up server-side against `admin_report_queue` to resolve a keyset position.
  A cursor naming a report no longer in the queue — deleted, or no longer
  matching the current filter — restarts the list from the top, the same
  rule as an unreadable cursor and as the public feed.
  - The admin list's tie-break for two reports submitted at the exact same
    instant orders past the anchor by ID, ordinally — the same rule the
    public feed's `string.Compare(report.Id, id) < 0` already applies
    (ADR-0153) — rather than merely excluding it. `admin_report_queue`'s ID
    column is `TinyId`, not `string`, so `TinyId` gains `IComparable<TinyId>`
    and `<`/`>`/`<=`/`>=` operators over `string.CompareOrdinal(Value, ...)`
    (`src/HpacSafety.Core/TinyId.cs`); EF Core translates the resulting
    `report.Id < id` the same way it already translates `OrderBy(report =>
    report.Id)`, against the mapped `char(11)` column. A dedicated test
    (`GivenSeveralReportsSharingOneSubmittedAt_...`) seeds more than one
    page's worth of reports at one identical instant and pages across the
    boundary, asserting no report is skipped or repeated.
- **The public feed's `?after=` address-bar cursor is removed.** REQ-MOD-082
  is amended in place (not renumbered): the back button now restores the
  same accumulated results and scroll position, rather than returning to a
  URL naming the first page. The hook persists accumulated items, the next
  cursor, and the scroll position to `sessionStorage`, keyed by every query
  parameter the page itself reads (an admin status filter, and later a
  search term), so a changed filter starts a fresh list instead of
  restoring a stale one, and a browser back-navigation to the same list
  restores exactly what was there.
- **Extra query parameters pass through unchanged.** The hook takes a
  `fetchPage(after)` callback the caller closes over its own parameters
  with, so `?q=` (search, #573) and an admin status filter (#574) need no
  change to the hook itself — only a new `storageKey` per distinct query.
- The README's admin-list out-of-scope line narrows from "pagination …
  other than newest first" to "search or sorting … other than newest
  submitted first, and a page-count or jump-to-page control" — paging itself
  is now built; a page-count or jump-to-page control stays out of scope for
  both lists.

## Consequences

- A short synthetic list (as in a stubbed Playwright fixture) can auto-load
  before an explicit scroll or click ever happens, because the sentinel
  already sits within the initial viewport. This is accepted as correct
  behavior — the whole point of the sentinel — and the test suite proves the
  fallback button in isolation by disabling `IntersectionObserver` for the
  scenarios that need to observe the button on its own.
- `ReportListItem` on the admin list is now wrapped in `ReportListPage`
  (`{ items, next }`); every caller of `GET /api/admin/reports` (the web
  client, the Playwright stubs, the Reqnroll steps) reads the new shape.
- No schema or migration change: `admin_report_queue` already carried
  `submitted_at` and `id`; only the endpoint and the query changed.

## Rejected alternatives

- **Numbered pages (`?page=3`).** Rejected: it does not match the "load more
  as you scroll" requirement, and reintroduces the same page-in-the-address
  problem #570 and this decision both move away from.
- **Keep the public feed's `?after=` address-bar cursor and only add
  auto-load on top of it.** Rejected: the product decision is explicit that
  the back button restores the same results and position, which a
  cursor-only address bar cannot do once several auto-loaded pages have
  accumulated past whatever cursor is currently named in the address.
- **Exclude the tie-break anchor by ID (`!=`) instead of giving `TinyId` a
  real ordering.** This shipped in an earlier draft of this decision, on the
  reasoning that an exact `submitted_at` collision is rare enough that a
  slightly weaker tie-break would not matter in practice. Rejected on
  review: "rare" is not "never," and a tie-break that can skip or repeat a
  row under a real (if uncommon) condition is a correctness gap the fix
  costs little to close. `TinyId` gaining `IComparable<TinyId>` is a small,
  generically useful addition to a type every table already uses, not a
  one-off workaround.
- **Always show the "Load more" button, whether or not auto-load already
  handles it** (this decision's own first draft). Rejected on review: a
  control that is redundant every time auto-load already works is exactly
  the kind of visible clutter infinite scroll is meant to remove; hiding it
  until keyboard focus keeps it reachable without showing it to a visitor
  who has no use for it.
