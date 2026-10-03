---
title: Public feed
description: Supporting detail for the public feed, a published report's own page, and public search.
type: spec
area: public-feed
---

# Public feed

Supporting detail for [`public-feed.feature`](public-feed.feature)
that doesn't fit Gherkin.

## The public feed and report page (#329)

**View safety reports** (`/reports`) is anonymous. It lists every publishable
report, newest submitted first, a tie broken by report ID
([ADR-0153](../../decisions/ADR-0153-the-public-feed-sorts-by-submission-time.md)).
Each entry still shows its summary in the visitor's language and its
publication date — the two can therefore look out of order, since a report
approved and published later can have been submitted earlier — and links to
the report's own address, `/reports/<id>`. A visitor can bookmark, share, or
reload that address. Paging forward puts an opaque cursor in the address bar
(`?after=`), so the back button returns to the page the visitor came from.
The cursor carries only the last page's last report ID — already public on
that report's own page — and never a timestamp. The API resolves that ID's
submission time itself, server-side, to find where the next page starts;
submission time never appears in a response and never travels in a cursor.

A summary is Markdown with a section per paragraph question
([ADR-0180](../../decisions/ADR-0180-a-summary-is-markdown-with-one-section-per-public-paragraph-question.md)).
The report's own page renders it (headings, paragraphs, bold, italic, lists, and
line breaks only, see `REQ-WLD-045`); the feed's three-line preview shows the
body of the first section as plain text, without its heading or any Markdown
characters (`REQ-MOD-210`). The admin review page renders both languages and
every version in the revision history the same way (`REQ-MOD-208`), and the
summary editor stays a plain text area with no Markdown hint (`REQ-MOD-211`).

The API reads both pages from the `public_reports` database view. The view
holds the whole publication invariant, so it is the only place the public
side decides what is public
([ADR-0055](../../decisions/ADR-0055-ef-core-migrations-sql-files-stored-procedures.md)).
Its displayed publication time is `published_at`, or the pair's approval time
for a report approved before approval published it; its sort key, read but
never returned, is `submitted_at`. A report that stops being publishable
disappears from both pages with no further step, and a cursor naming one
simply starts the feed over from the top.

The site's language, chosen with the language toggle in the header, decides
which summary text is shown. A report page has no language control of its
own. To read the other language, the visitor switches the site's language.
The locale is edge state, not extra report data. The admin report view links to a published report's public
address.

A report's own page also says when its summary was translated: a muted line
under the published date, "Translated from French" or "Translated from
English" (French: "Traduit du français" or "Traduit de l'anglais"), shown only
when the language the reporter wrote the report in differs from the site's
current language. It follows the header's language toggle without a reload and
is absent when the two match (#682, REQ-MOD-190 to REQ-MOD-193,
[ADR-0176](../../decisions/ADR-0176-a-published-report-page-shows-the-language-it-was-written-in.md)).
The owner accepted that publishing the reporter's language is a slight
identifying hint in a small community. The language travels only on a report's
own page, from the `public_reports` view's `language` column; the feed and its
search never carry it. Not built: labelling comments or attachments.

A signed-in Administrator or Safety Officer sees a same-tab link, next to the
published date, from a report's public page to that same report's admin
detail page (`/admin/reports/<id>`, #657). The public payload is unchanged —
the link needs only the report ID the page already has, and the signed-in
member's role decided from the token (invariant 7); the admin route's own
guard and the admin endpoints, not the button's presence, keep the detail
page private. A `User`, or a visitor who is not signed in, sees nothing extra.

A search box at the top of `/reports` fuzzy-searches the approved published
summary and visible member comments, in the visitor's current site
language only, best match first while the box holds text; an empty box is
the plain feed above, unchanged (#574,
[ADR-0157](../../decisions/ADR-0157-the-public-search-privacy-boundary.md)).
It reads only `public_reports` and `public_report_comments` — the same rule
as everywhere else on this page: nothing not already public can be searched,
because nothing not already public is in either view. The query lives in
`?q=`, the same way the cursor lives in `?after=`: bookmarkable, shareable,
and it survives the back button and a reload. Its cursor is the same
report-ID-only cursor the plain feed uses, never a rank score.

The engine is Postgres full-text search (language-appropriate stemming) plus
`pg_trgm` (typo tolerance) and `unaccent` (accent tolerance — "securite"
finds "sécurité" and back), shared with the admin search — that engine
choice is [#573](https://github.com/HPAC-Safety/safety-report/issues/573)'s
own ADR (ADR-0156 at the time of writing). No search index backs it; ADR-0156
decided one is not justified at HPAC's report volume.

## Out of scope

What not to build here. The global list in
[system overview](../../system-overview.md) still holds; this narrows it
to this area ([ADR-0083](../../decisions/ADR-0083-specification-driven-development.md)).

- Filtering or sorting of the public feed other than newest submitted first
  or, while the search box holds text, best match first (#574), and a
  page-count or jump-to-page control.
- Any attachment metadata on the public report page beyond each public file's
  opaque id, its kind, and a document's format. Which files are public is
  [`.spec/features/media`](../media/README.md)'s rule (ADR-0117, ADR-0119).
