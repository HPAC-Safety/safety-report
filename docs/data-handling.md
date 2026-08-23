# Data handling

Incident reports can identify people even when the public summary cannot.

## Stored data

- PostgreSQL stores reports, exact question revision ids, nullable answers,
  candidate summaries, review state, and audit/outbox rows.
- Text answers are encrypted by the application before PostgreSQL stores them.
- Selected option codes remain invariant and queryable.
- Optional uploads live in one private Canadian S3 bucket. They are not public
  summary input and have no public URL.
- Question revisions are never edited or deleted; a new revision preserves what
  each reporter saw.

## Access and output

The public form can submit but cannot read raw reports. Authenticated,
allowlisted reviewers may load raw answers and the candidate needed for review.
Public endpoints return only a positively consented, human-approved summary.

Logs contain ids, states, timings, retry counts, and content-free error codes.
They never contain request bodies, answer values, private question labels paired
with values, upload contents, model payloads, or candidate text.

## Model boundary

The Worker decrypts answers only while building `ReportForSummaryDto`, then
partitions them according to the exact immutable question privacy flags. See
[anonymization-policy.md](anonymization-policy.md). The model provider receives
the minimum one-call payload and no upload bytes.

## Operations

Report data, backups, and uploads remain in AWS `ca-central-1`. Runtime secrets
live in Secrets Manager. Losing the field-encryption key loses access to stored
text, so key backup and rotation require an explicit operational procedure.
Never use production report content in issues, pull requests, tests, or support
messages.
