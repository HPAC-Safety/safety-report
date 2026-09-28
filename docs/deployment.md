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
  application and Resource Group — **`hpac-safety-staging`** and
  **`hpac-safety-production`** — a grouping and cost-visibility tool, not a security
  boundary; the myApplications application still relies on the
  `Project=HPAC-Safety` tag, since its ARN carries an opaque id rather than
  this name, but the Resource Group's own ARN carries the name, so it is
  scoped by name as well. Both accounts are reached only by their own short-lived GitHub
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
waits for the `hpac-safety-admins` GitHub team to approve the `hpac-safety-production` environment
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
ever deletes and recreates, and every release does so; everything else —
including the RDS instance, the uploads bucket, secrets, and log groups,
which additionally carry `prevent_destroy` — is created once and updated in
place, protected from deletion.

Alarms route through SNS to `safety@hpac.ca`, in production only; staging's
topic has no subscriber.

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

## Bootstrapping each account's roles (once per account)

See "Environments and accounts" above for what staging and production each
are. Each account gets its own `hpac-safety-deploy` role (trusted only by
GitHub Actions jobs running under that account's matching GitHub
*environment*, named `hpac-safety-staging` and `hpac-safety-production`, not just
`staging`/`production` — see below), its own `hpac-safety-plan` role, and its
own Terraform state bucket. Every scoping safeguard described below is
load-bearing, not defensive dressing: in the shared staging account, it is
the only thing keeping this system's deploy role off somebody else's
resources. Nothing is shared between the two accounts, and there is no AWS
Organizations relationship between them — one is never reached by "switch
role" from the other.

Do this once in **each** account: the owner's personal account for `staging`,
and HPAC's account for `production`. Nothing here needs a password or key to
be shared with anyone — the whole point of OIDC is that GitHub Actions
authenticates without one, and this script itself runs under whatever session
you already have open, never a new long-lived credential.

1. Sign in to that AWS account directly, as an administrator —
   <https://console.aws.amazon.com>. There is no organization to switch roles
   through; each account is its own sign-in.
2. At the top right, set the region to **Canada (Central) ca-central-1**.
3. Click the **CloudShell** icon (`>_`) in the top bar and wait for the
   prompt. CloudShell already has the AWS CLI and a POSIX shell; nothing
   needs installing.
4. Paste one line. For the staging account:
   ```sh
   git clone https://github.com/HPAC-Safety/safety-report && sh safety-report/infra/bootstrap.sh staging
   ```
   For the production account, use `production` in place of `staging`.
5. `infra/bootstrap.sh` is idempotent: re-running it in the same account
   converges the existing OIDC provider, roles, policies, and state bucket
   onto the current definitions and changes nothing else. It tolerates — and
   never modifies or deletes — an OIDC provider, client ID, or thumbprint that
   another application already created in that account; it only adds the
   `sts.amazonaws.com` audience if that one is missing.
6. It creates, in that account only:
   - the GitHub OIDC identity provider (or reuses one that exists);
   - `hpac-safety-deploy`, trusted only by
     `repo:HPAC-Safety/safety-report:environment:<hpac-safety-staging|hpac-safety-production>`
     — exactly the subject a job with `environment: hpac-safety-staging` (or
     `hpac-safety-production`) presents, and nothing else. The script argument stays
     `staging`/`production`; it maps that to the GitHub environment's actual
     name (`hpac-safety-staging`/`hpac-safety-production`) for the trust condition, while
     the AWS-side `Environment` tag and the Terraform state key stay
     `staging`/`production`;
   - `hpac-safety-plan`, trusted only by this repository's pull requests, with
     `ReadOnlyAccess` plus Terraform state read, and explicit denies on
     uploaded report objects, log/RDS-log content, secret values, `rds-data`,
     and any object, function, image, parameter, table, stream, or queue
     outside this system's own resources — `ReadOnlyAccess` alone would let
     a plan on any pull request read data belonging to staging's other,
     unrelated applications;
   - the Terraform state bucket `hpac-safety-tfstate-<account-id>`
     (versioned, encrypted, public access blocked, TLS-only).
7. It prints four `NAME=value` lines on stdout:
   ```
   AWS_DEPLOY_ROLE_ARN=arn:aws:iam::<account-id>:role/hpac-safety-deploy
   AWS_PLAN_ROLE_ARN=arn:aws:iam::<account-id>:role/hpac-safety-plan
   TF_STATE_BUCKET=hpac-safety-tfstate-<account-id>
   AWS_ACCOUNT_ID=<account-id>
   ```
   In GitHub, open **Settings → Environments → `hpac-safety-staging`** (or
   **`hpac-safety-production`**) **→ Environment variables**, and add each one.
   These are identifiers, not secrets — no GitHub secret is ever set from
   this script's output.

What `hpac-safety-deploy` may do, and what it may never do, is documented in
the "What the deploy role may do" section of #30 and enforced by four
customer-managed policies `infra/bootstrap.sh` attaches to it (one managed
policy is limited to 6144 characters; a policy this thorough about
tag/name scoping does not fit in one):

- **`hpac-safety-deploy`** — read/write its own Terraform state object,
  manage `hpac-safety-*` S3 buckets, Secrets Manager secrets, Lambda
  functions, ECR repositories, SNS topics, EventBridge rules, Scheduler
  schedules, and its own log groups **by name**, and — because neither RDS
  nor Auto Scaling has a context key that ties a tag-adding call to the
  create call that needed it — tags only an RDS resource named
  `db`/`subgrp`/`pg`/`snapshot`/`cluster`:`hpac-safety*`, or an Auto Scaling
  group named `hpac-safety*`.
- **`hpac-safety-deploy-services`** — manages the account's one Resource
  Group **by name** too (`group/<hpac-safety-staging|hpac-safety-production>`,
  matching this account's own GitHub environment name, now that #591 gave it
  the `hpac-safety-` prefix), the one exception being CloudFront/ACM-style
  services and the AppRegistry application itself, which keep tag-only
  scoping — the myApplications application's ARN carries an AWS-assigned
  opaque id, never the name, so there is no ARN pattern to write for it in
  advance. For the remaining services with no HPAC-Safety-only name pattern
  to scope by (ACM, Auto Scaling, CloudFront, CloudWatch, EC2, RDS,
  AppRegistry): read-only metadata
  broadly, `Create*` only when the request carries the `Project=HPAC-Safety`
  tag (`aws:RequestTag`, a real `StringEquals`, not `IfExists`), and
  `ec2:CreateTags` only when AWS's own `ec2:CreateAction` context key names
  one of those same create calls — a key EC2 populates only when tagging is
  bundled into a genuine create request, never for a standalone `CreateTags`
  call. KMS use (not administration) of the AWS-managed keys is scoped to
  calls made *via* the services this system actually uses (see "KMS" below).
- **`hpac-safety-deploy-guardrails`** — denies every mutating verb
  (`Delete*`/`Modify*`/`Update*`/`Put*`/`Stop*`/`Start*`/`Reboot*`/
  `Terminate*`/`Attach*`/`Detach*`/`Associate*`/`Disassociate*`/
  `Authorize*`/`Revoke*`, and adding or removing a tag through any action
  other than the two carve-outs above) on a resource that is not
  **already** tagged `Project=HPAC-Safety` (`aws:ResourceTag`, a real
  `StringNotEquals`, with no `IfExists` — an untagged resource is denied
  exactly like one tagged for someone else's project). This is what makes
  the tag condition an actual guard: a `StringEqualsIfExists` check alone
  passes whenever a resource carries **no** `Project` tag at all, which
  describes almost every resource belonging to staging's other, unrelated
  applications — and it is also what closes the tag-hijack path: without
  it, `aws:RequestTag` alone would let this role stamp
  `Project=HPAC-Safety` onto ANY existing, untagged resource in the
  account, after which this same guard would treat it as ours and let it
  be modified or deleted.
- **`hpac-safety-deploy-iam`** — the IAM/identity portion: roles, policies,
  and instance profiles (for the NAT instance, #465) named `hpac-safety-*`;
  `iam:PassRole` only to those roles and only with `iam:PassedToService` in
  `[lambda.amazonaws.com, scheduler.amazonaws.com, ec2.amazonaws.com]`; and
  the explicit denies: never read a secret value even its own, never read
  an uploaded report file or RDS/log content, never read another
  application's Lambda function, ECR image, SSM parameter, DynamoDB item,
  Kinesis record, or SQS message, never create an IAM user or access key,
  touch an OIDC/SAML provider, Organizations, or the account, or edit its
  own role.

**Two narrow, named exceptions** to the untagged-mutation guard, because
they touch a resource that legitimately has no HPAC-Safety tag and never
will: `ec2:RunInstances` (a Create verb, so it is governed by the
tag-on-create guard instead) also names the public `fck-nat` AMI (#465) as
a resource in the same call; and the handful of EC2 calls that attach a
brand-new VPC/subnet/route table to itself
(`ec2:AttachInternetGateway`, `ec2:AuthorizeSecurityGroupIngress`, and
similar — see `EstablishNetworkAttachmentsOnResourcesWeJustCreated` in
`infra/bootstrap.sh`) are allowed unconditionally by that one statement —
but every one of those same verbs is *also* in the guardrails deny list, so
the untagged-mutation guard still applies on top and requires the resource
on the other end (a subnet, a route table, a security group) to already
carry the tag this role's own creation calls put there moments earlier.
Nothing here is actually excluded from the guard. `ec2:CreateRoute` is
named explicitly in the guardrails deny list rather than covered by a verb
wildcard, because — unlike every other `Create*` action — it mutates an
existing route table rather than creating a new resource.

**KMS**: `kms:Decrypt`/`Encrypt`/`GenerateDataKey*` are allowed only with
`kms:ViaService` naming the services this system uses (RDS, Secrets
Manager, S3, Lambda, Logs, ECR) — a customer-managed key's own key policy,
not this IAM policy, is usually what actually delegates decrypt access, so
without this condition the role could decrypt data under another
application's key too. `kms:CreateGrant`/`ListGrants`/`RevokeGrant` are
allowed only with `kms:GrantIsForAWSResource: true`. A deny closes the gap
those two conditions leave open: any of the five actions above **without**
`kms:ViaService` present at all — a direct call to KMS, not AWS calling KMS
on this role's behalf — is refused outright. `DescribeKey`/`ListAliases`
stay unconditional, since they return metadata, not data.

**Residual risk, recorded rather than hidden**: nothing in this repository
can call AWS, so none of the above has been exercised against a real AWS
account — it is reviewed by inspection, JSON validation, and shellcheck
only, the same as `infra`'s required CI check does. The exact action
lists, the four-policy split, and the two named exceptions are a
best-effort, reviewable starting point, refined twice already through
review rather than testing. One specific, named risk this leaves: if the
Terraform AWS provider ever tags an ACM certificate, a CloudFront
distribution, a CloudWatch alarm, an AppRegistry application, or a
Resource Group with a *separate* API call after creating it, rather than
through that create call's own tag parameter, that specific call is
refused on the first real `apply` — a loud, narrow failure to fix, not a
silent security gap. More generally: an AWS action this policy did not
anticipate, a service added to the deploy role's scope without a matching
name-scope or tag guard, or an untested interaction between the
`CreateOnlyAsOurProject`/`TagOnlyAtEc2CreationTime` and
`NeverMutateAnUntaggedResource` statements, should be checked for on the
first real `apply` in the staging account, before production is
bootstrapped. The NAT instance's exact IAM role/instance-profile and Auto
Scaling group names are not yet settled (#465 is unmerged); coordinate the
`hpac-safety-*` naming this policy assumes with that work before the first
apply.
