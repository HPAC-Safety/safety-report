# Deployment

How the four deployables reach AWS, what credentials that needs, and where each
one lives. Hosting rationale is [ADR-0009](decisions/ADR-0009-hosting-on-aws.md);
the infrastructure itself is Terraform, per
[ADR-0010](decisions/ADR-0010-infrastructure-as-code.md).

> **Status: scaffold.** The workflows exist and are wired. The three `deploy-*`
> workflows fail with a clear message because the AWS environment has not been
> created yet; `terraform.yml` validates `infra/` on every pull request and skips
> its AWS jobs with a notice for the same reason. The Terraform itself is written
> and lives in [`infra/`](../infra/README.md) — it has never been applied.
> Creating the account and running the bootstrap is
> [#32](https://github.com/HPAC-Safety/safety-report/issues/32); filling in the
> AWS calls in the deploy workflows is
> [#30](https://github.com/HPAC-Safety/safety-report/issues/30).

## What deploys, and from where

| Workflow | Deploys | Target |
|---|---|---|
| `.github/workflows/terraform.yml` | `infra/` | The AWS environment itself |
| `.github/workflows/deploy-api.yml` | `src/HpacSafety.Api` | ECS Fargate service |
| `.github/workflows/deploy-worker.yml` | `src/HpacSafety.Worker` | ECS Fargate service |
| `.github/workflows/deploy-web.yml` | `src/web/public`, `src/web/admin` | One S3 bucket + one CloudFront distribution |

### Where it all answers

| URL | Serves |
|---|---|
| `https://safety.hpac.ca/` | The public report form |
| `https://safety.hpac.ca/admin/` | The admin review queue |
| `https://api.hpac.ca` | The API. **HTTPS only**; port 80 redirects. |

**One website, not two.** The review queue is a route on `safety.hpac.ca`, served
from the same bucket and the same CloudFront distribution as the public form.
This supersedes ADR-0009's "one distribution each for public and admin", and it
supersedes the `S3_BUCKET_PUBLIC` / `S3_BUCKET_ADMIN` and
`CLOUDFRONT_DISTRIBUTION_PUBLIC` / `CLOUDFRONT_DISTRIBUTION_ADMIN` variables named
in [#32](https://github.com/HPAC-Safety/safety-report/issues/32).

The consequence to hold on to: **there is no network control in front of the
review queue and there cannot be one that does not also apply to the public
form.** WAF, geo restriction, and IP allowlisting are distribution-level in
CloudFront. What protects the queue is the API's authorization
([#24](https://github.com/HPAC-Safety/safety-report/issues/24)); the admin bundle
is static assets holding no report data. `/admin/*` does get its own cache
behavior, `X-Robots-Tag: noindex, nofollow`, and `Cache-Control: no-store`. The
reasoning and what was rejected are in
[ADR-0031](decisions/ADR-0031-terraform-shape-and-topology.md).

Containers are produced by `dotnet publish -c Release /t:PublishContainer`.
.NET 10 emits an OCI image directly, so there is no Dockerfile to drift from the
project file.

```mermaid
flowchart LR
    pr["pull request"] --> ci["CI<br/>build · test · coverage · web<br/>e2e · agent-config · i18n"]
    ci --> merge["squash merge to main"]
    merge --> ci2["CI on main"]
    ci2 -->|"workflow_run: success"| dep["Deploy API · Worker · Web"]
    ci2 -->|"failure"| stop["no deploy"]
    dep --> env["environment: production<br/>required reviewer"]
    env --> aws["AWS ca-central-1"]
```

Every deploy workflow triggers on `workflow_run` of **CI**. A red build cannot
deploy, and neither can an unmerged one. Each also has `workflow_dispatch`, so a
rollback never requires a commit.

### The trigger guard

`workflow_run`'s `branches: [main]` filter matches the *triggering run's*
`head_branch`, and a `pull_request`-triggered CI run reports its `head_branch` as
the pull request's **source** branch — a green CI run on a feature branch here
reports `head_branch: ci/pipeline-and-deploy-scaffolding`, not `main`.

That filter is therefore doing load-bearing work, and its exact semantics are not
worth betting an ECR push on. Every `preflight` job carries an explicit guard as
well:

```yaml
if: >-
  github.event_name == 'workflow_dispatch' ||
  (github.event.workflow_run.event == 'push' &&
   github.event.workflow_run.head_branch == 'main' &&
   github.event.workflow_run.conclusion == 'success')
```

`event == 'push'` is the important one. It admits only a merge to `main`, never a
pull request run and never a dispatched CI run. Without it, a green CI run on an
open pull request could reach `image`, which is **not** behind the `production`
environment — the required reviewer guards `migrate` and `deploy` only — and push
a container built from unreviewed, unmerged code.

### Image tags

`IMAGE_TAG` is `github.event.workflow_run.head_sha`, never `github.sha`.

On a `workflow_run` trigger `github.sha` is the tip of the default branch **at
run time**, not the commit CI tested. If a second pull request merges between CI
finishing and the deploy workflow starting, `github.sha` moves and the image is
tagged with a commit it was not built from. Rollback is by tag, so that failure
stays silent until the day it matters.

`head_sha` is the commit CI tested, and is what every checkout step uses.

## Authentication: OIDC, no stored AWS keys

Workflows assume an IAM role through GitHub's OIDC provider:

```yaml
permissions:
  id-token: write
  contents: read
```

There is no `AWS_ACCESS_KEY_ID` and no `AWS_SECRET_ACCESS_KEY` anywhere in this
repository, and there never should be.

The role's trust policy must be scoped to **this repository** and to
`ref:refs/heads/main`. A role that trusts `repo:HPAC-Safety/*:*` would let any
repository in the org deploy this system; a role that trusts
`repo:HPAC-Safety/safety-report:*` would let any branch — including one pushed
to a fork — do the same.

```json
{
  "Condition": {
    "StringEquals": {
      "token.actions.githubusercontent.com:aud": "sts.amazonaws.com",
      "token.actions.githubusercontent.com:sub": "repo:HPAC-Safety/safety-report:ref:refs/heads/main"
    }
  }
}
```

### Two roles, because a pull request is not a branch

`terraform plan` runs on pull requests, and a `pull_request`-triggered workflow
presents the subject `repo:HPAC-Safety/safety-report:pull_request` — not a branch
ref. It cannot assume the role above, and widening that role so it could is
exactly what the scoping rule forbids.

So `infra/bootstrap.sh` creates two:

| Role | Trusts | May |
|---|---|---|
| `hpac-safety-deploy` | `…:ref:refs/heads/main` | Manage the environment, push images, update services |
| `hpac-safety-plan` | `…:pull_request` | Read. Nothing else. |

`hpac-safety-plan` is `ReadOnlyAccess` plus read on the state bucket, minus three
explicit `Deny` statements: `s3:GetObject` on the uploads bucket (those objects
are photographs of crash sites), `secretsmanager:GetSecretValue`, and the RDS
log-download actions. `Deny` beats `Allow` unconditionally, so those hold
whatever AWS adds to `ReadOnlyAccess` later.

Neither role can mint a credential: both policies `Deny` `iam:CreateUser`,
`iam:CreateAccessKey`, `iam:CreateLoginProfile`, `organizations:*`, and
`account:*`. Reasoning: [ADR-0032](decisions/ADR-0032-terraform-ci-without-an-aws-account.md).

## Deploy-time configuration

### Secrets

Two, and neither is usable without the trust policy above. They are role names,
not credentials.

| Name | Purpose | Printed by |
|---|---|---|
| `AWS_DEPLOY_ROLE_ARN` | Role assumed via OIDC by the deploy workflows and by `terraform apply` | `bootstrap.sh`, on stdout |
| `AWS_PLAN_ROLE_ARN` | Read-only role assumed by `terraform plan` on a pull request | `bootstrap.sh`, in its closing instructions |

```bash
gh secret set AWS_DEPLOY_ROLE_ARN --repo HPAC-Safety/safety-report --body "$(./infra/bootstrap.sh)"
gh secret set AWS_PLAN_ROLE_ARN   --repo HPAC-Safety/safety-report
```

### Variables

ARNs and resource names are not credentials. Keeping them as variables means a
failed deploy log says which cluster it could not reach.

| Name | Example | Used by |
|---|---|---|
| `AWS_REGION` | `ca-central-1` | all |
| `TF_STATE_BUCKET` | `hpac-safety-tfstate-123456789012` | `terraform.yml` |
| `ECR_REPOSITORY_API` | `hpac-safety/api` | api |
| `ECR_REPOSITORY_WORKER` | `hpac-safety/worker` | worker |
| `ECS_CLUSTER` | `hpac-safety` | api, worker |
| `ECS_SERVICE_API` | `hpac-safety-api` | api |
| `ECS_SERVICE_WORKER` | `hpac-safety-worker` | worker |
| `ECS_TASK_DEFINITION_MIGRATE` | `hpac-safety-migrate` | api |
| `ECS_SUBNETS` | `subnet-…,subnet-…` | api |
| `ECS_SECURITY_GROUPS` | `sg-…` | api |
| `S3_BUCKET_SITE` | `hpac-safety-site-123456789012` | web |
| `CLOUDFRONT_DISTRIBUTION_SITE` | `E…` | web |
| `SITE_ADMIN_PREFIX` | `admin` | web |

```bash
gh variable set AWS_REGION --body ca-central-1 --repo HPAC-Safety/safety-report
```

`TF_STATE_BUCKET` is the one value that cannot be committed: `backend.tf` is a
**partial** configuration because the bucket name carries the AWS account id, and
`terraform init` is passed it as `-backend-config="bucket=$TF_STATE_BUCKET"`.

Most of the rest are Terraform **outputs**. `terraform output -json
deploy_variables` returns every one of them from state, which is where they
should be read from — two sources of truth for a bucket name is one too many.
The `apply` job writes them into its run summary, and wiring the deploy
workflows to read them instead of `vars.*` is
[#30](https://github.com/HPAC-Safety/safety-report/issues/30).

`preflight` validates **every** variable the workflow's later jobs read,
including the three only `migrate` uses. A variable missing from `preflight`
does not go unnoticed — it fails two jobs later inside an `aws ecs run-task`
call, with an AWS CLI error rather than the named one this contract promises.

The check is a composite action, [`.github/actions/require-config`](../.github/actions/require-config/action.yml),
shared by all three workflows. It began as three near-identical copies of a
shell function, and they had already drifted.

Its manifest contains **no `${{ … }}` expression of any kind**, including inside
a description. GitHub template-evaluates an action manifest in full, the
`secrets` context does not exist at that point, and a worked example in a
description string was enough to make the whole action fail to load — see
[#36](https://github.com/HPAC-Safety/safety-report/issues/36). CI exercises the
action on every pull request so the manifest cannot break unnoticed again.

## Runtime secrets do not belong in GitHub

This is the distinction most easily got wrong. The application needs these
**while it runs**, not while it deploys. They live in **AWS Secrets Manager**
and are injected into the ECS task definition. Putting them in GitHub would
copy them into a second system that has no need to hold them.

| Secrets Manager entry | Injected as | Held by |
|---|---|---|
| `hpac-safety/anthropic-api-key` | `Anthropic__ApiKey` | Worker — summarize, PII audit, translate |
| `hpac-safety/turnstile-secret-key` | `Turnstile__SecretKey` | API — server-side `siteverify` |
| `hpac-safety/connection-string` | `ConnectionStrings__Default` | API and Worker |
| `hpac-safety/notifications-to` | `Notifications__To` | Worker — `safety@hpac.ca` in production |

Terraform creates those four **entries** and none of their **values**. There is
no `aws_secretsmanager_secret_version` resource in `infra/`, and adding one is
the defect rather than the fix — see
[ADR-0010](decisions/ADR-0010-infrastructure-as-code.md). A task whose secret has
no value fails to start, loudly, in the ECS event log; that is the intended
behaviour, since an API booting without its Turnstile key would accept
unverified submissions.

The RDS master password is a fifth secret that Terraform never sees at all:
`manage_master_user_password` has RDS generate and rotate it into its own entry.
`terraform output database_master_password_secret_arn` says where.

The Turnstile **site** key is public by design and may be a variable. The
Turnstile **secret** key must never reach a static bundle.

## One production email address

**`safety@hpac.ca`.** Report notifications from the Worker and operational alarms
from CloudWatch both go there. There is not a second address, and
[#26](https://github.com/HPAC-Safety/safety-report/issues/26) must not invent
one.

It is both a **sender identity** and a **recipient**, and those are separate
mechanisms that happen to share a domain. The DNS administrator will be reading
the list below, so here is which record serves which purpose:

| Record | On | Purpose |
|---|---|---|
| 3 × DKIM `CNAME` | `<token>._domainkey.hpac.ca` | Verifies the domain identity **and** publishes the public half of the key SES signs with. All three are required — two out of three is a domain that stays unverified. |
| `MX` | `mail.hpac.ca` | Bounce handling for the custom MAIL FROM subdomain. **On the subdomain only** — it does not touch the MX for `hpac.ca`, so it cannot affect mail delivered *to* `safety@hpac.ca`. |
| `TXT` (SPF) | `mail.hpac.ca` | Authenticates the envelope sender, so DMARC aligns with the visible `From` header. |
| `TXT` (DMARC) | `_dmarc.hpac.ca` | Policy. `p=none` reports without rejecting — the correct starting point; tighten to quarantine then reject once reports show only SES sending. |
| 2 × `CNAME` (ACM) | `_….hpac.ca` | Certificate validation for `safety.hpac.ca` and `api.hpac.ca`. |
| 2 × `CNAME` (alias) | `safety.hpac.ca`, `api.hpac.ca` | Point the website at CloudFront and the API at the load balancer. |

Every one of them, with its live value, comes out of
`terraform output dns_records_to_publish`. Receiving mail at `safety@hpac.ca`
depends on HPAC's existing MX and mailbox, which this account neither owns nor
touches.

> ### This address is inert until two things land
>
> **Nothing reaches `safety@hpac.ca` today, so alarms and report notifications
> are both silent.** Two independent reasons, and neither is visible from
> Terraform:
>
> - **Alarms** are an SNS email subscription, created *pending confirmation*. AWS
>   emails a link and a human has to click it. Terraform cannot, and reports the
>   subscription as created either way. Until someone clicks, the alarms fire,
>   are visible in CloudWatch, and email nobody. Alarm mail comes from Amazon SNS
>   rather than through the SES identity, so it is **not** held up by the SES
>   sandbox — only by the click and by the mailbox existing.
> - **Report notifications** go through SES as `hpac.ca`, so they need the DKIM
>   and MAIL FROM records above published **and** SES production access granted.
>   In sandbox, SES will not deliver to an address it has not individually
>   verified.
>
> Do not read a green `terraform apply` as "alerting is live". It is not, until
> the DNS records are published, production access clears, and someone confirms
> the subscription.

## The metric contract

Two of the alarms watch metrics **the application publishes**. They are not
derived from anything AWS knows on its own, so if the Worker does not emit them,
the alarms watch nothing and report a permanent `INSUFFICIENT_DATA` or, worse,
sit quietly at OK.

| Namespace | Metric | Kind | Published by | Meaning |
|---|---|---|---|---|
| `HpacSafety` | `SummaryFailed` | count | Worker | A summarization attempt failed. A real occurrence report is sitting unprocessed. |
| `HpacSafety` | `OutboxOldestAgeSeconds` | gauge, on each poll | Worker | Age of the oldest unclaimed outbox row. Rises when the Worker is wedged and stays flat when it is merely busy — which is why it is the age and not the depth. |

The namespace is injected into the task as `Metrics__Namespace`. Both task roles
may call `cloudwatch:PutMetricData`, and only within that namespace.

`OutboxOldestAgeSeconds` alarms on **missing data**: a Worker that has stopped
publishing entirely is the failure the alarm exists for.

**These names are the contract.** The Worker issue implements against them; do
not rename one on either side alone. Thresholds: `SummaryFailed >= 1` in five
minutes, and `OutboxOldestAgeSeconds > 900` for two consecutive five-minute
periods.

## The environment itself

`infra/` is the whole AWS environment as Terraform, and
`.github/workflows/terraform.yml` is how it runs. The three `deploy-*` workflows
ship artifacts *into* it and never create anything.
[`infra/README.md`](../infra/README.md) describes what it owns.

| Job | Runs on | Needs AWS? | Does |
|---|---|---|---|
| `infra` | every pull request and push | no | `fmt -check`, `init -backend=false`, `validate`, `tflint`, `shellcheck bootstrap.sh`, `node --check` on the CloudFront Function |
| `plan` | pull request, same-repo only | yes, read-only | `terraform plan`, posted as a pull request comment |
| `apply` | merge to `main`, or dispatch | yes | `plan` then `apply`, behind `environment: production` |

`infra` is a **required status check** and is the only one that can be, because
it is the only one that works with no account behind it. `plan` and `apply`
detect that the AWS configuration is missing, emit a `::notice::` naming #32, and
exit 0 — the reasoning is
[ADR-0032](decisions/ADR-0032-terraform-ci-without-an-aws-account.md), and it is
[ADR-0011](decisions/ADR-0011-ci-contexts-precede-their-checks.md)'s applied to
the same problem: a permanently-red check trains people to merge past red.

After every apply the job runs `terraform plan -detailed-exitcode` again and
fails if it is not empty. ADR-0010 requires apply on an unchanged repository to
be a no-op; that step is what makes it an assertion rather than an aspiration.

## Uploads: the quarantine prefix

The uploads bucket is private, versioned, encrypted, and holds one photo or video
per report. Its key layout is report-id-first — `<report-id>/original/…`,
`<report-id>/stripped/…` — with one deliberate departure:

```mermaid
flowchart LR
    browser["browser<br/>pre-signed PUT"] --> q["quarantine/…<br/>unverified bytes"]
    q --> ingest["ingest<br/>sniff content type<br/>strip EXIF"]
    ingest -->|"promote"| keep["&lt;report-id&gt;/original/…<br/>&lt;report-id&gt;/stripped/…"]
    ingest -->|"reject"| gone["lifecycle expiry"]
    q -.->|"ingest never ran"| gone
```

**Every upload lands in `quarantine/` first and is promoted out only after
validation.** The `expire-quarantine` lifecycle rule is therefore not an
edge-case cleanup for rejected files — it is what removes **any upload whose
ingest never completed**: a rejected file, a crashed ingest, or an upload slot
issued for a report the pilot never went on to submit. Those are unverified bytes
of a crash photograph, and nothing else in the system deletes them.

It is a **prefix** filter, not a tag filter, and that is load-bearing. Both ways
of applying a tag fail **open**: tagging at upload means signing `x-amz-tagging`
into the pre-signed PUT and trusting the browser to send it, so a client that
omits it produces an object that never expires; tagging after ingest means an
object whose ingest never ran never gets tagged — precisely the case the rule
exists for. The prefix fails **closed**, because `quarantine/` is the only place
an upload URL can write. Per-report enumeration is still a single literal prefix
either way. See [#16](https://github.com/HPAC-Safety/safety-report/issues/16).

### Two hops, two different numbers

The bucket is **versioned**, so expiry does not delete bytes — it writes a delete
marker and makes the object version noncurrent. The rule carries a matching
one-day noncurrent expiry, so the bytes fall a pass later rather than to the
bucket-wide 90-day rule. That is two sequential day-granular hops, and they are
**two distinct moments with two different floors**:

| | Floor | In practice |
|---|---|---|
| The key stops resolving — a `GET` returns the delete marker | 24 hours | up to ~48 |
| The bytes are permanently gone | **48 hours** | up to ~96 |

> **Both numbers are floors, not deadlines.** S3 lifecycle is day-granular and
> asynchronous. Nothing here guarantees deletion within any window, and no design
> decision elsewhere should assume it does. If something needs a hard deletion
> deadline, lifecycle is the wrong mechanism for it.

**Between the two hops the object is still fetchable by version id**, by anything
holding `s3:GetObjectVersion` on the bucket. That is not reachable through the
application: a pre-signed URL names a key and no version, so the report path
cannot see it. It is a question about **direct bucket credentials**, and it is why
the uploads denial on both IAM roles names the versioned actions —
`s3:GetObjectVersion` is a distinct action from `s3:GetObject`, and denying only
the latter would leave every noncurrent version readable. Neither
`hpac-safety-deploy` nor `hpac-safety-plan` can read an upload, current or
noncurrent; see "Authentication" above.

A second lifecycle rule, `expire-quarantine-delete-markers`, clears the expired
object delete markers left behind after both hops. Quarantine objects are created
continuously, so without it those markers accumulate indefinitely — near-zero
cost, but a listing of `quarantine/` would eventually say more about objects that
are gone than about ones that are there. It has to be a separate rule: S3 rejects
`ExpiredObjectDeleteMarker` and `Days` in the same expiration block.

## Manual steps, and why each one has to be

Everything below needs an account that does not exist yet, a human decision, or
another organisation. None of it can be automated, and the first two take real
calendar time — start them before anything needs them.

| Step | Blocked on |
|---|---|
| Create the AWS account, or a dedicated account in an organisation | A person with a payment method. A separate account is worth it: this one holds personal information about real accidents. |
| Enable MFA on the root user, then stop using it | Physical possession of the factor |
| Create an admin user or SSO permission set for whoever runs the bootstrap | The account existing |
| Set a budget alert | A number somebody is willing to be woken up for |
| Request SES production access | **A human review at AWS, a day or more.** Until it clears, mail only reaches individually verified addresses — `safety@hpac.ca` receives nothing. |
| Publish DNS records on `hpac.ca` — three DKIM CNAMEs, MAIL FROM MX and SPF, DMARC, two ACM validation CNAMEs, two alias CNAMEs | **HPAC's DNS administrator.** Another organisation's zone; days, not minutes. What each record is for is in "One production email address" above; the live values are in the `dns_records_to_publish` output. |
| **Confirm the SNS alarm subscription** — click the link AWS emails to `safety@hpac.ca` | A human with access to that mailbox. Terraform creates the subscription *pending confirmation* and cannot complete it. Until it is confirmed, the alarms fire and email nobody. |
| `aws secretsmanager put-secret-value` for each of the four entries | Values that must never enter Terraform state |

## First deploy on a fresh account

The first `terraform apply` does not produce a working system on its own —
nothing has been pushed to ECR and no secret has a value. That is a property of
bootstrapping, not a defect. The order:

```mermaid
flowchart TD
    boot["./infra/bootstrap.sh<br/>OIDC · two roles · state bucket"]
    boot --> gh["gh secret set AWS_DEPLOY_ROLE_ARN, AWS_PLAN_ROLE_ARN<br/>gh variable set TF_STATE_BUCKET"]
    gh --> apply1["terraform apply<br/>everything except a running task"]
    apply1 --> dns["publish the DNS records from<br/>the dns_records_to_publish output"]
    dns --> ses["request SES production access"]
    dns --> confirm["confirm the SNS subscription<br/>in safety@hpac.ca"]
    apply1 --> secrets["put-secret-value ×4"]
    secrets --> deploy["deploy-api · deploy-worker · deploy-web"]
    deploy --> up["services stabilise"]
    ses --> mail["alarms and report notifications<br/>actually arrive"]
    confirm --> mail
```

Two things look like failures on the way and are not:

- **The ECS services do not stabilise after the first apply.** The task
  definitions point at an image tag nothing has pushed yet, and the secrets have
  no values. Both are fixed by the two steps after it.
- **`aws_acm_certificate_validation` blocks**, for up to two hours, waiting for
  DNS records only HPAC's DNS administrator can publish. That is deliberate: the
  alternative is a listener serving a certificate that was never validated. Both
  hostnames are HTTPS-only, so this gate is on the critical path by design.
- **Alerting is not live when apply goes green.** See "This address is inert
  until two things land" above.

## Recovery

### A workflow cannot assume its role

The trust policy is the whole mechanism, and it is converged — not skipped — on
every bootstrap run. Change the constant at the top of `infra/bootstrap.sh` and
re-run it. Check the subject the run actually presented: `main` gives
`repo:HPAC-Safety/safety-report:ref:refs/heads/main`, a pull request gives
`…:pull_request`, and they need different roles.

### The plan says something changed that nobody changed

That is drift, and it means somebody clicked in the console. **Nothing is created
or edited by hand after bootstrap.** Either import the change or revert it; do
not add it to `infra/` retroactively as if Terraform had made it. A plan people
have learned to skim is the review artifact ADR-0010 was chosen for, lost.

### The state file is lost or corrupt

The state bucket is versioned for exactly this. Restore the previous version:

```bash
aws s3api list-object-versions --bucket "$TF_STATE_BUCKET" \
  --prefix hpac-safety/production.tfstate
aws s3api copy-object --bucket "$TF_STATE_BUCKET" \
  --key hpac-safety/production.tfstate \
  --copy-source "$TF_STATE_BUCKET/hpac-safety/production.tfstate?versionId=<previous>"
```

Then `terraform plan` and read it before doing anything else. If state is gone
entirely, the resources still exist — recovery is `terraform import`, resource by
resource, not `terraform apply`.

### A lock is stuck

An apply that was cancelled mid-run leaves the lock held. The lock is a
`.tflock` object beside the state in the same bucket — `use_lockfile`, not a
DynamoDB table (ADR-0031). `terraform force-unlock <id>` releases it. Confirm no
apply is actually running first; two concurrent applies is what the lock exists
to prevent.

### Rebuilding the environment from scratch

`terraform destroy` then `terraform apply`, with one deliberate obstacle: the RDS
instance sets `deletion_protection = true` and `skip_final_snapshot = false`.
Clearing that is a conscious act, which is the right amount of friction for the
one resource holding data about real accidents. The uploads bucket is versioned
and will not delete while it holds objects.

Rebuilding does **not** restore the database. That is a snapshot restore, and it
is a separate decision from rebuilding the infrastructure around it.

## Migrations

Migrations are their own job in `deploy-api.yml`, with an explicit `needs` edge,
and they complete before the new task set takes traffic.

They do **not** run at application startup. Two API tasks booting together would
both try to take the migration lock, and the loser either crashes or serves
traffic against a half-migrated schema.

```mermaid
flowchart LR
    pf["preflight"] --> img["image<br/>publish to ECR"]
    img --> mig["migrate<br/>one-off ECS task"]
    mig --> dep["deploy<br/>update service"]
```

Only `deploy-api.yml` owns the schema, so exactly one workflow can change it. If
a release needs both, deploy the API first.

## The `production` environment

All jobs that touch AWS state declare `environment: production`, which carries a
required reviewer. A deploy is a deliberate act, not a side effect of merging.

The environment exists, with `ChaseFlorell` and `jopekar` as required
reviewers and `protected_branches: true`, so only `main` can deploy. It was
created with, and can be reapplied by:

```bash
gh api -X PUT repos/HPAC-Safety/safety-report/environments/production \
  --input - <<'JSON'
{
  "wait_timer": 0,
  "prevent_self_review": false,
  "reviewers": [
    { "type": "User", "id": 471626 },
    { "type": "User", "id": 7506460 }
  ],
  "deployment_branch_policy": {
    "protected_branches": true,
    "custom_branch_policies": false
  }
}
JSON
```

## Rollback

Every deploy workflow takes an `image_tag` input on `workflow_dispatch`.
Rolling back is re-running it with the previous commit SHA — no revert commit,
no rebuild.

```bash
gh workflow run deploy-api.yml -f image_tag=<previous-sha>
```

For the static sites, re-run `deploy-web.yml` from the previous commit and let
the CloudFront invalidation follow.

**A rollback does not undo a migration.** Migrations must be written so that the
previous application version still runs against the new schema — add columns,
do not rename them; drop only in a later release. That constraint is on the
migration author, not on this workflow.

## Local development

None of this is needed to develop. `IBlobStore` resolves to a filesystem
implementation and `IEmailSender` to a logging one, so a local run never touches
AWS. See the per-app READMEs under `src/`.
