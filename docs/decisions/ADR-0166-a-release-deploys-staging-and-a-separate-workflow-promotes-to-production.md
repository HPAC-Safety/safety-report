---
title: A release deploys staging, and a separate workflow promotes a staged tag to production
description: release.yml builds once and deploys only hpac-safety-staging. promote.yml, dispatched on a release tag, deploys that tag's green release run's artifacts to hpac-safety-production after approval. Each environment holds its own concurrency group, so a waiting approval never holds a staging release.
type: adr
status: accepted
date: 2026-09-28
decision-makers: Chase Florell
keywords: release, promotion, promote.yml, GitHub Actions, concurrency, environment approval, artifacts, rollback, ADR-0158, ADR-0164, CON-INF-012, lesson 0026
---

# ADR-0166 — A release deploys staging, and a separate workflow promotes a staged tag to production

## Status

Accepted. **Partially supersedes**
[ADR-0158](ADR-0158-two-aws-accounts-staged-and-promoted-by-approval.md)
("Production waits" inside the release; "one release workflow"; rollback by
re-running the release) and
[ADR-0164](ADR-0164-release-workflow-build-once-deploy-and-promote.md)
(`release.yml` calling the deploy job twice; rollback by re-running the
original run). Everything else in both stands: two accounts, build once, the
same artifacts in both, one reusable `deploy-environment.yml`, approval by
`hpac-safety-admins` on the `hpac-safety-production` environment, no apply on
merge, the pull-request plan.

## Context

Staging came first (#606). The owner needs to release to staging many times
without promoting any of them (owner, 2026-09-28). Under ADR-0158/0164, every
release ran `staging` and then a `production` job waiting on approval:

- Every staging release asked for a production approval that had to be
  rejected, and the run then showed as failed.
- `release.yml`'s workflow-level `concurrency: release` group was held by the
  waiting `production` job, so the next release queued behind it until someone
  rejected the approval — the failure
  [lesson 0026](../lessons/0026-a-run-waiting-on-reviewers-held-every-later-run.md)
  recorded for `terraform.yml`, repeated here.
- Before `hpac-safety-production` exists, GitHub creates an environment on
  first use with no protection, so the `production` job would have run with no
  approval at all.

## Decision

- **`release.yml` deploys staging only.** It builds once, as before, and calls
  `deploy-environment.yml` for `hpac-safety-staging`. It never deploys to, or
  waits on, production. Every run is titled `Release <tag>` (`run-name`).
- **`promote.yml` promotes one staged tag.** A maintainer dispatches it on the
  release tag (**Use workflow from → Tags**, or
  `gh workflow run promote.yml --ref <tag>`). Its `locate` job refuses, before
  any AWS call:
  - a run not dispatched on a tag, or on one that is not `YYYY.MM.DD-N`;
  - a tag with no successful `Release <tag>` run — a tag never green on
    staging is never promoted;
  - a run whose `api-image`, `worker-image`, or `web-dist` artifact has
    expired (90 days).

  Its `production` job calls the same `deploy-environment.yml` for
  `hpac-safety-production`, passing that run's ID (`artifacts_run_id`), so it
  downloads and deploys exactly what the release built and staged. Never a
  rebuild (CON-INF-012). The environment's required reviewers gate the job.
- **It runs on the tag, not a tag input**, because both environments allow
  deployments only from refs matching `20*` (infra/SETUP.md 2.1, 3.1). A run
  dispatched from `main` would be refused by the environment. For the same
  reason, `release.yml`'s `workflow_dispatch` rebuild now refuses a dispatch
  that is not on the tag it names, before building.
- **Separate concurrency groups**: `release-staging` and `promote-production`.
  A promotion waiting on approval holds only other promotions.
- **Rollback**: production, promote an earlier tag; staging, re-run that
  release's run, or dispatch `release.yml` on the tag once its artifacts have
  expired.

## Alternatives considered

- **A pre-release flag.** Publish as a pre-release to deploy staging; untick
  it (`released` event) to promote. Rejected: publishing a full release
  straight away fires `published` and `released` together, so staging and
  production would race unless one run waited on the other across workflows.
  Promotion state in the release list is not worth that.
- **Keep one run and reject each approval**, splitting only the concurrency
  group. Rejected: every staging release still ends as a failed run and an
  approval request nobody means to grant.
- **A tag input on `promote.yml`, dispatched from `main`.** Rejected: the
  environment's `20*` rule refuses a job whose ref is a branch.

## Consequences

- `deploy-environment.yml` gains the optional `artifacts_run_id` input and
  `actions: read`; both callers grant `actions: read`.
- `promote.yml` runs as it stood at the tag's commit. Only tags cut after this
  change can be promoted, which is every tag: no release existed before it.
- A promotion needs the release run's artifacts; after 90 days it needs a
  rebuild through `release.yml`, which also restages that tag.
- CON-INF-012, `docs/deployment.md`, `infra/SETUP.md`,
  `.github/workflows/README.md`, and `manage-hpac-infrastructure` describe
  this shape.
