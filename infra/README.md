---
title: Infrastructure
description: The Terraform for the two target AWS environments and the topology it builds.
type: readme
---

# Infrastructure

One Terraform root, `infra/`, builds two AWS environments — **staging**
(the owner's existing account, synthetic data only) and **production** (a
separate, HPAC-owned account, real reports) — in `ca-central-1`
([ADR-0158](../docs/decisions/ADR-0158-two-aws-accounts-staged-and-promoted-by-approval.md)).
The two environments differ ONLY in `staging.tfvars` and `production.tfvars`;
`diff` between them is the complete list of differences. Nothing
environment-specific lives anywhere else in this directory.

The topology: the API and the Worker as Lambda functions behind one
CloudFront distribution that also serves the website, RDS PostgreSQL, private
attachment S3, and no ALB
([ADR-0042](../docs/decisions/ADR-0042-lambda-hosted-api-with-fargate-migration-path.md),
[ADR-0048](../docs/decisions/ADR-0048-one-website-admin-as-a-route.md),
[ADR-0123](../docs/decisions/ADR-0123-the-worker-runs-on-lambda-and-the-website-on-s3-and-cloudfront.md),
[ADR-0159](../docs/decisions/ADR-0159-cloudfront-routes-api-to-a-function-url-no-alb.md)).
See [`docs/infrastructure-and-operations.md`](../docs/infrastructure-and-operations.md)
for the full target design, [`docs/deployment.md`](../docs/deployment.md)
for the operator guide, and [`SETUP.md`](SETUP.md) for the step-by-step
human setup of each environment.

GitHub Actions assumes roles through OIDC, one per account per purpose
(`hpac-safety-deploy`, `hpac-safety-plan`); there is no long-lived AWS key.
Migrations run at each function's cold start under an advisory lock. Backups,
deletion protection, and focused alerts are required in both environments.

The bootstrap (`sh infra/bootstrap.sh <staging|production>`, #464) creates only
the resources needed before Terraform can authenticate: the GitHub OIDC
provider, the per-environment `hpac-safety-deploy` and `hpac-safety-plan`
roles, and the remote state bucket. It runs once in each of the two separate,
unrelated AWS accounts — staging and production — never through an
Organizations relationship between them; see
[`../docs/deployment.md`](../docs/deployment.md). Secret values stay out of
Terraform state, with exactly one documented exception (`secrets.tf`);
application data uses AWS-managed encryption and TLS.

## What each file creates

| File | Creates | Lasts, or replaced every release? |
|---|---|---|
| `backend.tf` | The S3 remote-state backend (partial config: bucket and key are supplied at init time, per account and per environment) | — |
| `providers.tf` | The `aws` provider (default region + `us_east_1` alias for the CloudFront certificate), the `allowed_account_ids` guard, `default_tags` | — |
| `variables.tf` | Every input, including `environment`, with every default marked DECIDED | — |
| `staging.tfvars` / `production.tfvars` | The only environment-specific values | — |
| `locals.tf` | Shared naming, tags, subnet math, log group names | — |
| `grouping.tf` | The `hpac-safety-<environment>` tag-based Resource Group (ADR-0170) | Lasts |
| `network.tf` | VPC, subnets, the S3 gateway endpoint, and the NAT instance (`fck-nat` module) | VPC/subnets/endpoint last; **the NAT instance is deleted and recreated every release** — the only resource in this system that is |
| `security-groups.tf` | Security groups for the API/Worker Lambda functions and RDS | Lasts |
| `database.tf` | RDS PostgreSQL, its subnet and parameter groups | Lasts — `prevent_destroy`, deletion protection, final snapshot |
| `storage.tf` | The private uploads bucket and the private site bucket | Lasts — uploads bucket carries `prevent_destroy`; site bucket **contents** are replaced by every release's web build, the bucket itself is not |
| `ecr.tf` | The `api` and `worker` image repositories | Lasts |
| `lambda.tf` | The API and Worker Lambda functions, their Function URL, and the EventBridge Scheduler sweep (#443) | Lasts — updated in place with each new image |
| `iam.tf` | What each Lambda function's own role may do | Lasts |
| `acm.tf` | The one us-east-1 certificate covering both production hostnames (skipped entirely in staging) | Lasts |
| `cdn.tf` | The one CloudFront distribution: default → site bucket, `/admin/*` → same bucket with its own headers policy, `/api/*` → the API's Function URL, SPA fallback to `index.html` | Lasts |
| `secrets.tf` | Secrets Manager entries (values supplied out of band, except the CloudFront origin-verify secret, which Terraform originates itself — see its header comment) | Lasts — `prevent_destroy` |
| `observability.tf` | Log groups, the alarm SNS topic and its subscription, and every alarm | Log groups last, `prevent_destroy`, 90-day retention; alarms/topic last |
| `outputs.tf` | Everything a deploy workflow or a human operator needs to read out of state | — |

## Credential-free local checks

```bash
terraform -chdir=infra fmt -check -recursive -diff
terraform -chdir=infra init -backend=false -lockfile=readonly
terraform -chdir=infra validate
tflint --chdir=infra --init
tflint --chdir=infra
shellcheck -s sh infra/bootstrap.sh
```

None of these touch AWS. A `terraform plan`/`apply` needs `-var-file` naming
one environment's tfvars and a real AWS session in that account; see
`docs/deployment.md`.

Do not add SES, ALB/ECS/Fargate, outbound notifications, public attachment
delivery, speculative scaling, a managed NAT gateway, or long-lived AWS keys.
Every human step to set up staging or production — GitHub settings,
bootstrap, variables, secrets, DNS, alarm email — is in
[`SETUP.md`](SETUP.md).

## Metrics and alarms (issue #467)

The owner scaled operations back for a lightly used system: four alarms,
production email only. Three read AWS's own signals directly (the API's and
the Worker's Lambda `Errors`, and the NAT instance's Auto Scaling group
`GroupInServiceInstances`); the fourth — the oldest outbox row exceeding 15
minutes — reads the one metric the Worker publishes itself,
`OutboxOldestAgeSeconds`, as a CloudWatch Embedded Metric Format log line
under the `HpacSafety` namespace (`local.metric_namespace`) — no AWS SDK
call, no agent. No dimension is ever derived from report content (AGENTS.md
invariant 8). Each alarm's own description is short and self-contained; none
links elsewhere.

## Provider lock file

`.terraform.lock.hcl` is regenerated by
[`.github/workflows/terraform-relock.yml`](../.github/workflows/terraform-relock.yml)
on a weekly schedule, using the real `terraform` binary — not by Renovate.
Renovate's terraform manager computes lock hashes itself rather than running
terraform, and has produced a checksum that did not match what the registry
actually serves (issue #118); `renovate.json` disables `lockFileMaintenance`
for the terraform manager for that reason. `./relock-providers.sh` is what
that workflow runs; run it by hand after widening a version constraint if you
don't want to wait for the schedule. `terraform init -lockfile=readonly`
(the script's last step, and the required `infra` CI check) verifies the
result either way.
