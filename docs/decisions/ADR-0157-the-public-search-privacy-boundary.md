---
title: The public search privacy boundary
description: A search box at the top of the public feed fuzzy-searches only the approved published summary and visible member comments, in the visitor's current site language, through one Postgres function reading only the public_reports and public_report_comments views; its keyset cursor still carries only a report ID, never a rank score.
type: adr
status: accepted
date: 2026-09-27
decision-makers: Chase Florell
keywords: public search, public_reports, public_report_comments, pg_trgm, full-text search, role parity, REQ-MOD-140, REQ-MOD-143, REQ-MOD-149, ADR-0055, ADR-0153, ADR-0156, #574
---

# ADR-0157 — The public search privacy boundary

## Status

Accepted.

## Context

[Issue #574](https://github.com/HPAC-Safety/safety-report/issues/574) asks for
a search box at the top of the public feed (`/reports`) that fuzzy-searches
published reports. The product owner's decision, recorded on the issue,
settles the product shape:

- Matches only public content: the approved published summary and member
  comments on published reports, in the visitor's current site language only
  — a comment's text as shown in that language, its original or its Worker
  translation.
- Never answers, private notes, names, file names, or anything not already
  public. Reads only through `public_reports` and the public comment read
  side.
- The same engine as the admin search (#573): Postgres full-text search
  (language-appropriate stemming) plus `pg_trgm` (typo tolerance). That
  engine choice is [#573](https://github.com/HPAC-Safety/safety-report/issues/573)'s
  own ADR, **ADR-0156**, which this ADR cites rather than repeats. At the time
  this was written, #573 had not yet merged; `CREATE EXTENSION IF NOT EXISTS
  pg_trgm` in this pull request's migration means either pull request may
  create the extension first without conflict.
- Best match first while a query is active; an empty box is newest submitted
  first, unchanged (#570, ADR-0153).
- `?q=` lives in the address bar: bookmarkable, shareable, survives back and
  reload.
- Anonymous, and the query text is never logged.
- Ranked paging must work with the infinite-scroll feed (#572) without
  leaking a non-public value in the cursor.

The hard question this ADR actually answers is the privacy boundary: what
guarantees that a search box, added to an anonymous, unauthenticated
endpoint, can never be used to find a private answer, an unpublished report,
or a hidden comment — including through a side channel such as an inflated
comment count, a nonzero result count, or a distinguishing rank score. The
existing public reads (`PublicReportEndpoints`, ADR-0055, ADR-0116) already
answer this the same way for the plain feed: put the whole invariant in one
view, and make every read go through it. Search needs the same discipline
extended to two texts (a report's summary and its comments), one locale
choice, and a ranking function — none of which exist yet in this codebase.

## Decision

**Read nothing but `public_reports` and `public_report_comments`.** A new SQL
function, `search_public_reports(query, locale, after_id, limit)`
(`src/HpacSafety.Infrastructure/Persistence/Sql/20260928012335_AddPublicReportSearch.sql`),
joins only these two views — the same ones the plain feed and its comment
reads already use — for its candidate text. Neither view can name a private
answer, a name, an unpublished report, or a hidden or deleted comment; a row
that is not in them cannot be found by a query that also is not in them,
independent of anything this function does with the text once it has it. This
is the same structural guarantee ADR-0116 states for every other public read:
the invariant lives in the view, not in the code that queries it.

**A locale, not a language guess.** The API accepts `?locale=`, the visitor's
current site language (`en-CA` or `fr-CA`, `HpacSafety.Core.Locale`'s own
codes), defaulting to `en-CA` for anything else. For a given row, the
function reads exactly one text per source:

- the summary: `ai_summary_fr` for `fr-CA`, `ai_summary_en` otherwise — never
  both, and never the language the visitor is not reading;
- each comment: the same "shown text" the UI itself computes
  (`ReportComments.tsx`, `shownText`) — its own text when it was written in
  the requested locale, its Worker translation once one exists, otherwise
  (not yet translated) still its original text. This is a lookup already
  public in effect: a visitor who is not searching still sees that same
  string when they read the report's comments. Search never has an extra
  reason to hide it, and never has an extra reason to expose the other
  language's version instead.

**Matching combines full-text rank and trigram word-similarity with
`GREATEST`,** exactly the "same engine" decision: `to_tsvector`/
`plainto_tsquery` in the locale's own text-search configuration (`english` or
`french`) for stemmed matches, and `word_similarity` for typo tolerance,
scored against the whole candidate string rather than token-by-token so a
long summary does not dilute a short query the way whole-string `similarity`
would. `plainto_tsquery`'s implicit `AND` between words is rewritten to `OR`
before use, since "best match first" over "landing gear failure" should not
require every word on the same row — ranking, not filtering, is what decides
order.

**The cursor still carries only a report ID (ADR-0153), even for a ranked
query.** `PublicReportEndpoints.Search` reuses the exact `Cursor` type the
plain feed already uses: base64url of the last report's ID and nothing
else — never a rank score, a timestamp, or anything else derived from either.
Continuing a page does not decode a score out of the token; the SQL function
resolves its own continuation position by looking that same ID back up
against itself (`after_row` in the script), inside the one query, the same
way `List` already resolves `submitted_at` from a cursor's ID before it
existed. A cursor naming a report that is no longer public or no longer
matches resolves to nothing, and results start from the top — the same rule
an unreadable cursor already follows. This is the one requirement this pull
request adds to ADR-0153's own decision, generalizing "never a timestamp" to
"never anything not already public, rank included," which happens to cover
timestamps as a special case.

**A blank search box takes the exact code path it already took.** `q`
blank or whitespace-only routes to the unchanged `List` method; only a
non-blank, trimmed query (capped at 200 characters — `MaxSearchQueryLength`)
reaches `Search`. Nothing about the plain feed's cursor, order, or shape
changes.

**No logging.** The endpoint reads `q` as an ordinary ASP.NET Core route
parameter; nothing in `PublicReportEndpoints` or the SQL function writes it
anywhere. The existing request-logging middleware logs the path and status,
never the query string, for every route already — this adds no new
exception to audit.

**The public search endpoint reads no identity at all, so it cannot widen by
role.** `List` and `Search` take no `ClaimsPrincipal`, `HttpContext.User`, or
authorization policy — nothing about the caller reaches either method, sent
or not. This is verified directly, not just argued: `PublicReportEndpointTests`
sends the identical query anonymously and as each of the three member roles
(`User`, `SafetyOfficer`, `Administrator`) and asserts byte-identical
responses, and REQ-MOD-143 seeds one report carrying a distinct word behind
each of four private surfaces — a private answer, a staff-only private note,
a staff-only private attachment's file name, and a second report's own
summary that is never published — and proves that searching any of those
four words, as any of the four caller identities, finds nothing, while the
same report's public summary word finds it every time. The admin search
(#573, [PR #580](https://github.com/HPAC-Safety/safety-report/pull/580))
reads private notes and private attachments through its own admin-only view,
gated by the Reviewer authorization policy; `search_public_reports` and
`PublicReportEndpoints` name neither that view nor `report_private_notes` or
`report_private_attachments` anywhere, and never will, because nothing
routed through the anonymous public group can carry a policy check in the
first place. Two search endpoints, two boundaries: the admin one reads what
a signed-in reviewer already reads, and the public one reads only what
`public_reports` and `public_report_comments` already hold — one function
never substitutes for the other's rule.

## Consequences

- Search and the plain feed are one endpoint (`GET /api/v1/public/reports`),
  branching only on whether `q` is blank; a client that never sends `q` sees
  no behavior change at all.
- The search box in `ViewReportsPage.tsx` writes `?q=` into the address bar
  (debounced, one history entry per settled query) so it is bookmarkable and
  survives reload and the back button — `?q=` was already reserved in the
  storage key #572's `useInfiniteReportList` keys its accumulated list by, so
  a later rebase onto #572 only has to route search's results through that
  hook rather than invent a place for `q` to live.
- `CREATE EXTENSION IF NOT EXISTS pg_trgm` and four best-effort trigram `GIN`
  indexes ship in this migration; whichever of #573 or this pull request
  merges first creates the extension, and the other's identical statement is
  a no-op.
- Four new trigram indexes exist alongside the search function; a functional
  full-text index tied to one fixed language configuration is not added,
  since the function chooses its configuration per request rather than at
  index-build time — perf tuning against real query volume is left as a
  follow-up, not a claim this ADR makes.

## Rejected alternatives

- **Filter search results in C#, after reading candidate rows with LINQ.**
  Rejected: `ai_summary_en`/`ai_summary_fr` and comment text would have to be
  pulled into the application to be scored, defeating both engines' point
  (stemming and trigram indexes are a database's job) and making "best match
  first" paging require holding the whole matching set in memory to sort it.
  ADR-0055's own preference for a SQL view or function over a C#-side rule
  applies here without qualification.
- **A separate `public_search_results` view, materialized or plain, instead
  of a function.** Rejected: ranking depends on two request-time inputs (the
  query text and the locale) a view's `SELECT` cannot parameterize; a
  function is the natural shape once ranking is parameterized, and it is
  still read-only and still `.sql`-file-authored under ADR-0055.
- **Filter with `WHERE score > 0.05`, a bare numeric floor on the combined
  score.** Tried first, then rejected on inspection: `ts_rank`'s scale is not
  comparable to `word_similarity`'s 0–1 scale, so a single threshold either
  passed near-nothing or matched almost everything depending on document
  length. Matching now requires either engine's own boolean hit — `@@` for
  full-text, `word_similarity(...) > 0.4` for trigram — and only ranks by the
  combined score afterward.
- **Plain `similarity()` instead of `word_similarity()` for the trigram
  half.** Rejected: `similarity` compares the whole query string against the
  whole candidate string, so a short query word against a long summary scores
  low regardless of how exact the match is — verified directly (a deliberate
  misspelling of "hydraulic" scored `0.1` against a full sentence containing
  the correctly spelled word, and did not clear any usable threshold).
  `word_similarity` finds the best-matching substring instead, scoring the
  same misspelling `0.46` against the same sentence — the function pg_trgm
  ships specifically for this shape of comparison.
- **Encode the rank score in the cursor, alongside or instead of the report
  ID**, so continuing a page would not need a second lookup. Rejected for the
  same reason ADR-0153 rejected encoding `submitted_at`: a score is not
  otherwise public, decoding a client-opaque token is not the same thing as
  keeping a value internal, and the extra lookup this ADR does instead — one
  more evaluation of the same scoring expression, filtered to one ID — costs
  a single indexed-adjacent query, not a second round trip to the client.
- **`unaccent`, for accent-insensitive matching.** Left out of this pull
  request: the product decision does not ask for it, `#573` may or may not
  add it for the admin search's own reasons, and Postgres's `french` text
  search configuration already normalizes some accented forms on its own.
  Revisit if a real query shows it matters; it is not a boundary this ADR
  needs to draw now.
