# ADR-0040: Restore anti-spam, translation, notification, and media, without reopening the scrubber or the classifier

Status: Accepted — 2026-08-23

## Context

Issue #74 asked for a narrow simplification: remove the deterministic PII
scrubber and second model audit, remove typed projections for ordinary
answers, and remove the aircraft-classification subsystem — because
classification's own invariant (the class comes only from the reporter's own
answer, never inferred) made a classifier subsystem redundant, not because
classification itself was unwanted. The resulting pull request, and
[ADR-0039](ADR-0039-minimal-immutable-report-flow.md), went further than the
issue authorized: it also dropped Cloudflare Turnstile anti-spam, question and
summary translation, application email notification, and the media/blob-storage
upload pipeline, and then declared all of them out of scope "without a new
approved requirement" — turning an over-broad diff into a standing decision.

None of those four were ever unwanted. They are restored here.

## Decision

- **Anti-spam.** `ITurnstileVerifier` is restored in `HpacSafety.Core`. A
  submission that fails Cloudflare Turnstile's server-side `siteverify` is
  rejected before it reaches the database.
- **Translation.** `ITranslator` is restored, with two call sites:
  - **Question authoring.** An administrator writes a question's label in one
    language; translating it into the other is the next step, through
    `ITranslator`, before the row is saved. This does not resurrect the
    separate `QuestionVersion`/`QuestionTranslation`/`QuestionOptionTranslation`
    tables ADR-0039 collapsed — `Question.LabelEn`/`LabelFr` stay inline
    columns on one immutable revision. Only the authoring *process* changes:
    an administrator is never required to type both languages by hand.
  - **Summary translation.** The Worker generates one candidate summary in the
    report's own language, then translates it through `ITranslator` to produce
    the second official language. `Report` now stores one `Summary` per
    locale (`AddSummary` rejects a second summary in the same locale, not a
    second summary at all), and `Report.IsPublishable` requires both locales
    approved. Translation receives the anonymized summary text only — never
    report content or private context.
  - **Site UI chrome** keeps its own, unrelated pipeline: `locales/en-CA.json`
    is the source of truth, CI generates `fr-CA.json` via DeepL
    (`tools/translator.mjs`, `tools/translate-locale.mjs`), and
    `.github/workflows/i18n-translate.yml` opens the review pull request. This
    was never removed by ADR-0039's decision list — the workflow file and
    tooling were deleted anyway, and are restored verbatim. See ADR-0007,
    ADR-0021, ADR-0022, and `skills/localize-hpac-app/SKILL.md`.
- **Notification.** `IEmailSender` is restored. After both summary candidates
  are stored, the Worker sends one notification to `safety@hpac.ca` that the
  report is ready for review, riding the same outbox row so a failed send can
  never roll back the report submission.
- **Media.** The upload pipeline is restored verbatim: `IBlobStore`,
  `IExifStripper`, `IMediaSniffer`, `ReportFile`, `ReviewerMediaLink`, and the
  Infrastructure `Media`/`Storage` adapters (Magick.NET EXIF stripping and
  sniffing, `FileSystemBlobStore` for local development, `S3BlobStore` for
  production). A reviewer reaches a file only through
  `ReviewerMediaLink`, which issues a presigned URL for the stripped
  derivative and refuses the private original.

## What is not reinstated

The rest of ADR-0039 stands. There is still no deterministic text scrubber, no
second model (PII-audit) call, no aircraft-classification subsystem, and no
generic publication-channel framework. Aircraft classification specifically is
not a subsystem needing a decision here: `aircraft_certification` (public) and
`aircraft_manufacturer`/`aircraft_model` (private) are two ordinary
data-driven questions, already present in `QuestionBankSeed`, handled by the
same privacy partition as every other answer. ADR-0029, ADR-0030, and
ADR-0036 describe a deterministic classifier that no longer exists and are not
revived by this decision.

## Consequences

- `Report.IsPublishable` now requires an approved summary in **both** official
  locales, not one summary total. `SummaryConfiguration`'s unique index moves
  from `(ReportId)` to `(ReportId, Locale)`.
- `Report.Files` and the `report_files` table return; a new migration adds the
  table and the per-locale summary index in one change.
- The `i18n` CI job gains a second responsibility: it already checks that
  every seeded question is bilingual, and now also runs
  `translate-locale.mjs --check` (read-only, no provider, no secret — fork
  pull requests cannot trigger inference or write a generated locale file).
- Deployment gains back `hpac-safety/turnstile-secret-key` and
  `hpac-safety/notifications-to` Secrets Manager entries, the SES resources in
  `infra/ses.tf`, and the Worker task role's `ReadUploads`/`SendNotifications`
  statements. The CI-only DeepL credential (`DEEPL_API_KEY`) is a GitHub
  Actions repository secret, not an AWS one — it never reaches production.
- No Api or Worker implementation exists yet to wire any of these ports into,
  so this change is ports, schema, tooling, and documentation only. The
  implementing pull requests apply `ITurnstileVerifier`, `ITranslator`, and
  `IEmailSender` when the Api and Worker are built.
