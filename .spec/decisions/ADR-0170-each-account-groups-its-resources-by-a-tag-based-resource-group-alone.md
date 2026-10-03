---
title: Each account groups its resources by a tag-based Resource Group alone
description: AWS closed Service Catalog AppRegistry (myApplications) to accounts that had never used it on 2026-07-30, so neither account can create the hpac-safety-<environment> application ADR-0158 called for; the tag-based Resource Group of the same name, which AWS names as the replacement, is the only grouping, and the deploy role loses every AppRegistry permission.
type: adr
status: accepted
date: 2026-09-28
decision-makers: Chase Florell
keywords: AppRegistry, myApplications, Resource Groups, grouping, awsApplication, tags, bootstrap.sh, hpac-safety-deploy, ADR-0158, ADR-0169
---

# ADR-0170 — Each account groups its resources by a tag-based Resource Group alone

**Status:** Accepted. Amends "Grouping" and the deploy role's "Can manage" list in
[ADR-0158](ADR-0158-two-aws-accounts-staged-and-promoted-by-approval.md), and
the AppRegistry parts of
[ADR-0169](ADR-0169-the-deploy-role-manages-what-is-tagged-ours-and-tags-only-as-ours.md).
Issue [#633](https://github.com/HPAC-Safety/safety-report/issues/633).

## Context

ADR-0158 gave each account a myApplications application (Service Catalog
AppRegistry) and a tag-based Resource Group, both named
`hpac-safety-<environment>`. Every resource carried the application's
`awsApplication` tag through `local.app_tags`, so every resource depended on
the application.

The first staging apply to reach it (release run 36506227548) was refused:

> AWS Service Catalog AppRegistry is in maintenance mode and is no longer
> available to new customers as of July 30, 2026.

[AWS's notice](https://docs.aws.amazon.com/servicecatalog/latest/arguide/app-registry-availability-change.html)
says accounts that had not used AppRegistry before that date cannot use it
at all. Neither of our accounts had. It names tagging and a tag-based AWS
Resource Group as the replacement for grouping resources.

## Decision

- **No AppRegistry application.** `infra/grouping.tf` keeps only the
  `hpac-safety-<environment>` Resource Group, matching `Project=HPAC-Safety`
  and `Environment=<environment>`. No resource carries an `awsApplication`
  tag; each keeps the four `default_tags` and its `Name`.
- **The output is `resource_group`** (name and ARN), replacing
  `myapplications`.
- **The deploy role has no AppRegistry permission.** `infra/bootstrap.sh`
  drops every `servicecatalog:*` action and the AppRegistry service-linked
  role from all four policies.

## Consequences

- The console grouping is the Resource Group. Cost visibility comes from the
  `Project` and `Environment` tags, activated as cost-allocation tags.
- ADR-0169's accepted residual risk shrinks to an untagged ACM certificate or
  CloudFront distribution of another workload.
- Re-running `infra/bootstrap.sh` removes the AppRegistry permissions from an
  account's deploy role. Until then they remain granted but unused; the
  deploy needs nothing new.

## Considered options

- **AWS Resource Explorer**: search across Regions and accounts. Not needed
  for one Region per account, and it adds an index to manage.
- **CloudWatch Application Signals**: an observability product, paid beyond
  its free tier, and not a grouping of resources.
