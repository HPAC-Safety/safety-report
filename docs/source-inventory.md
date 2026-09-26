---
title: Source inventory
description: Every project and directory under src/, what it holds, and the decisions that govern it.
type: guide
---

# Source inventory

This page maps every project and directory under `src/`, as of 2026-09-25
(#437). It is kept at directory level so it stays true: a new file in an
existing directory needs no entry, but a new directory does. Issue #444 adds
the check that fails when an entry is missing or names a directory that no
longer exists. Migrations are listed in
[`Persistence/Migrations/README.md`](../src/HpacSafety.Infrastructure/Persistence/Migrations/README.md),
not here.

## HpacSafety.Api — the HTTP host

It runs on Lambda
([ADR-0042](decisions/ADR-0042-lambda-hosted-api-with-fargate-migration-path.md);
today's Terraform still uses ECS, #443). It validates tokens and applies
migrations at startup, and does no AI work.

| Directory | Holds |
|---|---|
| [`src/HpacSafety.Api`](../src/HpacSafety.Api/) | The project, `Program.cs` (host, forwarded headers per ADR-0081, startup migrations per ADR-0055), and settings. |
| [`Admin/`](../src/HpacSafety.Api/Admin/) | Reviewer and administrator endpoints: question authoring and Translate drafts (ADR-0062), report review and actions (ADR-0105), attachment links and hide/show (ADR-0117, ADR-0119), the answer-translation queue (ADR-0112), pending counts (ADR-0116), and Typeform import/export (ADR-0077, ADR-0078). |
| [`Authentication/`](../src/HpacSafety.Api/Authentication/) | JWT bearer validation, the three role policies, the Development-only token issuer and credential sources, including the members-site sign-in (ADR-0064, ADR-0066, ADR-0079). |
| [`Properties/`](../src/HpacSafety.Api/Properties/) | Local launch profiles. |
| [`PublicQuestions/`](../src/HpacSafety.Api/PublicQuestions/) | `GET /api/v1/questions`: the anonymous current form. |
| [`PublicReports/`](../src/HpacSafety.Api/PublicReports/) | The public feed, a report's page, its media links (ADR-0117, ADR-0119), and member comments (ADR-0114). |
| [`RateLimiting/`](../src/HpacSafety.Api/RateLimiting/) | Per-IP policies for submission and sign-in (ADR-0081). |
| [`Reports/`](../src/HpacSafety.Api/Reports/) | `POST /api/v1/uploads`, which mints a pre-signed PUT to quarantine, and the JSON report submission that validates and claims each upload (ADR-0096, ADR-0098, ADR-0126). |

## HpacSafety.Core — domain rules and small ports

Core has no runtime package dependency.

| Directory | Holds |
|---|---|
| [`src/HpacSafety.Core`](../src/HpacSafety.Core/) | Shared value types (`TinyId`, `UploadId`, `BlobKey`, `Locale`, `EnumCode`) and the ports `IBlobStore`, `IAiChatClient`, and `ITranslator`. |
| [`Features/Comments/`](../src/HpacSafety.Core/Features/Comments/) | A member's comment and its immutable revisions (ADR-0114). |
| [`Features/Moderation/`](../src/HpacSafety.Core/Features/Moderation/) | Roles, the token identity, and the append-only audit entry (ADR-0064, ADR-0065). |
| [`Features/Outbox/`](../src/HpacSafety.Core/Features/Outbox/) | Outbox messages and their four types (ADR-0002). |
| [`Features/QuestionBank/`](../src/HpacSafety.Core/Features/QuestionBank/) | Questions, immutable revisions, forks, owned choices, dependencies, grouping, and the two system consent questions (ADR-0071, ADR-0095, ADR-0117). |
| [`Features/QuestionBank/Typeform/`](../src/HpacSafety.Core/Features/QuestionBank/Typeform/) | The Typeform mapper, export builder, and the hard-deleted pending-logic notes (ADR-0077, ADR-0078). |
| [`Features/Reporting/`](../src/HpacSafety.Core/Features/Reporting/) | The report aggregate, answers, files, summary, lifecycle, the media policy and ingestor, the private-value marker (ADR-0082), and the two link chokepoints, `ReviewerMediaLink` and `PublicMediaLink`. |

## HpacSafety.Infrastructure — adapters

| Directory | Holds |
|---|---|
| [`src/HpacSafety.Infrastructure`](../src/HpacSafety.Infrastructure/) | Persistence service registration. |
| [`AiChatClient/`](../src/HpacSafety.Infrastructure/AiChatClient/) | The Gemini client and the fail-closed unconfigured client (ADR-0104). |
| [`Media/`](../src/HpacSafety.Infrastructure/Media/) | Sniffers, the Magick.NET image stripper (ADR-0025), and the ffmpeg remuxer and its verification (ADR-0094, ADR-0122). |
| [`Persistence/`](../src/HpacSafety.Infrastructure/Persistence/) | The `DbContext`, `MigrationRunner` (ADR-0055), the outbox claimer, and the concurrency token. |
| [`Persistence/Configurations/`](../src/HpacSafety.Infrastructure/Persistence/Configurations/) | EF mappings for every table and view. |
| [`Persistence/Conventions/`](../src/HpacSafety.Infrastructure/Persistence/Conventions/) | Snake-case naming and the soft-delete filters with their exceptions (ADR-0040, ADR-0095). |
| [`Persistence/Conversions/`](../src/HpacSafety.Infrastructure/Persistence/Conversions/) | `TinyId`, `Locale`, and enum-code converters. |
| [`Persistence/Migrations/`](../src/HpacSafety.Infrastructure/Persistence/Migrations/) | Every migration and the model snapshot. |
| [`Persistence/Seeding/`](../src/HpacSafety.Infrastructure/Persistence/Seeding/) | The question-bank seed from the Typeform fixtures (ADR-0020, ADR-0077), deterministic seed IDs, and the inert `DevelopmentAdminSeed` that only the first migration still calls. |
| [`Persistence/Sql/`](../src/HpacSafety.Infrastructure/Persistence/Sql/) | Raw SQL the migrations load: data transforms and every view (ADR-0055, ADR-0116). |
| [`Persistence/Views/`](../src/HpacSafety.Infrastructure/Persistence/Views/) | Read-only entities for the six views. |
| [`Storage/`](../src/HpacSafety.Infrastructure/Storage/) | `S3BlobStore`, the one storage adapter: S3 in AWS, RustFS in development (ADR-0096, ADR-0110). |
| [`Translation/`](../src/HpacSafety.Infrastructure/Translation/) | The DeepL translator behind `ITranslator` (ADR-0022, ADR-0115). |

## HpacSafety.Worker — outbox processing

It runs on Lambda
([ADR-0123](decisions/ADR-0123-the-worker-runs-on-lambda-and-the-website-on-s3-and-cloudfront.md);
today it is a polling loop on ECS, #443).

| Directory | Holds |
|---|---|
| [`src/HpacSafety.Worker`](../src/HpacSafety.Worker/) | The host, the polling loop (`Worker.cs`), settings, and the Dockerfile that installs Ubuntu's ffmpeg (ADR-0118). |
| [`Outbox/`](../src/HpacSafety.Worker/Outbox/) | One processor per message type: summarize, process an attachment (ADR-0098), translate answers (ADR-0112), and translate a comment (ADR-0114). |
| [`Prompts/`](../src/HpacSafety.Worker/Prompts/) | The versioned runtime prompts. The newest is current, and each summary records its version. |
| [`Properties/`](../src/HpacSafety.Worker/Properties/) | Local launch profiles. |
| [`Summarization/`](../src/HpacSafety.Worker/Summarization/) | The prompt-driven summarizer that makes the one model call. |

## web — the one website

One React/TypeScript/Vite build, with the review queue as its `/admin` route
([ADR-0043](decisions/ADR-0043-react-typescript-vite-web-front-end.md),
[ADR-0048](decisions/ADR-0048-one-website-admin-as-a-route.md)). It is served
from S3 through CloudFront
([ADR-0123](decisions/ADR-0123-the-worker-runs-on-lambda-and-the-website-on-s3-and-cloudfront.md)).

| Directory | Holds |
|---|---|
| [`src/web`](../src/web/) | The npm project, Vite config, `index.html`, and the theme preview page. |
| [`assets/`](../src/web/assets/) | Logos. |
| [`assets/fonts/`](../src/web/assets/fonts/) | Self-hosted Aleo and Poppins WOFF2 files and their licences (ADR-0023). |
| [`src/`](../src/web/src/) | The app shell, routes table, global CSS tokens (ADR-0024), and theme bootstrap. |
| [`src/api/`](../src/web/src/api/) | Typed clients for the public, reporter, and admin endpoints. |
| [`src/auth/`](../src/web/src/auth/) | The session, sign-in, and role-aware context; the browser never parses a JWT. |
| [`src/components/`](../src/web/src/components/) | Shared UI: header and menus, the admin route guard (ADR-0092), review actions, report media (ADR-0117) and comments, and the question editor. |
| [`src/i18n/`](../src/web/src/i18n/) | Locale resolution and catalogue loading. |
| [`src/lib/`](../src/web/src/lib/) | Answer formatting and the word diff behind translation confirmation (ADR-0108). |
| [`src/report-form/`](../src/web/src/report-form/) | The paged reporter form, its saved report, attachment field, and the media-consent rule (ADR-0100, ADR-0119). |
| [`src/routes/`](../src/web/src/routes/) | One component per page. |
| [`src/theme/`](../src/web/src/theme/) | Light/dark theme selection. |
