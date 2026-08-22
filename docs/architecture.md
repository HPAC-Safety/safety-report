# Architecture

## The shape of it

```mermaid
flowchart LR
    subgraph static["Static hosting"]
        pub["web/public<br/>report form"]
        adm["web/admin<br/>review queue"]
    end

    api["HpacSafety.Api<br/>ASP.NET Core 10"]
    db[("PostgreSQL")]
    worker["HpacSafety.Worker"]
    hpac["members.hpac.ca"]
    blob[("blob storage")]

    pub -->|"POST /api/v1/reports"| api
    adm -->|"/api/v1/admin/* (session)"| api
    api -->|"report + outbox row,<br/>one transaction"| db
    api -.->|"IMemberAuthenticator"| hpac
    api -.->|"pre-signed PUT"| blob
    db -->|"FOR UPDATE SKIP LOCKED"| worker
    worker -->|"summaries EN + FR"| db
```

Four deployables: two static sites, one API, one worker. The web apps are plain
HTML and JavaScript with no bundler, served independently of the API so either
can move hosts without touching the other.

## Why a separate worker

Summarization takes seconds, calls a third-party model, and fails in ways an
HTTP request should not have to care about. A pilot submitting a report after a
crash gets an immediate acknowledgement; the AI work happens behind that.

Splitting the process also means summarization can be restarted, scaled, or
switched off entirely without affecting the ability to *receive* reports — which
is the part that must never be down.

## Why an outbox

The API writes the report and its outbox row in a single transaction. There is
no "save, then notify" — that loses reports whenever the process dies between
the two, and a lost safety report is not recoverable from anywhere.

```mermaid
sequenceDiagram
    participant B as Browser
    participant A as API
    participant D as PostgreSQL
    participant W as Worker

    B->>A: POST /api/v1/reports
    A->>D: BEGIN
    A->>D: INSERT report
    A->>D: INSERT outbox_message
    A->>D: COMMIT
    A-->>B: 202 Accepted
    W->>D: SELECT ... FOR UPDATE SKIP LOCKED
    W->>W: scrub → summarize → audit → translate → audit
    W->>D: INSERT summaries (en-CA, fr-CA)
    W->>D: mark outbox row processed
```

Everything asynchronous rides the same mechanism: summarization, translation,
notification email. `SKIP LOCKED` lets more than one worker run without
coordination. Failures back off exponentially and move aside after a poison
threshold rather than retrying forever.

Postgres `LISTEN/NOTIFY` may be added later purely to cut latency. Polling
stays the source of truth — a notification delivered while the worker is
restarting is a notification nobody hears.

## Projects

| Project | Responsibility | Depends on |
|---|---|---|
| `HpacSafety.Core` | Entities, enums, interfaces, the question bank, the deterministic scrub | nothing |
| `HpacSafety.Infrastructure` | EF Core, Anthropic, blob storage, HPAC auth, email | Core |
| `HpacSafety.Api` | HTTP surface, validation, sessions | Core, Infrastructure |
| `HpacSafety.Worker` | Outbox consumer | Core, Infrastructure |

`Core` depending on nothing is the rule that keeps the anonymization logic
testable without a database, a network, or a model. The deterministic scrub in
particular must be provable in a plain unit test.

Inside it, code is organised by **feature** — `Features/Reporting`,
`Features/QuestionBank`, `Features/Moderation`, `Features/Outbox` — with the
handful of genuinely cross-cutting types in `SharedKernel/`. Each feature owns
its entities, its enums, and the ports it calls out through. See
[ADR-0018](decisions/ADR-0018-feature-folders-in-core.md).

## Why uploads bypass the API

The browser PUTs a photo straight to a private bucket through a pre-signed URL
the API mints, scoped to one key and valid for minutes. That keeps
multi-megabyte bodies out of the request pipeline, and — the part that matters
more — it means the API is not a second door onto Restricted media with its own
authorization story to get wrong. There is no route that serves blob bytes, and
a test walks the live route table to keep it that way.

Every upload lands in `quarantine/` and nothing leaves it until this system has
decided what the bytes are. Accepted media is promoted to
`<report id>/original/<file>` — the Restricted record, retained untouched — and,
where the format can be stripped, to `<report id>/stripped/<file>`, which is the
only thing a reviewer's browser ever fetches. A refused upload is never
promoted and expires in quarantine, which is why nothing here has a delete.

Video is accepted and retained but has no derivative yet, so a reviewer sees
nothing for it rather than something unsafe (#65). See
[ADR-0025](decisions/ADR-0025-magick-net-for-exif-stripping.md),
[ADR-0026](decisions/ADR-0026-presigned-urls-and-private-blob-storage.md), and
`docs/data-handling.md`.

## Why the questions are in the database

The form HPAC asks is a table, not a class. Questions carry a type, an order,
their own options, and per-locale wording; answers reference the question
*version* they were given under. A safety officer changes the form without a
deploy, and a report from two seasons ago still renders the question it was
actually answering.

The answers that logic reads — consent above all — additionally project onto
typed columns on `reports`, so the invariants stay enforceable in `Core` with no
database. See
[ADR-0016](decisions/ADR-0016-data-driven-question-bank.md).

## Deliberately deferred

- Publication to the website, WhatsApp, or Telegram. `IPublicationChannel` is
  declared and unimplemented.
- Hosting choice. Everything host-shaped sits behind `IBlobStore` and
  `IEmailSender`.
- Postgres `LISTEN/NOTIFY`.

## Related

- `docs/anonymization-policy.md`
- `docs/authentication.md`
- `docs/data-handling.md`
- `docs/decisions/`
