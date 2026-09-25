---
title: Glossary
description: "The shared terms this repository's documents use, and what each one means."
type: guide
---

# Glossary

| Term | Meaning |
|---|---|
| Answer | Immutable, for one exact answer-producing question revision: a single-select, multi-select, or type-ahead answer names its choice and reads both labels from it (ADR-0128); every other answer is one string, in the reporter's own words and language (ADR-0072). A multi-select stores one row per chosen choice. An answer with neither records a skip. |
| Attachment | An optional image, video, or document submitted with a report. Stored privately. With media consent, a published report shows its verified image and video derivatives and offers its validated documents as forced downloads (ADR-0117, ADR-0119). |
| Complete revision | One immutable question record containing every value needed to render, validate, order, classify, and localize that revision, except its choices, which belong to the question (ADR-0095). |
| Group | A question type that collects no answer and acts as a section heading; other questions may be grouped under it so the form renders them together. |
| Grouped under | A question revision's reference to a live `Group` question it renders alongside, distinct from a conditional dependency. |
| Statement | A question type that collects no answer and displays instructional text with no input control. |
| Reporter-added choice | A value a reporter typed that a type-ahead question did not offer, added to that question's own choices at submission in the language typed, given its other language by the Worker, and flagged for a Safety Officer or Administrator to review (ADR-0129). |
| Replaced choice | A picker option an Administrator replaced with a new one: retired, still named by every earlier answer, and linked to its replacement (ADR-0128). |
| Merged value | A type-ahead value merged into another: retired, and every answer naming it reads the value it was merged into (ADR-0129). |
| Consent projection | The nullable `consent_publish`, `consent_media`, and `consent_documents` values copied from the two system consent answers because publication logic must query them directly. They are the only answer projections (ADR-0117, ADR-0119). |
| Deleted | Nullable soft-deletion timestamp on every persisted record except `audit_log` and the hard-deleted `pending_import_logic` notes (ADR-0077); a value means hidden and terminal in normal application flows. |
| Derivative | A decoded and re-encoded image, or a video remuxed into MP4 (never transcoded), with unsafe metadata removed (ADR-0094, ADR-0122). Documents do not have anonymized derivatives. |
| Document | Private unredacted evidence such as PDF, Word, RTF, Markdown, text, or ODT. It is format-checked (no malware scan — ADR-0089) and offered as a forced download: to reviewers, and to the public on a published report whose media consent names documents (ADR-0119). It is never model input. |
| Immutable | Never updated in place after creation. A change creates a new complete revision. Soft deletion remains a separately audited lifecycle operation. |
| Managed encryption | Encryption at rest provided by AWS for RDS, backups, S3, logs, and secrets, combined with TLS in transit; no application ciphertext fields. |
| Outbox | Database rows committed atomically with state changes so asynchronous work cannot be lost between saving a report and notifying the Worker. |
| Private context | Labeled private answers sent to the one summary call only to recognize identifying material repeated in eligible content. They may not contribute facts. |
| Public DTO | The strict allowlist returned by public endpoints: report ID, both summary texts, publication timestamp, and visible comment count; a report's own page adds each public file's opaque id, kind, and a document's format (ADR-0114, ADR-0117, ADR-0119). |
| Quarantine | Private object-storage prefix where the API stores an upload it has already validated, the moment it is attached, until a submission claims it or it expires after 15 days (ADR-0096, ADR-0100). |
| Question key | Stable non-localized logical identifier joining the immutable revisions of the same question. |
| Question revision | Exact immutable form record referenced by an answer, including bilingual copy and all behavior/display flags. Choices are not part of a revision; they belong to the question (ADR-0095). |
| Report content | Labeled non-private answered fields eligible to supply safety facts to the model. |
| Reporter | An HPAC member submitting an occurrence. Sign-in is required and proves membership only; nothing stored records who filed the report. |
| Member role | The role claim on a validated token: `User`, `SafetyOfficer`, or `Administrator`. Never stored — this system holds no user records. |
| User | The lowest role. Proves HPAC membership and may submit a report; has no review, authoring, or publication capability. |
| Safety officer | Authorized reviewer who can see private reports, edit/approve the summary pair, publish when consent permits, and soft-delete reports. |
| Token subject | The `sub` claim of a validated token, stored as an opaque string on an audit entry or a summary approval. Joins to nothing; there is no user table. |
| Summary pair | One row and one review unit containing English and French anonymized summaries with shared model/prompt provenance and approval. |
| TinyId | Opaque compact application identifier used externally instead of sequential database IDs. |
| Worker | Long-running .NET service that consumes outbox work, processes attachments, and performs the one-call bilingual summarization operation. |
