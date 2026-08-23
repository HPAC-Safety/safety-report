# Report submission

## Browser continuity

Before submission, the report exists only in the current browser. The public
site stores the selected locale, shown question-revision IDs, and entered
answers in local browser storage with a 15-day expiry. It clears that state
after a successful submission and ignores or removes expired state.

Image, video, and document attachments are never placed in browser storage and
are not restored after a reload. The UI explains this and places attachment
selection last. There is no
server draft, report ID reservation, upload token, or resumable upload protocol.

## Endpoint

The only write endpoint for a reporter is:

`POST /api/v1/reports`

It accepts `multipart/form-data` with:

- one `report` part containing the JSON submission DTO;
- zero or more repeated `files` parts;
- one Turnstile response token as transport/security metadata, not persisted
  report content.

The JSON DTO is finalized, not a draft:

```json
{
  "language": "en-CA",
  "answers": [
    {
      "question_revision_id": "text-revision-id",
      "value": "A short answer",
      "option_codes": null,
      "attachment_part_indexes": null
    },
    {
      "question_revision_id": "select-revision-id",
      "value": null,
      "option_codes": [],
      "attachment_part_indexes": null
    },
    {
      "question_revision_id": "file-revision-id",
      "value": null,
      "option_codes": null,
      "attachment_part_indexes": [0]
    }
  ]
}
```

There is exactly one answer entry for every answer-producing revision the
client says it was shown. Textual/scalar answers use `value`; selection answers
use `option_codes`; file-upload answers use zero-based indexes into the repeated
`files` parts. Fields for the other shapes are null. A skipped scalar has a null
value, a skipped selection has an empty list, and a skipped file upload has an
empty index list. The exact revision type resolves a null skip without guessing.
The API rejects duplicate revision IDs, a non-null field from the wrong shape,
duplicate/out-of-range file indexes, an unreferenced file part, one file part
referenced more than once, and entries for statement/group revisions.

Dates use ISO `YYYY-MM-DD`; times, if a question requests one, use local wall
clock `HH:mm` without inventing an offset; numbers use invariant JSON numbers.
The report language is exactly `en-CA` or `fr-CA`.

## Validation

The API performs, in order:

1. request-size, multipart-shape, trusted-client-IP, rate-limit, and Turnstile
   checks;
2. DTO syntax, locale, duplicate, and count checks;
3. revision lookup including soft-deleted rows;
4. rejection of unknown or deleted revisions and validation against each exact
   historical type and option set;
5. enforcement of an explicit answer to the `consent_publish` revision;
6. attachment mapping/count, per-file size, declared content type, and
   detected-type checks.

The API accepts known superseded revisions. It does not require the submitted
set to equal the latest form, because a valid browser session may span an admin
edit. It may reject a revision that was never returned as answer-producing or
an internally inconsistent combination of revisions for the same stable key.

Reporter-visible errors are localized and safe. They never echo an answer,
client filename, Turnstile token, credential, or storage key. Routine invalid
requests are not logged with body content.

## File and database handoff

For each accepted attachment, the API mints an opaque server-side filename/key and
streams at most 50 MB into the quarantine compartment while computing the
actual byte count and inspecting its signature. It never buffers a whole file
in memory and never persists or logs the client filename.

Documents use the allowlist and handling rules in [attachments](media.md). They
are never extracted into answers or sent to the summarization model.

After all parts validate, one database transaction creates:

- the report and consent projection;
- one answer per shown answer-producing revision, including skips;
- report-file metadata linked to its file-upload answer for successfully
  quarantined blobs;
- one summarization outbox item; and
- one independent attachment-processing outbox item per file.

If the transaction fails, no report is visible and any already-written
quarantine blobs are unreferenced. Storage lifecycle rules expire those
orphans. The API must not attempt a fragile distributed rollback.

## Response and idempotency

A successful request returns `202 Accepted` with an opaque report ID and the
status `submitted`. Processing is asynchronous. The response contains no raw
answers or attachment URLs.

The first target version does not add a durable idempotency subsystem. The UI
disables repeat submission while a request is in flight and retains its local
state on an uncertain network result. If production evidence shows duplicate
reports are material, an idempotency key can be added as a focused change.

## Abuse controls

Submission requires Cloudflare Turnstile verification and a rate limiter keyed
from the client IP obtained only through explicitly trusted proxy headers. The
system fails closed when Turnstile is required but unavailable or misconfigured.
Limits are configurable, return `429` with a safe retry signal, and do not store
IP addresses on the report. Administrative authentication has separate,
stricter throttling and lockout rules.
