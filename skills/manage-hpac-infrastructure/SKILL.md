---
name: manage-hpac-infrastructure
description: Maintain HPAC Safety's minimal Canadian AWS, Terraform, deployment, secrets, backups, and focused Worker alerts. Use for infrastructure or operations changes.
---

# Manage HPAC Safety infrastructure

Extends [`design-cloud-infrastructure`](../design-cloud-infrastructure/SKILL.md);
read that first. This skill holds only what is specific to this repository,
under the same section names where they exist, and wins where they differ.

## Target

- Ownership: [`hpac-safety-conventions`](../hpac-safety-conventions/SKILL.md)
  "Before implementing".
- Constraints: the infrastructure-and-operations constraint page, and
  `AGENTS.md` invariant 8 (managed encryption, no deletion).
- In `ca-central-1`: the API and the Worker as Lambda functions (the
  Lambda-hosted API and Worker-on-Lambda decisions), RDS PostgreSQL, private S3 attachment storage, and one website,
  with admin as a route, served from a private S3 bucket through CloudFront,
  which also routes `/api/*` to the API's Function URL — no ALB (the
  one-website-admin-as-a-route, website-on-S3-and-CloudFront, and
  CloudFront-to-Function-URL decisions). The topology and today's Terraform
  differences are in the infrastructure-and-operations constraint page.
- **Two accounts, one Terraform root.** Staging is the owner's personal AWS
  account (synthetic data only); production is a separate, HPAC-owned account
  (real reports), not created from staging and not linked to it. Both build
  from the one `infra/` root, differing only in `infra/staging.tfvars` and
  `infra/production.tfvars`
  (the two-accounts-staged-and-promoted-by-approval decision).
  A dated GitHub Release deploys to staging automatically and never to
  production. A maintainer promotes a staging-green tag with `promote.yml`,
  which deploys the same artifacts to production only after the
  `hpac-safety-admins` team approves the `hpac-safety-production` GitHub
  environment (the release-deploys-staging decision). First goal: staging alone; production
  follows once HPAC's own account and DNS exist.
- Each account groups its resources under its own tag-based Resource Group
  (no AppRegistry application; the tag-based-resource-group decision), `hpac-safety-staging`/`hpac-safety-production` — a cost/grouping
  view, not a security boundary — tagged `Project=HPAC-Safety`,
  `Environment=<staging|production>`, `ManagedBy=terraform`, `Repo=HPAC-Safety/safety-report`
  (the two-accounts decision).
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

## Terraform vs. the deploy role

Lessons from staging's first real release, #613 through #638
([issue #639](https://github.com/HPAC-Safety/safety-report/issues/639)):

- Confirm this repository's actual GitHub OIDC subject form
  (`gh api repos/<org>/<repo>/actions/oidc/customization/sub`) before writing
  a trust policy — `use_immutable_subject: true` changes the subject GitHub
  actually sends (a trust policy named a subject form GitHub no longer sends).
- Every resource name Terraform creates must match an ARN pattern in
  `bootstrap.sh`'s deploy policy, and every tag value it writes must match the
  policy's value exactly, case included (IAM tag comparisons are
  case-sensitive) — check both in the same pull request (an AMI pin that named no image and a tag that drifted in case; Terraform arguments and tags never checked against AWS and the deploy role).
- Audit the deploy role's policy against every resource Terraform creates and
  every AWS call the deploy workflow makes — tag-on-create, later updates, and
  non-tag-scopable services included — before the role's first real use, not
  one error at a time (a deploy role with more gaps than its first error showed).
- Pin a third-party AMI (or any external image) to an exact, dated build, not
  a module or package version string, and confirm the lookup resolves to
  exactly one image (an AMI pin that named no image).
- Before depending on an AWS service, check that it is still open to new
  accounts — AWS can close one with no signal visible in Terraform or the
  deploy role until the resource is created (a service closed to new accounts).
- Security-group (and rule) descriptions accept only AWS's allowed character
  set — no em dash, curly quote, or other non-ASCII character. Provider
  `default_tags` do not reach an `aws_autoscaling_group`'s own `tag` blocks or
  a third-party module's own `tags` argument — pass `local.tags` explicitly. Guard
  a service by resource name wherever its ARNs carry our name; a tag condition
  holds only for actions where that service evaluates `aws:ResourceTag`, and
  CloudWatch Logs refused our own tagged log groups. A Terraform-managed
  secret version is read back by the provider on create and on every later
  plan, so the deploy and plan roles must be able to read it: keep secret
  values out of Terraform except where both readers need the literal (Terraform arguments and tags never checked against AWS and the deploy role).
- A tag condition must name the resource type it guards: AWS evaluates a
  call against every resource it touches, including one it is creating that
  cannot be tagged yet (a security-group rule) and one owned by another
  account (a public AMI). Never set a Lambda-reserved environment variable
  (`AWS_REGION`, `AWS_LAMBDA_*`, `_HANDLER`, and the rest) (a guard checked against the resource a call creates).
- Declare every attribute as AWS records it: a value AWS normalizes (a
  default CloudFront certificate's TLS minimum, an RDS parameter's apply
  method) is a permanent diff that fails the release's drift re-plan (a setting AWS records differently than it was asked).
- A failed create can leave a resource tainted; a `prevent_destroy` resource
  that is tainted blocks every later plan — the deploy untaints those before
  applying (the same Terraform-arguments-and-tags lesson).

## Workflow mechanics

- `-chdir=infra` resolves every later relative flag, including `-var-file`,
  from `infra/`, not the repository root — a workflow that uses `-chdir`
  passes an absolute path (`$GITHUB_WORKSPACE/<path>`) (a var-file path that only worked outside chdir).
- A local action (`./.github/actions/...`) runs from the checked-out
  workspace — check out before running one, in every workflow. A Lambda
  function created from a container image needs that image already in the
  registry — create the registry and push before the apply that creates the
  function depends on it (a deploy job that ran steps before their own prerequisites).
- Pull a CI base image from a registry with no per-IP anonymous rate limit —
  mirror it into this org's own registry when the only upstream source has
  one. A retry loop retries only errors that can clear on their own; anything
  else fails on the first attempt (a registry limit and a retry that outlasted nothing).

## Data

- AWS-managed encryption at rest and TLS.
- Migrations apply at startup: the API and the Worker each run
  `EnsureMigrated` (`MigrationRunner`) under an advisory lock, and there is no migrate job or
  migration deploy step (the EF-Core-migrations-with-SQL-files decision).
- Quarantine unreferenced uploads with lifecycle expiry; keep report-linked
  objects private.

## Secrets and identity

- Runtime secret values live in Secrets Manager, out of Terraform state and out
  of GitHub where deployment does not need them.
- The identity provider's client secret is an ordinary Secrets Manager entry.
- The development JWT signing key is a committed throwaway, not a secret.
  Production holds no signing key; it validates against the provider's
  published keys
  (the JWT-bearer-authentication decision).
- Provider choice is deferred. Residency matters when it is made:
  `ca-central-1` favors AWS Cognito.
- **`AUTH_AUTHORITY` is an external dependency, not chosen here.** Until the
  identity provider exists, the API still starts and serves public pages in
  either environment (the scenario "an environment with no identity provider configured still starts and answers public requests"), but every bearer token is refused, so
  sign-in, filing a report, review, and administration cannot work.

## Operations

- Alert on terminal summary failures and stuck or aged outbox work.
- Keep logs content-free.
- The owner promotes to production.
- Never put report content, a secret, or a state file in a report.

## Remove

SES and email resources, separate public/admin site assumptions (the one-website decision), an
ALB in front of the API (the no-ALB decision), a managed NAT gateway (the
two-accounts decision), and
external publication integrations.
