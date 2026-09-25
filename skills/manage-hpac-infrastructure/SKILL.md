---
name: manage-hpac-infrastructure
description: Maintain HPAC Safety's minimal Canadian AWS, Terraform, deployment, secrets, backups, and focused Worker alerts. Use for infrastructure or operations changes.
---

# Manage HPAC Safety infrastructure

## Target

- In `ca-central-1`: the API and the Worker as Lambda functions (ADR-0042,
  ADR-0123), RDS PostgreSQL, private S3 attachment storage, and one website,
  with admin as a route, served from a private S3 bucket through CloudFront
  ([ADR-0048](../../docs/decisions/ADR-0048-one-website-admin-as-a-route.md),
  [ADR-0123](../../docs/decisions/ADR-0123-the-worker-runs-on-lambda-and-the-website-on-s3-and-cloudfront.md)).
  The topology and today's Terraform differences are in
  [`infrastructure-and-operations.md`](../../docs/infrastructure-and-operations.md).
- Terraform and GitHub OIDC. Never create long-lived AWS keys.
- Preserve least privilege.

## Data

- AWS-managed encryption at rest and TLS.
- Migrations apply at startup: the API and the Worker each run
  `EnsureMigrated` (`MigrationRunner`) under an advisory lock, and there is no migrate job or
  migration deploy step (ADR-0055).
- Keep tested backups.
- Quarantine unreferenced uploads with lifecycle expiry; keep report-linked
  objects private.

## Secrets and identity

- Runtime secret values live in Secrets Manager, out of Terraform state and out
  of GitHub where deployment does not need them.
- The identity provider's client secret is an ordinary Secrets Manager entry.
- The development JWT signing key is a committed throwaway, not a secret.
  Production holds no signing key; it validates against the provider's
  published keys
  ([ADR-0064](../../docs/decisions/ADR-0064-jwt-bearer-authentication-with-three-roles.md)).
- Provider choice is deferred. Residency matters when it is made:
  `ca-central-1` favors AWS Cognito.

## Operations

- Alert on terminal summary failures and stuck or aged outbox work.
- Keep logs content-free.
- In CI where possible: validate formatting, static security, and a
  credential-free plan path.

## Remove

SES and email resources, separate public/admin site assumptions (ADR-0048), external publication
integrations, speculative scaling, and secrets or alarms that exist only for
retired features.
