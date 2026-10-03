---
title: Summaries are append-only revisions, and a live edit publishes itself
description: A report's summary becomes an append-only list of revisions in its own table, each recording its author and how it was written. A rollback adds a new revision. On a Published report a saved revision is approved by its author and public at once; on any other report it is a draft.
type: adr
status: accepted
date: 2026-09-29
decision-makers: Chase Florell
keywords: summary, revision, rollback, history, approval, publication, invariant 6, public_reports, ADR-0065, ADR-0105, ADR-0108, ADR-0177
---

# ADR-0177 — Summaries are append-only revisions, and a live edit publishes itself

**Status:** Accepted. Amends `AGENTS.md` product invariant 6 ("Editing either
language clears the pair approval" now applies only before a report is first
published) and supersedes the return-to-Pending half of `REQ-MOD-032` and
`REQ-DOM-005` for a live report. Amends
[ADR-0108](ADR-0108-a-reviewer-may-machine-translate-a-summary-language.md)
where it says saving the pair "clears approval as any edit does", and
[ADR-0105](ADR-0105-approving-a-consented-pair-publishes-it.md)
where the version it names now ends in the latest revision. Part of
[#665](https://github.com/HPAC-Safety/safety-report/issues/665), decided by the
owner on 2026-09-29 in
[#668](https://github.com/HPAC-Safety/safety-report/issues/668).

## Context

`summaries` held exactly one row per report. `Report.EditSummary` overwrote the
text in place, the row named only the approver, and the audit log records
`EditedSummary` and the subject but never the text. So a reviewer's edit left no
history, nobody could see what the Worker had written once it was edited, and a
bad edit could not be undone. Every edit also cleared approval and sent a
Published report back to Pending, which took a live, correct report off the
public feed for as long as a reviewer took to approve their own one-word fix.

## Decision

**A summary is an append-only list of revisions.** `summaries` stays one row per
report, as the identity its revisions hang from and the carrier of its soft
deletion. A new table, `summary_revisions`, holds one row per saved version:

| Column | Holds |
|---|---|
| `summary_id`, `sequence` | The summary, and 1, 2, 3… Unique together, so two reviewers cannot both append the same next revision. |
| `ai_summary_en`, `ai_summary_fr` | Both languages, always saved together. |
| `source_en`, `source_fr` | `generated`, `human`, or `machine` ([ADR-0108](ADR-0108-a-reviewer-may-machine-translate-a-summary-language.md)). A language a save left unchanged keeps its source. |
| `model`, `prompt_version` | The provenance of the text the revision descends from, carried forward by an edit and copied by a rollback. `manual` for a pair written by hand. |
| `author_subject` | The opaque token subject of whoever saved it ([ADR-0065](ADR-0065-no-user-records-identity-is-the-token-subject.md)). Null for the Worker, and for a revision written before authors were recorded. |
| `created_at`, `restored_from_id` | When it was saved, and the earlier revision it restores when it is a rollback. |
| `approved_by_subject`, `approved_at` | The approval, set together or not at all. |
| `deleted` | Stamped with its report's ([REQ-DOM-007](../features/domain-and-lifecycle/domain-and-lifecycle.feature)). |

- **Who.** Safety Officers and Administrators edit and roll back — the same
  roles that edit today.
- **The Worker** writes revision 1, `generated`, with no author. A pair a
  reviewer writes by hand after a failed summarization (REQ-MOD-059) is revision
  1 too, authored by that reviewer, with `manual` provenance. The Worker and its
  one model call are unchanged ([invariant 3](../../AGENTS.md)).
- **Edit** saves a new revision containing both languages. A save that changes
  neither language is refused: a revision holds a change.
- **Rollback** picks an earlier revision and saves a **new** revision that
  copies its text, sources, and provenance and names it in `restored_from_id`.
  The earlier revision is untouched. Versions only move forward. Restoring the
  current revision is refused.
- **Publish rules.**
  - A report that is **Published**: the new revision, from an edit or a
    rollback, is approved by the person who saved it and is public at once. The
    report stays Published with no gap on the feed, and `PublishedAt` keeps the
    first publish date.
  - A report that is **Pending**: the new revision is a draft. It is not public.
    Approve & Publish approves the **latest** revision and checks consent, as
    before.
  - A report that is **Unpublished** (with consent): the new revision is a draft
    and the report returns to Pending, as before.
  - Unpublishing clears the latest revision's approval, as before. Every earlier
    revision keeps the approval it was given, so history still says who approved
    what.
- **The public reads only the latest approved revision of a Published report,
  through a SQL view.** `public_reports` reads the new
  `latest_approved_summary_revisions` view. A draft newer than the approved
  revision is never seen.
- **Audit.** `EditedSummary` stays, and `RolledBackSummary` is added. Neither
  records text; both record the subject, the report, and the time.
- **Concurrency.** The version a review command carries
  ([ADR-0105](ADR-0105-approving-a-consented-pair-publishes-it.md))
  is the report's `xmin` and the latest revision's `xmin`. A stale approval is
  refused by the row version; two edits from the same view are refused by the
  unique `(summary_id, sequence)` index, and the second reviewer is told to
  reload.

### What an edit's approval means

An approval belongs to the revision it was given to. Saving a live edit is
itself the approval: the reviewer who chose to save it has read it. No
third-party review sits between a Published report and its next revision. That
is the owner's decision, made to keep a correct report on the feed while it is
corrected. The one thing that still needs Approve & Publish is a report that is
not live.

### What is not built

- Regenerating a summary with the model. There is still one call per attempt.
- Showing revision history publicly. History is a reviewer view, read as part of
  the audited report detail.
- Editing or deleting a saved revision, or hiding one from the history.

## Consequences

### The `summaries` columns fold forward

The migration `AddSummaryRevisions` copies each existing row into revision 1 and
**then drops the moved columns from `summaries`** (`ai_summary_en`,
`ai_summary_fr`, `source_en`, `source_fr`, `model`, `prompt_version`,
`approved_by_subject`, `approved_at`, `generated_at`, `updated_at`) and the check
constraints that named them. No row is deleted and the table is kept. This is
the same shape as the carved exceptions in invariant 8 — data folded forward and
the emptied shape removed — but it is a column drop, not a table drop, and it
copies every value first. It is argued here on its own facts and does not
generalize: keeping the columns would leave two sources of truth for the same
text, and the old `NOT NULL` columns would force every new summary to fill them.
The migration's `Down` restores the columns from each summary's latest revision;
it cannot restore earlier revisions.

- Each existing `summaries` row becomes revision 1 with its sources, provenance,
  and approval, `created_at` from its `generated_at`, and no author. Earlier
  edits cannot be recovered: the audit log never held the text.
- `public_reports`, `admin_report_queue`, and `admin_report_search_document` are
  recreated to read the two new views, `latest_summary_revisions` and
  `latest_approved_summary_revisions`. `public_reports` keeps its columns and
  `published_at` still comes from `reports.published_at`.
- `AGENTS.md` invariant 6 now reads: approval belongs to a revision; an edit
  before first publication is a draft; an edit to a Published report is approved
  and published at once.
- `REQ-MOD-032` and `REQ-DOM-005` are amended in place to the new rule.
  `REQ-MOD-063`'s UI scenario now edits a Pending report and sees it stay
  Pending, and `REQ-MOD-061` gains a row for `RolledBackSummary`. New scenarios:
  `REQ-MOD-194`..`REQ-MOD-207`.
- The admin report detail shows the revision history (who, when, source,
  restored-from), lets a reviewer view any revision, and offers **Restore this
  version** behind a confirmation. The editor no longer warns that saving clears
  approval on a live report.

## Considered options

**Keep one row and add an edit-history table beside it.** Two writers to the
current text, and a public reader that has to pick between them. Making the
revisions the only place text lives means "current" is one query.

**Approve the live edit later, keeping the old text on the feed until then.**
Considered: it keeps a reviewer from publishing their own words unread. The owner
rejected it for the extra state (a Published report with an unapproved newest
revision) and the delay before a correction, since the person saving is a reviewer
already.

**Drop `summaries` and hang revisions off `reports`.** Rejected for this change:
dropping a table needs its own argument, the row is the soft-deletion carrier the
existing filters and the one-per-report unique index already use, and nothing is
gained for a table of three columns.

**A rollback that rewrites the current revision in place.** Loses the history
this decision exists to keep, and makes "who wrote what the public read at 3 p.m."
unanswerable.

## Related

- [ADR-0108](ADR-0108-a-reviewer-may-machine-translate-a-summary-language.md) — per-language sources, amended
- [ADR-0105](ADR-0105-approving-a-consented-pair-publishes-it.md) — the version a command carries, amended
- [ADR-0125](ADR-0125-a-report-is-pending-published-or-unpublished.md) — the three review statuses
- [ADR-0065](ADR-0065-no-user-records-identity-is-the-token-subject.md) — the author is an opaque token subject
- [ADR-0055](ADR-0055-ef-core-migrations-sql-files-stored-procedures.md) — the views are SQL files
- [issue #665](https://github.com/HPAC-Safety/safety-report/issues/665), [issue #668](https://github.com/HPAC-Safety/safety-report/issues/668)
