# Implementation status

This page compares the target specification with audited main at
`5f7340415e88706035a713bd8322e3dda466e821`. “Implemented” means source and tests
substantially enforce the target behavior, not merely that an issue was closed.

## Capability matrix

| Capability | Main on the audit baseline | Target disposition |
|---|---|---|
| API host | Minimal ASP.NET Core host with `/health`; unmapped routes return 404. | Keep host; build the specified public/admin API. |
| Worker host | Long-running service that logs startup; no outbox loop or handlers. | Implement outbox claims, one summary handler, and per-attachment handlers. |
| Question bank | Database-backed bilingual questions. Stable `Question` owns mutable order/active/privacy/role; wording/type/options are versioned children. | Replace with complete immutable revisions including order, privacy, active, flags, copy, and options. |
| Form query and UI | Seed/form specification exists; public web directory is empty. | Implement latest-revision-per-key filtering and bilingual static form without resurrecting inactive/deleted questions. |
| Required behavior | Core protects explicit consent; several ordinary answers also project to typed report fields. | Retain only consent projection; all ordinary questions optional revision-bound answers. |
| Submission | Domain aggregate and transactional persistence primitives exist; no report endpoint. Current media design reserves report/upload keys before submit. | Implement one finalized multipart endpoint, accept known superseded revisions, stream attachments, and atomically enqueue work. |
| Browser continuity | Not implemented. | Same-browser answer persistence for 15 days; never restore files. |
| Abuse prevention | Turnstile port and Terraform resources exist; no endpoint enforcement/rate limit. | Verify Turnstile and trusted-IP rate limits on public submit; separate admin lockout. |
| Summarization DTO | Partitioned `report_content`/`private_context` Core model and tests exist. | Keep concept; query exact revision labels/answers and exclude consent and all attachments. |
| AI orchestration | Ports and prompts describe a one-language summarizer plus translator/PII auditor and, historically, deterministic scrub. No Worker execution. | One Worker prompt, one call, strict bilingual JSON, bounded retry, one pair row. Remove obsolete ports/pipeline. |
| Aircraft handling | Typed aircraft aggregate/classification guidance exists; current decisions moved classification toward the prompt. | Remove subsystem/typed projection; allow safe non-guessing normalization in the one prompt. |
| Summary persistence | One `Summary` row per locale with source/translation links and per-row approval. | Migrate to one row containing EN/FR texts, shared provenance, and one approval. |
| Media images | Signature sniffing, 50 MB policy, private storage, and decode/re-encode metadata stripping are implemented and tested. | Reuse validated pieces behind final multipart ingest and configurable total attachment count. |
| Media videos | MP4/QuickTime are detected and retained but deliberately have no reviewer derivative. | Add metadata-safe remux/transcode derivative; fail closed. |
| Documents | No PDF, DOC, DOCX, RTF, Markdown, text, or ODT support. | Validate/scan and retain private originals; authorized forced download only; never extract, anonymize, send to LLM, or publish. |
| Blob access | Filesystem/S3 stores implement pre-signed upload/read URLs; reviewer link is derivative-only. | Remove pre-submit upload slots; keep private streaming and short-lived reads for verified derivatives/private documents. |
| Authentication | `IMemberAuthenticator`, roles, admin allowlist entity, and audit entity exist; no API adapter/session flow. | Implement hardcoded-TLS HPAC adapter with kill switch, secure cookie/CSRF/lockout; preserve adapter seam. |
| Review/admin web | Domain review methods exist; admin web directory is empty. | Implement queue/detail, pair editing/approval, safe attachment access, questions, allowlist, deletion. |
| Publication | Domain currently checks consent, report state, and separately approved locale rows; no public endpoints/UI. Publication-channel abstraction exists. | Implement minimal feed/detail allowlist over one approved pair; remove external-channel abstraction. |
| Soft deletion | Not modeled across entities. Admin has only `IsActive`; migrations use physical relational rows. | Add `Deleted` everywhere except audit log, filters, transactional cascade stamping, no restore/physical delete. |
| Retention | Storage lifecycle handles some quarantine states; no complete report-retention/deletion flow. | Retain until explicit soft deletion; expire only unreferenced quarantine; keep report-linked bytes private. |
| Encryption | AES-GCM field cipher/converters encrypt selected answer/time columns; managed AWS encryption also exists. | Remove application field encryption and keys/converters; require managed encryption plus TLS. |
| Localization | Locale catalogues, parity/lint/CI translation tooling, and locale value type exist. No real pages. | Keep catalogue tooling for app chrome; manually store both question languages; one AI call supplies both summaries. |
| Design system | Tailwind v4 tokens, dark token redefinition, self-hosted Aleo/Poppins, preview, and placeholder logo exist. | Keep and apply to accessible public/admin sites; replace logo only with approved asset. |
| Infrastructure | Broad AWS Terraform includes ECS, RDS, S3/CloudFront, one combined site distribution, SES, secrets, alarms, and OIDC workflows. | Prune SES/speculative pieces, split public/admin hosting, retain Canadian minimal services, backups, explicit migrations, OIDC, focused Worker alerts. |
| Tests/CI | Strong Core/persistence/media primitives and repository gates; API/Worker/UI feature coverage is mostly scaffold-level. | Rewrite superseded contracts and add target API, Worker, browser, deletion, document, and public-boundary coverage. |

## Current database shape

The baseline has 13 tables: `reports`, `report_answers`, `report_aircraft`,
`report_files`, `summaries`, `questions`, `question_versions`,
`question_options`, `question_translations`, `question_option_translations`,
`admin_users`, `audit_log`, and `outbox_messages`. Two migrations create that
shape and replace the earlier sensitivity scheme with question privacy. The
target shape and required migration are specified in
[data and persistence](data-and-persistence.md).

## Test evidence

The audited tests strongly protect TinyId/blob-key formats, consent, the current
question-version model, typed projections, privacy partitioning, outbox retry,
PostgreSQL 17 schema/atomicity/seeding, AES field encryption, pre-signed
filesystem/S3 storage, image metadata stripping, and the current video fail-
closed behavior. API tests cover health/404/no-blob-route; Worker tests cover
only lifecycle/start logging. JavaScript tests cover coverage and translation
tooling. Those observations explain which target gaps are real even when issue
history says a feature was completed.

## Guidance and skill audit

The repository skill system is useful, but several skills encode superseded
implementation detail. Alignment work should produce a small task-oriented set:

| Skill/guidance | Disposition |
|---|---|
| HPAC conventions and delivery/testing workflow | Keep; update canonical spec links and target test commands. |
| Incident domain model | Rewrite for complete question revisions, pair summaries, consent-only projection, and universal soft deletion. |
| Anonymize HPAC reports | Collapse to one concise skill explaining purpose, content/private partition, role replacement, one bilingual response, and human approval. Remove scrub/auditor/translator mechanics. |
| Localization | Keep UI-catalogue practices; remove one-language/translation-pipeline assumptions and clarify manually bilingual questions. |
| Persistence | Rewrite to remove application AES and current normalized question/locale-summary schema. |
| Media handling | Rewrite for final multipart streaming, video derivatives, and private non-anonymized documents. |
| Web UI | Keep static/Tailwind/accessibility rules; point product behavior to this specification. |
| Infrastructure | Rewrite for separate sites, no SES, managed encryption, and focused operations. |
| Aircraft classification | Remove as a standalone skill; its small safe-normalization rule belongs in the anonymization prompt/skill. |
| Generic Gang of Four / SOLID guidance | Remove from the project-specific installed set unless a concrete task needs it; it encourages abstractions the product does not require. |
| Requirements clarification | Keep only if concise and repository-specific; this specification resolves the current product decisions. |

Generated copies under agent-specific directories must be regenerated from the
single authored skill source, never hand-edited independently. Skill pruning is
an implementation change and should delete obsolete generated copies and
references together.

## Recommended implementation order

1. Align the domain and migration: complete question revisions, consent-only
   report, bilingual summary row, attachment kinds, and universal soft delete.
2. Replace obsolete ports/prompts/skills/tests with the small target boundaries.
3. Implement current-form and finalized multipart submission with Turnstile,
   streaming quarantine, transaction, and outbox.
4. Implement Worker summary and attachment handlers, including documents and
   safe video derivatives.
5. Implement member authentication, review UI/API, pair approval, deletion, and
   the exact public DTO.
6. Complete both static sites and end-to-end bilingual/privacy tests.
7. Prune and split infrastructure, deploy through explicit migration, and
   verify focused operational alerts.
