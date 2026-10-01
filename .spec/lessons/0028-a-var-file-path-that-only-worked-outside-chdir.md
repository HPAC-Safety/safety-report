---
title: A -var-file path that only worked outside -chdir
description: Every Terraform command ran with -chdir=infra, which resolves -var-file after the chdir, so a repository-root path never found the tfvars file.
type: lesson
date: 2026-09-28
issue: 615
status: accepted
---

# Lesson 0028 — A -var-file path that only worked outside -chdir

## Symptom

Once OIDC trust worked (lesson 0027), `plan (staging)` on PR #613
authenticated and then failed: `Given variables file infra/staging.tfvars
does not exist` (run 36495032795). The first release's `terraform apply`
would have failed at the same flag.

## Root cause

`terraform.yml`'s `plan` and `deploy-environment.yml`'s `terraform apply` and
drift re-plan all run with `-chdir=infra`, and pass `-var-file` a
repository-root path (`infra/staging.tfvars`). Terraform resolves `-var-file`
*after* the `-chdir`, so it looked for `infra/infra/staging.tfvars`. No run
had reached this flag before #612's OIDC fix, so nothing had caught it.

## Spec delta

None upstream: this is a workflow flag, not a product claim. The three call
sites now pass `-var-file="$GITHUB_WORKSPACE/<path>"`; callers keep passing
repository-root paths.

## Scenario

None. This is a process lesson, and no scenario can prove it.

## Skill

[`manage-hpac-infrastructure`](../../skills/manage-hpac-infrastructure/SKILL.md)
gains "Terraform vs. the deploy role": `-chdir=infra` resolves every later
relative flag, `-var-file` included, from `infra/`, not the repository root —
a workflow that uses `-chdir` passes an absolute path
(`$GITHUB_WORKSPACE/<path>`).
