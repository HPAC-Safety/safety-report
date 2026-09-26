---
title: A Terraform apply waits in its own concurrency group
description: terraform.yml serialises applies with a job-level group on apply, and gives each non-pull-request run its own workflow group, so an apply waiting on its reviewers no longer stops infra from reporting on later main pushes.
type: adr
status: accepted
date: 2026-09-26
decision-makers: Chase Florell
keywords: terraform, concurrency, apply, environment, required reviewers, infra, main, ADR-0032, ADR-0147
---

# ADR-0148 — A Terraform apply waits in its own concurrency group

**Status:** Accepted. Amends how `terraform.yml` groups its runs
([ADR-0032](ADR-0032-terraform-ci-without-an-aws-account.md)). It leaves
[ADR-0147](ADR-0147-pull-requests-merge-through-a-merge-queue.md)'s rule
standing: only a newer push to the same pull request cancels a run.

## Context

`terraform.yml` had one workflow-level group per ref, `terraform-<ref>`, which
never cancelled on `main`. Its comment said this was so two applies never
overlap. `apply` runs on every `main` push behind the `production`
environment's required reviewers, even while AWS is not bootstrapped.

On 2026-09-22 run 35673762842's `apply` began waiting for a reviewer, and
nobody approved or rejected it. A run waiting on an environment holds its
group. GitHub keeps one pending run per group, and each new one cancels the
one pending before it. So every later `main` push queued, was cancelled
without a job, and never reported `infra`. That was 198 of the last 200.
`main` had no `infra` result for four days (#552).

## Decision

- The workflow-level group is `terraform-<ref>` for a pull request, which
  still cancels on a newer push (ADR-0147). Every other event gets
  `terraform-<run id>`, a group of its own. `infra` then runs on every `main`
  push and merge group, whatever `apply` is doing.
- `apply` carries its own job-level group, `terraform-apply`, one for every
  ref, with `cancel-in-progress: false`. A `workflow_dispatch` from a branch
  changes the same AWS account. Applies still never overlap. An apply waiting
  on reviewers holds the group, and a newer one queues behind it and replaces
  any older one still queued. So the newest commit applies next.

## Alternatives considered

- **Cancel in progress on `main` too.** A newer push would cancel an apply
  mid-run, leaving the state lock held and AWS half-changed.
- **Skip `apply` until AWS is configured**, gating the job on a variable, so
  nothing waits on reviewers. That fixes today's case only. Once AWS exists,
  an apply awaiting approval would still starve `infra` on `main`.
- **Keep the workflow group and reject stale waiting runs by hand.** It depends
  on someone noticing, and nobody did for four days.

## Consequences

- `infra` reports on every `main` commit.
- A run whose queued `apply` is replaced shows as cancelled overall, while its
  `infra` context stays green.
- Run 35673762842 still holds `terraform-apply` until a reviewer approves or
  rejects it. That run is stale, so a maintainer should reject it.
