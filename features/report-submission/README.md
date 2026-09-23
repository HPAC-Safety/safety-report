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

Each file uploads the moment the reporter attaches it
([ADR-0096](../../docs/decisions/ADR-0096-an-attachment-uploads-on-attach-and-is-claimed-at-submission.md)):

- `POST /api/v1/uploads` takes the file itself as the request body, with its
  declared type in `Content-Type`. The client filename is never sent. The API
  reads at most one byte past 50 MB into a temporary file, sniffs and validates
  it, and only then writes it to `quarantine/<upload id>`.
- `201` returns `{{ "uploadId": "…", "kind": "image" | "video" | "document" }}`.
  The upload ID is 22 URL-safe characters (128 random bits).
- `400` returns a problem with a `reason` of `empty`, `too_large`,
  `unrecognised_content`, `unaccepted_media_type`, or `declared_type_mismatch`,
  which the form maps to a localized message on that file's row.
- `DELETE /api/v1/uploads/{{id}}` erases every version of that quarantine object
  and returns `204`, whether or not it existed.
- The browser keeps each upload ID in memory only, never in the saved draft.

At submission a file-upload answer names its uploads in `attachments`, each an
`uploadId` and the `fileName` the reporter's browser knew it by. The API
sanitizes that name and keeps it on the report file only as a reviewer's
download name
([ADR-0097](../../docs/decisions/ADR-0097-a-reviewer-downloads-an-attachment-under-its-sanitized-original-name.md)).
If any named upload no longer exists, the API refuses the whole submission
before writing anything, with a `400` whose `expiredUploadIds` lists exactly
those IDs.

## Submission DTO shape

```json
{
  "language": "en-CA",
  "answers": [
    {
      "questionRevisionId": "text-revision-id",
      "value": "A short answer",
      "optionCodes": null,
      "attachments": null
    },
    {
      "questionRevisionId": "select-revision-id",
      "value": null,
      "optionCodes": [],
      "attachments": null
    },
    {
      "questionRevisionId": "file-revision-id",
      "value": null,
      "optionCodes": null,
      "attachments": [
        { "uploadId": "kP3x9QmR2vT8wLb6nYc4Dg", "fileName": "launch-site.jpg" }
      ]
    }
  ]
}
```

Field names are camelCase on the wire (ASP.NET's default JSON casing), not
the snake_case the Gherkin prose uses when it names them — the scenarios are
talking about the concept, not literal JSON.

Dates use ISO `YYYY-MM-DD`; times, if a question requests one, use local wall
clock `HH:mm` without inventing an offset; numbers use invariant JSON numbers.
The report language is exactly `en-CA` or `fr-CA`.

## Bilingual answers (ADR-0080)

`value` and `locale` are written once, here, and never again — no endpoint
ever updates either column after this one inserts them. `value_translated`
and `translation_source` stay null on insert; this endpoint enqueues one
answer-translation outbox message so the Worker can fill them later,
mechanically, via the same `ITranslator` port ADR-0062 built for admin-drafted
translation. This endpoint never calls a translation provider itself.

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
- **No** removes the saved report from the browser and opens a fresh form.
- A saved answer whose question revision is not on the current form is not
  listed and not restored. If no saved answer is on the current form, there is
  nothing to continue: the saved report is removed and no dialog is shown.

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
- A resumable or chunked upload protocol, a pre-signed upload URL handed to the
  browser, or a percentage progress bar. An upload is one request with an
  indeterminate indicator.
- Restoring attachments after a reload, from browser storage or by keeping
  upload IDs. Answers and shown revision IDs persist locally; files and upload
  IDs never do.
- Counting uploads against the attachment limit on the server before
  submission. An upload belongs to no report until it is claimed; the form
  enforces the limit as files are attached, and the API enforces it on the IDs
  a submission names.
- Restoring a saved report without asking, keeping more than one saved report,
  or editing saved values inside the continue dialog. The dialog is a yes/no
  question with a read-only table.
- Recording who submitted a report — no subject, no user id, no audit line, no
  log line
  ([ADR-0067](../../docs/decisions/ADR-0067-a-reporter-must-be-a-member-and-is-not-recorded.md)).
- Calling a translation provider on the submission path.
- Echoing submitted content back in a validation error.
- A per-reporter throttle. Rate limiting is by trusted IP.
- Searching or filtering inside the multi-select picker, or a third-party
  select widget to provide one.
