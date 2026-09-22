---
title: Glossary
description: "The shared terms this repository's documents use, and what each one means."
type: guide
---

# Glossary

| Term | Meaning |
|---|---|
| Answer | The report's nullable scalar or option selection for one exact answer-producing question revision. A stored null/empty selection records a skip. |
| Attachment | An optional image, video, or document submitted with a report. Attachments are always private. |
| Complete revision | One immutable question record/aggregate containing every value needed to render, validate, order, classify, and localize that revision. |
| Group | A question type that collects no answer and acts as a section heading; other questions may be grouped under it so the form renders them together. |
| Grouped under | A question revision's reference to a live `Group` question it renders alongside, distinct from a conditional dependency. |
| Statement | A question type that collects no answer and displays instructional text with no input control. |
| Reporter-added choice | An option a reporter typed that a shared choice list did not offer, added at submission and flagged for an Administrator to curate. |
| Consent projection | The nullable `ConsentPublish` value copied from the system consent answer because publication logic must query it directly. It is the only answer projection. |
| Deleted | Nullable soft-deletion timestamp on every persisted record except `audit_log`; a value means hidden and terminal in normal application flows. |
| Derivative | A decoded/re-encoded image or remuxed/transcoded video with unsafe metadata removed. Documents do not have anonymized derivatives. |
| Document | Private unredacted evidence such as PDF, Word, RTF, Markdown, text, or ODT. It is format-checked (no malware scan — ADR-0089) and offered only as an authorized forced download; it is not model input or public content. |
| Immutable | Never updated in place after creation. A change creates a new complete revision. Soft deletion remains a separately audited lifecycle operation. |
| Managed encryption | Encryption at rest provided by AWS for RDS, backups, S3, logs, and secrets, combined with TLS in transit; no application ciphertext fields. |
| Outbox | Database rows committed atomically with state changes so asynchronous work cannot be lost between saving a report and notifying the Worker. |
| Private context | Labeled private answers sent to the one summary call only to recognize identifying material repeated in eligible content. They may not contribute facts. |
| Public DTO | The strict allowlist of report ID, both summary texts, and publication timestamp returned by public endpoints. |
| Quarantine | Private object-storage compartment where the API first streams an accepted attachment before its database transaction/Worker validation completes. |
| Question key | Stable non-localized logical identifier joining the immutable revisions of the same question. |
| Question revision | Exact immutable form record referenced by an answer, including bilingual copy/options and all behavior/display flags. |
| Report content | Labeled non-private answered fields eligible to supply safety facts to the model. |
| Reporter | An HPAC member submitting an occurrence. Sign-in is required and proves membership only; nothing stored records who filed the report. |
| Member role | The role claim on a validated token: `User`, `SafetyOfficer`, or `Administrator`. Never stored — this system holds no user records. |
| User | The lowest role. Proves HPAC membership and may submit a report; has no review, authoring, or publication capability. |
| Safety officer | Authorized reviewer who can see private reports, edit/approve the summary pair, publish when consent permits, and soft-delete reports. |
| Token subject | The `sub` claim of a validated token, stored as an opaque string on an audit entry or a summary approval. Joins to nothing; there is no user table. |
| Summary pair | One row and one review unit containing English and French anonymized summaries with shared model/prompt provenance and approval. |
| TinyId | Opaque compact application identifier used externally instead of sequential database IDs. |
| Worker | Long-running .NET service that consumes outbox work, processes attachments, and performs the one-call bilingual summarization operation. |
