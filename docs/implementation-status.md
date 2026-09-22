# Implementation status

This page compares the target specification with audited main at
`866f035e9e8d316ffaba7540113b57ead309c6cd`. “Implemented” means source and tests
substantially enforce the target behavior, not merely that an issue was closed.

## Capability matrix

| Capability | Main on the audit baseline | Target disposition |
|---|---|---|
| API host | ASP.NET Core host with `/health`, `/api/auth/*`, and the admin question-authoring endpoints under `/api/admin/` (#180). The session-header stub is gone: a validated bearer token and a role policy now gate every admin endpoint (#191). | Keep host; build the remaining public/admin API. |
| Worker host | Claims typed outbox messages safely (`OutboxClaimer`, `FOR UPDATE SKIP LOCKED`, #271) via one `IOutboxMessageProcessor` per type: `TranslateAnswersProcessor` translates every answer with a value — narrative included — into its second official language via the same `ITranslator` port question authoring uses (ADR-0080), and `SummarizeReportProcessor` runs the summarization pipeline (#17, see below). No per-attachment handler exists yet. | Implement per-attachment handlers (#81). |
| Question bank | Implemented, and now authorable. `Question` is a stable key/role/system-flag shell; every order/privacy/active/required/type/copy/option/dependency/grouping fact lives on an immutable `QuestionRevision`, selected by highest revision number. Revising creates a new revision rather than mutating fields. A revision may be conditional on a yes/no question or on a single-select question's required option (ADR-0060, ADR-0074), may snapshot a shared `option_set` (ADR-0058), and `Statement`/`Group` question types plus `GroupedUnderQuestionId` are implemented (ADR-0076) — a `Group` heading's children are recorded but a public form does not yet render them together. `AllowsReporterAdditions` widens ADR-0063's reporter-addition mechanism to `MultiSelect` (ADR-0077) — the domain/API/authoring-UI side is implemented; `POST /api/v1/reports` (#14) is a caller now, but `OptionSet.AddFromReporter` itself is not yet wired into that endpoint. `QuestionBankSeed` is currently empty (#222 cleared it). | Populate the seed via Typeform import (ADR-0077); wire `OptionSet.AddFromReporter` into the submission endpoint. |
| Form query and UI | `docs/form-spec.md` is hand-maintained evidence, not seed data; `QuestionBankSeed` is empty; public web directory has an authoring screen (`/admin/questions`, including `Statement`/`Group` authoring and grouping) but no reporter-facing form. | Implement latest-revision-per-key filtering and bilingual static form without resurrecting inactive/deleted questions; render `Statement`/`Group` and grouped children together; exclude both from the submission's answer-producing set. |
| Required behavior | Implemented, server-side. `Report.Project` only reads `QuestionRole.ConsentPublish`; every other answer is stored as data with no typed projection. `is_required` is authored per revision and consent is forced required (ADR-0061); `ReportAnswer.For` now rejects a blank value against a required revision (#14), so submission-time enforcement is built. **Client-side blocking of Next before answering a required question is not built** — it lands with the reporter-facing form (#80). | Build the client-side check with the public form. |
| Submission | Implemented: `POST /api/v1/reports` (#14) validates in order, reloads known superseded revisions, streams attachments through the existing media pipeline, enforces required/select/file-shape rules, and atomically persists the report, every answer (immutable `value`/`locale`, ADR-0080), file rows, and three outbox messages (summary, per-file attachment, answer-translation — the Worker now claims and processes the last of these, #271). The reporter-facing page requires a signed-in member and states that the report is not linked to their account (#193), but the form itself is still a placeholder — nothing calls this endpoint yet. The pre-submit upload-slot entity was removed (#100); `IBlobStore.CreateUploadUrlAsync` remains as a port method but nothing in Core calls it outside the removed slot flow. `OptionSet.AddFromReporter` (ADR-0063) is not yet wired into this endpoint. | Wire the reporter-facing form (#80) to this endpoint; wire `AddFromReporter` for reporter-added choices. |
| Browser continuity | Not implemented. | Same-browser answer/revision persistence for 15 days; never restore files or write unfinished report state to the API, database, or object storage. |
| Abuse prevention | Implemented (#15). Turnstile and its infrastructure are removed (ADR-0068); the submission endpoint requires a valid member bearer token (#14) and a sliding-window, trusted-client-IP `RateLimiter` policy; the Development sign-in endpoint has its own stricter policy partitioned by identity. `X-Forwarded-For` is trusted because the API's security group admits traffic only from the one AWS ALB in front of it (ADR-0081). | None. |
| Summarization DTO | Implemented (#17). `SummarizeReportProcessor` queries `ReportForSummaryDto` — exact revision labels/answers/privacy flags, joined on `ReportAnswer.QuestionRevisionId` — excluding consent, skipped/null answers, and file-upload answers; the deterministic marking pass (`PrivateValueMarker`, ADR-0082) then runs on `report_content`. | Keep. |
| AI orchestration | Implemented end to end except a reviewed provider (#17, #20). `SummarizeReportProcessor` (an `IOutboxMessageProcessor`, #271's dispatch framework) builds the DTO, calls the one `ISummarizer` (`PromptDrivenSummarizer`), and persists a `Summary` row plus the report's `PendingReview`/`SummaryFailed` transition — bounded retries via `OutboxClaimer`'s own backoff/poison threshold, content-free logging. `IAiChatClient`'s only registered concretion is the fail-closed `UnconfiguredAiChatClient` — no provider has been reviewed/approved yet. | Review and wire a real `IAiChatClient` concretion (a follow-on issue; first candidate is Google Gemini). |
| Aircraft handling | Implemented. `ReportAircraft` and the typed `Discipline`/`InjurySeverity`/`PilotRating`/`Province`/`TimeOfDay` enums were deleted (#100); aircraft responses are ordinary `ReportAnswer` rows against revision-bound questions like any other. | Keep. |
| Summary persistence | Implemented. `Summary` is one row per report with `AiSummaryEn`/`AiSummaryFr`, shared `Model`/`PromptVersion` provenance, and one `ApprovedBy`/`ApprovedAt` pair; rewriting either language clears approval. | Keep. |
| Media images | Signature sniffing, 50 MB policy, private storage, and decode/re-encode metadata stripping are implemented and tested. | Reuse validated pieces behind final multipart ingest and configurable total attachment count. |
| Media videos | MP4/QuickTime are detected and retained but deliberately have no reviewer derivative. | Add metadata-safe remux/transcode derivative; fail closed. |
| Documents | No PDF, DOC, DOCX, RTF, Markdown, text, or ODT support. | Validate/scan and retain private originals; authorized forced download only; never extract, anonymize, send to LLM, or publish. |
| Blob access | Filesystem/S3 stores implement pre-signed upload/read URLs; reviewer link is derivative-only. The pre-submit `MediaUploadSlot` entity was removed (#100), but `IBlobStore.CreateUploadUrlAsync` still exists on the port with no current caller. | Confirm the finalized multipart endpoint streams uploads server-side rather than reintroducing a pre-signed PUT flow; keep private streaming and short-lived reads for verified derivatives/private documents. |
| Authentication | Implemented end to end. JWT bearer validation, three role policies, and a Development-only token issuer in the API (#191); the web application signs in with real credentials, stores the token, re-checks it against `/api/auth/me` on load, and draws role-aware chrome (#192). The submission endpoint (#14) requires the `Member` policy — any of the three roles. | — |
| Typeform import/export | Import is implemented end to end, API and admin UI: `POST /api/admin/typeform/import` (multipart English+French, bearer-authenticated, Administrator-only) parses via `TypeformQuestionMapper` and returns a preview (drafts, rejected fields, pending-logic note ids) without persisting any `Question`; `/admin/questions`' "Import from Typeform" dialog reviews each draft through the ordinary `QuestionEditor`, which still saves it like any other question. `pending_import_logic` rows persist immediately and are managed via `GET`/`DELETE /api/admin/typeform/pending-logic` (a deliberate hard-delete exception to the soft-delete convention). Tested against the organization's real `formENG.json`/`formFR.json` pair. A clean database now seeds the organization's real 28-question form: `QuestionBankSeed.Questions` was populated by running the mapper against the fixtures and reviewing the result by hand (every French-defaulted choice/question translated; privacy, system, and role are editorial decisions the mapper never makes), and a migration calls `QuestionBankSeedWriter.Write`. The three fields with real Typeform branching logic (`Where:`, `Pilot injury:`, `Passenger injury:`) seed unconditionally — an Administrator wires the equivalent "Depends on" relationship by hand. Export is implemented too: `GET /api/admin/typeform/export` (Administrator-only) returns a zip of `form-en.json`/`form-fr.json` built by `TypeformExportBuilder`, one flat field per live question (a `Group`/`Statement` is not re-nested into Typeform's own shape), with an `hpac` extension object per field carrying everything Typeform has no slot for (real type, privacy, required, depends-on/grouped-under by key, reporter-additions) — the file otherwise validates as plain Typeform JSON. `/admin/questions` has an "Export to Typeform JSON" button. `TypeformQuestionMapper` reads the `hpac` extension back on import: it names the real type where the native Typeform type is ambiguous (`Group`/`Statement` both `statement`; `Autocomplete`/`SingleSelect` both `multiple_choice`), and carries privacy, required, and the depends-on/grouped-under relationship by key — recoverable this way even though an export is flat and does not use Typeform's own `group`/`contact_info` nesting. A file with no `hpac` object (a real Typeform export, or a hand-authored fixture) keeps the original native-type-derived behavior. Exporting and reimporting reproduces the same drafts. Re-importing the same form updates in place: reviewing a draft whose key matches a live question opens the ordinary edit flow (a new revision, or a fork if it has been answered, per ADR-0071) instead of trying to create a second question under the same key. The Typeform import/export sequence (ADR-0077, ADR-0078) is now complete. | — |
| Review/admin web | `/admin/questions` is a working authoring screen (#180) — create, edit, reorder by pointer or keyboard, options, shared lists, conditions, and Translate. `/admin/choice-lists` (#184) curates the shared lists and flags reporter-added choices. The review queue and the rest of `/admin` remain placeholders, The Admin menu is now role-aware: a `User` sees none, a `SafetyOfficer` sees manage-reports, an `Administrator` sees all three (#192). | Implement queue/detail, pair editing/approval, safe attachment access, and deletion. There is no allowlist screen to build. |
| Publication | Domain currently checks consent, report state, and separately approved locale rows; no public endpoints/UI. Publication-channel abstraction exists. | Implement minimal feed/detail allowlist over one approved pair; remove external-channel abstraction. |
| Soft deletion | Implemented. Every entity except `AuditLogEntry` has a `Deleted` timestamp and an EF global query filter; `ModelTests` asserts both the universal filter and the audit-log exception. No restore or physical delete path exists. | Keep. |
| Retention | Storage lifecycle handles some quarantine states; no complete report-retention/deletion flow. | Retain until explicit soft deletion; expire only unreferenced quarantine; keep report-linked bytes private. |
| Encryption | Implemented. The application-side AES-GCM field cipher, its converters, and `IFieldCipher` were removed (#100); the schema now relies on managed AWS encryption plus TLS, per ADR-0019 (superseded by [data-and-persistence.md](data-and-persistence.md)). | Keep. |
| Localization | Locale catalogues, parity/lint/CI translation tooling, and locale value type exist. No real pages. | Keep catalogue tooling for app chrome; manually store both question languages; one AI call supplies both summaries. |
| Design system | Tailwind v4 tokens, dark token redefinition, self-hosted Aleo/Poppins, preview, and placeholder logo exist. | Keep and apply to accessible public/admin sites; replace logo only with approved asset. |
| Infrastructure | Broad AWS Terraform includes ECS, RDS, S3/CloudFront, one combined site distribution, SES, secrets, alarms, and OIDC workflows. | Prune SES/speculative pieces, host the one web app (public form + admin route) as a single ECS Fargate container behind one CloudFront distribution (ADR-0048), retain Canadian minimal services, backups, explicit migrations, OIDC, focused Worker alerts. |
| Tests/CI | Strong Core/persistence/media primitives and repository gates; API/Worker/UI feature coverage is mostly scaffold-level. The acceptance suite now boots the API for the scenarios that describe what it refuses over HTTP (#209). | Rewrite superseded contracts and add target API, Worker, browser, deletion, document, and public-boundary coverage. |

## Current database shape

The baseline has 11 tables: `reports`, `report_answers`, `report_files`,
`summaries`, `questions`, `question_revisions`, `question_revision_options`,
`option_sets`, `option_set_items`, `audit_log`, and `outbox_messages`. There is
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
added the reporter-added marker to `option_set_items`. Every table except
`audit_log` carries a `Deleted` timestamp and an EF global query filter. The
target shape and required migration are specified in
[data and persistence](data-and-persistence.md).

## Test evidence

The audited tests strongly protect TinyId/blob-key formats, consent-only
projection, the current question-revision model, privacy partitioning, outbox
retry, PostgreSQL 17 schema/atomicity/seeding (including the universal soft-
delete filter and its `audit_log` exception), pre-signed filesystem/S3
storage, image metadata stripping, and the current video fail-closed behavior.
API tests cover health/404/no-blob-route; Worker tests cover only lifecycle/
start logging. JavaScript tests cover coverage and translation tooling. Those
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
3. Implement Worker summary and attachment handlers, including documents and
   safe video derivatives.
4. Implement JWT member authentication, review UI/API, pair approval, deletion, and
   the exact public DTO.
5. Complete both React/TypeScript sites and end-to-end bilingual/privacy tests.
6. Prune and split infrastructure, deploy through explicit migration, and
   verify focused operational alerts.
