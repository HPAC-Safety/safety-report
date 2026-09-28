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
release will do once #466 lands, not something already running. See "Where
today's Terraform differs" below for exactly what is still scaffolding.

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

**Releasing** ([`release.yml`](../.github/workflows/release.yml),
[`deploy-environment.yml`](../.github/workflows/deploy-environment.yml)):

1. **Publish a GitHub Release** on `main`, tagged with the date,
   `YYYY.MM.DD-N` (e.g. `2026.10.02-1`). This is the only thing that triggers
   `release.yml`, and the only human action every release afterwards needs
   (issue #30 "Human work" H6). Any other tag shape is rejected before
   anything is built.
2. **`build`** checks out that tag and, with no AWS credential of any kind,
   builds the API image (the Lambda Web Adapter image, `tools/build-api-image.sh`,
   #443), the Worker image (`tools/build-worker-image.sh`, ADR-0118), and the
   web bundle, each tagged by the commit SHA the release tag points to. All
   three are uploaded as workflow artifacts — nothing is pushed to either
   account's ECR yet.
3. **`staging`** (GitHub environment `hpac-safety-staging`, no required reviewer)
   loads those same artifacts, runs `terraform apply -var-file=infra/staging.tfvars`,
   re-plans and fails the job on drift, replaces the NAT instance, pushes the
   images to staging's ECR, refreshes `GEMINI_API_KEY`/`DEEPL_API_KEY` in
   staging's Secrets Manager, updates both Lambda functions to the pushed
   image digests, syncs the web bundle to the site bucket, invalidates
   CloudFront, and smoke-tests `/api/health`.
4. **`production`** (GitHub environment `hpac-safety-production`) needs `staging` to
   succeed, then waits for the `hpac-safety-admins` team's approval on that
   environment — configured on the environment itself, not in the workflow —
   before repeating the same steps against the production account with
   `infra/production.tfvars` and the **identical image digests and web
   bundle** staging already deployed. Never a rebuild
   (CON-INF-012). Its job summary prints `dns_records_to_publish` — meaningful
   the first time production exists (issue #30 "Human work" H7), harmless
   every other run.

There is no `terraform apply` on a merge to `main`
([`terraform.yml`](../.github/workflows/terraform.yml)). A pull request only
plans, against both accounts, through its own `plan` job (a matrix, one leg
per account) — never through the `hpac-safety-staging`/`hpac-safety-production` GitHub
environments, because both restrict deployment to the release tag pattern and
would simply never run a pull-request-triggered job. `plan` instead uses a
same-repo pull request's own AWS role/state-bucket pair, one per account
(below).

**Rollback** is re-running the release for an earlier tag:

- **Preferred**: open that release's own workflow run under **Actions** and
  choose **Re-run all jobs**. This rebuilds from the exact commit that tag
  already pointed to and redeploys it to whichever environment(s) you pick —
  functionally identical to the original run, because the same commit and the
  same build scripts produce the same image content and therefore the same
  registry digest.
- **If that run has aged out** (workflow artifacts expire; see the
  `retention-days` on `release.yml`'s `upload-artifact` steps), dispatch
  `release.yml` manually (`workflow_dispatch`) with that earlier tag. This
  rebuilds from the tagged commit rather than reusing the original run's
  artifacts — use it only when the native re-run is unavailable.
- Either way, migrations stay expand/contract (CON-INF-007): a rollback
  redeploys an artifact, and the schema already supports it.

Runtime secret values stay out of source control and Terraform state. Use
AWS-managed encryption at rest and TLS.

### Required GitHub configuration

Set once per account, by issue #30 "Human work" H2–H4 and `infra/bootstrap.sh`
(#464, #466, #465):

| Name | Kind | Scope | Used by |
|---|---|---|---|
| `AWS_DEPLOY_ROLE_ARN` | variable | `hpac-safety-staging` / `hpac-safety-production` environment | `release.yml` → `deploy-environment.yml` (OIDC role assumed by the deploy job) |
| `TF_STATE_BUCKET` | variable | `hpac-safety-staging` / `hpac-safety-production` environment | same — Terraform backend bucket |
| `AWS_ACCOUNT_ID` | variable | `hpac-safety-staging` / `hpac-safety-production` environment | same — `require-config` only; not otherwise read by the workflow |
| `GEMINI_API_KEY`, `DEEPL_API_KEY` | secret | `hpac-safety-staging` / `hpac-safety-production` environment | same — copied into that account's Secrets Manager on every release, never logged |
| `AWS_PLAN_ROLE_ARN_STAGING`, `AWS_PLAN_ROLE_ARN_PRODUCTION` | variable | repository | `terraform.yml`'s `plan` job — read-only OIDC role per account, one matrix leg each |
| `TF_STATE_BUCKET_STAGING`, `TF_STATE_BUCKET_PRODUCTION` | variable | repository | same — Terraform backend bucket per account, read-only |

The `_STAGING`/`_PRODUCTION`-suffixed pair is a **second** copy of two values
`infra/bootstrap.sh` already prints — the same `AWS_PLAN_ROLE_ARN` and
`TF_STATE_BUCKET`, set as plain repository variables instead of
`hpac-safety-staging`/`hpac-safety-production` environment variables. Both
copies have to exist: `hpac-safety-plan`'s OIDC trust condition matches the
subject `repo:HPAC-Safety/safety-report:pull_request` (bootstrap.sh's own
comment on the plan role), which a job presents only when it does **not**
declare `environment:` — the moment a job adds `environment: hpac-safety-staging`
to read that environment's variables, its subject becomes
`repo:...:environment:hpac-safety-staging` instead, which is what
`hpac-safety-deploy` trusts, not what `hpac-safety-plan` trusts. On top of
that, both environments' deployment branch/tag policy (issue #30 "Human work"
H2) restricts them to the release tag pattern, so a pull-request-triggered
job scoped to either would be refused before any step ran anyway.

`infra/bootstrap.sh` prints both copies together, in the same run, under
their own "Repository variables" heading (ADR-0164) — there is no separate,
hand-run step. Running it once per account and setting everything it prints
is the whole H3:

```sh
gh variable set AWS_PLAN_ROLE_ARN_STAGING --repo HPAC-Safety/safety-report --body <printed AWS_PLAN_ROLE_ARN_STAGING>
gh variable set TF_STATE_BUCKET_STAGING   --repo HPAC-Safety/safety-report --body <printed TF_STATE_BUCKET_STAGING>
# and the _PRODUCTION pair from the production account's run
```

This naming (the `_STAGING`/`_PRODUCTION` suffix) was decided in this pull
request, not by #464 or #591 — `infra/bootstrap.sh` was extended in this pull
request to print it.

Every step that reads an AWS resource name — ECR repositories, the Lambda
function names, the site and uploads buckets, the CloudFront distribution,
the NAT instance's Auto Scaling group, the Gemini/DeepL secret ids — comes
from `terraform output`, never a GitHub variable, so `infra/` stays the one
place those names are decided: `deploy_variables`, and the standalone
outputs `nat_autoscaling_group_arn`, `secret_entries`, `site_urls`, and
`dns_records_to_publish`. `node tools/check-terraform-outputs.mjs` (`ci.yml`'s
`docs` job) fails the build if `release.yml` or `deploy-environment.yml` ever
reads an output name or JSON key `infra/outputs.tf` doesn't declare.

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

The Terraform and the application code now match this shape (#443, #465):
Lambda functions with no ALB, origin-secret verification, the Worker's
Lambda-invocation mode, the API's async nudge, `staging.tfvars`/
`production.tfvars`, both accounts' AppRegistry applications, both hostname
sets, and a NAT instance instead of a managed NAT gateway. `release.yml` and
`deploy-environment.yml` (#466) are now aligned against the real
`infra/outputs.tf` this Terraform declares. Issue #30 tracks what remains;
see
[`infrastructure-and-operations.md`](infrastructure-and-operations.md)'s
"Where today's Terraform differs" for exactly what is still open.
Do not interpret a successful Terraform validation as proof that the target
environment exists or has been applied — **no AWS deployment exists yet**,
and nothing in `infra/` runs against AWS on merge.

## Estimated monthly cost

Estimated from AWS's published `ca-central-1` list prices as of this pull
request. **This is an estimate, not an AWS Pricing Calculator export** — #465
was written without console access to produce one; someone with console
access should still confirm it in the AWS Pricing Calculator before the first
real deploy, per issue #30's acceptance criteria.

| Resource | Staging (per month) | Production (per month) | Basis |
|---|---|---|---|
| RDS `db.t4g.micro`, 20 GB gp3, single-AZ | ~US$13 | ~US$13 | On-demand instance-hour rate × 730 h, plus 20 GB gp3 storage |
| RDS automated backup storage | ~US$0 (1-day retention, within the free allowance) | ~US$1–2 (7-day retention) | Backup storage beyond the free allowance equal to the database's own size |
| NAT instance (`t4g.nano`) | ~US$3 | ~US$3 | On-demand instance-hour rate × 730 h |
| NAT instance's Elastic IP | ~US$4 | ~US$4 | A public IPv4 address is billed hourly whether or not attached |
| Secrets Manager (Gemini, DeepL, origin-verify; the RDS master password is billed separately) | ~US$1.20 | ~US$1.20 | US$0.40/secret/month × 3 |
| Lambda (API + Worker) | ~US$0–1 | ~US$1–3 | Dozens of reports a year; well within the perpetual free tier's request and compute allowances outside a burst of video remuxing |
| S3 (site + uploads) | <US$1 | ~US$1–3 | Storage plus PUT/GET requests; grows with attachment volume |
| CloudFront | <US$1 | ~US$1–2 | PriceClass_100, low request volume |
| ECR | <US$1 | <US$1 | Two small image repositories, 30-image lifecycle |
| CloudWatch Logs + alarms | <US$1 | ~US$1 | 90-day retention on a handful of small log groups |
| ACM certificate | — | $0 | ACM certificates for CloudFront are free |
| **Total** | **~US$22–25** | **~US$25–30** | |

Both together: roughly **US$50–55/month**, close to issue #30's original
US$45–50 estimate — the difference is mostly the NAT instance's Elastic IP,
which #30's estimate already itemized separately at ~US$3.65 and this table
rounds up slightly for margin. Prices are US dollars per AWS's published rate
card; actual CAD billing depends on the account's currency settings and
fluctuates with exchange rates.

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
