---
title: HpacSafety.Api
description: The deployable ASP.NET Core HTTP surface and its current implementation status.
type: readme
---

# HpacSafety.Api

Deployable ASP.NET Core HTTP surface. The target contract is in
[`../../docs/interfaces-and-data-flow.md`](../../docs/interfaces-and-data-flow.md).

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

The API never calls AI, issues pre-signed upload URLs, logs request content,
or exposes attachment bytes publicly. Short-lived reviewer access is authorized
per request.

## Current status

Current main is mostly a host scaffold; several legacy Core/Infrastructure
types describe the superseded upload and persistence design. See
[`../../docs/implementation-status.md`](../../docs/implementation-status.md) and the
linked implementation issues before extending them.

```bash
docker compose up -d db
dotnet run --project src/HpacSafety.Api
```

Runtime secrets use local user-secrets in development and AWS Secrets Manager
in production. Migrations run as an explicit deployment step, not at startup.
