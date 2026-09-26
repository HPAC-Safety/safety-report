---
title: HPAC Safety occurrence reporting
description: What the system is, how to run it locally, and where the specification lives.
type: readme
---

# HPAC Safety Occurrence Reporting

HPAC Safety is the bilingual occurrence-reporting system for the Hang Gliding
and Paragliding Association of Canada. It collects database-driven reports,
creates an anonymized English/French safety-summary pair with one AI call, and
requires human approval before publication.

> **Implementation status:** the repository contains substantial domain,
> persistence, media, web-asset, CI, and infrastructure scaffolding, but the
> complete target flow is not implemented. The audited gaps are listed in
> [`docs/implementation-status.md`](docs/implementation-status.md). Do not infer
> feature completion from an old closed issue or README.

## How this repository works

HPAC Safety is built specification-first
([ADR-0083](docs/decisions/ADR-0083-specification-driven-development.md)).
Behavior is written down as an executable scenario before it is implemented,
and every hop between a need and the code is a tracked file rather than a
message in a conversation:

```
need (issue)
  → scenario            features/<area>/<area>.feature
  → supporting detail   features/<area>/README.md, docs/*.md
  → step definitions    tests/HpacSafety.Acceptance.Tests | tests/e2e/steps
  → code                src/**
```

Three consequences are worth knowing before you open a pull request:

- **The specification is corrected, not the conversation.** When the code does
  the wrong thing, the first question is whether the scenario said the wrong
  thing. If it did, the scenario changes and the chain re-runs from there.
- **Every claim has a stable ID.** A scenario carries one `@REQ-<AREA>-<NNN>`
  tag and a normative constraint in `docs/` carries a `CON-<PAGE>-<NNN>` ID, so
  a claim can be cited from an ADR, an issue, a review finding, or a commit
  already in history. IDs are never reused or renumbered
  ([ADR-0084](docs/decisions/ADR-0084-stable-claim-ids-and-a-generated-traceability-matrix.md)).
  [`docs/traceability.md`](docs/traceability.md) is generated from those files
  by `node tools/traceability.mjs`, never maintained by hand, and CI fails on a
  difference.
- **Out of scope is part of the specification.** What not to build is written
  down beside what to build, because a specification that states only the
  target invites an implementation to over-deliver.

Scenarios are not decorative: one without `@ui` executes as an xUnit test
through Reqnroll, and one tagged `@ui` executes through `playwright-bdd`
against the real browser. A scenario still waiting for its step definitions
carries `@ignore`.

## Canonical specification

[`features/README.md`](features/README.md) is the design authority and index for every
feature, boundary, DTO, lifecycle rule, and implementation gap. Older ADRs and
GitHub issues are historical context when they disagree with `/features`.

The target flow is deliberately small.
[`docs/architecture.md`](docs/architecture.md) draws it, in the repository's
one report-flow diagram.

- Questions are complete immutable English/French database revisions. An
  administrator may make any question required; publication consent can never
  be optional
  ([ADR-0061](docs/decisions/ADR-0061-administrators-may-require-any-question.md)).
- An unfinished report exists only in the respondent's browser for 15 days.
  Nothing is written to the API or database until the one final submission.
  Each attachment uploads to private quarantine when it is attached, and
  expires unless that submission claims it. A continued saved report restores
  its attached files too, within the same 15 days
  ([ADR-0100](docs/decisions/ADR-0100-an-attachment-is-kept-as-long-as-the-saved-report.md)).
- Private answers help the one model call recognize identifying text; they are
  never facts for publication. A repeated private name becomes a role such as
  “the pilot” / “le pilote,” with no name fragment left behind.
- Images and videos receive metadata-free derivatives. Documents are validated
  and kept unchanged; they are never anonymized, parsed, or sent to AI.
- Public output is the report ID, both approved summary texts, publication
  time, and member comments
  ([ADR-0114](docs/decisions/ADR-0114-members-may-comment-on-a-published-report.md)).
  With media consent, a published report also shows its image and video
  derivatives and offers its documents as forced downloads
  ([ADR-0117](docs/decisions/ADR-0117-a-published-report-shows-the-reporters-photos-and-video.md),
  [ADR-0119](docs/decisions/ADR-0119-a-published-report-offers-its-documents-for-download.md)).

Summarization makes one model call, with no second call, PII-audit call, or
translation call. A deterministic marking pass runs before it
([ADR-0082](docs/decisions/ADR-0082-a-deterministic-marking-pass-precedes-the-one-model-call.md)).
Answer and comment translation is a separate DeepL step in the Worker
([ADR-0112](docs/decisions/ADR-0112-only-answers-that-need-it-get-a-second-language.md),
[ADR-0114](docs/decisions/ADR-0114-members-may-comment-on-a-published-report.md)).
The system has no specialized aircraft processing, application-managed field
encryption, email-notification pipeline, server-side draft, or external
publication channel.

## Technology

| Area | Choice |
|---|---|
| API and Worker | .NET 10 / ASP.NET Core |
| Database | PostgreSQL with EF Core |
| Web | React 18 + TypeScript, built with Vite; Tailwind v4 via `@tailwindcss/vite`; `@dnd-kit` for reordering, behind one owned component ([ADR-0059](docs/decisions/ADR-0059-dnd-kit-for-reordering.md)) |
| Authentication | Bearer JWT from an external OAuth/OIDC provider — Auth0 or AWS Cognito, not yet chosen — with three roles read from a claim and no user records stored ([ADR-0064](docs/decisions/ADR-0064-jwt-bearer-authentication-with-three-roles.md), [ADR-0065](docs/decisions/ADR-0065-no-user-records-identity-is-the-token-subject.md)). Development signs its own tokens ([ADR-0066](docs/decisions/ADR-0066-a-development-identity-provider-signed-with-a-dev-key.md)) |
| Summarization model | Google Gemini `gemini-3.7-flash` at reasoning `low`, paid key, behind a provider strategy chosen by the Worker's `AiChatClient:Provider` setting ([ADR-0104](docs/decisions/ADR-0104-summaries-are-generated-by-gemini-through-a-paid-key.md)) |
| Attachment processing | Magick.NET re-encodes images ([ADR-0025](docs/decisions/ADR-0025-magick-net-for-exif-stripping.md)); ffmpeg remuxes video as a child process, installed from Ubuntu's archive in the Worker's Dockerfile-built image ([ADR-0094](docs/decisions/ADR-0094-video-is-remuxed-not-transcoded-and-never-refused.md), [ADR-0118](docs/decisions/ADR-0118-the-worker-image-installs-ubuntus-ffmpeg.md)) |
| Tests | xUnit, Shouldly, Testcontainers, `node:test`, Playwright |
| Hosting target | AWS `ca-central-1`. API and Worker on Lambda, website on S3 + CloudFront ([ADR-0042](docs/decisions/ADR-0042-lambda-hosted-api-with-fargate-migration-path.md), [ADR-0123](docs/decisions/ADR-0123-the-worker-runs-on-lambda-and-the-website-on-s3-and-cloudfront.md)); deployed through GitHub OIDC |

One Vite/React app serves the report form as its default route and the
review queue at `/admin`; the API's role-claim authorization is the security
boundary, not the delivery path
([ADR-0048](docs/decisions/ADR-0048-one-website-admin-as-a-route.md)). The API
and the Worker run as container images on Lambda. The API sits behind the ALB
([ADR-0042](docs/decisions/ADR-0042-lambda-hosted-api-with-fargate-migration-path.md)).
The Worker is nudged by the API after each commit and swept every minute by
EventBridge. The website is static files in a private S3 bucket behind
CloudFront
([ADR-0123](docs/decisions/ADR-0123-the-worker-runs-on-lambda-and-the-website-on-s3-and-cloudfront.md)).
The AWS topology, with a diagram of how every service connects, is in
[infrastructure and operations](docs/infrastructure-and-operations.md#production-topology).
Runtime data stays in Canada, object storage remains private, and the API and Worker apply pending migrations at startup under an
advisory lock
([ADR-0055](docs/decisions/ADR-0055-ef-core-migrations-sql-files-stored-procedures.md)).

## Getting started

You need Git. The setup script checks or installs the repository-pinned tools:

```bash
git clone git@github.com:HPAC-Safety/safety-report.git
cd safety-report
./init-dev.sh
```

On Windows, run it from Git Bash. To inspect without installing anything:

```bash
./init-dev.sh --check
```

To also render the graphify knowledge graph into a local Obsidian vault at
`obsidian-vault/` (opt-in, gitignored, rebuilt on each run with the flag):

```bash
./init-dev.sh --obsidian
```

`./init-dev.sh` also asks for the two private provider keys local development
needs — `DEEPL_API_KEY` (translation) and `GEMINI_API_KEY` (summaries) — and
writes them to a `.env` file at the root of the primary checkout. That file is
gitignored and never committed. `./dev-up.sh` passes it to the API and Worker
containers, from the primary checkout and from every worktree:

```bash
DEEPL_API_KEY=...
GEMINI_API_KEY=...
```

Without them, translation is unavailable and summaries fail. There is no
stand-in ([ADR-0109](docs/decisions/ADR-0109-no-translation-stand-in-in-any-environment.md)).

Common verification commands:

```bash
dotnet build HpacSafety.slnx
dotnet test HpacSafety.slnx
node --test $(find tests/js -name '*.test.mjs')
node tools/traceability.mjs
npm --prefix src/web ci && npm --prefix src/web run build
```

Integration tests require Docker. See [`tests/README.md`](tests/README.md) and
[`CONTRIBUTING.md`](CONTRIBUTING.md).

Before opening a pull request, run its checks locally. Commit first, write
the draft pull request body to a file, then:

```bash
tools/ci-local.sh --body pr-body.md
```

It runs the pull request workflows themselves — `linked-issue.yml`,
`feature-coverage.yml`, terraform `infra`, and every `ci.yml` job, including
the coverage ratchet against main's last green run — under
[act](https://github.com/nektos/act), in an Ubuntu 24.04 container
([ADR-0145](docs/decisions/ADR-0145-a-pull-requests-checks-run-locally-under-act.md)).
It needs Docker and act at the version in `.act-version`
(`./init-dev.sh --check` reports it; `brew install act` on macOS). `--job <id>`
runs one job. GitHub stays the authority: a local pass is necessary, not
sufficient.

It passes act one secret, `GITHUB_TOKEN`, read from `HPAC_ACT_TOKEN`. Create a
[fine-grained token](https://github.com/settings/personal-access-tokens/new)
for this repository only, with **Actions**, **Contents**, and **Metadata** set
to read. Without it the script stops. `--allow-gh-token` uses your `gh` login
instead, which can write; act hands it to every job and action, so it is
opt-in.

## Repository map

| Path | Purpose |
|---|---|
| [`features/`](features/README.md) | Canonical product and system specification, one claim per scenario |
| [`src/`](src/HpacSafety.Core/README.md) | Core, Infrastructure, API, Worker, and the React/Vite web app |
| [`tests/`](tests/README.md) | Unit, integration, contract, JS, and browser tests |
| [`skills/`](skills/hpac-safety-conventions/SKILL.md) | Focused coding-agent guidance: generic skills, and the project skills that extend them |
| [`docs/`](docs/architecture.md) | Constraints, operational notes, the generated [traceability matrix](docs/traceability.md), and historical ADRs |
| [`infra/`](infra/README.md) | Terraform and AWS bootstrap scaffolding |
| [`locales/`](locales/en-CA.json) | Reviewed application UI catalogues |

Runtime AI instructions live with the Worker under
`src/HpacSafety.Worker/Prompts/`.

## Security

Never put real report content in an issue, PR, test, fixture, or log. Report
vulnerabilities privately as described in [`SECURITY.md`](SECURITY.md).

## Licence

MIT — see [`LICENSE`](LICENSE).
