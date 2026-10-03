---
title: HpacSafety.Api
description: The deployable ASP.NET Core HTTP surface and its current implementation status.
type: readme
---

# HpacSafety.Api

Deployable ASP.NET Core HTTP surface. The target contract is in
[`../../.spec/interfaces-and-data-flow.md`](../../.spec/interfaces-and-data-flow.md).

## Target responsibilities

- Return the ordered current bilingual question revisions.
- Receive each attachment as it is attached, validate it, and hold it in
  private quarantine under an opaque upload ID; delete one on request.
- Receive one final JSON report request naming its upload IDs.
- Verify the member bearer token, rate limits, exact revision/answer shapes,
  attachment bounds, and consent.
- Claim the named uploads and atomically store the report, asked
  questions/answers, file rows, and outbox work; return `202`.
- Expose authenticated review/administration commands and minimal public
  read-only DTOs.

The API never calls AI, logs request content, or passes attachment bytes
through itself or exposes them publicly; it mints the pre-signed PUT an upload
goes to quarantine by
([ADR-0126](../../.spec/decisions/ADR-0126-an-attachment-uploads-straight-to-quarantine-by-pre-signed-put.md)). Short-lived reviewer access is authorized
per request.

## Current status

A gap is an `@ignore` claim in the generated
[traceability matrix](../../.spec/traceability.md) or an open issue in
[issue traceability](../../docs/issue-traceability.md).

```bash
docker compose up -d db
dotnet run --project src/HpacSafety.Api
```

Runtime secrets use local user-secrets in development and AWS Secrets Manager
in production. Migrations run as an explicit deployment step, not at startup.
