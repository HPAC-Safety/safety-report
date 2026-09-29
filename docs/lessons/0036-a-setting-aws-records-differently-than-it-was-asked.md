---
title: A setting AWS records differently than it was asked
description: Staging's first successful apply failed the release's drift re-plan, because CloudFront and RDS each store a setting differently from how Terraform requested it, which leaves a permanent diff.
type: lesson
date: 2026-09-29
issue: 645
status: accepted
---

# Lesson 0036 — A setting AWS records differently than it was asked

## Symptom

Release run 36512908028's `terraform apply` succeeded, and every staging
resource was created. "Re-plan and fail on drift" (CON-INF-013) then found
three in-place changes:

- CloudFront: `minimum_protocol_version = "TLSv1" -> "TLSv1.2_2021"`.
- RDS parameter group: `rds.force_ssl` `apply_method = "pending-reboot" -> "immediate"`.
- The site bucket policy, "known after apply".

## Root cause

- With CloudFront's default certificate, which staging uses, CloudFront
  records `TLSv1` whatever minimum was requested.
- RDS records `rds.force_ssl` with `pending-reboot`. The provider's default
  apply method is `immediate`.
- The bucket policy's data source depends on the distribution. While the
  distribution had a pending change, Terraform deferred the data source, so
  that "change" was a consequence of the first, not drift of its own.

Each is invisible to `terraform validate` and to a plan against an empty
account. It appears only when a plan follows a real apply, which is exactly
what the drift check does.

## Spec delta

None upstream. #645 sets `minimum_protocol_version` to `TLSv1` for the
default certificate and keeps `TLSv1.2_2021` for production's own
certificate. It declares `apply_method = "pending-reboot"` for
`rds.force_ssl`.

## Scenario

None. This is a process lesson, and no scenario can prove it.

## Skill

[`manage-hpac-infrastructure`](../../skills/manage-hpac-infrastructure/SKILL.md)
"Terraform vs. the deploy role" now says: declare every attribute as AWS
records it. A value AWS normalizes (a default certificate's TLS minimum, an
RDS parameter's apply method) is a permanent diff that fails the release's
drift re-plan. When the drift check fails, read which attribute flips and
match it.
