---
name: manage-hpac-infrastructure
description: Maintain HPAC Safety's minimal Canadian AWS, Terraform, deployment, secrets, backups, and focused Worker alerts. Use for infrastructure or operations changes.
---

# Manage HPAC Safety infrastructure

## Target

- In `ca-central-1`: one small API service, one small Worker service, RDS
  PostgreSQL, private S3 attachment storage, and one website serving admin as a
  route ([ADR-0048](../../docs/decisions/ADR-0048-one-website-admin-as-a-route.md)).
- Terraform and GitHub OIDC. Never create long-lived AWS keys.
- Preserve least privilege.

## Data

- AWS-managed encryption at rest and TLS.
- Run database migrations explicitly before application rollout; keep tested
  backups.
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

SES and email resources, combined-site assumptions, external publication
integrations, speculative scaling, and secrets or alarms that exist only for
retired features.
