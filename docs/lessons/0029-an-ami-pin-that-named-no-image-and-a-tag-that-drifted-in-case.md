---
title: An AMI pin that named no image, and a tag that drifted in case
description: The fck-nat AMI lookup was pinned to the Terraform module's version rather than an AMI build, and Terraform tagged every resource Project=hpac-safety while the deploy role's policy required the exact case HPAC-Safety.
type: lesson
date: 2026-09-28
issue: 617
status: accepted
---

# Lesson 0029 — An AMI pin that named no image, and a tag that drifted in case

## Symptom

The first `terraform plan` to reach the staging account (PR #616) failed on
`data.aws_ami.fck_nat`: `Your query returned no results` (run 36496995039).
The same plan showed every resource tagged `Project = "hpac-safety"`.

## Root cause

- `network.tf` looked up `fck-nat-al2023-${var.fck_nat_ami_version}-arm64-ebs`
  with `fck_nat_ami_version = "1.6.1"` — the fck-nat **Terraform module's**
  version, not an AMI build. fck-nat's real image names carry `hvm-` and a
  build date, and one version has several dated builds, so only a dated pin
  names exactly one image.
- `locals.tf` tagged every resource `Project = var.project`, and `var.project`
  is the lowercase name prefix (`hpac-safety`). `bootstrap.sh`'s deploy
  policy, ADR-0158, CON-INF-007, and `docs/deployment.md` all specify
  `Project=HPAC-Safety`. IAM tag comparisons are case-sensitive, so every
  create the policy gates on `aws:RequestTag/Project` would have been denied.

## Spec delta

None upstream: this is Terraform's AMI pin and tag value, not a product
claim. `fck_nat_ami_version` is now `1.4.0-20260701`
(`ami-0fbfd45d019c81c22`), and `local.project_tag = "HPAC-Safety"` is
separated from `var.project`, the name prefix.

## Scenario

None. This is a process lesson, and no scenario can prove it.

## Skill

[`manage-hpac-infrastructure`](../../skills/manage-hpac-infrastructure/SKILL.md)
gains "Terraform vs. the deploy role":

- pin a third-party AMI (or any external image) to an exact, dated build, not
  a module or package version string, and confirm the lookup resolves to
  exactly one image before relying on it;
- a tag value Terraform writes must match the deploy role's policy exactly,
  case included — IAM tag comparisons are case-sensitive.
