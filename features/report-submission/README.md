# Report submission

Supporting detail for [`report-submission.feature`](report-submission.feature)
that doesn't fit Gherkin.

## Submission DTO shape

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

Dates use ISO `YYYY-MM-DD`; times, if a question requests one, use local wall
clock `HH:mm` without inventing an offset; numbers use invariant JSON numbers.
The report language is exactly `en-CA` or `fr-CA`.

## Validation order

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

## Document handoff

Documents use the allowlist and handling rules in
[attachments](../media/media.feature). They are never extracted into answers
or sent to the summarization model.

## Idempotency

The first target version does not add a durable idempotency subsystem. If
production evidence shows duplicate reports are material, an idempotency key
can be added as a focused change.

## Administrative authentication

Administrative authentication has separate, stricter throttling and lockout
rules than reporter submission; see
[moderation, authentication, and publication](../moderation-authentication-and-publication/moderation-authentication-and-publication.feature).
