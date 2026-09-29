---
title: The deploy role manages what is already tagged ours, and tags only as ours
description: hpac-safety-deploy may run any action of the un-scopable services (ACM, Auto Scaling, CloudFront, CloudWatch, EC2, RDS, AppRegistry) on a resource already tagged Project=HPAC-Safety, and may add a tag at creation only when that tag is Project=HPAC-Safety; an untagged ACM certificate, CloudFront distribution, or AppRegistry application of another workload is the accepted residual risk.
type: adr
status: accepted
date: 2026-09-28
decision-makers: Chase Florell
keywords: bootstrap.sh, hpac-safety-deploy, IAM, aws:ResourceTag, aws:RequestTag, tag-on-create, TagResource, guardrails, staging, ADR-0158, CON-INF-011
---

# ADR-0169 — The deploy role manages what is already tagged ours, and tags only as ours

## Status

Accepted. Amends the deploy-role policy described under "Deploy credentials"
in [ADR-0158](ADR-0158-two-aws-accounts-staged-and-promoted-by-approval.md);
everything else there stands. Issue
[#626](https://github.com/HPAC-Safety/safety-report/issues/626).

**AppRegistry removed by
[ADR-0170](ADR-0170-each-account-groups-its-resources-by-a-tag-based-resource-group-alone.md):**
the system has no AppRegistry application, so every `servicecatalog` action
and the AppRegistry service-linked role below are gone, and the residual risk
is an untagged ACM certificate or CloudFront distribution alone.

## Context

The first real staging `terraform apply` (release run 36501034107) failed:
AWS authorizes `servicecatalog:TagResource` on every `CreateApplication` that
carries tags, and the policy allowed that action nowhere and denied it on any
resource not already tagged ours — a resource being created is not tagged yet.
`infra/bootstrap.sh` had named exactly this as its residual risk.

Auditing the policy against every resource Terraform creates and every deploy
step found the same shape of gap repeated. For the services whose resources
have AWS-assigned ids and so cannot be scoped by name (ACM, Auto Scaling,
CloudFront, CloudWatch, EC2, RDS, AppRegistry), the policy allowed read and
create-with-our-tag, and **no update at all**: not an RDS parameter group's
parameters, a NAT network interface's `source_dest_check`, the release's own
`StartInstanceRefresh` and `CreateInvalidation`, nor any later change.

## Decision

- **Manage what is tagged ours.** `ManageWhatIsAlreadyTaggedOurs` allows
  `acm:*`, `autoscaling:*`, `cloudfront:*`, `cloudwatch:*`, `ec2:*`, `rds:*`,
  and `servicecatalog:*` on a resource whose `aws:ResourceTag/Project` is
  `HPAC-Safety`. Every explicit deny (report data, log content, secret values,
  other applications' data, its own role) still wins.
- **Tag at creation only as ours.** `acm:AddTagsToCertificate`,
  `cloudfront:TagResource`, and `servicecatalog:TagResource` are allowed only
  when the request's `Project` tag is `HPAC-Safety`; `cloudwatch:TagResource`
  also only on `alarm:hpac-safety-*`; `resource-groups:Tag` only on this
  account's group by name. These five leave the blanket untagged-mutation
  deny, and `NeverRetagAnotherProjectsResource` denies them on a resource
  already tagged for another project.
- **Also fixed:** the four CloudFront configuration types that cannot carry
  tags (origin access control, response headers policy, cache policy,
  function) may be created untagged; secrets named `hpac-safety/<name>` are in
  scope beside `hpac-safety-*`; RDS may create our database's `rds!db-*`
  master secret as the caller; security-group rules may be tagged at
  creation; AppRegistry's service-linked role may be created.

## Consequences

- **Accepted residual risk:** an *untagged* ACM certificate, CloudFront
  distribution, or AppRegistry application belonging to another workload in
  the same account could be tagged `Project=HPAC-Safety` by this role and then
  changed. Their ARNs carry opaque ids, so no name scope can be written in
  advance. The role is assumable only by this repository's protected GitHub
  environments, running reviewed Terraform from a release tag. A resource
  tagged for any other project stays refused.
- CloudFront configuration objects that cannot carry tags are created, but
  the guardrail still refuses updating or deleting them; a change to one needs
  its own decision.
- The owner re-runs `infra/bootstrap.sh` in each account to publish the new
  policy versions.
- The policy is still proven only by a real `apply`; a further gap found
  there is fixed the same way, on its own issue.
