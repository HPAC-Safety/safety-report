# ADR-0009 — Host on AWS, in ca-central-1

**Status:** Superseded in part by the
[infrastructure specification](../../spec/infrastructure-and-operations.md).
AWS `ca-central-1` remains; the target has separate public/admin static sites
and no SES/email resources.

## Context

Hosting had been left open, with everything host-shaped behind `IBlobStore` and
`IEmailSender` so the decision could be deferred. It is now made: **AWS**.

The system stores accounts of real accidents including names, phone numbers, and
injuries, so data residency is a live concern rather than a formality.

## Decision

AWS, **`ca-central-1`** for every service that touches report data.

| Concern | Service |
|---|---|
| API | ECS Fargate service behind an ALB |
| Worker | ECS Fargate service, no load balancer |
| Database | RDS PostgreSQL |
| Uploads | S3, private bucket |
| Email | SES |
| Static sites | S3 + CloudFront — ~~one distribution each for public and admin~~, superseded: **one** site, admin as a route (ADR-0031) |
| Images | ECR |
| Runtime secrets | Secrets Manager |
| Deploy identity | IAM role assumed by GitHub Actions via OIDC |

## Why these choices

**ECS Fargate rather than App Runner.** App Runner is simpler and would suit the
API, but it only runs HTTP services. The worker is a long-running process that
serves no traffic, so it does not fit. Running both on the same primitive is
worth more than App Runner's convenience on one of them.

**ECS rather than Lambda for the worker.** The worker polls continuously and
holds database connections. Lambda would mean re-architecting around events for
a system that processes on the order of dozens of reports a year.

**CloudFront Functions for URL rewrites.** The static sites need clean URLs
served at 200, which is already a hard requirement. A viewer-request
CloudFront Function does this without an origin round trip.

**OIDC rather than long-lived access keys.** GitHub Actions assumes an IAM role
with a short-lived token. There is no AWS secret to store, rotate, or leak. This
is the single most valuable security decision in the deployment story.

## Consequences

- `S3BlobStore` and an SES `IEmailSender` become the production registrations.
  Nothing outside `Infrastructure` changes.
- `ca-central-1` keeps the PIPEDA position simple. **Do not** move the database,
  bucket, or mail to a US region for cost or latency without revisiting
  `docs/data-handling.md`.
- ~~Two CloudFront distributions, so the admin surface can take network controls
  the public form must not have.~~ **Superseded by
  [ADR-0031](ADR-0031-terraform-shape-and-topology.md).** One distribution, with
  the review queue at `/admin/`. Per-area WAF and IP allowlisting are given up —
  they are distribution-level — because the admin bundle is static assets holding
  no report data, and the boundary that matters is the API's authorization.
- Cost is dominated by RDS and the two always-on Fargate tasks. For a national
  association receiving dozens of reports a year this is small but not zero;
  the smallest viable instance sizes are correct here.
- SES starts in sandbox mode. Production access must be requested before
  `safety@hpac.ca` receives anything, and `hpac.ca` needs SPF, DKIM, and DMARC
  records permitting the sender — otherwise an alert about a fatality lands in
  spam.

## Related

- `docs/data-handling.md`
- `src/web/README.md` — the URL rewrite requirement
