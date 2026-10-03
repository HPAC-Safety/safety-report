---
title: A guard checked against the resource a call creates
description: Staging's apply was refused three more ways once it reached the Lambda functions, security-group rules, and NAT Auto Scaling group — a reserved Lambda variable, a tag guard AWS evaluated against a brand-new untaggable rule, and a launch that needed permission on another account's AMI.
type: lesson
date: 2026-09-29
issue: 643
status: accepted
kind: incident
---

# Lesson 0035 — A guard checked against the resource a call creates

## Symptom

Release run 36511888790 created the secret version and both security
groups, then failed three ways:

- `lambda:CreateFunction` (worker): `contains reserved keys ... AWS_REGION`.
- `ec2:AuthorizeSecurityGroupEgress` on `security-group-rule/*`: explicit
  deny from `hpac-safety-deploy-guardrails`.
- `autoscaling:CreateAutoScalingGroup`: `You are not authorized to use launch
  template`.

## Root cause

- `infra/lambda.tf` passed `AWS_REGION`, which Lambda sets itself and refuses
  as input.
- AWS authorizes one call against every resource it touches, including the
  one it creates. `AuthorizeSecurityGroupEgress` is checked against the
  (tagged) security group **and** the new rule, which has no tag yet. The
  guardrail denied `ec2:Authorize*` on anything not tagged ours, so it denied
  the rule.
- Launching from a launch template needs `ec2:RunInstances` on each resource
  it names, the AMI included. fck-nat's AMI belongs to another account and
  can never carry our tag. Neither the tag-scoped allow nor the request-tag
  allow could match it.

As in [lesson 0034](0034-terraform-arguments-and-tags-never-checked-against-aws-and-the-deploy-role.md),
nothing checked these against AWS before a real apply reached them.

## Spec delta

None upstream. #643 removes `AWS_REGION` from the Lambda environment. It
scopes the `Authorize*`/`Revoke*` guardrail to `security-group/*` not tagged
ours (`NeverChangeAnotherApplicationsSecurityGroupRules`), and allows
`ec2:RunInstances` on `image/*` (`LaunchFromAnyImage`). The instance, volume,
and interface still need our request tag.

## Scenario

None. This is a process lesson, and no scenario can prove it.

## Skill

[`manage-hpac-infrastructure`](../../skills/manage-hpac-infrastructure/SKILL.md)
"Terraform vs. the deploy role" now says:

- A tag condition must name the resource type it guards. AWS evaluates a
  call against every resource it touches, including one it is creating that
  cannot be tagged yet (a security-group rule) and one owned by another
  account (a public AMI).
- Never set a Lambda-reserved environment variable (`AWS_REGION`,
  `AWS_LAMBDA_*`, `_HANDLER`, and the rest); the runtime provides them.
