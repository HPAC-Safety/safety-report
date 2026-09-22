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
  browser. No report, attachment, draft, reserved ID, or database row exists on
  the server.
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

## Model boundary

The Worker sends answered non-private fields as eligible `report_content` and
answered private fields as recognition-only `private_context` to one summary
call. Consent, attachments, document text, and deleted data never cross that
boundary. Model prompts/responses and report values are never logged.

## Attachments

The API streams final multipart attachments to private quarantine with a
configurable count (default 5) and 50 MB per-file limit. It sniffs format and
uses server-generated names. There is no malware scan (ADR-0089).

Safe image/video derivatives may be previewed by authorized reviewers through
short-lived access. Validated documents remain unmodified private originals and
are forced downloads only; they are never anonymized, parsed for AI, rendered
inline, or published. Unreferenced quarantine bytes expire by storage lifecycle;
report-linked bytes remain private after soft deletion.

## Logging

Log opaque IDs, state, safe error codes, timing, and aggregate metrics only.
Never log request/DTO bodies, answers, question copy containing answers,
private context, prompts/responses, credentials/tokens, IP addresses beyond
ephemeral security processing, client filenames, object keys, or access URLs.
