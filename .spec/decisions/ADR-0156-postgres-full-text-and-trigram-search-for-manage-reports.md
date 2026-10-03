---
title: Postgres full-text and trigram search powers the admin search box
description: Manage reports gains a fuzzy search box over every part of a live report — every answer including private ones, choice labels in both languages, the summary pair, private notes, member comments, and attachment file names — built entirely on PostgreSQL's own full-text search (English and French, accent-insensitive) plus pg_trgm word-similarity, computed at query time with no new service, no materialized view, and no GIN index.
type: adr
status: accepted
date: 2026-09-28
decision-makers: Chase Florell
keywords: search, full-text search, pg_trgm, unaccent, word_similarity, admin_report_queue, admin report search, REQ-MOD-130, REQ-MOD-131, REQ-MOD-132, REQ-MOD-133, #573, #572, #574, ADR-0116, ADR-0155
---

# ADR-0156 — Postgres full-text and trigram search powers the admin search box

**Status:** Accepted. Amends
[ADR-0133](ADR-0133-staff-keep-private-notes-on-a-report.md) item 5 and
[ADR-0135](ADR-0135-staff-add-private-attachments-to-a-report.md) item 1:
each said no database view reads its table; `admin_report_search_document`
is the one reviewed exception to both, carved out below.

## Context

A safety officer or administrator managing dozens of reports a year had no
way to find one except by scrolling and status filter. #573 asks for a
search box at the top of Manage reports that fuzzy-searches every part of a
report — every answer, private ones included, a choice's label in both
languages, the summary pair, staff-only private notes, member comments, and
attachment file names, reporter-uploaded and staff-only — best match first
while a query is active, within whichever status filter is already chosen,
never logging the query text.

The product decision (recorded on the issue) is "Elasticsearch-like, inside
Postgres": full-text search with English and French stemming, plus
trigram similarity for typos and partial words, no new service, because
private data stays in the one store it already lives in.

#572 (ADR-0155) gives the admin list its first server-side keyset paging,
landing separately and not yet merged when this was built; this decision's
endpoint change is written to compose with it at the rebase, not to
duplicate it (see Consequences). #574 (public search, ADR-0157) is being
built concurrently by a different agent and needs the same two Postgres
extensions on the public side; both migrations guard their `CREATE
EXTENSION` with `IF NOT EXISTS` so either can land first.

## Decision

- **No new service.** `pg_trgm` and `unaccent` are enabled with `CREATE
  EXTENSION IF NOT EXISTS` in the same migration that adds this decision's
  view and function (ADR-0055). Nothing reaches the model, a translation
  provider, or any process outside the database this data already lives in.
- **One view gathers everything a report can be found by**:
  `admin_report_search_document`, one row per live report, concatenating
  (space-joined, `NULL`-safe): every live answer's value and translated
  value, a choice answer's label in both languages, the summary pair, a
  private note's current-revision text, a member comment's current-revision
  text and its translation, and both reporter-uploaded and staff-only
  attachment file names. It carries no per-source structure — a match does
  not say which source matched, because the decision only asks that the
  report be found, not that the hit be attributed.
- **Amends ADR-0133 item 5 and ADR-0135 item 1, on one condition.** Both said
  no database view reads `report_private_notes`/`report_private_note_revisions`
  or `report_private_attachments`. The owner approved the search box covering
  private notes and staff-only attachment names (recorded on #573)
  **on condition that the API refuses the search itself to anyone who is not
  `SafetyOfficer` or `Administrator`** — not merely that the view's own
  content stays unattributed. `admin_report_search_document` is the one
  named, reviewed exception to each ADR, never a general opening:
  - It is reachable through exactly one code path: `GET
    /api/admin/reports?q=`, on the same `/api/admin/reports` group every
    other admin report route shares, gated by
    `RequireAuthorization(HpacPolicies.Reviewer)`. An anonymous caller gets
    `401`; a `User`-role token gets `403`; neither response carries the
    report's identifier or any of its content, private or otherwise — proven
    by REQ-MOD-138 (a `Scenario Outline` with one term embedded only in a
    private answer, a private note, and a staff-only attachment name, and
    Examples for anonymous, `User`, `SafetyOfficer`, and `Administrator`) and
    matching `HpacSafety.Api.Tests` (`GivenAnonymousCaller_...`,
    `GivenUserRole_...`, `GivenSafetyOfficerRole_...`,
    `GivenAdministratorRole_...`).
  - No other endpoint, background job, or code path queries
    `search_admin_reports` or reads `admin_report_search_document`.
    `SearchIsTheOnlyReaderOfPrivateContentTests` (mirroring
    `ReviewerLinkIsTheOnlyChokepointTests`'s chokepoint-scan pattern) walks
    every shipping `.cs` file and fails the build if
    `HpacSafetyDbContext.SearchAdminReports` is called from anywhere but
    `ReportEndpoints.cs`, and asserts that file both calls it and requires
    the `Reviewer` policy.
  - It reads a note's *current* revision only (a removed note, or any
    revision but the current one, is left out — the same rule the report
    view already applies to a comment or note's history); it reads a *live*
    (non-removed) attachment's file name only; and it never returns that
    text anywhere — the endpoint's response is a `ReportListItem`,
    unchanged, naming only which report matched and nothing about why.
  - `PrivateNoteSteps.ThenNoViewReadsTheNotes` and
    `PrivateAttachmentSteps.ThenNoViewReadsTheTable` assert, through
    `information_schema.view_table_usage`, that the exact set of views
    reading each table is `{admin_report_search_document}` — no other view
    may ever join to either table without failing the build.
- **One function ranks a query against it**: `search_admin_reports(query
  text)`, returning `(report_id, rank)`. `rank` is the greater of three
  signals: English `ts_rank`, French `ts_rank` (both through
  `to_tsvector`/`websearch_to_tsquery`, both wrapped in `unaccent` so
  "atterrissage" and "atterissage" or "café" and "cafe" are the same word),
  and `word_similarity(query, document)`. Whichever signal is strongest
  decides the rank, so a clean phrase in either language and a near-miss
  typo are all comparable on one scale.
- **`word_similarity`, not plain `similarity`/`%`.** Plain trigram
  similarity compares the query's length against the *entire* gathered
  blob's length — dozens of unrelated words from every other source on the
  same report dilute a short, exact filename or word to a similarity near
  zero, so it would almost never cross the default threshold. `word_similarity`
  (and its `<%` operator) instead looks for the best-matching contiguous
  extent of words inside the blob, which is what a search box actually
  needs: "does the query appear somewhere in here," not "is the query the
  whole document." This was found empirically while proving REQ-MOD-130 for
  an attachment file name and a typo — see the rejected alternative below.
- **Computed at query time; no materialized view, no maintained tsvector
  column, no GIN index.** HPAC receives dozens of reports a year. The view
  re-aggregates every source per query, and the function re-derives both
  tsvectors and the trigram comparison per row per query; at this volume
  that is microseconds of work against a few dozen small rows, not a cost
  worth a maintained index or a materialized view's staleness window. If
  report volume ever grows enough for this to matter, the fix is a
  generated `tsvector` column with a GIN index and a trigram GIN index on
  the same view's expression, in a follow-up migration — nothing in this
  shape prevents that later.
- **Within the current filter.** The endpoint composes the existing
  `Filters` dictionary's `Where` (REQ-MOD-049/050/124) with a join against
  `search_admin_reports(q)` only when `q` is present, and orders by rank
  first, submission time second, when it is. An empty box keeps the
  existing newest-submitted-first order untouched.
- **The query text is a bound SQL parameter throughout** — `FromSqlInterpolated`
  parameterizes it into `search_admin_reports({query})`, and it is never
  written to a log, an audit row, or a database column of its own.

## Consequences

- `HpacSafetyDbContext.SearchAdminReports(query)` exposes
  `AdminReportSearchMatch { ReportId, Rank }`, mapped `HasNoKey().ToView(null)`
  — it is not a table or view EF can query on its own, only the shape
  `search_admin_reports`'s result set returns through `FromSqlInterpolated`.
- `GET /api/admin/reports` gains an optional `q`. #572 is not yet merged as
  of this decision, so today's endpoint still returns a bare array; once
  #572's `{ items, next }` keyset shape lands, the two changes are combined
  at the rebase so a ranked search stays pageable — its own keyset over
  `(rank, id)`, excluding the anchor row by ID the same way ADR-0155's
  `(submitted_at, id)` cursor does, since `TinyId` has no translatable
  ordering either. That combination is intentionally deferred rather than
  guessed at here, so this decision does not commit #572 to a shape it
  has not reviewed.
- A choice answer's label match, a private note match, and so on are all
  the same `rank` — a reviewer sees which *report* matched, not which part
  of it did. Good enough for "find the report fast"; showing the matched
  snippet is not asked for and is not built.
- `?q=` lives in the address bar; the web hook already built for #572
  (`useInfiniteReportList`) was designed to accept it as one more query
  parameter closed over by the caller, so wiring the search box costs no
  hook change once combined with paging.

## Considered options

- **A dedicated search service (Elasticsearch, Meilisearch, Algolia).**
  Rejected outright by the product decision: private data (names, contact
  information, medical narrative) would leave the one store this system
  already treats as the privacy boundary, for a system receiving dozens of
  reports a year.
- **Plain `similarity()`/`%` trigram matching against the whole gathered
  blob**, tried first. It correctly found reports through full-text search
  but silently failed the acceptance scenario for an attachment file name
  and for a single-character typo: the query's short length compared
  against the entire per-report blob's length (narrative, choices, summary,
  notes, comments, and every file name at once) drove the similarity score
  below any usable threshold. `word_similarity`/`<%` replaced it because it
  compares the query against the best-matching extent within the blob
  instead of the blob's whole length.
- **A maintained `tsvector` column with a GIN index**, or a materialized
  view refreshed on write. Rejected for now on the volume argument above;
  revisit if report volume, or query latency actually observed, changes
  that argument's facts.
- **Attributing which source matched** (answer vs. note vs. file name).
  Not asked for; it would mean carrying per-source ranks and picking one to
  show, which adds real complexity the issue's acceptance criteria do not
  need.
