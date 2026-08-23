# Glossary

| Term | Meaning |
|---|---|
| Answer | The report's nullable scalar or option selection for one exact answer-producing question revision. A stored null/empty selection records a skip. |
| Attachment | An optional image, video, or document submitted with a report. Attachments are always private. |
| Complete revision | One immutable question record/aggregate containing every value needed to render, validate, order, classify, and localize that revision. |
| Consent projection | The nullable `ConsentPublish` value copied from the system consent answer because publication logic must query it directly. It is the only answer projection. |
| Deleted | Nullable soft-deletion timestamp on every persisted record except `audit_log`; a value means hidden and terminal in normal application flows. |
| Derivative | A decoded/re-encoded image or remuxed/transcoded video with unsafe metadata removed. Documents do not have anonymized derivatives. |
| Document | Private unredacted evidence such as PDF, Word, RTF, Markdown, text, or ODT. It is format/malware checked and offered only as an authorized forced download; it is not model input or public content. |
| Immutable | Never updated in place after creation. A change creates a new complete revision. Soft deletion remains a separately audited lifecycle operation. |
| Managed encryption | Encryption at rest provided by AWS for RDS, backups, S3, logs, and secrets, combined with TLS in transit; no application ciphertext fields. |
| Outbox | Database rows committed atomically with state changes so asynchronous work cannot be lost between saving a report and notifying the Worker. |
| Private context | Labeled private answers sent to the one summary call only to recognize identifying material repeated in eligible content. They may not contribute facts. |
| Public DTO | The strict allowlist of report ID, both summary texts, and publication timestamp returned by public endpoints. |
| Quarantine | Private object-storage compartment where the API first streams an accepted attachment before its database transaction/Worker validation completes. |
| Question key | Stable non-localized logical identifier joining the immutable revisions of the same question. |
| Question revision | Exact immutable form record referenced by an answer, including bilingual copy/options and all behavior/display flags. |
| Report content | Labeled non-private answered fields eligible to supply safety facts to the model. |
| Reporter | Any person submitting an occurrence; authentication is not required for public submission. |
| Safety officer | Authorized reviewer who can see private reports, edit/approve the summary pair, publish when consent permits, and soft-delete reports. |
| Summary pair | One row and one review unit containing English and French anonymized summaries with shared model/prompt provenance and approval. |
| TinyId | Opaque compact application identifier used externally instead of sequential database IDs. |
| Worker | Long-running .NET service that consumes outbox work, processes attachments, and performs the one-call bilingual summarization operation. |
