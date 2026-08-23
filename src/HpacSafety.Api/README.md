# HpacSafety.Api

Deployable ASP.NET Core HTTP surface. The target contract is in
[`spec/interfaces-and-data-flow.md`](../../spec/interfaces-and-data-flow.md).

## Target responsibilities

- Return the ordered current bilingual question revisions.
- Receive one final multipart report request: JSON DTO plus optional files.
- Verify Turnstile, rate limits, exact revision/answer shapes, attachment bounds,
  and consent.
- Stream files to private quarantine and atomically store the report, asked
  questions/answers, file rows, and outbox work; return `202`.
- Expose authenticated review/administration commands and minimal public
  read-only DTOs.

The API never calls AI, creates pre-submit upload slots, logs request content,
or exposes attachment bytes publicly. Short-lived reviewer access is authorized
per request.

## Current status

Current main is mostly a host scaffold; several legacy Core/Infrastructure
types describe the superseded upload and persistence design. See
[`spec/implementation-status.md`](../../spec/implementation-status.md) and the
linked implementation issues before extending them.

```bash
docker compose up -d db
dotnet run --project src/HpacSafety.Api
```

Runtime secrets use local user-secrets in development and AWS Secrets Manager
in production. Migrations run as an explicit deployment step, not at startup.
