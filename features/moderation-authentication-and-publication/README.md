---
title: Moderation, authentication, and publication
description: Supporting detail for the member authentication, review, and public feed scenarios.
type: spec
area: moderation-authentication-and-publication
---

# Moderation, authentication, and publication

Supporting detail for
[`moderation-authentication-and-publication.feature`](moderation-authentication-and-publication.feature)
that doesn't fit Gherkin.

## Authentication boundary

Identity arrives as a signed JWT, presented as `Authorization: Bearer <jwt>`.
The API validates the signature, the issuer, the audience `hpac-safety-api`,
and the lifetime, then reads exactly two claims: the subject and the role.
Nothing else — not a name, not an email address, not a picture
([ADR-0064](../../docs/decisions/ADR-0064-jwt-bearer-authentication-with-three-roles.md)).

There is no allowlist and no user table. **This system stores no user records
at all** ([ADR-0065](../../docs/decisions/ADR-0065-no-user-records-identity-is-the-token-subject.md)).
Where a report's approver or an audit entry's actor is recorded, it is the
token subject as an opaque string that joins to nothing. Revoking access is the
identity provider's decision, and it takes effect when a token stops being
issued or expires.

The role claim's **name** is configuration; its **values** are the invariant
codes `user`, `safety_officer`, and `administrator`. A claim may be a string or
an array, and the highest role present wins. A validated token with no
recognized role authenticates as `User`.

The production provider is not yet chosen — Auth0 and AWS Cognito are the
candidates, and any provider emitting the claim shape above satisfies the
contract. In development the API issues its own genuinely signed token and
validates it through the same middleware, so only the issuer and the key differ
([ADR-0066](../../docs/decisions/ADR-0066-a-development-identity-provider-signed-with-a-dev-key.md)).
Development may also sign a real member in by checking their password against
the live members site, for that one call only
([ADR-0079](../../docs/decisions/ADR-0079-a-development-login-may-verify-against-the-live-members-site.md)).
The third-party sign-in option is production-only; the browser learns whether
to offer it from `GET /api/auth/config`, never from a build flag.

A bearer token carries no ambient authority, so state-changing admin requests
need no CSRF protection.

## Roles

| Role | Capabilities |
|---|---|
| User | Proves HPAC membership. May submit an occurrence report. Nothing else — no review, authoring, or publication capability. |
| SafetyOfficer | View the review queue and private report material; view safe image/video derivatives and download validated unredacted documents; edit the bilingual summary pair; publish, unpublish, and soft-delete reports; keep private notes on a report ([ADR-0133](../../docs/decisions/ADR-0133-staff-keep-private-notes-on-a-report.md)); add, download, and remove a report's private attachments ([ADR-0135](../../docs/decisions/ADR-0135-staff-add-private-attachments-to-a-report.md)); review type-ahead values (approve, correct, merge, remove) ([ADR-0129](../../docs/decisions/ADR-0129-a-type-ahead-value-is-edited-in-place-merged-and-reviewed.md)). |
| Administrator | Every SafetyOfficer capability, plus create question revisions and author each question's choices, including fixing or replacing a picker option ([ADR-0128](../../docs/decisions/ADR-0128-an-answer-names-its-choice-and-a-picker-option-is-fixed-or-replaced.md)). |

Submission is a membership capability rather than a privileged one, so any of
the three roles may file a report — and the report records nothing about who
did ([ADR-0067](../../docs/decisions/ADR-0067-a-reporter-must-be-a-member-and-is-not-recorded.md)).

`Administrator` is not a superuser. No Administrator, migration, background
worker, or direct API caller can bypass a publication guard.

## The admin report list

`/admin/reports` lists every live report, newest first. Each row shows the
submission time, a badge for its workflow status, a separate **Private (no
consent)** badge when the reporter refused publication, and a **Stuck** badge
when it has waited in Submitted or Summarizing for more than 24 hours. Private
is about consent and Unpublished is a status, so the two are never merged into
one badge: a report without consent shows both.

The API gives a report's publication consent (`consent`, on the row and the
detail) and media consent (`mediaConsent`, on the detail) as a JSON `true`,
`false`, or `null` when unanswered — never a word. The interface renders each
from its locale catalogue
([ADR-0130](../../docs/decisions/ADR-0130-a-yes-or-no-answer-is-stored-as-a-boolean.md),
REQ-MOD-096).

| Filter | Shows |
|---|---|
| All (default) | every live report |
| Needs action | Pending, Summary failed, and stuck reports |
| Published | Published |
| Private | reports whose reporter refused consent, whatever their status |
| Unpublished | Unpublished |
| Summary failed | Summary failed |

The chosen filter is kept in the address bar. The list carries status and
timing only — never answer or summary text. Opening a report shows its detail
view, and that read is audited as `ViewedRawReport`
([REQ-MOD-051](moderation-authentication-and-publication.feature)).
Attachments are listed by kind and state; opening one goes through its own
audited view or download request (REQ-MOD-046).

## Pending counts on the Admin menu (#418)

The Admin menu shows how much work is waiting, so a reviewer sees it without
opening each page. **Manage reports** carries the number of reports the
*Needs action* filter lists, and **Answers awaiting translation** carries the
number of answers in that queue. That second number is only for an
administrator, since the queue is theirs alone. The closed **Admin** button
carries the total of the counts the member can see. A count of zero shows no
badge. The badge is a filled brand-red pill
([design system](../../docs/design-system.md)).

`GET /api/admin/counts` answers any reviewer. It gives the translation count
only to an administrator and carries no report content, so it is not
audited. The counts are read from the same database views as the list and the
queue, so they cannot disagree with them
([REQ-MOD-084..089](moderation-authentication-and-publication.feature)). The
menu refetches on each navigation.

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
([ADR-0125](../../docs/decisions/ADR-0125-a-report-is-pending-published-or-unpublished.md)).
A report without consent is never summarized: the Worker sets it Unpublished,
and it stays there for good. It never needs action, is never counted on the
Admin menu, and can only be deleted (REQ-DOM-015, REQ-MOD-090).

**Publish** approves the current pair and makes the report public at once.
**Unpublish** takes a report off the public feed, or declines a pending one,
and takes an optional note that only reviewers see; **Publish** brings it back.
**Edit summary** saves both texts together and returns the report to Pending,
taking it off the public feed if it was there. A hand-written pair after a
failed summarization carries `manual` as its model and prompt version.

Every action carries the version of the report the reviewer loaded. If another
reviewer changed it since, the API answers `409` and nothing is saved; the page
asks the reviewer to reload. Each action writes one content-free audit entry
in the same transaction.

## Private notes (#508)

A safety officer or administrator may keep notes on a report: calls made,
follow-ups, what an investigator said
([ADR-0133](../../docs/decisions/ADR-0133-staff-keep-private-notes-on-a-report.md)).
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
  [ADR-0135](../../docs/decisions/ADR-0135-staff-add-private-attachments-to-a-report.md)).

## Private attachments (#507)

A safety officer or administrator may add files to a report that are for
staff only
([ADR-0135](../../docs/decisions/ADR-0135-staff-add-private-attachments-to-a-report.md)).
The report view has a **Private attachments** section, newest first. Each
lists its file name, size, optional description, who added it (**You**, or
the adder's opaque token subject), and when. Adding a file shows its progress
and can be cancelled; any reviewer may download any attachment, or remove one
after confirming.

- Any report that is not deleted, in any status, including a report without
  publication consent (REQ-MOD-108). Any file type, up to the configured cap;
  how the file travels and is stored is
  [`features/media`](../media/README.md)'s rule.
- The file name is required and is sanitized; the description is optional
  plain text of at most 500 characters (REQ-MOD-110).
- Removal soft-deletes the row, records who removed it, and writes one
  `RemovedPrivateAttachment` audit entry; the bytes stay in storage. Deleting
  the report does the same to its private attachments (REQ-MOD-109,
  REQ-MOD-111).
- The endpoints live under
  `/api/admin/reports/{reportId}/private-attachments` and answer only a Safety
  Officer or an Administrator (REQ-MOD-107). Nothing else reads the table: not
  the report detail DTO or its attachment list, not a count, not a database
  view, not the public feed or its media, not the Worker or the model
  (REQ-MOD-112, REQ-MOD-113). An attachment count on a report list (#427)
  counts the reporter's attachments only.

## Reading a date, time, or yes/no answer (#403)

A date is stored as ISO 8601 `YYYY-MM-DD` and a time as `HH:mm`
([ADR-0072](../../docs/decisions/ADR-0072-every-answer-is-stored-as-a-string.md)),
and a yes/no as a boolean, `true` or `false`, sent as a JSON boolean
([ADR-0130]../../docs/decisions/ADR-0130-a-yes-or-no-answer-is-stored-as-a-boolean.md)).
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
([ADR-0108](../../docs/decisions/ADR-0108-a-reviewer-may-machine-translate-a-summary-language.md)).
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

## The public feed and report page (#329)

**View safety reports** (`/reports`) is anonymous. It lists every publishable
report, newest published first. Each entry shows its summary in the visitor's
language and its publication date, and links to the report's own address,
`/reports/<id>`. A visitor can bookmark, share, or reload that address.
Paging forward puts an opaque cursor in the address bar (`?after=`), so the
back button returns to the page the visitor came from.

The API reads both pages from the `public_reports` database view. The view
holds the whole publication invariant, so it is the only place the public
side decides what is public
([ADR-0055](../../docs/decisions/ADR-0055-ef-core-migrations-sql-files-stored-procedures.md)).
Its publication time is `published_at`, or the pair's approval time for a
report approved before approval published it. A report that stops being
publishable disappears from both pages with no further step.

The site's language, chosen with the language toggle in the header, decides
which summary text is shown. A report page has no language control of its
own. To read the other language, the visitor switches the site's language.
The locale is edge state, not extra report data. The admin report view links to a published report's public
address.

## Out of scope

What not to build here. The global list in
[system overview](../../docs/system-overview.md) still holds; this narrows it
to this area ([ADR-0083](../../docs/decisions/ADR-0083-specification-driven-development.md)).

- A live-updating or polling count on the Admin menu. The counts refresh when
  the member navigates.
- A count on Manage questions, or a per-status breakdown of the reports
  needing action.
- A user table, an allowlist, an allowlist-management screen, or a session
  store. Roles come from the token
  ([ADR-0065](../../docs/decisions/ADR-0065-no-user-records-identity-is-the-token-subject.md)).
- Handling a member's password, outside the one Development-only carve-out
  ([ADR-0079](../../docs/decisions/ADR-0079-a-development-login-may-verify-against-the-live-members-site.md)).
- CSRF machinery or Turnstile. A bearer token carries no ambient authority
  ([ADR-0068](../../docs/decisions/ADR-0068-the-member-token-replaces-turnstile-on-submission.md)).
- Email, push, or chat notification of a reviewer, a reporter, or anyone else.
- Any publication channel besides the HPAC public feed.
- Automatic approval or publication, including "approve if the model is
  confident."
- A per-reporter rate limit, which would mean identifying the reporter.
- Pagination, search, or sorting of the admin report list other than newest
  first. HPAC receives dozens of reports a year.
- Showing answer or summary text in the admin report list itself.
- Re-running summarization from the review screen. A failed summary is
  written by hand.
- Editing one language without the other in separate saves: the pair is
  saved together.
- Showing an unpublishing note anywhere but the admin report view.
- An Approve step separate from Publish, or a Reject or Reopen action
  ([ADR-0125](../../docs/decisions/ADR-0125-a-report-is-pending-published-or-unpublished.md)).
- Search, filtering, or sorting of the public feed other than newest
  published first, and a page-count or jump-to-page control.
- Any attachment metadata on the public report page beyond each public file's
  opaque id, its kind, and a document's format. Which files are public is
  [`features/media`](../media/README.md)'s rule (ADR-0117, ADR-0119).
- Translating a summary automatically on save, or with the summarization
  model. Translation is a draft the reviewer asks for and accepts.
- Changing how a date or time answer is stored, sent by the API, or sent to
  the Worker or the model. It stays in its ISO 8601 form; only what a person
  reads is localized, and it never gets a second language
  ([ADR-0112](../../docs/decisions/ADR-0112-only-answers-that-need-it-get-a-second-language.md)).
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
- For private attachments: any count of them outside their own list, a
  preview or inline view, and anything the media area rules out for them
  ([`features/media`](../media/README.md)).
