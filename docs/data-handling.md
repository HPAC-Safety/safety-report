---
title: Data handling
description: What personal information a report can contain and how each boundary treats it.
type: guide
---

# Data handling

Occurrence reports can contain identities, contact details, injuries, and
fatalities. The canonical storage, deletion, AI, and attachment rules are in
[`data-and-persistence.md`](data-and-persistence.md),
[`features/ai-anonymization/ai-anonymization.feature`](../features/ai-anonymization/ai-anonymization.feature), and
[`features/media/media.feature`](../features/media/media.feature).

## Storage and retention

- Before final submission, unfinished answers/revision IDs exist only in that
  browser. No report, draft, reserved ID, or database row exists on the server.
  An attached file is the one exception: it sits in private quarantine under
  an opaque upload ID, names no member, is erased when the reporter removes
  it or abandons the report, and expires by lifecycle rule fifteen days after
  upload unless a submission claims it. The browser's saved report keeps its
  upload ID and name for the same window
  ([ADR-0096](decisions/ADR-0096-an-attachment-uploads-on-attach-and-is-claimed-at-submission.md),
  [ADR-0100](decisions/ADR-0100-an-attachment-is-kept-as-long-as-the-saved-report.md)).
- Use AWS-managed encryption at rest and TLS. Do not maintain application AES
  keys or ciphertext converters.
- Keep raw reports private until an authorized officer soft-deletes them.
- **Filing a report requires a signed-in HPAC member, and nothing about that
  member is persisted.** No report, answer, file, consent projection, outbox
  message, audit entry, or log line records the submitter's subject, and no
  column, join table, or hash links a report to whoever filed it. Sign-in
  proves membership; its answer is discarded
  ([ADR-0067](decisions/ADR-0067-a-reporter-must-be-a-member-and-is-not-recorded.md)).
- Store no user records at all. Identity and role come from claims on a
  validated token, per request. Where an approver or an audit actor is
  recorded, it is an opaque token subject that joins to nothing
  ([ADR-0065](decisions/ADR-0065-no-user-records-identity-is-the-token-subject.md)).
- Every application record except append-only `audit_log` has an irreversible
  deletion timestamp. Report deletion cascade-stamps dependents in one
  transaction; there is no restore or physical-delete workflow.
- Public queries use exact allowlist DTOs and never join raw answers or files.
  The one file-shaped public read is `public_report_media`: the opaque id and
  kind of a published report's verified image and video derivatives, when its
  reporter consented to sharing media
  ([ADR-0117](decisions/ADR-0117-a-published-report-shows-the-reporters-photos-and-video.md)).

## Model boundary

The Worker sends answered non-private fields as eligible `report_content` and
answered private fields as recognition-only `private_context` to one summary
call. Consent, attachments, document text, and deleted data never cross that
boundary. Model prompts/responses and report values are never logged.

Only a report whose reporter consented to publication makes that call; a report without consent is never sent to the model. That call goes to Google Gemini with a paid, billing-enabled key, so Google
does not use the content to train its models, and it is processed outside
Canada. That is the one place report content leaves `ca-central-1`
([ADR-0104](decisions/ADR-0104-summaries-are-generated-by-gemini-through-a-paid-key.md)).

## Attachments

The API validates each attachment as it is uploaded — bounded at 50 MB, sniffed,
checked against the allowlist — and writes only accepted bytes to private
quarantine under a server-generated upload ID. The final submission may claim
a configurable count (default 5). The upload carries no filename; the final
submission names each file, and that name is kept, sanitized, only as a
reviewer's download name ([ADR-0097](decisions/ADR-0097-a-reviewer-downloads-an-attachment-under-its-sanitized-original-name.md)). There is no malware scan (ADR-0089).

Safe image/video derivatives may be previewed by authorized reviewers through
short-lived access. On a published report whose reporter also consented to
sharing media, those same derivatives — never an original — are shown to any
visitor through a pre-signed URL that lives at most fifteen minutes, minted per
file by an anonymous endpoint that refuses a hidden or unpublished file. The
bucket stays private, and nothing is copied to the CDN
([ADR-0117](decisions/ADR-0117-a-published-report-shows-the-reporters-photos-and-video.md)).
Validated documents remain unmodified private originals and
are forced downloads only; they are never anonymized, parsed for AI, rendered
inline, or published. Unreferenced quarantine bytes expire by storage lifecycle;
report-linked bytes remain private after soft deletion.

## Logging

Log opaque IDs, state, safe error codes, timing, and aggregate metrics only.
Never log request/DTO bodies, answers, question copy containing answers,
private context, prompts/responses, credentials/tokens, IP addresses beyond
ephemeral security processing, client filenames, object keys, or access URLs.
