---
title: Report submission
description: Supporting detail for the browser continuity, upload and submission API, DTO, and validation scenarios.
type: spec
area: report-submission
---

# Report submission

Supporting detail for [`report-submission.feature`](report-submission.feature)
that doesn't fit Gherkin.

## Attachment uploads

Each file uploads the moment the reporter attaches it, straight to private
quarantine through a pre-signed PUT the API mints
([ADR-0096](../../docs/decisions/ADR-0096-an-attachment-uploads-on-attach-and-is-claimed-at-submission.md),
[ADR-0126](../../docs/decisions/ADR-0126-an-attachment-uploads-straight-to-quarantine-by-pre-signed-put.md)):

- `POST /api/v1/uploads` takes JSON naming the file's declared content type and
  its exact size in bytes. The client filename is never sent. A file may be at
  most 250 MB for a video and 25 MB for an image or a document.
- `201` returns
  `{{ "uploadId": "…", "kind": "image" | "video" | "document", "uploadUrl": "…", "expiresAt": "…" }}`.
  The upload ID is 22 URL-safe characters (128 random bits). The URL is a
  `PUT` to `quarantine/<upload id>` only, signed for the declared type and
  exact size, and lives at most 15 minutes.
- `400` returns a problem with a `reason` of `empty`, `too_large`, or
  `unaccepted_media_type`, which the form maps to a localized message on that
  file's row. Nothing is minted.
- The browser then sends the file itself as the body of that `PUT`, with the
  declared `Content-Type`. Storage refuses a body that differs from what was
  signed. Cancel aborts the `PUT`.
- `DELETE /api/v1/uploads/{{id}}` erases every version of that quarantine object
  and returns `204`, whether or not it existed.
- The browser keeps each finished upload's ID, the file's name, and its size in
  the saved report beside the answers, never the file itself. Continuing the
  saved report lists those files again
  ([ADR-0100](../../docs/decisions/ADR-0100-an-attachment-is-kept-as-long-as-the-saved-report.md)).

At submission a file-upload answer names its uploads in `attachments`, each an
`uploadId` and the `fileName` the reporter's browser knew it by. The API
sanitizes that name and keeps it on the report file only as a reviewer's
download name
([ADR-0097](../../docs/decisions/ADR-0097-a-reviewer-downloads-an-attachment-under-its-sanitized-original-name.md)).
If any named upload no longer exists, the API refuses the whole submission
before writing anything, with a `400` whose `expiredUploadIds` lists exactly
those IDs.

The API then validates each upload it claims. It reads the stored size and
only the bytes sniffing needs — the leading bytes, and a DOCX or ODT's zip
directory at its end
([ADR-0134](../../docs/decisions/ADR-0134-a-claim-reads-a-zip-packages-directory-as-well-as-its-leading-bytes.md))
— sniffs the content, requires the declared and detected types to agree, and
checks the real size against the limit of the detected kind. If any upload
fails, the API refuses the whole submission before writing anything, with a
`400` whose `refusedUploads` lists each refused upload's `uploadId` and
`reason` (`unrecognised_content`, `declared_type_mismatch`, or `too_large`).
One response carries both lists when some uploads expired and others were
refused. The form marks those files' rows and keeps every other answer and
upload.

### The drop zone (#367)

The field is a bordered drop zone rather than the browser's bare file control.
One button, holding a large upload icon and the prompt "Drag files here, or
choose files", opens the file chooser; it is an ordinary button, so the
keyboard reaches it and nothing depends on a pointer. The type, count, and
size guidance sits in the zone below it. The native file input stays in the
page, still labelled by the question, but is visually hidden.

Dragging files over the zone highlights it. Dropping them attaches them
through exactly the path choosing them takes, so the attachment limit, the
size check, and each file's indicator, Cancel, and Remove behave the same. A
file dropped anywhere else on the form is ignored: the browser neither opens
it nor leaves the form.

## Submission DTO shape

```json
{
  "language": "en-CA",
  "answers": [
    {
      "questionRevisionId": "text-revision-id",
      "value": "A short answer",
      "choices": null,
      "attachments": null
    },
    {
      "questionRevisionId": "multi-select-revision-id",
      "value": null,
      "choices": ["Qm7pR2xT9aB", "Zt4kW8nV1cD"],
      "attachments": null
    },
    {
      "questionRevisionId": "file-revision-id",
      "value": null,
      "choices": null,
      "attachments": [
        { "uploadId": "kP3x9QmR2vT8wLb6nYc4Dg", "fileName": "launch-site.jpg" }
      ]
    }
  ]
}
```

`choices` carries the identifiers of the choices a single-select,
multi-select, or type-ahead answer names; the API accepts only live choices of
that question. A type-ahead answer naming a value the question does not offer
carries the reporter's typed text in `value` instead, and becomes a new
reporter-added value. No answer carries a choice's wording or code
([ADR-0128](../../docs/decisions/ADR-0128-an-answer-names-its-choice-and-a-picker-option-is-fixed-or-replaced.md),
[ADR-0129](../../docs/decisions/ADR-0129-a-type-ahead-value-is-edited-in-place-merged-and-reviewed.md)).

Field names are camelCase on the wire (ASP.NET's default JSON casing), not
the snake_case the Gherkin prose uses when it names them — the scenarios are
talking about the concept, not literal JSON.

Dates use ISO `YYYY-MM-DD`; times, if a question requests one, use local wall
clock `HH:mm` without inventing an offset; numbers use invariant JSON numbers.
A yes/no or checkbox answer is a JSON `true` or `false`, whatever the report
language, and a string for one is refused
([ADR-0130]../../docs/decisions/ADR-0130-a-yes-or-no-answer-is-stored-as-a-boolean.md)). The form holds a language-free
answer while the reporter works and sends the boolean when it submits, so
switching language mid-form loses nothing. The report language is exactly
`en-CA` or `fr-CA`.

## Bilingual answers (ADR-0080, ADR-0112, ADR-0128, ADR-0129, ADR-0130)

`value`, `value_boolean`, and `locale` are written once, here, and never again — no endpoint
ever updates either column after this one inserts them. How an answer gets
its second language depends on its question:

| Question | Second language |
|---|---|
| Long or short text marked **Auto-translate answer** | The Worker, mechanically, via `ITranslator` |
| Single-select, multi-select | The named choice's other label, read from the choice whenever the answer is read (`choice`); nothing is copied onto the answer |
| Type-ahead | As a picker. A new reporter-added value gets its other label from the Worker, on the value itself, not on the answer |
| Yes/no, checkbox | None, ever: a boolean holds no words; the interface renders it in the reader's language |
| Text not marked, email, phone, date, time, number, file | None, ever |

Each answer records which of these applies (`translation_mode`), so the admin
report view never shows a "translation" of an answer that has none. This
endpoint enqueues one answer-translation outbox message and never calls a
translation provider itself; reading a choice's label is a lookup, not a
translation.

Out of scope: detecting which language a reporter actually typed, and
translating the invariant types above.

## Presentation order (#80)

The reporter-facing form pages the ordered question set one page at a time,
built purely client-side from `GET /api/v1/questions/`'s response — nothing
here is a separate server concept:

1. The leading live `statement` revision, if the form has one, renders as an
   introduction: Next only, no Back, no answer collected.
2. Every other top-level entry is one page — a plain question, or a `group`
   and its children together (see
   [question-bank-and-form](../question-bank-and-form/README.md#the-group-page-contract)).
3. A question or group whose conditional parent's current answer does not
   satisfy the condition is skipped from paging entirely, and re-evaluated
   live as the reporter answers earlier pages.
4. A required, unanswered question on the current page blocks Next with
   inline validation; `consent_publish` is required by default and has no
   default selection.
5. Next becomes Submit on the last page.

A `multi_select` ("Pick several") question renders as a picker dropdown, the
same closed-control shape as a single-select: one trigger labelled by the
question, naming what is chosen, that opens a list of checkable options and
stays open while several are checked. Escape or leaving it closes it.

## Returning to a saved report (#344)

When the report page opens and this browser holds an unexpired saved report,
the form asks before restoring anything. The dialog offers No and Yes, and
below them a read-only table of the saved values — each question's label in
the current locale beside the answer as it was saved. The table and the
decision are built entirely from local storage; nothing is requested from or
sent to the server.

- **Yes** restores the saved answers and reopens the page the reporter was on.
  If that page is no longer on the form, the form opens at its first page.
- **No** asks the API to delete every upload the saved report names, removes
  the saved report from the browser, and opens a fresh form.
- The page is saved by its question key, beside the answers, in the same
  15-day local-storage report. A report saved before that names its page by
  revision ID and still reopens it.
- A saved date, time, or yes/no answer reads in the interface language, as
  the admin report view shows it (#403): `2026-09-13` reads "September 13,
  2026" or "13 septembre 2026". The saved report keeps the stored form.
- The table lists each saved attached file by name under its file-upload
  question, and **Yes** lists those files as attached again, each with its
  Remove control.
- The 15 days run from the moment the report was first saved, however often
  it is edited afterward, because each upload's quarantine copy expires
  fifteen days after it was made and cannot be renewed. A report saved before
  this rule has no start time and is dated from its last save. When the form
  finds a saved report past its window, it asks the API to delete that
  report's uploads before discarding it.

## Discarding a report (#373)

A **Discard report** control sits below the page navigation whenever the
form holds an answer or an attached file. It asks first, in a dialog whose
focus starts on the choice that keeps the report. Confirming deletes every
finished upload, abandons any still in flight, removes the saved report, and
returns to the introduction with no answers.
- A saved answer whose question revision is not on the current form is not
  listed and not restored. If no saved answer is on the current form, there is
  nothing to continue: the saved report is removed and no dialog is shown.

## The page in the address (#366)

Decided in [ADR-0099](../../docs/decisions/ADR-0099-a-report-page-is-addressed-by-its-question-key.md).

The introduction is `/report`; every other page is `/report/<question-key>`,
named by the key of the question heading it. Pressing Next or Back adds a
browser history entry, so the browser's own Back and Forward move between
pages — and obey the form's rules: a page whose earlier required question is
unanswered cannot be reached that way, and the form returns to that question
with its inline message.

The address follows the form; it never leads it on arrival. Opening any
`/report/...` address asks the continue question exactly as `/report` does
when a saved report exists, and the answer decides the page. With no saved
report, the form opens at its introduction. An address naming a page the form
does not have, or a conditional page not currently shown, becomes `/report`.

## Validation order

The API performs, in order:

1. request-size, JSON-shape, trusted-client-IP, rate-limit, and bearer-token
   checks;
2. DTO syntax, locale, duplicate, and count checks;
3. revision lookup including soft-deleted rows;
4. rejection of unknown or deleted revisions and validation against each exact
   historical type and the question's live choices;
5. enforcement of an explicit answer to the `consent_publish` revision;
6. upload-ID shape, duplicate, and count checks;
7. existence of every named upload, refusing with the missing IDs before
   anything is written.

Per-file size, declared content type, and detected type are checked earlier,
when each file is uploaded, and again when the upload is claimed.

## Failure handling

If the persistence transaction fails, the API must not attempt a fragile
distributed rollback across the database and object storage — the storage
lifecycle rule expiring unclaimed quarantine uploads is what cleans those up.
After a successful commit the API removes the claimed uploads from quarantine
on a best-effort basis; a removal that fails is left to the same rule.

## Document handoff

Documents use the allowlist and handling rules in
[attachments](../media/media.feature). They are never extracted into answers
or sent to the summarization model.

## Idempotency

The first target version does not add a durable idempotency subsystem. If
production evidence shows duplicate reports are material, an idempotency key
can be added as a focused change.

## Authentication, and what is not recorded

Submitting requires a signed-in HPAC member. Every role may submit —
membership is what the endpoint requires, not privilege
([ADR-0067](../../docs/decisions/ADR-0067-a-reporter-must-be-a-member-and-is-not-recorded.md)).

**The identity is then discarded.** No report, answer, file, consent
projection, outbox message, audit entry, or log line records who submitted.
There is no column, join table, or hash linking a report to the member who
filed it, so "who filed this?" has no answer to retrieve. The form tells the
reporter so, in their own language, because a guarantee they cannot see does
not change what they are willing to write down.

This also means a reporter cannot retrieve, amend, or withdraw a submission,
and abuse cannot be attributed after the fact. Both are accepted costs.

Turnstile is not used. The member token is the abuse control, alongside per-IP
rate limiting
([ADR-0068](../../docs/decisions/ADR-0068-the-member-token-replaces-turnstile-on-submission.md)).
There is no per-reporter throttle, because a per-reporter throttle would mean
identifying the reporter.

The rate limit itself is a sliding-window `RateLimiter` policy, partitioned by
client IP, using ASP.NET Core's built-in middleware rather than a third-party
package. The client IP comes from `X-Forwarded-For`, trusted unconditionally
because the API's security group admits traffic only from the one AWS ALB in
front of it — the network layer is the actual trust boundary, not a static
proxy allowlist
([ADR-0081](../../docs/decisions/ADR-0081-trust-forwarded-headers-from-the-security-group-boundary.md)).
The client IP is used only in memory for the rate-limiter partition key; it is
never persisted on a report or logged. A rejected request gets `429` with a
safe, content-free problem response.

Administrative operations are authorized by role on the same token; see
[moderation, authentication, and publication](../moderation-authentication-and-publication/moderation-authentication-and-publication.feature).

## Out of scope

What not to build here. The global list in
[system overview](../../docs/system-overview.md) still holds; this narrows it
to this area ([ADR-0083](../../docs/decisions/ADR-0083-specification-driven-development.md)).

- A server-side draft, an autosave, or a reserved report ID. Nothing but an
  attachment reaches the server before the one final request.
- A resumable or chunked upload protocol, a pre-signed upload URL for any key
  but the one upload's quarantine key, or a percentage progress bar. An upload
  is one `PUT` with an indeterminate indicator (ADR-0126).
- Keeping a file's bytes in browser storage, restoring an attachment on another
  browser or device, or a server endpoint that lists, reads, previews, or
  renews an unsubmitted upload. The saved report's upload IDs and names are
  the only record of an unsubmitted file, and they never leave the browser.
- Extending a saved report or its uploads past fifteen days from the moment
  the report was started, or checking whether a restored upload still exists
  before submission. The submission's refusal naming missing uploads is the
  one check.
- Counting uploads against the attachment limit on the server before
  submission. An upload belongs to no report until it is claimed; the form
  enforces the limit as files are attached, and the API enforces it on the IDs
  a submission names.
- Restoring a saved report without asking, keeping more than one saved report,
  or editing saved values inside the continue dialog. The dialog is a yes/no
  question with a read-only table.
- Opening a page straight from its address, a French or otherwise localized
  page slug, a page number in the address, or anything the reporter entered in
  the address. The address holds only an administrator-authored question key.
- Keeping the saved page in `sessionStorage`. It would be gone when the tab
  closes, which is exactly when a reporter comes back to continue.
- Recording who submitted a report — no subject, no user id, no audit line, no
  log line
  ([ADR-0067](../../docs/decisions/ADR-0067-a-reporter-must-be-a-member-and-is-not-recorded.md)).
- Calling a translation provider on the submission path.
- Echoing submitted content back in a validation error.
- A per-reporter throttle. Rate limiting is by trusted IP.
- Previews or thumbnails of attached files, pasting files from the clipboard,
  or a drop target covering the whole page. Only the drop zone accepts a
  drop.
- Searching or filtering inside the multi-select picker, or a third-party
  select widget to provide one.
