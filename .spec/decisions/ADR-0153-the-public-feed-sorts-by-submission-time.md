---
title: The public feed sorts by submission time
description: The public feed orders newest submitted first, a tie broken by report ID, instead of newest published first; public_reports gains submitted_at for the API to read but never return, the feed's cursor carries only a report ID and the API resolves its submission time server-side, and each entry keeps displaying published_at.
type: adr
status: accepted
date: 2026-09-27
decision-makers: Chase Florell
keywords: public feed, public_reports view, keyset cursor, REQ-MOD-037, submitted_at, published_at, sort order, #570
---

# ADR-0153 — The public feed sorts by submission time

**Status:** Accepted.

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
- **The feed's keyset cursor carries only the last page's last report ID —
  never a timestamp, in either direction.** The ID is already public on its
  own page, so the cursor's opacity is only ever a convenience, not a
  boundary the value's own sensitivity depends on; submission time is not
  that. `List` looks the cursor's ID up in `public_reports` server-side to
  read its `submitted_at`, and resolves the keyset position from that lookup.
  A cursor naming a report that is no longer publishable (unpublished,
  deleted, or never existed) resolves to nothing, and the feed starts from
  the top — the same rule as an unreadable cursor, since a cursor was always
  only ever a bookmark, never a contract.
- **Each entry keeps displaying its publication date** (`published_at`), read
  exactly as before. The displayed date and the list order can therefore look
  out of order — a report published later can have been submitted earlier —
  and that is accepted, not a bug.
- **Submission time is never displayed on the public side, and never crosses
  the wire in either direction.** `PublicReportView` and `PublicReportDetail`
  gain no field for it; the internal query row
  (`PublicReportEndpoints.PublicReportRow`) does not carry it either — it is
  read once, from the cursor's own server-side lookup, and used only inside
  that one request.
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
- A paged request now costs one extra indexed lookup by primary key (the
  cursor's report ID against `public_reports`) before the page query itself —
  negligible next to the trip already being paid, and the price of never
  putting a timestamp on the wire.

## Considered options

- **Keep `published_at` as the sort key and only fix the tie-break.** Rejected
  because it does not address the decision's actual complaint: two reports
  submitted a day apart can still publish in the reverse order, so "newest
  published first" never guaranteed "newest submitted first."
- **Expose `submitted_at` as a field on the public DTO, so a client could sort
  itself.** Rejected: the product decision is explicit that submission time is
  never displayed on the public side, and adding a field the UI does not use
  only grows the allowlist REQ-MOD-036 fixes.
- **Encode `submitted_at` and the report ID in the cursor, as the first draft
  of this change did**, matching the old `published_at`-keyed cursor's shape.
  Rejected on review: unlike `published_at`, submission time is never
  otherwise public, and a client can trivially base64url-decode `?after=` or
  `next` to read it — that is exposing it, not keeping it internal, whatever
  the cursor's nominal opacity. The fix moves the timestamp out of the token
  entirely: the cursor carries only the report ID, already public on that
  report's own page, and the API resolves `submitted_at` with one extra
  server-side lookup before building the keyset filter.
- **Encrypt or hash the cursor so its contents cannot be recovered at all.**
  Rejected as unneeded complexity now that the cursor carries only a report
  ID: an ID is already public, so there is nothing left in the token worth
  hiding from a client that decodes it.
