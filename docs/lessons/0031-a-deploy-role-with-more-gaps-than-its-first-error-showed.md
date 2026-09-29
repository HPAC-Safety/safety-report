---
title: A deploy role with more gaps than its first error showed
description: The deploy role's first real apply failed on servicecatalog:TagResource; auditing it against every resource Terraform creates found six more permission gaps that would each have failed a later release.
type: lesson
date: 2026-09-29
issue: 626
status: accepted
---

# Lesson 0031 — A deploy role with more gaps than its first error showed

## Symptom

Release run 36501034107 reached the first real `terraform apply` in staging
and failed: `hpac-safety-deploy … is not authorized to perform:
servicecatalog:TagResource`.

## Root cause

`bootstrap.sh`'s deploy-role policy had never been exercised against a real
account, and had never been checked resource by resource against everything
Terraform creates or every call the deploy job makes. AWS authorizes the tag
action on every create-with-tags, and `bootstrap.sh` had named this as a known
residual risk. An audit run after the first failure found six more gaps that
each would have failed a later release: tag-on-create refused for AppRegistry,
CloudFront, CloudWatch alarms, ACM, and the Resource Group; no update
allowance for RDS parameter-group parameters, the NAT ENI's
`source_dest_check`, `autoscaling:StartInstanceRefresh`, and
`cloudfront:CreateInvalidation`; CloudFront's origin access control and
response headers policy cannot carry tags at all; the secrets policy scoped
`secret:hpac-safety-*` while secrets are named `hpac-safety/<name>`; RDS's
`manage_master_user_password` creates a secret under `rds!db-*` as the caller;
security-group rule tagging was missing from `ec2:CreateAction`; and
AppRegistry's service-linked role was not in the allowed list.

## Spec delta

None upstream: this is the deploy role's IAM policy, not a product claim.
[ADR-0169](../decisions/ADR-0169-the-deploy-role-manages-what-is-tagged-ours-and-tags-only-as-ours.md)
amends ADR-0158's deploy-role policy: a `ManageWhatIsAlreadyTaggedOurs`
statement covers updates on a resource already tagged `Project=HPAC-Safety`,
tag-on-create statements are scoped to `aws:RequestTag/Project=HPAC-Safety`,
and the remaining six gaps are fixed by name.

## Scenario

None. This is a process lesson, and no scenario can prove it.

## Skill

[`manage-hpac-infrastructure`](../../skills/manage-hpac-infrastructure/SKILL.md)
gains "Terraform vs. the deploy role": audit the deploy role's policy against
every resource Terraform creates and every AWS call the deploy workflow
makes — tag-on-create, later updates, and non-tag-scopable services included
— before the role's first real use, not one error at a time.
