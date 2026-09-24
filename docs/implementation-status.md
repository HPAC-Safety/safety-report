---
title: Implementation status
description: How far main is from the target specification, capability by capability.
type: guide
---

# Implementation status

This page compares the target specification with audited main at
`866f035e9e8d316ffaba7540113b57ead309c6cd`. “Implemented” means source and tests
substantially enforce the target behavior, not merely that an issue was closed.

## Capability matrix

| Capability | Main on the audit baseline | Target disposition |
|---|---|---|
| API host | ASP.NET Core host with `/health`, `/api/auth/*`, and the admin question-authoring endpoints under `/api/admin/` (#180). The session-header stub is gone: a validated bearer token and a role policy now gate every admin endpoint (#191). | Keep host; build the remaining public/admin API. |
| Worker host | Claims typed outbox messages safely (`OutboxClaimer`, `FOR UPDATE SKIP LOCKED`, #271) via one `IOutboxMessageProcessor` per type: `TranslateAnswersProcessor` translates each answer whose mode is `machine` — free text marked as needing translation, or a type-ahead value naming no bilingual choice — into its second official language via the same `ITranslator` port question authoring uses (ADR-0080, ADR-0112); a select answer takes its choice's other label at submission, and every other answer has none, `SummarizeReportProcessor` runs the summarization pipeline (#17, see below), and `ProcessAttachmentProcessor` writes each attachment's derivative from its own message (#361, ADR-0098). docker-compose runs the Worker in development. Its image, development and deployed, is a Dockerfile over the publish output that installs Ubuntu's ffmpeg (#423, ADR-0118). | — |
| Question bank | Implemented, and now authorable. `Question` is a stable key/role/system-flag shell; every order/privacy/active/required/type/copy/option/dependency/grouping fact lives on an immutable `QuestionRevision`, selected by highest revision number. Revising creates a new revision rather than mutating fields. A revision may be conditional on a yes/no question or on a single-select question's required option (ADR-0060, ADR-0074), and `Statement`/`Group` question types plus `GroupedUnderQuestionId` are implemented (ADR-0076) — a `Group` heading's children are recorded but a public form does not yet render them together. A question owns one editable list of choices, `question_choices`, outside its revisions (ADR-0095); a reporter's new type-ahead value is added to it at submission. `QuestionBankSeed` is currently empty (#222 cleared it). | Populate the seed via Typeform import (ADR-0077). |
| Form query and UI | Implemented (#80). `GET /api/v1/questions/` (#270) returns the latest-live-revision-per-key DTO with a group's children nested; the reporter-facing form (`ReportForm.tsx`) renders the leading live `Statement` as an introduction (Next only, no Back, no answer), pages one question — or one `Group` and its children together — at a time, skips a conditional question/group while its parent condition is unmet and reveals it live once met, and fades between pages respecting `prefers-reduced-motion`. `docs/form-spec.md` is still hand-maintained evidence, not seed data, and `QuestionBankSeed` is still empty. | Populate the seed via Typeform import (ADR-0077). |
| Required behavior | Implemented, server-side and client-side. `Report.Project` only reads `QuestionRole.ConsentPublish`; every other answer is stored as data with no typed projection. `is_required` is authored per revision and consent is forced required (ADR-0061); `ReportAnswer.For` rejects a blank value against a required revision (#14). The reporter-facing form (#80) blocks its Next control with inline, localized validation until every visible required question on the current page is answered. | None. |
| Submission | Implemented end to end: `POST /api/v1/reports` (#14) validates in order, reloads known superseded revisions, claims the named uploads by a server-side copy into the report's original compartment (refusing expired ones by id before writing anything, ADR-0098), enforces required/select/file-shape rules, and atomically persists the report, every answer (immutable `value`/`locale`, ADR-0080), file rows with the reporter's sanitized filename (ADR-0097), and three outbox messages (summary, per-file attachment, answer-translation — the Worker claims and processes the last of these, #271). Attachments upload as they are attached through `POST /api/v1/uploads` into `quarantine/<upload id>`, and `DELETE` erases every version (#360, ADR-0096); the form's `AttachmentField` shows each upload's indicator and Cancel, and holds Next/Submit while any is in flight. A type-ahead value the question does not offer is added to that question's choices in the same transaction (ADR-0063, ADR-0095). | — |
| Browser continuity | Implemented (#80, #373). The form keeps the selected locale, shown question-revision IDs, entered answer values, and each finished upload's ID, name, and size in `localStorage` for 15 days from the report's first save or until a successful submit, whichever comes first; a `File` is never persisted, and continuing the saved report restores its uploaded files. Start over, Discard report, and an expired saved report delete its uploads (ADR-0100). No report-data write request is made before the reporter presses Submit. | None. |
| Abuse prevention | Implemented (#15). Turnstile and its infrastructure are removed (ADR-0068); the submission endpoint requires a valid member bearer token (#14) and a sliding-window, trusted-client-IP `RateLimiter` policy; the Development sign-in endpoint has its own stricter policy partitioned by identity. `X-Forwarded-For` is trusted because the API's security group admits traffic only from the one AWS ALB in front of it (ADR-0081). | None. |
| Summarization DTO | Implemented (#17). `SummarizeReportProcessor` queries `ReportForSummaryDto` — exact revision labels/answers/privacy flags, joined on `ReportAnswer.QuestionRevisionId` — excluding consent, skipped/null answers, and file-upload answers; the deterministic marking pass (`PrivateValueMarker`, ADR-0082) then runs on `report_content`. | Keep. |
| AI orchestration | Implemented end to end (#17, #20, #302). `SummarizeReportProcessor` (an `IOutboxMessageProcessor`, #271's dispatch framework) builds the DTO, calls the one `ISummarizer` (`PromptDrivenSummarizer`), and persists a `Summary` row plus the report's `PendingReview`/`SummaryFailed` transition — bounded retries via `OutboxClaimer`'s own backoff/poison threshold, content-free logging. `IAiChatClient` is a provider strategy selected by `AiChatClient:Provider`; its only concretion is `GeminiChatClient` (Google Gemini's OpenAI-compatible endpoint, `gemini-3.7-flash`, reasoning `low`) when `AiChatClient:ApiKey` is present, else the fail-closed `UnconfiguredAiChatClient` ([ADR-0104](decisions/ADR-0104-summaries-are-generated-by-gemini-through-a-paid-key.md)). Production has no key until the Worker service roll lands (#30). | Keep. |
| Aircraft handling | Implemented. `ReportAircraft` and the typed `Discipline`/`InjurySeverity`/`PilotRating`/`Province`/`TimeOfDay` enums were deleted (#100); aircraft responses are ordinary `ReportAnswer` rows against revision-bound questions like any other. | Keep. |
| Summary persistence | Implemented. `Summary` is one row per report with `AiSummaryEn`/`AiSummaryFr`, shared `Model`/`PromptVersion` provenance, and one `ApprovedBy`/`ApprovedAt` pair; rewriting either language clears approval. | Keep. |
| Media images | Signature sniffing, 50 MB policy, private storage, and decode/re-encode metadata stripping are implemented and tested; uploads are validated before they are stored (#360). | — |
| Media videos | MP4/QuickTime are remuxed by ffmpeg into a verified metadata-free derivative; one that cannot be remuxed is retained without a derivative (ADR-0094, #309). | — |
| Documents | PDF, DOC, DOCX, RTF, Markdown, text, and ODT are sniffed, validated, and ingested as private originals with no derivative (#310), and are reviewer-downloadable through `GET /api/admin/reports/{reportId}/attachments/{attachmentId}/download` (#311). | Never extract, anonymize, send to LLM, or render inline. Public forced download on a published report (ADR-0119, #428). |
| Reviewer attachment access | `GET .../attachments/{id}/view` (image/video derivative) and `.../download` (document original) mint short-lived, forced-download pre-signed URLs through `ReviewerMediaLink`, the one chokepoint over `IBlobStore.CreateReadUrlAsync`; both require the `Reviewer` policy, refuse a failed or still-processing file, and write an atomic `AuditAction.ViewedAttachment` row before disclosing the URL (#311, ADR-0090). | None open. |
| Blob access | One adapter, `S3BlobStore`: S3 through the ECS task role in AWS, RustFS in docker-compose for development (ADR-0096, ADR-0110). Reviewer URLs are short-lived, forced-download, signed for the browser-reachable host, and carry the reporter's sanitized filename (ADR-0097); reviewer links are derivative-only for images/video and original-only for documents. `FileSystemBlobStore` and `CreateUploadUrl` are removed. | The live S3 path is untested until AWS credentials exist (#30). |
| Authentication | Implemented end to end. JWT bearer validation, three role policies, and a Development-only token issuer in the API (#191); the web application signs in with real credentials, stores the token, re-checks it against `/api/auth/me` on load, and draws role-aware chrome (#192). The submission endpoint (#14) requires the `Member` policy — any of the three roles. | — |
| Typeform import/export | Import is implemented end to end, API and admin UI: `POST /api/admin/typeform/import` (multipart English+French, bearer-authenticated, Administrator-only) parses via `TypeformQuestionMapper` and returns a preview (drafts, rejected fields, pending-logic note ids) without persisting any `Question`; `/admin/questions`' "Import from Typeform" dialog reviews each draft through the ordinary `QuestionEditor`, which still saves it like any other question. `pending_import_logic` rows persist immediately and are managed via `GET`/`DELETE /api/admin/typeform/pending-logic` (a deliberate hard-delete exception to the soft-delete convention). Tested against the organization's real `formENG.json`/`formFR.json` pair. A clean database now seeds the organization's real 28-question form: `QuestionBankSeed.Questions` was populated by running the mapper against the fixtures and reviewing the result by hand (every French-defaulted choice/question translated; privacy, system, and role are editorial decisions the mapper never makes), and a migration calls `QuestionBankSeedWriter.Write`. The three fields with real Typeform branching logic (`Where:`, `Pilot injury:`, `Passenger injury:`) seed unconditionally — an Administrator wires the equivalent "Depends on" relationship by hand. Export is implemented too: `GET /api/admin/typeform/export` (Administrator-only) returns a zip of `form-en.json`/`form-fr.json` built by `TypeformExportBuilder`, one flat field per live question (a `Group`/`Statement` is not re-nested into Typeform's own shape), with an `hpac` extension object per field carrying everything Typeform has no slot for (real type, privacy, required, depends-on/grouped-under by key, reporter-additions) — the file otherwise validates as plain Typeform JSON. `/admin/questions` has an "Export to Typeform JSON" button. `TypeformQuestionMapper` reads the `hpac` extension back on import: it names the real type where the native Typeform type is ambiguous (`Group`/`Statement` both `statement`; `Autocomplete`/`SingleSelect` both `multiple_choice`), and carries privacy, required, and the depends-on/grouped-under relationship by key — recoverable this way even though an export is flat and does not use Typeform's own `group`/`contact_info` nesting. A file with no `hpac` object (a real Typeform export, or a hand-authored fixture) keeps the original native-type-derived behavior. Exporting and reimporting reproduces the same drafts. Re-importing the same form updates in place: reviewing a draft whose key matches a live question opens the ordinary edit flow (a new revision, or a fork if it has been answered, per ADR-0071) instead of trying to create a second question under the same key. The Typeform import/export sequence (ADR-0077, ADR-0078) is now complete. | — |
| Review/admin web | `/admin/questions` is a working authoring screen (#180) — create, edit, reorder by pointer or keyboard, choices (edited in place, reporter-added ones marked and counted for review), conditions, and Translate. Shared choice lists and `/admin/choice-lists` were removed (#352). `/admin/reports` lists every live report newest first with a status badge, a separate Private (no consent) badge, and a Stuck badge, filtered from the address bar, and `/admin/reports/{id}` is the audited report view: answers (private ones marked), the summary pair with provenance, and the review actions its state allows — edit or write the pair, approve (publishing when consented, ADR-0105), reject with an optional note, reopen, unpublish, delete behind a confirmation, and open an attachment through its audited link; a stale action is refused with 409 and the page offers to reload (#25). `/admin` itself remains a placeholder. The Admin menu is role-aware: a `User` sees none, a `SafetyOfficer` sees manage-reports, an `Administrator` sees every option (#192). The menu shows how much work is waiting: the reports needing action, the answers awaiting translation (administrators only), and their total on the closed button. The counts come from the `admin_pending_counts` view (#418, ADR-0116). | There is no allowlist screen to build. |
| Publication | The `public_reports` view states the publication invariant in SQL. `GET /api/v1/public/reports` is an anonymous, keyset-paginated feed, newest published first; `GET /api/v1/public/reports/{id}` returns one report or an indistinguishable `404` (#28). `/reports` lists the feed with each report's comment count, and `/reports/{id}` is each report's own deep-linkable page, with the visitor's language first. Members comment there; authors edit and delete their own, reviewers hide any, and the Worker machine-translates each revision (#329, ADR-0114). A report's page embeds its verified image and video derivatives when the reporter also answered yes to the `consent_media` system question: the `public_report_media` view holds the rule, `GET /api/v1/public/reports/{id}/media/{fileId}` mints a ≤15-minute inline link through `PublicMediaLink`, the page re-asks on an error and drops a file on 404, and reviewers hide or show a file, audited (#412, ADR-0117). | Commenter names (#413). |
| Soft deletion | Implemented. Every entity except `AuditLogEntry` has a `Deleted` timestamp and an EF global query filter; `ModelTests` asserts both the universal filter and the audit-log exception. No restore or physical delete path exists. | Keep. |
| Retention | Storage lifecycle handles some quarantine states; no complete report-retention/deletion flow. | Retain until explicit soft deletion; expire only unreferenced quarantine; keep report-linked bytes private. |
| Encryption | Implemented. The application-side AES-GCM field cipher, its converters, and `IFieldCipher` were removed (#100); the schema now relies on managed AWS encryption plus TLS, per ADR-0019 (superseded by [data-and-persistence.md](data-and-persistence.md)). | Keep. |
| Localization | Locale catalogues, parity/lint/CI translation tooling, and locale value type exist. No real pages. | Keep catalogue tooling for app chrome; manually store both question languages; one AI call supplies both summaries. |
| Design system | Tailwind v4 tokens, dark token redefinition, self-hosted Aleo/Poppins, preview, and placeholder logo exist. | Keep and apply to accessible public/admin sites; replace logo only with approved asset. |
| Infrastructure | Broad AWS Terraform includes ECS, RDS, S3/CloudFront, one combined site distribution, SES, secrets, alarms, and OIDC workflows. | Prune SES/speculative pieces, host the one web app (public form + admin route) as a single ECS Fargate container behind one CloudFront distribution (ADR-0048), retain Canadian minimal services, backups, explicit migrations, OIDC, focused Worker alerts. |
| Tests/CI | Strong Core/persistence/media primitives and repository gates; API/Worker/UI feature coverage is mostly scaffold-level. The acceptance suite now boots the API for the scenarios that describe what it refuses over HTTP (#209). | Rewrite superseded contracts and add target API, Worker, browser, deletion, document, and public-boundary coverage. |

## Current database shape

The baseline has 10 tables: `reports`, `report_answers`, `report_files`,
`summaries`, `questions`, `question_revisions`, `question_choices`,
`pending_import_logic`, `audit_log`, and `outbox_messages`. There is
no user table: `admin_users` was dropped by
[ADR-0065](decisions/ADR-0065-no-user-records-identity-is-the-token-subject.md),
and `audit_log.actor_subject` and `summaries.approved_by_subject` hold opaque
token subjects with no foreign key. Six migrations create that shape:
the initial schema, replacing the earlier sensitivity scheme with question
privacy, and `MigrateCanonicalDomainAndPersistence` (#100), which collapsed
the old `question_versions`/`question_options`/`question_translations`/
`question_option_translations` tables into `question_revisions`/
`question_revision_options`, dropped `report_aircraft`, and removed the
application-side field-encryption columns/converters; and
`AddQuestionAuthoring` (#180), which added `option_sets`/`option_set_items`,
the conditional-question and option-set provenance columns, and the `time` and
`autocomplete` question types; and `AddReporterAddedChoices` (#184), which
added the reporter-added marker to `option_set_items`; and
`GiveEachQuestionItsOwnChoices` (#352), which copied every choice onto its
question in `question_choices` and dropped `option_sets`, `option_set_items`,
and `question_revision_options` (ADR-0095). Every table except
`audit_log` carries a `Deleted` timestamp and an EF global query filter. The
target shape and required migration are specified in
[data and persistence](data-and-persistence.md).

## Test evidence

The audited tests strongly protect TinyId/blob-key formats, consent-only
projection, the current question-revision model, privacy partitioning, outbox
retry, PostgreSQL 17 schema/atomicity/seeding (including the universal soft-
delete filter and its `audit_log` exception), pre-signed filesystem/S3
storage, image metadata stripping, and the current video fail-closed behavior.
API tests cover health/404/no-blob-route; Worker tests cover the outbox loop,
summarization, translation, and attachment processors, and the shipped
prompt's rules. JavaScript tests cover coverage and translation tooling. Those
observations explain which target gaps are real even when issue history says
a feature was completed.

## Guidance and skill audit

Issue #78 reduced the repository skill system to a small task-oriented set and
aligned the remaining guidance with this specification:

| Skill/guidance | Disposition |
|---|---|
| HPAC conventions and delivery/testing workflow | Retained with canonical spec links and target test commands. |
| Incident domain model | Rewritten for complete question revisions, pair summaries, consent-only projection, and universal soft deletion. |
| Anonymize HPAC reports | Collapsed to one concise skill covering the content/private partition, role replacement, one bilingual response, and human approval. Scrub/auditor/translator mechanics were removed. |
| Localization | Retained for UI catalogues and manually bilingual questions; one-language/runtime-translation assumptions were removed. |
| Persistence | Rewritten for the target schema and managed encryption; application AES guidance was removed. |
| Media handling | Rewritten for final multipart streaming, video derivatives, and private non-anonymized documents. |
| Web UI | Retained for static/Tailwind/accessibility rules, with product behavior delegated to this specification. |
| Infrastructure | Rewritten for separate sites, no SES, managed encryption, and focused operations. |
| Specialized aircraft guidance | Removed. Aircraft responses follow the ordinary question/answer and summary rules. |
| Generic Gang of Four / SOLID guidance | Removed from the installed set because the product does not need pattern-driven abstractions. |
| Requirements clarification | Retained as concise repository-specific guidance; this specification resolves the current product decisions. |

Generated copies under agent-specific directories are regenerated from the
single authored skill source, never hand-edited independently. Issue #78 also
removed the separate auditor agent and moved the one runtime prompt into the
Worker.

## Recommended implementation order

1. ~~Align the domain and migration: complete question revisions, consent-only
   report, bilingual summary row, attachment kinds, and universal soft
   delete.~~ Done (#100).
2. Implement current-form and finalized multipart submission with a required member token,
   streaming quarantine, transaction, and outbox.
3. ~~Implement Worker summary and attachment handlers, including documents and
   safe video derivatives.~~ Done (#17, #20, #302, #386); the Worker image
   carries ffmpeg since #423.
4. Implement JWT member authentication, review UI/API, pair approval, deletion, and
   the exact public DTO.
5. Complete both React/TypeScript sites and end-to-end bilingual/privacy tests.
6. Prune and split infrastructure, deploy through explicit migration, and
   verify focused operational alerts.
