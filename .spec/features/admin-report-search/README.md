---
title: Admin report list and search
description: Supporting detail for the admin report list: its rows, filters, search, paging, and quick actions.
type: spec
area: admin-report-search
prefix: REQ-ARS
---

# Admin report list and search

Supporting detail for [`admin-report-search.feature`](admin-report-search.feature)
that doesn't fit Gherkin.

## The admin report list

`/admin/reports` lists every live report, newest submitted first, a tie broken
by report ID (REQ-MOD-129). Each row shows the submission time, a badge for
its workflow status, a separate **Private (no consent)** badge when the
reporter refused publication, and a **Stuck** badge when it has waited in
Submitted or Summarizing for more than 24 hours. Private is about consent and
Unpublished is a status, so the two are never merged into one badge: a report
without consent shows both.

Both this list and the public feed load more automatically as the reviewer or
visitor nears the end, the same infinite-scroll pattern
([ADR-0155](../../decisions/ADR-0155-infinite-scroll-replaces-load-more-on-both-report-lists.md)):
an `IntersectionObserver` sentinel triggers the next keyset page, with
nothing shown for it while auto-load keeps working. A "Load more" fallback
stays reachable by Tab at all times but is visually hidden until it holds
keyboard focus, so a sighted visitor never sees a control auto-load already
made unnecessary; a screen reader still reaches it in the normal reading
order. Once a page fails to load, the control becomes visible unconditionally
and reads "Retry." Every newly loaded batch is announced politely regardless
of how it loaded. The admin list gained server-side keyset paging for this
(it had none before, REQ-MOD-129); its cursor carries only the last row's
report ID, the same shape as the public feed's (ADR-0153) — never a
timestamp — and a cursor naming a report no longer in the queue restarts the
list from the top, ordinally past the anchor by ID the same way the public
feed's cursor does. The browser's back button restores the same accumulated
rows and scroll position rather than reloading the first page. Only a return
to the same history entry restores (Back or Forward, or a reload); opening a
list afresh, from a link or by typing its address, loads its first page and
starts at the top, however far it was scrolled earlier in the same tab
(REQ-MOD-178, REQ-MOD-179,
[ADR-0173](../../decisions/ADR-0173-a-fresh-navigation-starts-at-the-top-and-only-a-return-restores.md)).

Each row also shows the **reporter's name** and the **pilot's name** — the
only answer text the list ever carries (REQ-MOD-030, REQ-MOD-124). Both are
read by a stable question role, not by position, so a fork or a reworded
question never loses them (ADR-0154): `reporter_first_name`,
`reporter_last_name`, `pilot_first_name`, and `pilot_last_name` are four more
optional `QuestionRole` values alongside publication and media consent
(`AGENTS.md` invariant 1). Every one of the four stays independently optional.
A name shows whichever of first and last was answered, blank when neither
was. Reporter and pilot are never collapsed into one name when they are the
same person — each shows what its own answers hold. Names never reach a
public endpoint; `admin_report_queue` is an admin-only view.

Listing reports is not an audited read (REQ-MOD-030): reading the list writes
no `ViewedRawReport` entry. The reporter's and pilot's names are the one piece
of answer text this list shows, so they are the one piece of answer text a
reviewer can read here without an audit entry recording it. Opening a report
stays the audited read of everything else — every other answer, the summary
pair, and any attachment (ADR-0154).

The API gives a report's publication consent (`consent`, on the row and the
detail) and media consent (`mediaConsent`, on the detail) as a JSON `true`,
`false`, or `null` when unanswered — never a word. The interface renders each
from its locale catalogue
([ADR-0130](../../decisions/ADR-0130-a-yes-or-no-answer-is-stored-as-a-boolean.md),
REQ-MOD-096).

| Filter | Shows |
|---|---|
| All (default) | every live report |
| Needs action | Pending, Summary failed, and stuck reports |
| Published | Published |
| Private | reports whose reporter refused consent, whatever their status |
| Unpublished | Unpublished |
| Summary failed | Summary failed |

The list carries status and
timing only — never answer or summary text. Opening a report shows its detail
view, and that read is audited as `ViewedRawReport`
([REQ-MOD-051](../review-and-publication/review-and-publication.feature)).
Attachments are listed by kind and state; opening one goes through its own
audited view or download request (REQ-MOD-046).

### Search (#573)

A search box sits at the very top of the page, above the filter. It
fuzzy-searches every part of a live report — every answer including private
ones, a choice's label in both languages, the summary pair, staff-only
private notes, member comments, and both reporter-uploaded and staff-only
attachment file names — built on PostgreSQL's own full-text search and
`pg_trgm` word similarity, no new service
([ADR-0156](../../decisions/ADR-0156-postgres-full-text-and-trigram-search-for-manage-reports.md)).

- **Best match first** while the box holds text; **newest submitted first**
  when it is empty, exactly as before this decision (REQ-MOD-133,
  REQ-MOD-134).
- **Searches within the chosen filter.** Typing a query never leaves
  "Published" or "Needs action"; it narrows what that filter already shows
  (REQ-MOD-135).
- **Typo-tolerant and bilingual**: a misspelled word, a partial word, or a
  French query against an English answer (or the reverse) still finds the
  report (REQ-MOD-131, REQ-MOD-132).
- **Lives in the address bar** as `?q=`, alongside the filter — bookmarkable,
  and it survives a reload or the back button (REQ-MOD-136).
- **No results** shows a message naming the search text rather than an empty
  list with no explanation (REQ-MOD-137).
- Reads what it finds, never which part matched: a hit is the whole report,
  not an attributed snippet.
- Only live (non-deleted) reports, the same as the rest of the list. The
  search text itself is never logged (REQ-MOD-139) — report content is never
  logged (`AGENTS.md` invariant 8).
- SafetyOfficer and Administrator only, the same authorization the rest of
  Manage reports already requires; the search box adds no route and no
  policy of its own.

### Quick actions on each row (#568)

Each row also carries icon buttons, so a reviewer can act without opening the
report. Each has a name in both languages and a tooltip:

| Row | Buttons |
|---|---|
| Pending, or Unpublished (consented) | Publish, Delete |
| Published | Unpublish, Delete |
| Any other | Delete |

Publish and Unpublish run the same audited commands as the report view, and
the row's badge changes in place. Unpublishing from a row carries no note;
declining a Pending report with a note stays in the report view. Delete asks
for confirmation first, with the report view's dialog, because nothing
restores a deleted report (REQ-DOM-007).

Each row carries the report's review version
([ADR-0105](../../decisions/ADR-0105-approving-a-consented-pair-publishes-it.md)),
so a row action needs no detail read, and listing stays unaudited. A row
action based on a stale list is refused with `409`, and the page offers to
reload the list
([REQ-MOD-119..123](admin-report-search.feature)).

## Out of scope

What not to build here. The global list in
[system overview](../../system-overview.md) still holds; this narrows it
to this area ([ADR-0083](../../decisions/ADR-0083-specification-driven-development.md)).

- Sorting of the admin report list other than newest submitted first (or
  best match first while a search is active), and a page-count or
  jump-to-page control.
- Showing which part of a report matched a search, or a highlighted snippet
  of the match. A search only decides which reports are found and their
  order (ADR-0156).
- Showing answer or summary text in the admin report list itself, other than
  the reporter's and pilot's names (REQ-MOD-124, ADR-0154).
- Acting on several reports at once from the list, editing or writing the
  summary pair from a row, or an unpublishing note from a row.
