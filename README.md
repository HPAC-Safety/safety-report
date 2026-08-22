# HPAC Safety Occurrence Reporting

A reporting system for the **Hang Gliding and Paragliding Association of
Canada**. Pilots file reports about incidents and accidents; the system stores
them, uses AI to summarize **and anonymize** each one, and puts the result in
front of a human safety officer before anything is published.

HPAC runs a **non-punitive** reporting system. Pilots report their own mistakes
so that others can learn from them, on the understanding that what gets
published cannot identify anyone. This repository exists to keep that promise
mechanically rather than by hand.

> **Status: scaffolding.** There is no application code yet — deliberately. The
> design lives in `docs/`, and the work is filed as issues across the
> **Foundation**, **Phase 1**, and **Phase 2** milestones.

## How it works

```mermaid
flowchart LR
    subgraph static["Static sites"]
        pub["report form"]
        adm["review queue"]
    end

    api["HpacSafety.Api<br/>ASP.NET Core 10"]
    db[("PostgreSQL")]
    worker["HpacSafety.Worker"]

    pub -->|"POST /api/v1/reports"| api
    adm -->|"authenticated"| api
    api -->|"report + outbox row,<br/>one transaction"| db
    db -->|"FOR UPDATE SKIP LOCKED"| worker
    worker -->|"anonymized summary,<br/>EN + FR"| db
```

A pilot submits a report. The API stores it and, in the same transaction, writes
an outbox row. A separate worker picks that up and runs the anonymization
pipeline. The result waits for a safety officer.

## The anonymization pipeline

```mermaid
flowchart LR
    raw["raw report"] --> s1["1 · deterministic scrub<br/>no AI"]
    s1 --> s2["2 · summarize<br/>in the source language"]
    s2 --> s3["3 · PII audit"]
    s3 --> s4["4 · translate the summary"]
    s4 --> s5["5 · PII audit the translation"]
    s5 --> h["human review"]
```

Deterministic redaction runs **first**, before any model sees the text — never
ask a language model to remove something a regular expression removes reliably.
The audit stages read only the generated summary and flag findings for a
reviewer; they never silently rewrite.

## What this system will never publish

- A name — reporter, pilot, passenger, instructor, or witness
- A phone number, email address, or HPAC member number
- The make or model of an aircraft. A summary says "a high EN-B glider", never
  "an Ozone Rush 6"
- A specific launch or landing site
- An uploaded photo or video
- Anything at all without a safety officer's approval
- Anything at all where the reporter did not consent to publication

The last two are absolute: there is no code path around them.

Details in [`docs/anonymization-policy.md`](docs/anonymization-policy.md).

## Bilingual

`en-CA` and `fr-CA` throughout — form, admin UI, API messages, emails, and
published summaries. The browser's language selects the locale, English is the
fallback, and a toggle is always visible.

A report is stored in the language it was written in and **never translated** —
it is the reporter's own account. The *summary* is generated in that language
and then translated, so both versions exist for every report. A safety officer
approves the pair.

Details in [`docs/localization.md`](docs/localization.md).

## Stack

| Layer | Choice |
|---|---|
| API | ASP.NET Core, .NET 10 |
| Worker | .NET 10 background service, outbox consumer |
| Database | PostgreSQL + EF Core |
| Web | Plain HTML + JavaScript, no bundler; Tailwind v4 standalone CLI |
| AI | Anthropic API behind `ISummarizer` / `IPiiAuditor` |
| Tests | xUnit + Shouldly + Testcontainers; `node:test`; Playwright |
| Local dev | Docker Compose |
| Hosting | AWS `ca-central-1` — ECS Fargate, RDS, S3 + CloudFront, SES |
| Deploys | GitHub Actions via OIDC role assumption; no stored AWS keys |
| Infrastructure | Terraform in `infra/`, planned on PRs and applied on merge |

Storage and email still sit behind `IBlobStore` and `IEmailSender`, so local
development runs on the filesystem and a logging mailer without touching AWS.

## Layout

```
AGENTS.md          canonical agent instructions (CLAUDE.md et al. symlink to it)
Skillfile          declarative skill management
skills/ agents/    authored skills — sources for `skillfile install`
HpacSafety.slnx    solution (.NET 10)
src/               Core · Infrastructure · Api · Worker · web  (README in each)
tests/             unit · integration · anonymization golden files · js · e2e
prompts/           versioned runtime prompts sent to the model
locales/           en-CA.json (source), fr-CA.json (generated)
docs/              design, policy, ADRs
tools/             generators and build scripts
```

## Deployables

Four things ship, and they ship independently. Each has its own README with the
deployment specifics.

| Component | Deployable | How |
|---|---|---|
| [`src/HpacSafety.Api`](src/HpacSafety.Api/README.md) | Yes | Container, long-running service |
| [`src/HpacSafety.Worker`](src/HpacSafety.Worker/README.md) | Yes | Container, long-running service — not a cron job |
| [`src/web/public`](src/web/README.md) | Yes | Static site — the report form |
| [`src/web/admin`](src/web/README.md) | Yes | Static site — the review queue, separate origin |
| [`src/HpacSafety.Core`](src/HpacSafety.Core/README.md) | No | Class library. Depends on nothing. |
| [`src/HpacSafety.Infrastructure`](src/HpacSafety.Infrastructure/README.md) | No | Class library |

```mermaid
flowchart TD
    subgraph cdn["Static hosting"]
        pubsite["public form"]
        admsite["admin queue"]
    end
    subgraph svc["Container platform"]
        apisvc["Api"]
        wsvc["Worker"]
    end
    db[("Postgres")]
    blob[("Object storage")]

    pubsite --> apisvc
    admsite --> apisvc
    apisvc --> db
    apisvc --> blob
    wsvc --> db
    wsvc --> blob
```

Hosting is **AWS, `ca-central-1`** — ECS Fargate for the API and Worker, RDS
PostgreSQL, S3 and CloudFront for the static sites, SES for mail. The Canadian
region is deliberate: reports contain personal information about identifiable
people. See [ADR-0009](docs/decisions/ADR-0009-hosting-on-aws.md).

Deploys run from GitHub Actions using **OIDC role assumption — no AWS access
keys are stored anywhere.** CloudFront Functions provide the URL rewrites the
static sites need for clean URLs, which is why GitHub Pages was not an option;
see [`src/web/README.md`](src/web/README.md).

Two deployment rules worth stating up front:

- **Migrations run as their own step before the new API version takes traffic**,
  never automatically at startup — that races when more than one instance boots.
- **The public and admin sites deploy separately.** Different audiences,
  different risk. The admin surface can then sit behind extra network controls
  without affecting a pilot's ability to file a report.

## Getting started

```bash
git clone git@github.com:HPAC-Safety/safety-report.git
cd safety-report
skillfile install     # installs skills into .claude/ (gitignored)
```

Then read [`AGENTS.md`](AGENTS.md) and pick an issue from the Foundation
milestone.

Windows contributors need `git config core.symlinks true` and Developer Mode, or
the agent instruction symlinks arrive as plain text files. See
[`docs/agent-workflow.md`](docs/agent-workflow.md).

## Built by agents

This project is built primarily by AI agents, and the configuration is
deliberately tool-agnostic: `AGENTS.md` is the only real instruction file, and
`CLAUDE.md`, `.github/copilot-instructions.md`, and `.cursor/rules/agents.mdc`
are symlinks to it. Skills are pinned in `Skillfile.lock` so every contributor
and every tool gets the same setup.

## Documentation

| Topic | Where |
|---|---|
| System design | [`docs/architecture.md`](docs/architecture.md) |
| What the form asks | [`docs/form-spec.md`](docs/form-spec.md) *(generated)* |
| Redaction policy | [`docs/anonymization-policy.md`](docs/anonymization-policy.md) |
| Runtime prompts | [`prompts/`](prompts/) |
| Aircraft classes | [`docs/aircraft-classification.md`](docs/aircraft-classification.md) |
| Login, and why it's like that | [`docs/authentication.md`](docs/authentication.md) |
| Personal data | [`docs/data-handling.md`](docs/data-handling.md) |
| Colours and type | [`docs/design-system.md`](docs/design-system.md) |
| Strings and translation | [`docs/localization.md`](docs/localization.md) |
| Test conventions | [`docs/testing-conventions.md`](docs/testing-conventions.md) |
| Why things are the way they are | [`docs/decisions/`](docs/decisions/) |

## Roadmap

**Foundation** — CI, solution skeleton, dev environment, coverage gate.

**Phase 1** — the report form, the API, the database, the anonymization
pipeline, bilingual strings, and the moderation UI. This is the working system.

**Phase 2** — the public feed, publication channels, deployment, and cutover
from the existing Typeform.

## Reporting a security issue

See [`SECURITY.md`](SECURITY.md). This system holds personal information about
real accidents; please report privately rather than in a public issue.

## Licence

MIT — see [`LICENSE`](LICENSE).
