---
title: A service closed to new accounts
description: Terraform's AppRegistry application could never be created, because AWS closed AppRegistry to new customers months before the first apply tried to use it.
type: lesson
date: 2026-09-29
issue: 633
status: accepted
kind: incident
---

# Lesson 0033 — A service closed to new accounts

## Symptom

Release run 36506227548 failed in staging's "Create the image registries"
step: `creating AWS Service Catalog AppRegistry Application
("hpac-safety-staging"): … AccessDeniedException: AWS Service Catalog
AppRegistry is in maintenance mode and is no longer available to new
customers as of July 30, 2026.`

## Root cause

`infra/grouping.tf`'s `aws_servicecatalogappregistry_application.this`
(ADR-0158) could never be created in either account: AWS's own notice states
that an account that never used AppRegistry cannot access it from
2026-07-30 forward. Every resource also carried `local.app_tags`, that
application's computed tag, so even the targeted ECR apply depended on a
resource that could never exist. Nothing in the Terraform configuration or
the deploy role flagged this before the first apply reached it — a
dependency on an external AWS service's availability was never checked.

## Spec delta

None upstream: this is the AWS-console grouping of infrastructure resources,
not a product claim.
[ADR-0170](../decisions/ADR-0170-each-account-groups-its-resources-by-a-tag-based-resource-group-alone.md) removes
the AppRegistry application and `local.app_tags`; the tag-based
`hpac-safety-<environment>` Resource Group, which already existed, becomes
the only grouping. `bootstrap.sh` drops the AppRegistry permissions and
service-linked role.

## Scenario

None. This is a process lesson, and no scenario can prove it.

## Skill

[`manage-hpac-infrastructure`](../../skills/manage-hpac-infrastructure/SKILL.md)
gains "Terraform vs. the deploy role": before depending on an AWS service
(AppRegistry or any other), check that it is still open to new accounts —
AWS can close a service to new customers with no signal visible in Terraform
or the deploy role until the resource is created.
