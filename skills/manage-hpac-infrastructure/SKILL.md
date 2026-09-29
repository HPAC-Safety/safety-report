---
name: manage-hpac-infrastructure
description: Maintain HPAC Safety's minimal Canadian AWS, Terraform, deployment, secrets, backups, and focused Worker alerts. Use for infrastructure or operations changes.
---

# Manage HPAC Safety infrastructure

## Target

- In `ca-central-1`: the API and the Worker as Lambda functions (ADR-0042,
  ADR-0123), RDS PostgreSQL, private S3 attachment storage, and one website,
  with admin as a route, served from a private S3 bucket through CloudFront,
  which also routes `/api/*` to the API's Function URL — no ALB
  ([ADR-0048](../../docs/decisions/ADR-0048-one-website-admin-as-a-route.md),
  [ADR-0123](../../docs/decisions/ADR-0123-the-worker-runs-on-lambda-and-the-website-on-s3-and-cloudfront.md),
  [ADR-0159](../../docs/decisions/ADR-0159-cloudfront-routes-api-to-a-function-url-no-alb.md)).
  The topology and today's Terraform differences are in
  [`infrastructure-and-operations.md`](../../docs/infrastructure-and-operations.md).
- **Two accounts, one Terraform root.** Staging is the owner's personal AWS
  account (synthetic data only); production is a separate, HPAC-owned account
  (real reports), not created from staging and not linked to it. Both build
  from the one `infra/` root, differing only in `infra/staging.tfvars` and
  `infra/production.tfvars`
  ([ADR-0158](../../docs/decisions/ADR-0158-two-aws-accounts-staged-and-promoted-by-approval.md)).
  A dated GitHub Release deploys to staging automatically and never to
  production. A maintainer promotes a staging-green tag with `promote.yml`,
  which deploys the same artifacts to production only after the
  `hpac-safety-admins` team approves the `hpac-safety-production` GitHub
  environment
  ([ADR-0166](../../docs/decisions/ADR-0166-a-release-deploys-staging-and-a-separate-workflow-promotes-to-production.md)). First goal: staging alone; production
  follows once HPAC's own account and DNS exist.
- Each account groups its resources under its own tag-based Resource Group
  (no AppRegistry application; ADR-0170), `hpac-safety-staging`/`hpac-safety-production` — a cost/grouping
  view, not a security boundary — tagged `Project=HPAC-Safety`,
  `Environment=<staging|production>`, `ManagedBy=terraform`, `Repo=HPAC-Safety/safety-report`
  (ADR-0158).
- A NAT instance (`fck-nat`), recreated every release, is the only resource
  either account ever deletes and recreates; everything else is created once
  and updated in place, protected from deletion (`prevent_destroy` on RDS,
  the uploads bucket, secrets, and log groups).
- **Terraform and GitHub OIDC only** — `hpac-safety-deploy` (release) and
  `hpac-safety-plan` (pull-request plan) per account, scoped to
  `hpac-safety-*` names (including, now, the account's own Resource Group by
  name) and the `Project=HPAC-Safety` tag; that scoping, not the Resource
  Group, is the actual security boundary. Never create a long-lived AWS
  key.
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
- **`AUTH_AUTHORITY` is an external dependency, not chosen here.** Until the
  identity provider exists, public pages and submission can still deploy in
  either environment, but sign-in, review, and administration cannot work.

## Operations

- Alert on terminal summary failures and stuck or aged outbox work.
- Keep logs content-free.
- In CI where possible: validate formatting, static security, and a
  credential-free plan path.

## Remove

SES and email resources, separate public/admin site assumptions (ADR-0048), an
ALB in front of the API (ADR-0159), a managed NAT gateway (ADR-0158), external
publication integrations, speculative scaling, and secrets or alarms that
exist only for retired features.
