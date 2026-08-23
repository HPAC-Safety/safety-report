# Source inventory

This inventory accounts for every one of the 135 tracked paths under `src/` on
the audited main commit plus the Worker prompt added during guidance alignment.
“Current” describes what a path did on the baseline unless an alignment note
says otherwise; “target” identifies whether its idea is retained, revised, or
removed. Generated and binary assets were inspected by metadata/content
identity where source text does not exist.

## API — 6 paths

- [src/HpacSafety.Api/HpacSafety.Api.csproj](../src/HpacSafety.Api/HpacSafety.Api.csproj) — ASP.NET Core project and references; retain and add only target endpoint dependencies.
- [src/HpacSafety.Api/Program.cs](../src/HpacSafety.Api/Program.cs) — minimal host with `/health`; retain host and implement question, submission, admin, and public routes.
- [src/HpacSafety.Api/Properties/launchSettings.json](../src/HpacSafety.Api/Properties/launchSettings.json) — local HTTP/HTTPS launch profiles; development-only configuration.
- [src/HpacSafety.Api/README.md](../src/HpacSafety.Api/README.md) — aligned target-boundary orientation with an explicit current-scaffold warning.
- [src/HpacSafety.Api/appsettings.Development.json](../src/HpacSafety.Api/appsettings.Development.json) — development logging; add safe local adapters without secrets.
- [src/HpacSafety.Api/appsettings.json](../src/HpacSafety.Api/appsettings.json) — base logging/host settings; add validated target configuration references, never values of secrets.

## Core — 57 paths

### Moderation and outbox

- [src/HpacSafety.Core/Features/Moderation/AdminRole.cs](../src/HpacSafety.Core/Features/Moderation/AdminRole.cs) — SafetyOfficer/Administrator roles; retain exactly.
- [src/HpacSafety.Core/Features/Moderation/AdminUser.cs](../src/HpacSafety.Core/Features/Moderation/AdminUser.cs) — allowlist aggregate with role/active flag; add universal `Deleted` behavior and preserve credential-free identity.
- [src/HpacSafety.Core/Features/Moderation/AuditAction.cs](../src/HpacSafety.Core/Features/Moderation/AuditAction.cs) — stable audit action codes; expand for target revisions, pair review, publication, and deletion.
- [src/HpacSafety.Core/Features/Moderation/AuditLogEntry.cs](../src/HpacSafety.Core/Features/Moderation/AuditLogEntry.cs) — immutable actor/action/target record; retain and intentionally do not add `Deleted`.
- [src/HpacSafety.Core/Features/Moderation/IMemberAuthenticator.cs](../src/HpacSafety.Core/Features/Moderation/IMemberAuthenticator.cs) — upstream identity port; retain for HPAC credential-proxy and later OIDC/OAuth adapters.
- [src/HpacSafety.Core/Features/Outbox/OutboxMessage.cs](../src/HpacSafety.Core/Features/Outbox/OutboxMessage.cs) — identifier-only durable work with retries/poison threshold; retain concept, add claims and soft deletion.

### Question bank

- [src/HpacSafety.Core/Features/QuestionBank/Question.cs](../src/HpacSafety.Core/Features/QuestionBank/Question.cs) — current stable aggregate with mutable display/privacy state; replace with stable key plus complete immutable revision semantics.
- [src/HpacSafety.Core/Features/QuestionBank/QuestionKey.cs](../src/HpacSafety.Core/Features/QuestionBank/QuestionKey.cs) — validated stable logical key; retain, including `consent_publish` identity.
- [src/HpacSafety.Core/Features/QuestionBank/QuestionOption.cs](../src/HpacSafety.Core/Features/QuestionBank/QuestionOption.cs) — current version-bound option; revise as immutable complete-revision child with bilingual labels/order.
- [src/HpacSafety.Core/Features/QuestionBank/QuestionOptionTranslation.cs](../src/HpacSafety.Core/Features/QuestionBank/QuestionOptionTranslation.cs) — current normalized option locale row; fold into the complete revision aggregate/DTO unless physical normalization remains demonstrably simpler.
- [src/HpacSafety.Core/Features/QuestionBank/QuestionRole.cs](../src/HpacSafety.Core/Features/QuestionBank/QuestionRole.cs) — drives current ordinary typed projections; remove except the fixed consent invariant represented by key/system metadata.
- [src/HpacSafety.Core/Features/QuestionBank/QuestionTranslation.cs](../src/HpacSafety.Core/Features/QuestionBank/QuestionTranslation.cs) — current normalized label/help locale row; fold into complete bilingual revision semantics.
- [src/HpacSafety.Core/Features/QuestionBank/QuestionType.cs](../src/HpacSafety.Core/Features/QuestionBank/QuestionType.cs) — answer-shape enum including statement/group; retain stable types and localize only at edges.
- [src/HpacSafety.Core/Features/QuestionBank/QuestionVersion.cs](../src/HpacSafety.Core/Features/QuestionBank/QuestionVersion.cs) — current immutable wording/type container; expand/replace with the complete revision defined by this specification.

### Reporting and attachments

- [src/HpacSafety.Core/Features/Reporting/Discipline.cs](../src/HpacSafety.Core/Features/Reporting/Discipline.cs) — current typed aircraft projection enum; ordinary answers no longer require this report projection.
- [src/HpacSafety.Core/Features/Reporting/IExifStripper.cs](../src/HpacSafety.Core/Features/Reporting/IExifStripper.cs) — image-only processor port; revise to a clear attachment derivative processor boundary covering images/videos.
- [src/HpacSafety.Core/Features/Reporting/IMediaSniffer.cs](../src/HpacSafety.Core/Features/Reporting/IMediaSniffer.cs) — stream signature-detection port; retain concept and expand to documents.
- [src/HpacSafety.Core/Features/Reporting/IPiiAuditor.cs](../src/HpacSafety.Core/Features/Reporting/IPiiAuditor.cs) — separate PII model stage; remove.
- [src/HpacSafety.Core/Features/Reporting/IPublicationChannel.cs](../src/HpacSafety.Core/Features/Reporting/IPublicationChannel.cs) — generic external publication abstraction; remove because only the first-party public query exists.
- [src/HpacSafety.Core/Features/Reporting/ISummarizer.cs](../src/HpacSafety.Core/Features/Reporting/ISummarizer.cs) — current one-language summary port/result; revise to one strict bilingual result with shared provenance.
- [src/HpacSafety.Core/Features/Reporting/InjurySeverity.cs](../src/HpacSafety.Core/Features/Reporting/InjurySeverity.cs) — current typed report projection enum; keep only if useful as question option vocabulary, not an aggregate projection.
- [src/HpacSafety.Core/Features/Reporting/MediaIngestOutcome.cs](../src/HpacSafety.Core/Features/Reporting/MediaIngestOutcome.cs) — current retained/ingested/rejected outcome; revise for image/video derivatives and validated private documents.
- [src/HpacSafety.Core/Features/Reporting/MediaIngestStatus.cs](../src/HpacSafety.Core/Features/Reporting/MediaIngestStatus.cs) — current media processing state; revise/name for all attachment categories.
- [src/HpacSafety.Core/Features/Reporting/MediaIngestor.cs](../src/HpacSafety.Core/Features/Reporting/MediaIngestor.cs) — current image stripper/video retention orchestrator; revise for per-file Worker processing and document validation.
- [src/HpacSafety.Core/Features/Reporting/MediaKind.cs](../src/HpacSafety.Core/Features/Reporting/MediaKind.cs) — current image/video discriminator; add Document or rename to AttachmentKind.
- [src/HpacSafety.Core/Features/Reporting/MediaPolicy.cs](../src/HpacSafety.Core/Features/Reporting/MediaPolicy.cs) — per-file size and MIME/signature policy; retain, add count and document allowlist support.
- [src/HpacSafety.Core/Features/Reporting/MediaRejection.cs](../src/HpacSafety.Core/Features/Reporting/MediaRejection.cs) — localized-safe rejection mapping; revise for document failures without echoing filenames.
- [src/HpacSafety.Core/Features/Reporting/MediaRejectionReason.cs](../src/HpacSafety.Core/Features/Reporting/MediaRejectionReason.cs) — stable failure codes; expand for attachment count and invalid document container/text.
- [src/HpacSafety.Core/Features/Reporting/MediaType.cs](../src/HpacSafety.Core/Features/Reporting/MediaType.cs) — six current image/video types; retain and add configured PDF/DOC/DOCX/RTF/MD/TXT/ODT types.
- [src/HpacSafety.Core/Features/Reporting/MediaUploadSlot.cs](../src/HpacSafety.Core/Features/Reporting/MediaUploadSlot.cs) — pre-submit signed upload reservation; remove.
- [src/HpacSafety.Core/Features/Reporting/MediaValidation.cs](../src/HpacSafety.Core/Features/Reporting/MediaValidation.cs) — policy validation result; retain/adapt for attachments.
- [src/HpacSafety.Core/Features/Reporting/PilotRating.cs](../src/HpacSafety.Core/Features/Reporting/PilotRating.cs) — typed pilot-rating projection; ordinary question options do not need a report property.
- [src/HpacSafety.Core/Features/Reporting/Province.cs](../src/HpacSafety.Core/Features/Reporting/Province.cs) — typed province projection; ordinary question options do not need a report property.
- [src/HpacSafety.Core/Features/Reporting/Report.cs](../src/HpacSafety.Core/Features/Reporting/Report.cs) — aggregate with consent plus current ordinary projections/status transitions; simplify to revision answers, consent, pair summary, attachments, lifecycle, and deletion.
- [src/HpacSafety.Core/Features/Reporting/ReportAircraft.cs](../src/HpacSafety.Core/Features/Reporting/ReportAircraft.cs) — separate typed aircraft child; remove in favor of ordinary question-revision answers.
- [src/HpacSafety.Core/Features/Reporting/ReportAnswer.cs](../src/HpacSafety.Core/Features/Reporting/ReportAnswer.cs) — exact question-version answer and privacy snapshot; retain concept, reference complete revision and persist skips.
- [src/HpacSafety.Core/Features/Reporting/ReportFile.cs](../src/HpacSafety.Core/Features/Reporting/ReportFile.cs) — original/derivative metadata and processing state; expand to document kind, no filename/extracted text, and `Deleted`.
- [src/HpacSafety.Core/Features/Reporting/ReportStatus.cs](../src/HpacSafety.Core/Features/Reporting/ReportStatus.cs) — submitted-through-published lifecycle including `SummaryFailed`; retain with pair-level transitions and deletion outside the enum.
- [src/HpacSafety.Core/Features/Reporting/ReviewerMediaLink.cs](../src/HpacSafety.Core/Features/Reporting/ReviewerMediaLink.cs) — current derivative-only URL choke point; revise to permit validated private document originals only as forced downloads.
- [src/HpacSafety.Core/Features/Reporting/SummarizationInput.cs](../src/HpacSafety.Core/Features/Reporting/SummarizationInput.cs) — labeled public/private partition; retain and ensure all attachment/document material is absent.
- [src/HpacSafety.Core/Features/Reporting/Summary.cs](../src/HpacSafety.Core/Features/Reporting/Summary.cs) — current one-locale row/source-translation link; replace with one EN/FR row and one approval.
- [src/HpacSafety.Core/Features/Reporting/TimeOfDay.cs](../src/HpacSafety.Core/Features/Reporting/TimeOfDay.cs) — current time projection/bucketing; no target report projection, though reusable display logic may remain at an edge if needed.

### Core project and shared kernel

- [src/HpacSafety.Core/HpacSafety.Core.csproj](../src/HpacSafety.Core/HpacSafety.Core.csproj) — dependency-free Core project; retain minimal dependency direction.
- [src/HpacSafety.Core/README.md](../src/HpacSafety.Core/README.md) — aligned target-domain orientation that distinguishes useful current scaffolding from retired types.
- [src/HpacSafety.Core/SharedKernel/BlobKey.cs](../src/HpacSafety.Core/SharedKernel/BlobKey.cs) — validated opaque report/compartment/server filename key; retain and extend attachment semantics without client names.
- [src/HpacSafety.Core/SharedKernel/BlobUrlLifetime.cs](../src/HpacSafety.Core/SharedKernel/BlobUrlLifetime.cs) — enforces short signed-read maximum; retain for reviewer attachment reads, not pre-submit writes.
- [src/HpacSafety.Core/SharedKernel/DomainRuleViolationException.cs](../src/HpacSafety.Core/SharedKernel/DomainRuleViolationException.cs) — domain invariant exception; retain without private data in messages.
- [src/HpacSafety.Core/SharedKernel/EnumCode.cs](../src/HpacSafety.Core/SharedKernel/EnumCode.cs) — invariant enum-code conversion; retain.
- [src/HpacSafety.Core/SharedKernel/FieldDecryptionException.cs](../src/HpacSafety.Core/SharedKernel/FieldDecryptionException.cs) — application field-encryption error; remove with AES field encryption.
- [src/HpacSafety.Core/SharedKernel/IBlobStore.cs](../src/HpacSafety.Core/SharedKernel/IBlobStore.cs) — current read/write plus signed-upload/read port; revise to streaming private writes/reads and signed authorized reads, no upload URL.
- [src/HpacSafety.Core/SharedKernel/IEmailSender.cs](../src/HpacSafety.Core/SharedKernel/IEmailSender.cs) — unused outbound-email port; remove.
- [src/HpacSafety.Core/SharedKernel/IFieldCipher.cs](../src/HpacSafety.Core/SharedKernel/IFieldCipher.cs) — application encryption port; remove.
- [src/HpacSafety.Core/SharedKernel/ITranslator.cs](../src/HpacSafety.Core/SharedKernel/ITranslator.cs) — runtime summary translation port; remove.
- [src/HpacSafety.Core/SharedKernel/ITurnstileVerifier.cs](../src/HpacSafety.Core/SharedKernel/ITurnstileVerifier.cs) — anti-bot boundary; retain and implement at final submission.
- [src/HpacSafety.Core/SharedKernel/Locale.cs](../src/HpacSafety.Core/SharedKernel/Locale.cs) — exact `en-CA`/`fr-CA` value; retain.
- [src/HpacSafety.Core/SharedKernel/MediaCompartment.cs](../src/HpacSafety.Core/SharedKernel/MediaCompartment.cs) — quarantine/original/derivative compartments; rename/generalize for attachments while preserving private boundaries.
- [src/HpacSafety.Core/SharedKernel/TinyId.cs](../src/HpacSafety.Core/SharedKernel/TinyId.cs) — validated opaque compact identifier; retain.

## Infrastructure — 44 paths

### Project and attachment processing

- [src/HpacSafety.Infrastructure/HpacSafety.Infrastructure.csproj](../src/HpacSafety.Infrastructure/HpacSafety.Infrastructure.csproj) — EF/Npgsql/AWS/Magick.NET dependencies; retain project, add only the chosen video/document tooling and remove encryption-only dependencies if any.
- [src/HpacSafety.Infrastructure/Media/ImagingCapabilities.cs](../src/HpacSafety.Infrastructure/Media/ImagingCapabilities.cs) — verifies configured image codecs at startup; retain for image allowlist.
- [src/HpacSafety.Infrastructure/Media/MagickFormats.cs](../src/HpacSafety.Infrastructure/Media/MagickFormats.cs) — MediaType/Magick mapping; retain for allowed images.
- [src/HpacSafety.Infrastructure/Media/MagickNetExifStripper.cs](../src/HpacSafety.Infrastructure/Media/MagickNetExifStripper.cs) — decodes/re-encodes images and removes metadata; retain, with naming generalized from EXIF only.
- [src/HpacSafety.Infrastructure/Media/MagickNetMediaSniffer.cs](../src/HpacSafety.Infrastructure/Media/MagickNetMediaSniffer.cs) — detects supported image formats from bytes; retain.
- [src/HpacSafety.Infrastructure/Media/MediaPolicyOptions.cs](../src/HpacSafety.Infrastructure/Media/MediaPolicyOptions.cs) — configured 50 MB policy; add combined attachment count/default five and document allowlist.
- [src/HpacSafety.Infrastructure/Media/MediaSnifferChain.cs](../src/HpacSafety.Infrastructure/Media/MediaSnifferChain.cs) — composes image/video signature detectors; add document detector(s).
- [src/HpacSafety.Infrastructure/Media/MissingImagingCodecException.cs](../src/HpacSafety.Infrastructure/Media/MissingImagingCodecException.cs) — fail-fast codec configuration error; retain.
- [src/HpacSafety.Infrastructure/Media/README.md](../src/HpacSafety.Infrastructure/Media/README.md) — aligned attachment matrix and current image/video/document gap note.
- [src/HpacSafety.Infrastructure/Media/VideoContainerSniffer.cs](../src/HpacSafety.Infrastructure/Media/VideoContainerSniffer.cs) — bounded MP4/QuickTime `ftyp` detection; retain and pair with controlled derivative processing.

### Persistence

- [src/HpacSafety.Infrastructure/Persistence/Configurations/ModerationConfiguration.cs](../src/HpacSafety.Infrastructure/Persistence/Configurations/ModerationConfiguration.cs) — admin/audit/outbox mappings; revise for Deleted filters/constraints and immutable audit exception.
- [src/HpacSafety.Infrastructure/Persistence/Configurations/QuestionBankConfiguration.cs](../src/HpacSafety.Infrastructure/Persistence/Configurations/QuestionBankConfiguration.cs) — current normalized question/version/translation mappings; replace with complete-revision mappings.
- [src/HpacSafety.Infrastructure/Persistence/Configurations/ReportConfiguration.cs](../src/HpacSafety.Infrastructure/Persistence/Configurations/ReportConfiguration.cs) — current report/answer/aircraft/file/locale-summary mappings and encryption converters; simplify to target records and managed encryption only.
- [src/HpacSafety.Infrastructure/Persistence/Conventions/SnakeCaseNames.cs](../src/HpacSafety.Infrastructure/Persistence/Conventions/SnakeCaseNames.cs) — deterministic snake_case relational naming; retain.
- [src/HpacSafety.Infrastructure/Persistence/Conversions/EnumCodeConverter.cs](../src/HpacSafety.Infrastructure/Persistence/Conversions/EnumCodeConverter.cs) — stable enum string converter; retain.
- [src/HpacSafety.Infrastructure/Persistence/Conversions/LocaleConverter.cs](../src/HpacSafety.Infrastructure/Persistence/Conversions/LocaleConverter.cs) — locale value converter; retain.
- [src/HpacSafety.Infrastructure/Persistence/Conversions/TinyIdConverter.cs](../src/HpacSafety.Infrastructure/Persistence/Conversions/TinyIdConverter.cs) — TinyId string converter; retain.
- [src/HpacSafety.Infrastructure/Persistence/Encryption/AesGcmFieldCipher.cs](../src/HpacSafety.Infrastructure/Persistence/Encryption/AesGcmFieldCipher.cs) — AES-GCM field cipher; remove.
- [src/HpacSafety.Infrastructure/Persistence/Encryption/EncryptedStringConverter.cs](../src/HpacSafety.Infrastructure/Persistence/Encryption/EncryptedStringConverter.cs) — encrypted-string EF converter; remove.
- [src/HpacSafety.Infrastructure/Persistence/Encryption/EncryptedTimeOnlyConverter.cs](../src/HpacSafety.Infrastructure/Persistence/Encryption/EncryptedTimeOnlyConverter.cs) — encrypted-time EF converter; remove.
- [src/HpacSafety.Infrastructure/Persistence/Encryption/FieldCipherModelCacheKeyFactory.cs](../src/HpacSafety.Infrastructure/Persistence/Encryption/FieldCipherModelCacheKeyFactory.cs) — cipher-sensitive EF model cache key; remove.
- [src/HpacSafety.Infrastructure/Persistence/Encryption/FieldEncryptionOptions.cs](../src/HpacSafety.Infrastructure/Persistence/Encryption/FieldEncryptionOptions.cs) — application key configuration; remove.
- [src/HpacSafety.Infrastructure/Persistence/HpacSafetyDbContext.cs](../src/HpacSafety.Infrastructure/Persistence/HpacSafetyDbContext.cs) — current 13-set context, conventions, encryption injection, immutable-question guard; revise sets, global Deleted filters, and target guards.
- [src/HpacSafety.Infrastructure/Persistence/HpacSafetyDbContextFactory.cs](../src/HpacSafety.Infrastructure/Persistence/HpacSafetyDbContextFactory.cs) — design-time migration context with synthetic cipher; retain factory but remove cipher requirement.
- [src/HpacSafety.Infrastructure/Persistence/Migrations/20260823001528_InitialSchema.Designer.cs](../src/HpacSafety.Infrastructure/Persistence/Migrations/20260823001528_InitialSchema.Designer.cs) — generated metadata for current initial schema; historical migration evidence, not target shape.
- [src/HpacSafety.Infrastructure/Persistence/Migrations/20260823001528_InitialSchema.cs](../src/HpacSafety.Infrastructure/Persistence/Migrations/20260823001528_InitialSchema.cs) — creates current 13 tables and seed; preserve in migration history and add a forward target migration.
- [src/HpacSafety.Infrastructure/Persistence/Migrations/20260823022839_ReplaceSensitivityWithQuestionPrivacy.Designer.cs](../src/HpacSafety.Infrastructure/Persistence/Migrations/20260823022839_ReplaceSensitivityWithQuestionPrivacy.Designer.cs) — generated metadata for privacy migration; historical.
- [src/HpacSafety.Infrastructure/Persistence/Migrations/20260823022839_ReplaceSensitivityWithQuestionPrivacy.cs](../src/HpacSafety.Infrastructure/Persistence/Migrations/20260823022839_ReplaceSensitivityWithQuestionPrivacy.cs) — replaces sensitivity with current question privacy snapshot; migrate forward to revision-owned privacy.
- [src/HpacSafety.Infrastructure/Persistence/Migrations/HpacSafetyDbContextModelSnapshot.cs](../src/HpacSafety.Infrastructure/Persistence/Migrations/HpacSafetyDbContextModelSnapshot.cs) — generated current model snapshot; regenerate from target model.
- [src/HpacSafety.Infrastructure/Persistence/README.md](../src/HpacSafety.Infrastructure/Persistence/README.md) — aligned canonical-schema summary with explicit legacy migration gaps.
- [src/HpacSafety.Infrastructure/Persistence/Seeding/DevelopmentAdminSeed.cs](../src/HpacSafety.Infrastructure/Persistence/Seeding/DevelopmentAdminSeed.cs) — environment-guarded synthetic local admin SQL; retain only for development.
- [src/HpacSafety.Infrastructure/Persistence/Seeding/QuestionBankSeed.cs](../src/HpacSafety.Infrastructure/Persistence/Seeding/QuestionBankSeed.cs) — Typeform-derived bilingual seed definitions; convert to initial complete revisions.
- [src/HpacSafety.Infrastructure/Persistence/Seeding/QuestionBankSeedWriter.cs](../src/HpacSafety.Infrastructure/Persistence/Seeding/QuestionBankSeedWriter.cs) — migration SQL writer for current normalized schema; rewrite for target complete revisions.
- [src/HpacSafety.Infrastructure/Persistence/Seeding/SeedIds.cs](../src/HpacSafety.Infrastructure/Persistence/Seeding/SeedIds.cs) — deterministic seed TinyIds; retain.
- [src/HpacSafety.Infrastructure/Persistence/Seeding/SeededOption.cs](../src/HpacSafety.Infrastructure/Persistence/Seeding/SeededOption.cs) — current bilingual option seed record; adapt to complete revision child.
- [src/HpacSafety.Infrastructure/Persistence/Seeding/SeededQuestion.cs](../src/HpacSafety.Infrastructure/Persistence/Seeding/SeededQuestion.cs) — current seed question shape; add every complete revision field and remove projection roles.
- [src/HpacSafety.Infrastructure/PersistenceServiceCollectionExtensions.cs](../src/HpacSafety.Infrastructure/PersistenceServiceCollectionExtensions.cs) — Npgsql/DbContext registration and cipher options; retain registration but remove cipher/key plumbing.
- [src/HpacSafety.Infrastructure/README.md](../src/HpacSafety.Infrastructure/README.md) — aligned target-adapter overview and retired-feature warning.

### Private storage

- [src/HpacSafety.Infrastructure/Storage/FileSystemBlobStore.cs](../src/HpacSafety.Infrastructure/Storage/FileSystemBlobStore.cs) — local private storage with signed URL simulation/read/write; retain streaming/read contract, remove upload-slot surface.
- [src/HpacSafety.Infrastructure/Storage/FileSystemBlobStoreOptions.cs](../src/HpacSafety.Infrastructure/Storage/FileSystemBlobStoreOptions.cs) — local root/public-base configuration; revise so local URLs preserve target authorization behavior.
- [src/HpacSafety.Infrastructure/Storage/PresignedUrlRejectedException.cs](../src/HpacSafety.Infrastructure/Storage/PresignedUrlRejectedException.cs) — safe signed-URL rejection type; retain only if still useful for authorized reads.
- [src/HpacSafety.Infrastructure/Storage/README.md](../src/HpacSafety.Infrastructure/Storage/README.md) — aligned final-upload/private-review storage contract with legacy adapter note.
- [src/HpacSafety.Infrastructure/Storage/S3BlobStore.cs](../src/HpacSafety.Infrastructure/Storage/S3BlobStore.cs) — S3 private read/write and pre-signed operations; retain bounded streaming/short reads, remove public/pre-submit upload usage.
- [src/HpacSafety.Infrastructure/Storage/S3BlobStoreOptions.cs](../src/HpacSafety.Infrastructure/Storage/S3BlobStoreOptions.cs) — bucket configuration; retain for private attachment bucket.

## Worker — 8 paths after alignment

- [src/HpacSafety.Worker/HpacSafety.Worker.csproj](../src/HpacSafety.Worker/HpacSafety.Worker.csproj) — Worker project/reference scaffold; add persistence, model, and attachment handler composition.
- [src/HpacSafety.Worker/Prompts/summarize-anonymize.v1.md](../src/HpacSafety.Worker/Prompts/summarize-anonymize.v1.md) — one concise versioned runtime prompt added after the baseline audit; load it from the Worker and version it with summary provenance.
- [src/HpacSafety.Worker/Program.cs](../src/HpacSafety.Worker/Program.cs) — generic host registration; compose DB/outbox, one summarizer, and attachment processors.
- [src/HpacSafety.Worker/Properties/launchSettings.json](../src/HpacSafety.Worker/Properties/launchSettings.json) — local Worker launch profile; retain.
- [src/HpacSafety.Worker/README.md](../src/HpacSafety.Worker/README.md) — aligned one-call/attachment target and current-host warning.
- [src/HpacSafety.Worker/Worker.cs](../src/HpacSafety.Worker/Worker.cs) — startup log only; replace with an orchestrator that delegates typed outbox handlers without embedding domain policy.
- [src/HpacSafety.Worker/appsettings.Development.json](../src/HpacSafety.Worker/appsettings.Development.json) — development logging; add safe fake/local adapter configuration without secrets.
- [src/HpacSafety.Worker/appsettings.json](../src/HpacSafety.Worker/appsettings.json) — base Worker logging; add validated references for model/prompt/retries/tooling, not secret values.

## Web — 21 paths

- [src/web/README.md](../src/web/README.md) — aligned static-site, form-continuity, accessibility, and current-page gap summary.
- [src/web/admin/.gitkeep](../src/web/admin/.gitkeep) — empty admin-site placeholder; replace with sign-in/review/question/allowlist static pages and modules.
- [src/web/assets/README.md](../src/web/assets/README.md) — asset provenance/pinning guidance; retain.
- [src/web/assets/fonts/OFL-Aleo.txt](../src/web/assets/fonts/OFL-Aleo.txt) — Aleo SIL Open Font License; retain with font.
- [src/web/assets/fonts/OFL-Poppins.txt](../src/web/assets/fonts/OFL-Poppins.txt) — Poppins SIL Open Font License; retain with font.
- [src/web/assets/fonts/README.md](../src/web/assets/fonts/README.md) — self-hosted font source/subset provenance; retain.
- [src/web/assets/fonts/aleo-latin-ext.woff2](../src/web/assets/fonts/aleo-latin-ext.woff2) — Aleo variable Latin-ext WOFF2 binary; retained display font asset.
- [src/web/assets/fonts/aleo-latin.woff2](../src/web/assets/fonts/aleo-latin.woff2) — Aleo variable Latin WOFF2 binary; retained display font asset.
- [src/web/assets/fonts/poppins-400-latin-ext.woff2](../src/web/assets/fonts/poppins-400-latin-ext.woff2) — Poppins 400 Latin-ext WOFF2 binary; retained UI font asset.
- [src/web/assets/fonts/poppins-400-latin.woff2](../src/web/assets/fonts/poppins-400-latin.woff2) — Poppins 400 Latin WOFF2 binary; retained UI font asset.
- [src/web/assets/fonts/poppins-500-latin-ext.woff2](../src/web/assets/fonts/poppins-500-latin-ext.woff2) — Poppins 500 Latin-ext WOFF2 binary; retained UI font asset.
- [src/web/assets/fonts/poppins-500-latin.woff2](../src/web/assets/fonts/poppins-500-latin.woff2) — Poppins 500 Latin WOFF2 binary; retained UI font asset.
- [src/web/assets/fonts/poppins-600-latin-ext.woff2](../src/web/assets/fonts/poppins-600-latin-ext.woff2) — Poppins 600 Latin-ext WOFF2 binary; retained UI font asset.
- [src/web/assets/fonts/poppins-600-latin.woff2](../src/web/assets/fonts/poppins-600-latin.woff2) — Poppins 600 Latin WOFF2 binary; retained UI font asset.
- [src/web/assets/fonts/poppins-700-latin-ext.woff2](../src/web/assets/fonts/poppins-700-latin-ext.woff2) — Poppins 700 Latin-ext WOFF2 binary; retained UI font asset.
- [src/web/assets/fonts/poppins-700-latin.woff2](../src/web/assets/fonts/poppins-700-latin.woff2) — Poppins 700 Latin WOFF2 binary; retained UI font asset.
- [src/web/assets/hpac-logo.png](../src/web/assets/hpac-logo.png) — 260×125 raster placeholder; do not present as approved branding, replace only with supplied official asset.
- [src/web/public/.gitkeep](../src/web/public/.gitkeep) — empty public-site placeholder; replace with report form/feed/detail static pages and modules.
- [src/web/shared/.gitkeep](../src/web/shared/.gitkeep) — empty shared-code placeholder; add only genuinely shared locale/API/presentation utilities.
- [src/web/styles/tailwind.css](../src/web/styles/tailwind.css) — HPAC tokens, self-hosted font faces, components, and dark token overrides; retain as shared design source.
- [src/web/styles/theme-preview.html](../src/web/styles/theme-preview.html) — static token/component preview in both themes; retain as visual regression/design reference, not a product page.

## Test-derived observations

All 69 tracked paths under `tests/` were read to infer enforced behavior. The
test conclusions and target replacements are summarized in
[implementation status](implementation-status.md) and the required future
contracts are in [testing and quality](testing-and-quality.md). The binary HEIC
fixture is a small synthetic GPS-bearing image used to prove metadata removal;
it contains no real incident or person data.
