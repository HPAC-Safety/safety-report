---
title: Infinite scroll replaces Load more on both report lists
description: The public feed and Manage reports both auto-load their next keyset page as the reader nears the end, with an accessible fallback button and a Retry on failure; the admin list gains its first server-side keyset paging, using the same ID-only opaque cursor as the public feed, and the browser restores the same accumulated results and scroll position on a back-button return instead of reloading the first page.
type: adr
status: accepted
date: 2026-09-27
decision-makers: Chase Florell
keywords: infinite scroll, keyset pagination, admin report list, admin_report_queue, public feed, IntersectionObserver, back button, scroll restoration, REQ-MOD-129, REQ-MOD-126, REQ-MOD-127, REQ-MOD-128, #572, #570
---

# ADR-0155 — Infinite scroll replaces Load more on both report lists

## Status

Accepted.

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
  item.
- **An always-present fallback button** ("Load more") offers the same
  action for a keyboard or screen-reader visitor who never triggers the
  sentinel, and reads "Retry" once a page fails to load. It is not a
  progressive-enhancement afterthought: it renders whenever another page
  might exist, whether or not the sentinel already fired.
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
    instant excludes the anchor by ID rather than ordering past it
    lexicographically: `admin_report_queue`'s ID column is `TinyId`, which
    has no translatable ordering in this codebase's EF model, only equality
    — unlike the public feed's plain `string` ID column. An exact
    `submitted_at` collision against a database timestamp is vanishingly
    rare, so this is accepted as a pragmatic simplification rather than
    adding an ordering conversion for a case that does not occur in
    practice.
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
- **Give `AdminReportQueueItem.Id` a translatable ordering (an `IComparable`
  wrapper, or a shadow string column) so the admin tie-break could use a true
  `<` rather than excluding the anchor by ID.** Rejected for now: it would
  touch the shared `TinyId` type or add a projection used nowhere else, to
  correct an ordering edge case that requires an exact `submitted_at`
  collision to ever matter. Revisit if that assumption stops holding.
