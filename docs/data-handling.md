# Data handling

Occurrence reports can contain identities, contact details, injuries, and
fatalities. The canonical storage, deletion, AI, and attachment rules are in
[`spec/data-and-persistence.md`](../spec/data-and-persistence.md),
[`spec/ai-anonymization.md`](../spec/ai-anonymization.md), and
[`spec/media.md`](../spec/media.md).

## Storage and retention

- Before final submission, unfinished answers/revision IDs exist only in that
  browser. No report, attachment, draft, reserved ID, or database row exists on
  the server.
- Use AWS-managed encryption at rest and TLS. Do not maintain application AES
  keys or ciphertext converters.
- Keep raw reports private until an authorized officer soft-deletes them.
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
configurable count (default 5) and 50 MB per-file limit. It sniffs format,
malware-checks files, and uses server-generated names.

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
