---
title: Deployment
description: How the application reaches the target AWS environment.
type: guide
---

# Deployment

The target deployment is a small AWS environment in `ca-central-1`:

- the API and the Worker as Lambda functions
  ([ADR-0042](decisions/ADR-0042-lambda-hosted-api-with-fargate-migration-path.md),
  [ADR-0123](decisions/ADR-0123-the-worker-runs-on-lambda-and-the-website-on-s3-and-cloudfront.md));
- RDS PostgreSQL with backups;
- private S3 attachment storage;
- one website, with the review queue as its `/admin` route, served as static
  files from a private S3 bucket through CloudFront
  ([ADR-0048](decisions/ADR-0048-one-website-admin-as-a-route.md),
  [ADR-0123](decisions/ADR-0123-the-worker-runs-on-lambda-and-the-website-on-s3-and-cloudfront.md));
- Secrets Manager, identity-provider configuration, and focused alerts for failed
  or stuck Worker work.

GitHub Actions assumes AWS roles through OIDC. Do not create long-lived AWS
access keys. Runtime secret values stay out of source control and Terraform
state. Use AWS-managed encryption at rest and TLS.

Migrations apply at startup: the API and the Worker each run pending migrations
under an advisory lock, and there is no dedicated migration step
([ADR-0055](decisions/ADR-0055-ef-core-migrations-sql-files-stored-procedures.md)).
Rollback redeploys a previously tested artifact; schema changes must support the
previous application during staged rollout. Backup restoration must be tested
before cutover.

The current Terraform and deploy workflows are scaffolding. They still run the
API and the Worker on ECS Fargate (#443) and still hold an unused migrate task
and SES resources (#441). Issue #30 owns bringing the deployed topology to
[`infrastructure-and-operations.md`](infrastructure-and-operations.md), whose
"Where today's Terraform differs" lists every known gap.
Do not interpret a successful Terraform validation as proof that the target
environment exists or has been applied.

Local development requires no AWS account. `./init-dev.sh` prepares the
machine once, and `./dev-up.sh` builds and starts PostgreSQL, the S3 server,
the API, the Worker, and the web dev server in Docker (see the root
[`README.md`](../README.md)).

Terraform formatting/validation commands remain documented in
[`infra/README.md`](../infra/README.md).
