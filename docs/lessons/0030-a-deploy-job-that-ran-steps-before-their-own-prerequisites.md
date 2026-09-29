---
title: A deploy job that ran steps before their own prerequisites
description: deploy-environment.yml ran a local action before checkout, and would have run the full terraform apply before any image existed in ECR for Lambda's CreateFunction to read.
type: lesson
date: 2026-09-28
issue: 623
status: accepted
---

# Lesson 0030 — A deploy job that ran steps before their own prerequisites

## Symptom

Release run 36500103710 failed on `staging / Deploy`'s first step:
`Can't find 'action.yml' … Did you forget to run actions/checkout before
running your local action?`

## Root cause

- `deploy-environment.yml` ran the local `./.github/actions/require-config`
  action before `actions/checkout`. A local action is read from the
  workspace, which is empty until checkout runs.
- Reading the rest of the never-yet-run deploy job found a second ordering
  defect before it happened: the full `terraform apply` ran before any image
  was pushed, and `lambda.tf` creates both Lambda functions from
  `<repo>:latest`. Lambda's `CreateFunction` refuses an image ECR does not
  hold, so the first release's apply would have failed at the same class of
  problem next.

Neither had surfaced before, because no run had reached the deploy job.

## Spec delta

None upstream: this is deploy-job step order, not a product claim.
`deploy-environment.yml` now checks out before `require-config`; before the
full apply, a targeted apply creates the ECR repositories (a no-op after the
first release), then the images are pushed tagged with the SHA and `latest`,
reading repository URLs from `terraform show -json` since `deploy_variables`
also names resources not yet created.

## Scenario

None. This is a process lesson, and no scenario can prove it.

## Skill

[`manage-hpac-infrastructure`](../../skills/manage-hpac-infrastructure/SKILL.md)
gains "Workflow mechanics":

- a local action runs from the checked-out workspace — check out before
  running one, in every workflow;
- a Lambda function created from a container image needs that image already
  in the registry — create the registry and push before the apply that
  creates the function depends on it.
