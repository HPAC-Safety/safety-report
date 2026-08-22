# HpacSafety.Api

The HTTP surface. **Deployable.**

Receives occurrence reports from the public form, serves the admin surface, and
writes each report plus its outbox row in a single transaction. It does no AI
work — that belongs to [`HpacSafety.Worker`](../HpacSafety.Worker/README.md).

## Responsibilities

| | |
|---|---|
| `POST /api/v1/reports` | Validate, verify Turnstile, persist report + outbox row atomically, return `202` |
| `POST /api/v1/reports/{id}/upload-url` | Issue a scoped, short-lived pre-signed PUT |
| `/api/v1/admin/*` | Review queue, edit, approve, reject — authenticated |
| `POST /api/v1/admin/session` | Credential proxy to `members.hpac.ca`, then allowlist check |
| `/health`, `/health/ready` | Liveness and readiness |

## What it must never do

- Block a submission on a model call. The reporter gets an immediate response;
  summarization happens behind it.
- Log a request body on the report endpoints, or a credential at any level.
- Serve an uploaded file directly. Media goes through short-lived pre-signed
  GETs from a private bucket.

## Running locally

```bash
docker compose up -d db          # Postgres
dotnet run --project src/HpacSafety.Api
```

Configuration comes from `appsettings.json`, overridden by environment
variables. Secrets never go in a committed file — use user-secrets locally:

```bash
dotnet user-secrets set "ConnectionStrings:Default" "..." --project src/HpacSafety.Api
dotnet user-secrets set "Turnstile:SecretKey" "..."      --project src/HpacSafety.Api
```

## Deployment

Hosting is not finalised — AWS is the leading candidate. The mechanics are the
same regardless.

**Build a container:**

```bash
dotnet publish src/HpacSafety.Api -c Release /t:PublishContainer
```

.NET 10 publishes an OCI image without a Dockerfile. Push it to the registry the
chosen host uses (ECR, GHCR), then deploy as a long-running service.

**Sizing:** one small instance is ample. HPAC receives on the order of dozens of
reports a year; this API is sized for availability, not throughput. Run at least
two instances if you want zero-downtime deploys — it is stateless, so scaling
out is safe.

**Required configuration:**

| Variable | Notes |
|---|---|
| `ConnectionStrings__Default` | Postgres. Canadian region preferred — see `docs/data-handling.md` |
| `Turnstile__SecretKey` | Server-side only, never in the web bundle |
| `Blob__*` | Bucket, region, credentials |
| `HpacAuth__Enabled` | Kill switch for the credential proxy |
| `ASPNETCORE_ENVIRONMENT` | `Production` |

**Migrations run before the new version takes traffic**, as a separate step —
not automatically at startup, which races when more than one instance boots:

```bash
dotnet ef database update -p src/HpacSafety.Infrastructure -s src/HpacSafety.Api
```

**Deploy trigger:** GitHub Actions on merge to `main`, once the hosting decision
is made. Deployment is a Phase 2 issue.

**TLS is mandatory.** This endpoint receives personal information and proxies
member credentials. Terminate at the load balancer and enforce HSTS.

## Related

- [`docs/architecture.md`](../../docs/architecture.md)
- [`docs/authentication.md`](../../docs/authentication.md)
