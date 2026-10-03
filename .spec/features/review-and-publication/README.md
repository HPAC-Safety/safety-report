---
title: Review and publication
description: Supporting detail for reviewing a report, its summary revisions, publication, private notes, and how a reviewer reads an answer.
type: spec
area: review-and-publication
prefix: REQ-REV
---

# Review and publication

Supporting detail for [`review-and-publication.feature`](review-and-publication.feature)
that doesn't fit Gherkin.

## Review actions

The report view offers only what the report's state allows:

| Status | Actions |
|---|---|
| Pending | Edit summary, Publish, Unpublish, Delete |
| Published | Edit summary, Unpublish, Delete |
| Unpublished (consented) | Edit summary, Publish, Delete |
| Unpublished (no consent) | Delete |
| Summary failed | Write summary, Delete |
| Submitted, Summarizing | Delete |

Review exists only to check a summary
([ADR-0125](../../decisions/ADR-0125-a-report-is-pending-published-or-unpublished.md)).
A report without consent is never summarized: the Worker sets it Unpublished,
and it stays there for good. It never needs action, is never counted on the
Admin menu, and can only be deleted (REQ-DOM-015, REQ-MOD-090).

**Publish** approves the current pair and makes the report public at once.
**Unpublish** takes a report off the public feed, or declines a pending one,
and takes an optional note that only reviewers see; **Publish** brings it back.
**Edit summary** saves both texts together as a new revision, with the
reviewer as its author, and is refused when neither language changed (REQ-MOD-194,
REQ-MOD-205); the editor offers **Save summary** only once a language differs
(REQ-MOD-207). What the save does to the report depends on its state
([ADR-0177](../../decisions/ADR-0177-summaries-are-append-only-revisions-and-a-live-edit-publishes-itself.md)):

| Status | A saved revision |
|---|---|
| Published | Approved by the person who saved it and public at once. The report stays Published and keeps its first publish date (REQ-MOD-195). |
| Pending | A draft, not public. **Publish** approves the latest revision (REQ-MOD-198). |
| Unpublished (consented) | A draft, and the report returns to Pending (REQ-MOD-032). |

A hand-written pair after a failed summarization is revision 1, authored by the
reviewer, and carries `manual` as its model and prompt version.

**Summary history.** Below the summary, the report view lists every revision
newest first: who saved it (an opaque token subject, or "the AI" for the Worker's
revision 1, or "an unknown author" for a revision written before authors were
recorded), when, how each language was written, and which version it restored
(REQ-MOD-202). **View this version** shows any revision's two texts without
changing the current one (REQ-MOD-203). **Restore this version** asks for
confirmation, then saves a **new** revision that copies the old one; the old one
is untouched and versions only move forward. A Published report shows the
restored text at once; any other report holds it as a draft (REQ-MOD-196,
REQ-MOD-197, REQ-MOD-204). Restoring is audited as `RolledBackSummary`, and
neither it nor an edit records any text in the audit log (REQ-MOD-061). A
Safety Officer or an Administrator may edit and restore; a User may not
(REQ-MOD-201).

**Not built:** regenerating a summary with the model, and showing revision
history publicly.

Every action carries the version of the report the reviewer loaded. If another
reviewer changed it since, the API answers `409` and nothing is saved; the page
asks the reviewer to reload. Each action is audited (`REQ-MOD-061`).

## Private notes (#508)

A safety officer or administrator may keep notes on a report: calls made,
follow-ups, what an investigator said
([ADR-0133](../../decisions/ADR-0133-staff-keep-private-notes-on-a-report.md)).
The report view has a **Private notes** section, newest note first. Each note
shows its current text, who wrote that text (**You**, or the writer's opaque
token subject), when, and whether it was edited. Any reviewer may add a note,
edit any note, open a note's history, or remove a note after confirming.

- A note is plain text of 1 to 4000 characters, shown exactly as typed. It
  may be added to any report that is not deleted, in any status, including a
  report without publication consent (REQ-MOD-099, REQ-MOD-102).
- An edit is a new revision; the history lists every revision, oldest first,
  each with its own text, writer, and time. An edit based on a revision that
  is no longer the latest is refused with `409`, so two reviewers cannot
  silently overwrite each other (REQ-MOD-100).
- Removal soft-deletes the note and its revisions and writes one
  content-free `RemovedPrivateNote` audit entry. Deleting the report does the
  same to its notes (REQ-MOD-101, REQ-MOD-103).
- The endpoints live under `/api/admin/reports/{reportId}/private-notes` and
  answer only a Safety Officer or an Administrator (REQ-MOD-098). Nothing else
  reads the notes: not the report detail DTO, not a database view, not the
  public feed or comments, not the Worker or the model, and never a
  translation provider (REQ-MOD-104, REQ-MOD-105).
- A note may refer to one private attachment on the same report. The
  reference belongs to the revision, so an edit may add, change, or drop it,
  and the history shows each revision's own. An attachment on another report,
  or one already removed, is refused with `400`; a revision whose attachment
  was removed later still shows it, marked removed (REQ-MOD-114,
  [ADR-0135](../../decisions/ADR-0135-staff-add-private-attachments-to-a-report.md)).

## Reading a date, time, or yes/no answer (#403)

A date is stored as ISO 8601 `YYYY-MM-DD` and a time as `HH:mm`
([ADR-0072](../../decisions/ADR-0072-every-answer-is-stored-as-a-string.md)),
and a yes/no as a boolean, `true` or `false`, sent as a JSON boolean
([ADR-0130](../../decisions/ADR-0130-a-yes-or-no-answer-is-stored-as-a-boolean.md)).
That form is for storage only. The report view shows such an answer in the
interface language the reviewer chose, not the language the reporter
answered in: `2026-09-13` reads "September 13, 2026" in English and
"13 septembre 2026" in French, `14:30` reads "2:30 p.m." or "14 h 30", and
`true` reads "Yes" or "Oui". The detail view names each question's type so
the page can tell these answers from free text. Because the formatted value
already reads in the reviewer's language, the view shows no second-language
translation beside it. A stored value that is not a real date or time is
shown exactly as stored.

While editing a pair, a reviewer who changed one language may draft the other
from it by machine translation
([ADR-0108](../../decisions/ADR-0108-a-reviewer-may-machine-translate-a-summary-language.md)).
**Translate to French** appears once the English text was changed, **Translate
to English** once the French text was changed, and both when both were. A
language filled by an accepted translation does not count as changed, so it
never offers to translate back. Translating never overwrites silently: it
shows the current text beside the proposed one with their differences marked,
and replaces it only when the reviewer accepts. The same buttons appear when a
pair is written by hand after summarization failed.

Each saved language records how it was produced — `generated` by the Worker,
`human` when a reviewer typed it, or `machine` when it is an accepted
translation — and the report view shows it. Translation goes through the
server's translation port; safety officers and administrators may use it.

## Out of scope

What not to build here. The global list in
[system overview](../../system-overview.md) still holds; this narrows it
to this area ([ADR-0083](../../decisions/ADR-0083-specification-driven-development.md)).

- Email, push, or chat notification of a reviewer, a reporter, or anyone else.
- Any publication channel besides the HPAC public feed, until #661's decision
  specifies one.
- Automatic approval or publication, including "approve if the model is
  confident."
- Re-running summarization from the review screen. A failed summary is
  written by hand.
- Editing one language without the other in separate saves: the pair is
  saved together.
- Showing an unpublishing note anywhere but the admin report view.
- An Approve step separate from Publish, or a Reject or Reopen action
  ([ADR-0125](../../decisions/ADR-0125-a-report-is-pending-published-or-unpublished.md)).
- Translating a summary automatically on save, or with the summarization
  model. Translation is a draft the reviewer asks for and accepts.
- Changing how a date or time answer is stored, sent by the API, or sent to
  the Worker or the model. It stays in its ISO 8601 form; only what a person
  reads is localized, and it never gets a second language
  ([ADR-0112](../../decisions/ADR-0112-only-answers-that-need-it-get-a-second-language.md)).
  A yes/no answer is a boolean with no words and no second language
  (ADR-0130); only the view turns it into words.
- Converting a date or time between time zones. A date is a calendar date and
  a time is the wall-clock time the reporter entered; neither is shifted to
  the reviewer's zone.
- A per-reviewer date format preference. The format follows the interface
  language.
- For private notes: translating a note, Markdown or rich-text rendering,
  mentions or notifications, files attached to a note, search across notes,
  a count of notes anywhere outside the note list, and restoring a removed
  note (ADR-0133). A note may refer to a private attachment, but never holds a
  file of its own (ADR-0135).
