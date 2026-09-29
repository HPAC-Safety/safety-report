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

## Terraform vs. the deploy role

Lessons from staging's first real release, #613 through #638
([issue #639](https://github.com/HPAC-Safety/safety-report/issues/639)):

- Confirm this repository's actual GitHub OIDC subject form
  (`gh api repos/<org>/<repo>/actions/oidc/customization/sub`) before writing
  a trust policy — `use_immutable_subject: true` changes the subject GitHub
  actually sends
  ([lesson 0027](../../docs/lessons/0027-a-trust-policy-named-a-subject-form-github-no-longer-sends.md)).
- Every resource name Terraform creates must match an ARN pattern in
  `bootstrap.sh`'s deploy policy, and every tag value it writes must match the
  policy's value exactly, case included (IAM tag comparisons are
  case-sensitive) — check both in the same pull request
  ([lesson 0029](../../docs/lessons/0029-an-ami-pin-that-named-no-image-and-a-tag-that-drifted-in-case.md),
  [lesson 0034](../../docs/lessons/0034-terraform-arguments-and-tags-never-checked-against-aws-and-the-deploy-role.md)).
- Audit the deploy role's policy against every resource Terraform creates and
  every AWS call the deploy workflow makes — tag-on-create, later updates, and
  non-tag-scopable services included — before the role's first real use, not
  one error at a time
  ([lesson 0031](../../docs/lessons/0031-a-deploy-role-with-more-gaps-than-its-first-error-showed.md)).
- Pin a third-party AMI (or any external image) to an exact, dated build, not
  a module or package version string, and confirm the lookup resolves to
  exactly one image
  ([lesson 0029](../../docs/lessons/0029-an-ami-pin-that-named-no-image-and-a-tag-that-drifted-in-case.md)).
- Before depending on an AWS service, check that it is still open to new
  accounts — AWS can close one with no signal visible in Terraform or the
  deploy role until the resource is created
  ([lesson 0033](../../docs/lessons/0033-a-service-closed-to-new-accounts.md)).
- Security-group (and rule) descriptions accept only AWS's allowed character
  set — no em dash, curly quote, or other non-ASCII character. Provider
  `default_tags` do not reach an `aws_autoscaling_group`'s own `tag` blocks or
  a third-party module's own `tags` argument — pass `local.tags` explicitly. Guard
  a service by resource name wherever its ARNs carry our name; a tag condition
  holds only for actions where that service evaluates `aws:ResourceTag`, and
  CloudWatch Logs refused our own tagged log groups. A Terraform-managed
  secret version is read back by the provider on create and on every later
  plan, so the deploy and plan roles must be able to read it: keep secret
  values out of Terraform except where both readers need the literal
  ([lesson 0034](../../docs/lessons/0034-terraform-arguments-and-tags-never-checked-against-aws-and-the-deploy-role.md)).
- A failed create can leave a resource tainted; a `prevent_destroy` resource
  that is tainted blocks every later plan — the deploy untaints those before
  applying
  ([lesson 0034](../../docs/lessons/0034-terraform-arguments-and-tags-never-checked-against-aws-and-the-deploy-role.md)).

## Workflow mechanics

- `-chdir=infra` resolves every later relative flag, including `-var-file`,
  from `infra/`, not the repository root — a workflow that uses `-chdir`
  passes an absolute path (`$GITHUB_WORKSPACE/<path>`)
  ([lesson 0028](../../docs/lessons/0028-a-var-file-path-that-only-worked-outside-chdir.md)).
- A local action (`./.github/actions/...`) runs from the checked-out
  workspace — check out before running one, in every workflow. A Lambda
  function created from a container image needs that image already in the
  registry — create the registry and push before the apply that creates the
  function depends on it
  ([lesson 0030](../../docs/lessons/0030-a-deploy-job-that-ran-steps-before-their-own-prerequisites.md)).
- Pull a CI base image from a registry with no per-IP anonymous rate limit —
  mirror it into this org's own registry when the only upstream source has
  one. A retry loop retries only errors that can clear on their own; anything
  else fails on the first attempt
  ([lesson 0032](../../docs/lessons/0032-a-registry-limit-and-a-retry-that-outlasted-nothing.md)).

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
