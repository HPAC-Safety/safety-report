---
title: Two AWS accounts, staging and production, released by date tag and promoted by approval
description: The existing AWS account becomes staging, synthetic data only; a new account becomes production. One release builds once, deploys to staging automatically, then to production after hpac-safety-admins approval of the same artifacts. Each account groups its resources under its own hpac-safety-staging/hpac-safety-production myApplications application, and both run the same Terraform.
type: adr
status: partially-superseded
date: 2026-09-27
decision-makers: Chase Florell
keywords: AWS accounts, staging, production, environments, release, promotion, GitHub environments, AppRegistry, Resource Groups, NAT instance, fck-nat, ADR-0031
---

# ADR-0158 — Two AWS accounts, staging and production, released by date tag and promoted by approval

## Status

**Partially superseded by
[ADR-0166](ADR-0166-a-release-deploys-staging-and-a-separate-workflow-promotes-to-production.md)**:
a release now deploys staging only, and a separate `promote.yml` sends a
staged tag's same artifacts to production after the same approval. "Production
waits" inside the release, "one release workflow", and rollback by re-running
the release no longer hold; everything else below stands.

**Subject form amended by
[ADR-0167](ADR-0167-oidc-trust-names-the-immutable-subject.md):** every trust
subject quoted below as `repo:HPAC-Safety/safety-report:<context>` is
`repo:HPAC-Safety@307760008/safety-report@1341995834:<context>`, GitHub's
immutable form, which is the only form this repository's tokens carry.

Accepted. **Supersedes** the single-environment part of
[ADR-0031](ADR-0031-terraform-shape-and-topology.md) ("There is one
environment: production."). ADR-0031's other decisions — one root module of
flat `.tf` files, no modules directory, no workspaces, S3-native state locking
— stand: both environments still build from that one root, distinguished only
by tfvars.

**Amended 2026-09-28** ([#591](https://github.com/HPAC-Safety/safety-report/issues/591)):
one prefix, `hpac-safety-`, for every name this system owns in a grouping or
approval role. The GitHub org team renames `hpac-admins` → `hpac-safety-admins`;
the GitHub environments rename `hpac-staging`/`hpac-production` →
`hpac-safety-staging`/`hpac-safety-production`; the AWS myApplications
application and Resource Group rename to match. `bootstrap.sh`'s argument,
tfvars file names, `Environment` tag values, and backend state keys are
unaffected and stay `staging`/`production`.

## Context

[Issue #30](https://github.com/HPAC-Safety/safety-report/issues/30) plans
deploying HPAC-Safety to AWS. ADR-0031 rejected a staging environment,
reasoning that a national association receiving dozens of reports a year did
not need one and that synthetic data would be tempting to substitute for real
review. The owner has since decided the opposite, on 2026-09-25, for a
different reason: a release needs somewhere to prove itself — Terraform plan,
image build, and smoke test — before it reaches the database holding real
occurrence reports. ADR-0031's underlying worry, that a report goes into
staging, is closed by making staging hold synthetic data only, never by
skipping the environment.

Nothing here reopens the identity-provider choice (ADR-0064) or the ALB (see
[ADR-0159](ADR-0159-cloudfront-routes-api-to-a-function-url-no-alb.md)).

## Decision

### Two accounts, one Terraform root

- **Staging is the owner's personal AWS account**, which also runs other,
  unrelated workloads. It gets a smaller instance of every resource
  (`db.t4g.micro` RDS with 1-day backups, no alarm subscription) and never
  receives a real report: [ADR-0079](ADR-0079-a-development-login-may-verify-against-the-live-members-site.md)'s
  Development-only members-site login never runs there either, because
  staging is not Development and is not production. Because the account
  holds unrelated workloads, every scoping safeguard in #30's "What the
  deploy role may do" applies in full here: IAM only on `hpac-safety-*`
  roles/policies, S3 only on `hpac-safety-*` buckets, and `Project=HPAC-Safety`
  tag conditions everywhere the service supports them, so the deploy role
  cannot reach the account's other applications.
- **Production is a separate, HPAC-owned account**, created and billed by
  HPAC, sized the same way with 7-day backups and the alarm subscription
  live.
- **The two accounts are not linked.** Production is not created from
  staging through AWS Organizations, and neither account can assume a role in
  the other. `infra/bootstrap.sh` runs independently in each, from that
  account's own CloudShell: staging's run reuses the AWS IAM OIDC provider
  for GitHub Actions that already exists there (never replacing it), and
  production's run creates one, because it is a fresh account with nothing in
  it yet. Both runs create the same `hpac-safety-deploy` and
  `hpac-safety-plan` roles and the same Terraform state bucket shape.
- Both accounts are built from the same `infra/` root module (ADR-0031),
  varying only by `infra/staging.tfvars` and `infra/production.tfvars`.
  `diff` between them is the complete list of differences; nothing
  environment-specific is hard-coded elsewhere in the Terraform.
- **Sequencing: staging first.** The first goal is a working release pipeline
  against staging alone. Production is created, bootstrapped, and wired up
  once the HPAC-owned account exists and its DNS is ready; nothing in the
  release workflow requires both to exist at once, because the `production`
  GitHub environment's approval gate simply has nothing to promote to until
  then.

### Grouping

Each account gets its own AWS **myApplications** application (Service
Catalog AppRegistry) and tag-based Resource Group, named for the account it
groups — **`hpac-safety-staging`** and **`hpac-safety-production`** — so every resource the
system owns is visible in one place per account even though staging's account
also hosts unrelated applications. This is a grouping and cost-visibility
tool, not a security boundary; the tags are what a policy condition actually
checks. Every resource carries the tags `Project=HPAC-Safety`,
`Environment=<staging|production>`, `ManagedBy=terraform`, and
`Repo=HPAC-Safety/safety-report`, unchanged by the per-account application
name.

### Hostnames

- **Production** serves `safety.hpac.ca` (English) and `securite.acvl.ca`
  (French) from one CloudFront distribution with one `us-east-1` ACM
  certificate covering both names. This is the second production hostname
  #461 was asked to record: DNS for both `hpac.ca` and `acvl.ca` stays with
  their current hosts outside AWS, so a human adds the CNAME and certificate
  validation records once (issue #30, "Human work" H7). Which language a
  first-time visitor sees by hostname is #463's decision, layered on top of
  this pair existing.
- **Staging** uses only the default `*.cloudfront.net` address. It needs no
  certificate and no external DNS entry.

### Release and promotion

- A maintainer publishes a GitHub Release tagged with the date `YYYY.MM.DD-N`
  (for example `2026.10.02-1`).
- **Build once.** The release workflow builds the API image, the Worker
  image, and the web bundle exactly once, tagged by commit SHA.
- **Staging deploys on its own**, against the `hpac-safety-staging` GitHub environment
  (deployment branch/tag rule `20*`, no required reviewers): `terraform
  apply`, push images, update both Lambda functions, sync the site bundle,
  invalidate CloudFront, smoke-test `/api/health`.
- **Production waits.** The `hpac-safety-production` GitHub environment requires
  approval by the `hpac-safety-admins` org team before its job runs. It then repeats the
  same steps against the production account, deploying the **same image
  digests and the same web bundle** staging already ran — never a rebuild.
- **There is no apply on merge to `main`.** A pull request still gets a
  Terraform plan against both accounts (ADR-0032, ADR-0148), so drift and
  breakage surface before a release, but nothing changes either account
  outside a published release.
- **Rollback** is re-running the release job for an earlier tag; it deploys
  that tag's already-built artifacts again, to either or both accounts.

### Deploy credentials

Both accounts are reached only by short-lived OIDC roles, one per account per
purpose. Staging reuses the AWS IAM OIDC provider for GitHub Actions that
already exists in the owner's personal account (never replacing it);
production's `bootstrap.sh` run creates one, because it is a fresh account.

- **`hpac-safety-deploy`** (used by the release job), trusted only by
  `repo:HPAC-Safety/safety-report:environment:hpac-<staging|production>`.
  - **Can manage:** CloudFront, ACM, Lambda, ECR, EventBridge and Scheduler,
    RDS, VPC networking (EC2), S3, Secrets Manager (create entries and put
    values), CloudWatch and Logs, SNS, KMS (the AWS-managed keys), Service
    Catalog AppRegistry, Resource Groups, and tagging.
  - **Kept to HPAC-Safety resources:** IAM only on roles and policies named
    `hpac-safety-*`, and `iam:PassRole` only to Lambda and Scheduler; S3 only
    on `hpac-safety-*` buckets; `Project=HPAC-Safety` tag conditions wherever
    the service supports them — this scoping, not the AppRegistry/Resource
    Group grouping, is what keeps staging's deploy role off the account's
    other, unrelated applications.
  - **Explicitly denied:** reading uploaded report files, reading database
    logs, creating IAM users or access keys, changing OIDC/SAML providers,
    Organizations, or account settings, and editing its own role.
- **`hpac-safety-plan`** (used by the pull-request plan job), trusted only by
  this repository's pull requests: AWS `ReadOnlyAccess` plus reading
  Terraform state. Denied upload objects, secret values, `rds-data`, and
  database logs.

No long-lived AWS access key exists in either account.

### Alarms: production only

The SNS topic that pages a human exists in both accounts, but only
production's has a subscription: alarm email goes to `safety@hpac.ca` in
production only. Staging's topic has no subscriber, because staging never
holds a real report and nobody is on call for it. #467 defines the alarm set
this topic carries.

### The NAT instance is the one resource that is ever replaced

Outbound internet (the identity provider's signing keys, Gemini, DeepL) goes
through a **NAT instance** — `fck-nat` on a `t4g.nano`, pinned by Renovate and
kept current — in a one-instance Auto Scaling group, in both accounts,
instead of a managed NAT gateway. A NAT gateway would cost roughly $36/month
per account against roughly $4/month for the instance plus its public IPv4
address; at HPAC's traffic this difference buys nothing durability-relevant,
because the instance is stateless (it forwards traffic and stores no data) and
heals on its own between releases.

- **Every release deletes and recreates it.** A fresh instance each release
  means it never runs long enough to drift from its pinned image or fill up.
- **It is the only resource this system ever deletes and recreates.** The RDS
  instance, the uploads bucket, Secrets Manager entries, and CloudWatch log
  groups are created once, updated in place, and protected from deletion —
  `prevent_destroy` plus deletion protection where AWS offers it — in both
  environments. A Terraform plan that would destroy any of them fails review.
- **Self-healing between releases.** The one-instance Auto Scaling group
  replaces a failed instance on its own health check, without a human or a
  redeploy. If the alarm on it persists anyway, re-running the release job
  recreates it.
- **The fallback, if this project is ever abandoned, is a managed NAT
  gateway** — a one-resource Terraform swap, not a redesign.

### Design principles carried forward from #30

These govern every slice of #30, not only this one, and are recorded here
because this is the ADR that first writes the two-environment shape down:

1. One way to do each thing: one Terraform root (`infra/`), one bootstrap
   script (`infra/bootstrap.sh`), one release workflow (`release.yml`), one
   plan workflow (`terraform.yml`). No modules of our own, no workspaces, no
   Terragrunt.
2. The two environments differ only in their tfvars.
3. Everything is code. Apart from the one-time steps in #30's "Human work",
   nobody changes anything in the AWS console; the release re-plans after it
   applies and fails on drift.
4. Only the NAT instance is ever deleted and recreated; every other resource
   is created once and updated in place.
5. No hidden steps: every manual action is written down, everything else runs
   from GitHub.
6. Written down where an operator will look: every Terraform file opens with
   a comment on what it creates and why; `infra/README.md` maps each AWS
   service to its file; `docs/deployment.md` is the operator guide.
7. Kept current by automation: Renovate updates images, Actions, and the
   pinned NAT image; `terraform-relock.yml` refreshes provider locks.
8. Boring over clever: an abstraction is added only when a second real
   implementation needs it (AGENTS.md).

## Alternatives considered

- **One account, feature-flagged "environments" inside it.** Rejected: a
  Terraform bug or an over-broad IAM policy in "staging" could still reach
  production resources in the same account. Two accounts make that a hard
  boundary, not a convention.
- **A managed NAT gateway in both accounts.** Simpler to reason about, and
  still the documented fallback, but roughly 9x the monthly cost for a
  resource that carries no data and needs no durability.
- **Apply Terraform on every merge to `main`.** Rejected: it would mean two
  code paths change AWS (merge and release), and a merge is not yet something
  anyone has decided to ship. A release is the one thing that changes either
  account.

## Consequences

- `infra/staging.tfvars` and `infra/production.tfvars` must exist and their
  `diff` must be the whole story; a review that finds environment-specific
  logic anywhere else in the Terraform is a defect.
- Issue #30's "Human work" H1–H3 (HPAC creates its own AWS account for
  production, a maintainer sets up the `hpac-safety-admins` team and both GitHub
  environments, and `bootstrap.sh` runs independently in each account) is the
  one-time setup this ADR assumes exists before a release can run end to end
  against both accounts. Staging alone needs only H2's `hpac-safety-staging` environment
  and H3 run once, against the owner's existing account.
- `docs/infrastructure-and-operations.md`, `docs/deployment.md`,
  `features/README.md`, and the `manage-hpac-infrastructure` skill are updated
  in the same pull request as this record (ADR-0047).
- [ADR-0009](ADR-0009-hosting-on-aws.md) and
  [ADR-0118](ADR-0118-the-worker-image-installs-ubuntus-ffmpeg.md) each had a
  status line pointing only at ADR-0123's Fargate-to-Lambda move; both are
  corrected in this pull request to also name this record where they discuss
  environments, so a reader following either stale line lands on the current
  target.
- **The identity provider is an external dependency, not decided here**
  ([ADR-0064](ADR-0064-jwt-bearer-authentication-with-three-roles.md)). Until
  `AUTH_AUTHORITY` is set for an environment — staging or production — that
  environment can deploy public pages and submission, but sign-in, review,
  and administration cannot work there. This is a stated limitation, not
  something either environment works around.
- Vendor keys (`GEMINI_API_KEY`, `DEEPL_API_KEY`) are separate Secrets
  Manager entries per environment, holding the same values for now; upload
  size caps (250 MB video, 25 MB image or document) are unchanged by having
  two environments — both enforce the same caps.
