---
title: A trust policy named a subject form GitHub no longer sends
description: bootstrap.sh's OIDC trust policies matched the name-only subject form, but this repository has GitHub's immutable subject turned on, so no token it issues ever matched.
type: lesson
date: 2026-09-28
issue: 612
status: accepted
---

# Lesson 0027 — A trust policy named a subject form GitHub no longer sends

## Symptom

`plan (staging)` on PR #611 failed with `Not authorized to perform
sts:AssumeRoleWithWebIdentity` (run 36493783577). The first staging release's
deploy job would have failed the same way for `hpac-safety-deploy`.

## Root cause

`infra/bootstrap.sh` trusted the name-only OIDC subject
(`repo:HPAC-Safety/safety-report:pull_request`,
`…:environment:hpac-safety-staging`). This repository has
`use_immutable_subject: true`
(`gh api repos/HPAC-Safety/safety-report/actions/oidc/customization/sub`), so
every token GitHub issues carries the organization and repository IDs instead
(`repo:HPAC-Safety@307760008/safety-report@1341995834:<context>`). The trust
policy's `StringEquals` condition never matched, and nothing about the
mismatch was visible until a role was actually assumed.

## Spec delta

None upstream: this is the OIDC trust subject in a bootstrap script, not a
product claim. [ADR-0167](../decisions/ADR-0167-oidc-trust-names-the-immutable-subject.md)
records the decision to trust the immutable form rather than turn the
repository's customization off; `bootstrap.sh`, CON-INF-007, and every
document quoting the subject were corrected to match.

## Scenario

None. This is a process lesson, and no scenario can prove it.

## Skill

[`manage-hpac-infrastructure`](../../skills/manage-hpac-infrastructure/SKILL.md)
gains "Terraform vs. the deploy role": confirm this repository's actual OIDC
subject form with the GitHub API before writing a trust policy, rather than
assuming the name-only form.
