---
title: The public feed sorts by submission time
description: The public feed and its keyset cursor order newest submitted first, a tie broken by report ID, instead of newest published first; public_reports gains submitted_at for the API to read but never return, and each entry keeps displaying published_at.
type: adr
status: accepted
date: 2026-09-27
decision-makers: Chase Florell
keywords: public feed, public_reports view, keyset cursor, REQ-MOD-037, submitted_at, published_at, sort order, #570
---

# ADR-0153 — The public feed sorts by submission time

## Status

Accepted.

## Context

Every list of occurrence reports is meant to sort newest submitted first, so
whatever was reported most recently is at the top (#570). The admin report
list already does this: it orders by `submitted_at` (REQ-MOD-030), the one
date every report always has.

The public feed did not. `public_reports` (#28,
[ADR-0055](ADR-0055-ef-core-migrations-sql-files-stored-procedures.md)) ordered
by `published_at` — falling back to the summary pair's approval time for a
report approved before approval published it — and REQ-MOD-037 fixed that as
"newest published first, a tie broken by report ID." The feed's keyset cursor
encoded the same field.

Publication time and submission time diverge whenever review takes any time
at all: an older, harder report can sit in review while a newer, simpler one
is approved first, and then the newer one outranks it under "newest
published." That contradicts the owner's decision that submission time is the
one sort key, everywhere.

## Decision

- **The public feed's sort key becomes submission time (`submitted_at`), a
  tie broken by report ID — never occurrence date, publication date, or
  approval time.** This amends REQ-MOD-037 in place; the requirement is not
  renumbered.
- `public_reports` gains a `submitted_at` column, `reports.submitted_at`
  verbatim, added at the end of the view's column list so no existing column
  moves.
- The feed's keyset cursor now encodes `submitted_at` and the report ID
  instead of `published_at` and the report ID. The cursor is still one opaque
  base64 token; a client that decodes it can recover the timestamp it
  contains, but the value never appears as a field of a feed entry, a report
  detail, or any other public response. This is the trade-off the product
  decision itself accepts: submission time "is only the sort/cursor key."
- **Each entry keeps displaying its publication date** (`published_at`), read
  exactly as before. The displayed date and the list order can therefore look
  out of order — a report published later can have been submitted earlier —
  and that is accepted, not a bug.
- **Submission time is never displayed on the public side.** `PublicReportView`
  and `PublicReportDetail` gain no field for it; only the internal query row
  (`PublicReportEndpoints.PublicReportRow`) carries it, and only to order the
  query and build the cursor.
- A cursor from before this change decodes to a `published_at` value the new
  code reads as `submitted_at`; `Cursor.TryRead` still parses it as a token,
  so it silently lands at whatever position that timestamp now sorts to. This
  is acceptable: the endpoint already treats an unreadable or stale cursor as
  only a bookmark, never a contract, and restarts from the top rather than
  failing.
- Out of scope, for #570: implementing the search feature this ADR's wording
  anticipates ("with a search box empty, this order holds" — best match first
  applies once search exists). Search is tracked separately; nothing here
  builds it.

## Consequences

- The public feed and the admin report list now share one rule: newest
  submitted first, a tie broken by report ID. Neither reads occurrence date
  or publication date to order anything.
- A migration
  (`20260927223122_SortPublicFeedBySubmittedAt`) replaces the view; it adds a
  column and changes no table, so it needs no backfill and loses no data.
- Public feed pagination is stable across mixed submit/review speeds: a
  report already in review keeps its place by when it was submitted, not by
  whichever report the reviewer approves first.

## Rejected alternatives

- **Keep `published_at` as the sort key and only fix the tie-break.** Rejected
  because it does not address the decision's actual complaint: two reports
  submitted a day apart can still publish in the reverse order, so "newest
  published first" never guaranteed "newest submitted first."
- **Expose `submitted_at` as a field on the public DTO, so a client could sort
  itself.** Rejected: the product decision is explicit that submission time is
  never displayed on the public side, and adding a field the UI does not use
  only grows the allowlist REQ-MOD-036 fixes.
- **Encrypt or hash the cursor so a client cannot recover the timestamp at
  all.** Rejected as unneeded complexity: the cursor is already an
  implementation detail no client is meant to parse, and the equivalent
  `published_at` value it replaces was already both encoded in the cursor and
  publicly displayed, so this ADR does not raise the cursor's own opacity
  guarantee — it only stops the *response body* from ever naming the field.
