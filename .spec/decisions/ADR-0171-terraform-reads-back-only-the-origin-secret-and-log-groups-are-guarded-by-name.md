---
title: Terraform reads back only the origin secret, and log groups are guarded by name
description: The deploy and plan roles may read one secret value, the Terraform-generated CloudFront origin secret, because the provider reads a secret version back on create and on every plan and its value is already in Terraform state; every other secret value stays unreadable. The guardrail on log-group mutation matches by name instead of by tag.
type: adr
status: accepted
date: 2026-09-28
decision-makers: Chase Florell
keywords: bootstrap.sh, hpac-safety-deploy, hpac-safety-plan, GetSecretValue, cloudfront-origin-secret, CloudWatch Logs, PutRetentionPolicy, guardrails, ADR-0158, ADR-0159, ADR-0169
---

# ADR-0171 — Terraform reads back only the origin secret, and log groups are guarded by name

**Status:** Accepted. Amends the deploy and plan roles' explicit denies in
[ADR-0158](ADR-0158-two-aws-accounts-staged-and-promoted-by-approval.md) and
[ADR-0169](ADR-0169-the-deploy-role-manages-what-is-tagged-ours-and-tags-only-as-ours.md).
Issue [#637](https://github.com/HPAC-Safety/safety-report/issues/637).

## Context

Staging's first full apply (release run 36508320765) was refused twice by
the deploy role's own guardrails:

- `secretsmanager:GetSecretValue` on `hpac-safety/cloudfront-origin-secret`.
  The AWS provider reads a secret version back after creating it and again on
  every refresh. Both roles denied reading any secret value, ours included.
- `logs:PutRetentionPolicy` on `/aws/lambda/hpac-safety-api` and `-worker`.
  `NeverMutateAnUntaggedResource` gated `logs:Put*` on
  `aws:ResourceTag/Project`, which did not match the log groups Terraform had
  just created with that tag.

The origin secret is the one value Terraform generates itself
(`random_password`, ADR-0159). CloudFront's origin configuration needs the
same literal, so the value is already in Terraform state, which both roles
read.

## Decision

- **One readable secret.** Both roles may `GetSecretValue` on
  `secret:hpac-safety/cloudfront-origin-secret-*` only. The deny on every
  other secret stays, so vendor keys and the database's master secret remain
  unreadable to either role.
- **Log groups by name.** `NeverMutateAnotherApplicationsLogGroup` denies
  `logs:Delete*`, `logs:Put*`, untagging, and `DisassociateKmsKey` on every
  log group except `/aws/lambda/hpac-safety-*` and
  `/aws/rds/instance/hpac-safety*`. That is the same scope
  `ManageOurLogGroupsOnly` allows. The `logs` actions leave the tag-based
  `NeverMutateAnUntaggedResource`.

## Consequences

- Reading the origin secret exposes nothing either role could not already
  read from state. It proves only that a request came through CloudFront, and
  it guards no report data.
- Another application's log groups stay protected by name. Our own are
  guarded the way ECR, Lambda, and S3 already are.
- The owner re-runs `infra/bootstrap.sh` for each account to apply both.

## Considered options

- **A write-only secret attribute (`secret_string_wo`)**: the provider still
  looks the version up after creating it, and it would split the origin
  secret's handling from CloudFront's literal header value.
- **Keep the tag condition for logs and tag the log groups first**: they were
  already tagged at creation and were still refused. A name match does not
  depend on how CloudWatch Logs evaluates `aws:ResourceTag` for each action.
