---
title: A run waiting on reviewers held every later run
description: A Terraform apply awaiting environment approval held terraform.yml's workflow-level concurrency group on main, so every later main push was queued and cancelled without reporting infra for four days, and agent-config's unauthenticated API calls failed a required check on a shared rate limit.
type: lesson
date: 2026-09-26
issue: 552
status: accepted
kind: process
---

# Lesson 0026 — A run waiting on reviewers held every later run

## Symptom

- 198 of the last 200 `terraform.yml` push runs on `main` ended `cancelled`
  without starting a job. `infra` reported on no `main` commit from
  2026-09-22 to 2026-09-26.
- On the same day, the required `agent-config` check failed on `main` with
  `HTTP 403 … you may be rate-limited`. It had done the same once on #529.

## Root cause

- `terraform.yml`'s concurrency group was workflow-level, one per ref, so that
  two applies would never overlap. But it covered the whole run, `infra`
  included. An `apply` waiting on the `production` environment's reviewers
  held the group indefinitely. GitHub keeps one pending run per group, and each
  new one cancels the one before it, so every later push was cancelled while
  it waited. The lock guarded one job but was taken for all of them.
- `agent-config` ran `skillfile install` with no token. Its GitHub API calls
  shared the runner IP's anonymous quota with every other tenant on that IP.

## Spec delta

None upstream: this is CI, not a product claim.
[ADR-0148](../decisions/ADR-0148-a-terraform-apply-waits-in-its-own-concurrency-group.md)
moves the serialisation onto `apply`. `agent-config` now passes the job's
`GITHUB_TOKEN` to skillfile.

## Scenario

None. This is a process lesson, and no scenario can prove it.

## Skill

[`deliver-change`](https://github.com/ChaseFlorell/agent-team/blob/main/skills/deliver-change/SKILL.md) gains "Concurrency
and quotas":

- serialise only the job that needs it, never a whole workflow that also
  reports a check;
- a job that calls an API authenticates rather than sharing an anonymous
  quota.

[`deliver-hpac-change`](../../skills/deliver-hpac-change/SKILL.md) names this
repository's instances.
