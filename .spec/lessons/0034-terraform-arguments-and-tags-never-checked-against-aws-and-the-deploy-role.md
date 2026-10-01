---
title: Terraform arguments and tags never checked against AWS and the deploy role
description: Staging's first full apply created about 60 resources, then failed four more ways an ECR-repository-name mismatch had already foreshadowed, because no resource argument or tag flow had been checked against AWS's own constraints or the deploy role's guardrails before it ran for real.
type: lesson
date: 2026-09-29
issue: 637
status: accepted
---

# Lesson 0034 — Terraform arguments and tags never checked against AWS and the deploy role

## Symptom

- Release run 36507241225 failed at staging's "Create the image registries":
  `not authorized to perform: ecr:CreateRepository on resource:
  arn:…:repository/hpac-safety/api`.
- Release run 36508320765, after that fix, created about 60 staging resources
  — RDS, the VPC, S3, IAM — then failed on four more errors: `CreateSecurityGroup`
  refused non-ASCII characters (an em dash and an apostrophe) in the api and
  worker descriptions; `autoscaling:CreateAutoScalingGroup` on the NAT
  instance's group was denied for carrying no `Project` tag;
  `logs:PutRetentionPolicy` on the freshly created Lambda log groups hit an
  explicit deny; `secretsmanager:GetSecretValue` on the CloudFront origin
  secret hit an explicit deny.
- The same failed run left `aws_cloudwatch_log_group.this["api"]` and
  `["worker"]` tainted — AWS had created them before `PutRetentionPolicy` was
  denied — and both carry `prevent_destroy`. Every later plan then failed
  `Instance cannot be destroyed` until someone untainted them by hand
  (issue #640, fixed by #641).

## Root cause

Each was the same class of gap as #635/#636's ECR name, found only once a
real apply exercised it:

- `infra/ecr.tf` named the repositories `hpac-safety/api` and
  `hpac-safety/worker`; the deploy role's ECR statements match only
  `repository/hpac-safety-*`.
- The security-group descriptions used characters AWS's `CreateSecurityGroup`
  does not accept for `GroupDescription`.
- Provider `default_tags` do not reach an `aws_autoscaling_group`'s own `tag`
  blocks, and the fck-nat module was passed only `var.tags` (`{Name}`), so the
  ASG it creates carried no `Project` tag for the guardrail to match.
- The log-group guard, `NeverMutateAnUntaggedResource`, gated `logs:Put*` on
  `aws:ResourceTag/Project`. `PutRetentionPolicy` on log groups Terraform had
  just created with that tag was still denied: a tag condition holds only if
  the service evaluates `aws:ResourceTag` for that action, and nothing had
  checked that CloudWatch Logs does.
- The Terraform AWS provider reads a secret's value back immediately after
  creating it, and again on every later plan; the blanket
  `NeverReadASecretValueEvenOurOwn` deny refused the provider's own read of
  its own Terraform-generated secret.
- A resource AWS creates before a later step in the same apply fails is left
  tainted; with `prevent_destroy` set, Terraform's next plan tries to replace
  it anyway and refuses.

None of these had been checked against AWS's actual constraints or the
deploy role's actual policy before a real apply hit each one in turn.

## Spec delta

None upstream: this is Terraform resource arguments and the deploy role's
guardrails, not a product claim. #636 renamed the ECR repositories to
`hpac-safety-api`/`hpac-safety-worker`.
[ADR-0171](../decisions/ADR-0171-terraform-reads-back-only-the-origin-secret-and-log-groups-are-guarded-by-name.md) amends
ADR-0158/ADR-0169: both security-group descriptions were rewritten to AWS's
allowed character set; the fck-nat module now receives
`merge(local.tags, { Name = … })`; the log-group guard now matches by name
(`/aws/lambda/hpac-safety-*`, `/aws/rds/instance/hpac-safety*`) instead of by
tag; the deploy and plan roles may read back only
`hpac-safety/cloudfront-origin-secret-*`. #641 (issue #640) adds an untaint step to
`deploy-environment.yml`, run before the first apply, over every
`prevent_destroy` resource `infra/*.tf` declares.

## Scenario

None. This is a process lesson, and no scenario can prove it.

## Skill

[`manage-hpac-infrastructure`](../../skills/manage-hpac-infrastructure/SKILL.md)
gains "Terraform vs. the deploy role":

- every resource name Terraform creates must match an ARN pattern in
  `bootstrap.sh`'s deploy policy — check both in the same pull request;
- security-group (and rule) descriptions accept only AWS's allowed character
  set — no em dash, curly quote, or other non-ASCII character;
- provider `default_tags` do not reach an `aws_autoscaling_group`'s own `tag`
  blocks or a third-party module's own `tags` argument — pass `local.tags`
  explicitly;
- a tag-gated guard on a resource that is tagged in the same call that
  creates it needs a name-based exception, not only a tag-based one;
- a Terraform-managed secret's value is read back by the provider on create
  and on every later plan — the deploy and plan roles need read access to
  that one secret;
- a failed create can leave a resource tainted; a `prevent_destroy` resource
  that is tainted blocks every later plan — untaint before applying.
