---
title: Deployment
description: How the application reaches the target AWS environment.
type: guide
---

# Deployment

The target deployment is two small AWS environments in `ca-central-1`,
staging and production, built from the same Terraform
([ADR-0158](decisions/ADR-0158-two-aws-accounts-staged-and-promoted-by-approval.md)):

- the API and the Worker as Lambda functions
  ([ADR-0042](decisions/ADR-0042-lambda-hosted-api-with-fargate-migration-path.md),
  [ADR-0123](decisions/ADR-0123-the-worker-runs-on-lambda-and-the-website-on-s3-and-cloudfront.md));
- RDS PostgreSQL with backups;
- private S3 attachment storage;
- one website, with the review queue as its `/admin` route, served as static
  files from a private S3 bucket through CloudFront, which also routes
  `/api/*` to the API's Lambda Function URL — there is no ALB
  ([ADR-0048](decisions/ADR-0048-one-website-admin-as-a-route.md),
  [ADR-0123](decisions/ADR-0123-the-worker-runs-on-lambda-and-the-website-on-s3-and-cloudfront.md),
  [ADR-0159](decisions/ADR-0159-cloudfront-routes-api-to-a-function-url-no-alb.md));
- Secrets Manager, identity-provider configuration, and focused alerts for failed
  or stuck Worker work.

## Environments and accounts

**No AWS deployment exists yet.** This section describes the target: what a
release will do once #443, #464, #465, and #466 land, not something already
running. See "Where today's Terraform differs" below for exactly what is
still scaffolding.

- **Staging** will be the owner's personal AWS account, which also runs
  unrelated workloads. It is meant to hold synthetic data only and never run
  the Development-only members-site login. It serves only its default
  `*.cloudfront.net` address.
- **Production** will be a separate account that HPAC creates and owns. It is
  **not** created from the staging account through AWS Organizations — the
  two accounts are unrelated, and `infra/bootstrap.sh` is run independently in
  each one, from that account's own CloudShell. Once deployed, production
  holds real reports and serves `safety.hpac.ca` and `securite.acvl.ca` on one
  CloudFront distribution.
- Each account groups its resources under its own AWS myApplications
  application and tag-based Resource Group — **`hpac-staging`** and
  **`hpac-production`** — a grouping and cost-visibility tool, not a security
  boundary. Both accounts are reached only by their own short-lived GitHub
  OIDC roles
  (`hpac-safety-deploy`, `hpac-safety-plan`) — never a long-lived AWS access
  key.
- **First goal: staging only.** A working release pipeline against staging
  does not need the production account to exist yet. Production is created,
  bootstrapped, and connected once HPAC's account and its DNS records are
  ready; see issue #30's "Human work" for the exact one-time steps.
- **The identity provider is an external dependency, not chosen here**
  ([ADR-0064](decisions/ADR-0064-jwt-bearer-authentication-with-three-roles.md)).
  Until `AUTH_AUTHORITY` is set for an environment, that environment can still
  deploy public pages and submission, but sign-in, review, and administration
  cannot work there.

## Release and promotion

A maintainer publishes a GitHub Release tagged with the date, `YYYY.MM.DD-N`.
The release workflow builds the API image, the Worker image, and the website
bundle exactly once, deploys those artifacts to staging automatically, then
waits for the `hpac-admins` GitHub team to approve the `hpac-production` environment
before deploying the **same artifacts** — never a rebuild — to production.
Rollback re-runs the job for an earlier release's tag. There is no
`terraform apply` on a merge to `main`; a pull request only plans, against
both accounts.

Runtime secret values stay out of source control and Terraform state. Use
AWS-managed encryption at rest and TLS.

Migrations apply at startup: the API and the Worker each run pending migrations
under an advisory lock, and there is no dedicated migration step
([ADR-0055](decisions/ADR-0055-ef-core-migrations-sql-files-stored-procedures.md)).
Rollback redeploys a previously tested artifact; schema changes must support the
previous application during staged rollout. Backup restoration must be tested
before cutover.

Outbound internet, in both accounts, goes through a NAT instance (`fck-nat` on
a `t4g.nano`), not a managed NAT gateway. It is the one resource this system
ever deletes and recreates, and every release does so; everything else is
created once and updated in place, protected from deletion.

The current Terraform and deploy workflows are scaffolding. They still run the
API and the Worker on ECS Fargate behind an ALB (#443, #465), one AWS account
instead of two, and one CloudFront hostname instead of the production pair
plus a staging default address. Issue #30 owns bringing the deployed topology
to
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
