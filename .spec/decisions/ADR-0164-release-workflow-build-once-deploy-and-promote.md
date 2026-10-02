---
title: The release workflow builds once through a reusable deploy job, and a pull request plans through separate repository-scoped credentials
description: release.yml builds the API image, the Worker image, and the web bundle once with no AWS credential, then calls one reusable workflow_call job twice — hpac-safety-staging, then hpac-safety-production — passing the same artifacts unchanged. terraform.yml's pull-request plan reaches both accounts through repository secrets/variables, not the deploy environments, because those restrict deployment to the release tag pattern. Rollback re-runs an earlier release's own jobs.
type: adr
status: partially-superseded
date: 2026-09-27
decision-makers: Chase Florell
keywords: release, GitHub Actions, workflow_call, OIDC, rollback, NAT instance, Terraform plan, ADR-0158, CON-INF-012
---

# ADR-0164 — The release workflow builds once through a reusable deploy job, and a pull request plans through separate repository-scoped credentials

## Status

**Partially superseded by
[ADR-0166](ADR-0166-a-release-deploys-staging-and-a-separate-workflow-promotes-to-production.md)**:
`release.yml` now calls `deploy-environment.yml` for staging only, and
`promote.yml` calls it for production with the release run's artifacts.
Rollback is re-promoting an earlier tag (production) or re-running its release
(staging). The build job, the reusable deploy job, the pull-request plan, and
Terraform outputs as the interface stand.

**Subject form amended by
[ADR-0167](ADR-0167-oidc-trust-names-the-immutable-subject.md):** every trust
subject quoted below as `repo:HPAC-Safety/safety-report:<context>` is
`repo:HPAC-Safety@307760008/safety-report@1341995834:<context>`, GitHub's
immutable form, which is the only form this repository's tokens carry.

Accepted. Implements [ADR-0158](ADR-0158-two-aws-accounts-staged-and-promoted-by-approval.md)
and [CON-INF-011 through CON-INF-013](../infrastructure-and-operations.md).
Does not reopen anything either decided.

## Context

Issue #466 asked for `release.yml`: build once, deploy to `hpac-safety-staging`
automatically, promote to `hpac-safety-production` after `hpac-safety-admins` approves,
replacing the ECS-era `deploy-api.yml`/`deploy-worker.yml`/`deploy-web.yml`
stubs. ADR-0158 and CON-INF-011..013 already settled the shape — two
accounts, one Terraform root, build once, no apply on merge, the NAT instance
is the only thing ever replaced. What they left open is mechanical: how a
single GitHub Actions file builds once and deploys the identical result
twice, how a pull request's Terraform plan reaches two accounts without two
copies of the job, and what "re-running the release job for an earlier tag"
(ADR-0158's rollback line) means when GitHub Actions artifacts expire.

**No AWS account exists yet in this pull request.** This workflow is real —
every AWS call in it is genuine — but it triggers only on `release: published`
or a maintainer's `workflow_dispatch` redeploy, never on push, merge, or pull
request, and nobody publishes a release before #464's `infra/bootstrap.sh`
(PR #582) is actually run, by a human, against both real accounts. Nothing
here was run against AWS to write it.

This pull request is built on top of #443 (the Lambda-adapter API image,
`tools/build/build-api-image.sh`) and #465/#588 (the multi-account Terraform,
`infra/staging.tfvars`, `infra/production.tfvars`, and every Terraform
output this workflow and `deploy-environment.yml` read by name), all now
merged — every `terraform output` reference is aligned with the real
`infra/outputs.tf`, and `tools/infra/check-terraform-outputs.mjs` fails CI if that
ever drifts again.

## Decision

### One build job, one reusable deploy job, called twice

`release.yml` has three jobs: `validate-tag`, `build`, and two calls to a
fourth file, `deploy-environment.yml` (`on: workflow_call` only — it has no
trigger of its own and is not a second release workflow). `staging` and
`production` are that same reusable job with different inputs
(`environment_name`, `tfvars_file`, `state_key`). This is a design choice, not
a GitHub Actions requirement: two independent jobs with copy-pasted steps
would work too, but every past inconsistency in this repository between two
things that must stay identical has come from exactly that copy (see
`AGENTS.md` "Boring over clever" and #30's own design principle 1, "one way
to do each thing"). A staging-only bug fix that is never applied to
production's copy is the failure this shape makes structurally impossible.

`build` requests no AWS credential — no `id-token: write` permission is even
usable inside it, since the workflow-level `permissions:` only grants
`id-token: write` to the jobs that need it. It builds the API image
(`tools/build/build-api-image.sh`, #443), the Worker image
(`tools/build/build-worker-image.sh`, ADR-0118), and the web bundle, saves the two
images with `docker save | gzip`, and uploads all three as workflow
artifacts. `deploy-environment.yml` downloads them, `docker load`s the
images, and pushes them to that account's ECR. Because the loaded image is
byte-identical to what `build` produced, the manifest digest each account's
ECR reports is the same digest in both accounts — content addressing, not a
promise carried by hand. Each environment's job summary records its own
pushed digests, so a reviewer of a production run can compare them to
staging's without leaving the Actions UI.

### The pull-request plan cannot use the deploy environments' copy

`terraform.yml`'s `plan` job needs a read-only role in **both** accounts on
every pull request, unapproved and immediately. It cannot get there by
reading the `AWS_PLAN_ROLE_ARN` GitHub Environment variable #464 (PR #582,
merged) already prints and documents for `hpac-safety-staging`/`hpac-safety-production`,
for two compounding reasons:

1. `hpac-safety-plan`'s OIDC trust condition matches the subject
   `repo:HPAC-Safety/safety-report:pull_request` (bootstrap.sh's own comment:
   "assumable from any pull request against this repository"). A job
   presents that subject only when it does **not** declare `environment:`.
   The moment a job adds `environment: hpac-safety-staging` — which is what reading
   an environment's variables requires — its subject becomes
   `repo:...:environment:hpac-safety-staging` instead: the subject
   `hpac-safety-deploy` trusts, not `hpac-safety-plan`. Declaring the
   environment to read the variable would make the OIDC assumption itself
   fail.
2. Independently, issue #30 "Human work" H2 restricts both environments'
   deployment branch/tag policy to the release tag pattern (`20*`), and
   GitHub enforces that for any job that declares `environment:` regardless
   of what the job does with it — a pull-request ref never matches, so the
   job would be refused before a single step ran.

`plan` instead runs as a matrix (`staging`, `production`) and reads a
**second**, repository-level copy of the same two values, one pair per
account, distinguished by suffix — plain repository variables, matching
bootstrap.sh's own "identifiers, not secrets" treatment:
`AWS_PLAN_ROLE_ARN_STAGING` / `AWS_PLAN_ROLE_ARN_PRODUCTION` and
`TF_STATE_BUCKET_STAGING` / `TF_STATE_BUCKET_PRODUCTION`, read through GitHub
Actions' `vars[format(...)]` bracket syntax so the matrix stays one job
rather than two hand-written copies. **This suffix naming is decided in this
pull request, not by #464** — `infra/bootstrap.sh` (#464) printed only the
environment-scoped names; this pull request extends it to also print these
repository-scoped ones, under their own "Repository variables" heading, in
the same run. There is no separate hand-run step: running `bootstrap.sh` once
per account and setting everything it prints is the whole of H3. See
`docs/deployment.md` "Required GitHub configuration" for the exact commands.

`terraform.yml` loses its `apply` job entirely (CON-INF-012: no apply on
merge to `main`). Only `release.yml`'s deploy jobs ever run `terraform
apply`, and only against the account they are deploying to.

### Terraform outputs are the interface, not GitHub variables

Every AWS resource name `deploy-environment.yml` needs — ECR repositories,
Lambda function names, the site bucket, the CloudFront distribution, the NAT
instance's Auto Scaling group, the vendor-key Secrets Manager entries, the
public site URL — is read with `terraform output`, never duplicated as a
GitHub variable: `infra/outputs.tf`'s `deploy_variables` map for the first
group, and its own top-level outputs for the rest —
`nat_autoscaling_group_arn` (an ARN; the ASG's name is parsed off its last
path segment, since the AWS CLI calls this step makes take the name, not the
ARN), `secret_entries` (a map read by the `gemini_api_key`/`deepl_api_key`
keys `infra/secrets.tf` names them), and `site_urls` (a map keyed by
hostname — staging has exactly one entry, production two — read by taking
any one entry's `public` URL, since every hostname reaches the same
distribution and the same `/api/health`).

`tools/infra/check-terraform-outputs.mjs` (added in this pull request, wired into
`ci.yml`'s `docs` job) statically checks every `terraform output` name and
JSON key either workflow reads against what `infra/outputs.tf` actually
declares, and fails the build on a mismatch — the guard issue #466's review
asked for, so a renamed or removed output is caught here rather than at the
first real release.

### Rollback is re-running the release, preferring the original run

ADR-0158 already says rollback is "re-running the release job for an earlier
tag." The concrete mechanism: open that release's own workflow run under
**Actions** and choose **Re-run all jobs**. Because `build` produces
deterministic content from a fixed commit and fixed build scripts, re-running
it reproduces the same images and bundle; nothing about GitHub Actions makes
the original run's *artifacts* (as opposed to its *inputs*) durably reusable
past their retention window, so "re-run" means "run the same jobs again," not
"redownload what a previous run uploaded." `workflow_dispatch` with a `tag`
input exists as the fallback for when that original run is no longer visible
or its concurrency group needs a fresh trigger — it rebuilds from the tagged
commit rather than reusing anything. Both paths honor "same artifacts, never
a rebuild" *within* one release's own build→staging→production chain; they
differ only in whether that chain's `build` job is the original run's or a
new one, which is immaterial once the commit is fixed.

## Alternatives considered

- **Two independent jobs (`staging`, `production`) with the same steps
  copy-pasted.** Rejected: the one thing #30's design principles insist on
  is that the two environments never diverge outside their tfvars, and two
  copies of ~20 steps is exactly the kind of place that promise quietly
  breaks.
- **A composite action instead of a reusable `workflow_call` workflow.**
  Composite actions cannot declare `environment:`, and picking the right
  environment's secrets is the entire point of calling this twice. A
  reusable workflow's job can.
- **Scoping the pull-request plan to the `hpac-safety-staging`/`hpac-safety-production`
  environments anyway**, accepting that it would never run. Rejected outright
  — a required or expected check that never reports is the exact trap
  ADR-0011 already named for this repository.
- **A single suffixed variable set for both deploy and plan roles**
  (`AWS_DEPLOY_ROLE_ARN_STAGING`, etc.), dropping the environment-scoped
  `AWS_DEPLOY_ROLE_ARN`. Rejected: issue #466's own instructions, matching
  ADR-0158's environment shape, fix `vars.AWS_DEPLOY_ROLE_ARN` as an
  environment variable already; only the plan role needed a new name, because
  only the plan role cannot use a GitHub Environment at all.

## Consequences

- `infra/bootstrap.sh` (#464, merged as PR #582) already printed
  `AWS_DEPLOY_ROLE_ARN`, `AWS_PLAN_ROLE_ARN`, `TF_STATE_BUCKET`, and
  `AWS_ACCOUNT_ID` per account, for that account's GitHub Environment. This
  pull request extends it to also print `AWS_PLAN_ROLE_ARN_<ACCOUNT>` and
  `TF_STATE_BUCKET_<ACCOUNT>` in the same run, under their own "Repository
  variables" heading — no separate hand-run step, no config an operator has
  to invent from reading this ADR.
- `tools/build/build-api-image.sh` and `src/HpacSafety.Api/Dockerfile` (#443), and
  `infra/outputs.tf`'s `nat_autoscaling_group_arn`, `secret_entries`, and
  `site_urls` outputs (#465/#588), are merged; `release.yml` and
  `deploy-environment.yml` read every one of them by its real name, verified
  by `tools/infra/check-terraform-outputs.mjs`.
- `docs/deployment.md` "Release and promotion" and "Required GitHub
  configuration" carry the operator-facing half of this record; keep them
  and this ADR in agreement if either changes.
