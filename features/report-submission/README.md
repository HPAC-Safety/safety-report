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

## Bilingual answers (ADR-0080)

`value` and `locale` are written once, here, and never again — no endpoint
ever updates either column after this one inserts them. `value_translated`
and `translation_source` stay null on insert; this endpoint enqueues one
answer-translation outbox message so the Worker can fill them later,
mechanically, via the same `ITranslator` port ADR-0062 built for admin-drafted
translation. This endpoint never calls a translation provider itself.

## Validation order

The API performs, in order:

1. request-size, multipart-shape, trusted-client-IP, rate-limit, and bearer-token
   checks;
2. DTO syntax, locale, duplicate, and count checks;
3. revision lookup including soft-deleted rows;
4. rejection of unknown or deleted revisions and validation against each exact
   historical type and option set;
5. enforcement of an explicit answer to the `consent_publish` revision;
6. attachment mapping/count, per-file size, declared content type, and
   detected-type checks.

## Failure handling

If the persistence transaction fails, the API must not attempt a fragile
distributed rollback across the database and object storage — the storage
lifecycle rule expiring unreferenced quarantine blobs is what cleans those up.

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

Administrative operations are authorized by role on the same token; see
[moderation, authentication, and publication](../moderation-authentication-and-publication/moderation-authentication-and-publication.feature).
