---
title: The OIDC trust policies name GitHub's immutable subject, with the organization and repository IDs
description: This repository's OIDC tokens carry the immutable subject repo:HPAC-Safety@307760008/safety-report@1341995834:<context>. Both AWS roles trust that exact subject, not the name-only form, so no re-created organization or repository of the same name can assume them.
type: adr
status: accepted
date: 2026-09-28
decision-makers: Chase Florell
keywords: OIDC, GitHub Actions, trust policy, immutable subject, bootstrap.sh, hpac-safety-deploy, hpac-safety-plan, ADR-0032, ADR-0158, ADR-0164, CON-INF-007
---

# ADR-0167 — The OIDC trust policies name GitHub's immutable subject, with the organization and repository IDs

**Status:** Accepted. Amends the subject form that
[ADR-0032](ADR-0032-terraform-ci-without-an-aws-account.md),
[ADR-0158](ADR-0158-two-aws-accounts-staged-and-promoted-by-approval.md), and
[ADR-0164](ADR-0164-release-workflow-build-once-deploy-and-promote.md) quote
(`repo:HPAC-Safety/safety-report:<context>`). Which contexts each role trusts
is unchanged.

## Context

`HPAC-Safety/safety-report` uses GitHub's immutable OIDC subject
(`gh api repos/HPAC-Safety/safety-report/actions/oidc/customization/sub`:
`use_immutable_subject: true`). Every token's `sub` names the organization and
repository by name and numeric ID:

- `repo:HPAC-Safety@307760008/safety-report@1341995834:pull_request`
- `repo:HPAC-Safety@307760008/safety-report@1341995834:environment:hpac-safety-staging`

`infra/bootstrap.sh` wrote both trust policies for the name-only form, which
no token from this repository carries. The first real run found it:
`terraform.yml`'s `plan (staging)` on PR #611 failed with
`Not authorized to perform sts:AssumeRoleWithWebIdentity` (#612). A release's
deploy job would have failed the same way.

## Decision

- `bootstrap.sh` holds `GITHUB_ORG_ID=307760008` and
  `GITHUB_REPO_ID=1341995834` beside the names, and both trust policies name
  `repo:HPAC-Safety@307760008/safety-report@1341995834:` followed by the same
  contexts as before: `environment:<GitHub environment>` for
  `hpac-safety-deploy`, `pull_request` for `hpac-safety-plan`. Both conditions
  stay `StringEquals`, never a wildcard.
- The IDs are constants, not looked up at run time, like the names and the
  region: bootstrap trusts exactly one repository, decided here.
- Re-running `bootstrap.sh` in an account converges its trust policies; it
  changes nothing else.

## Considered options

- **Turn the repository's immutable subject off**, so tokens carry the
  name-only form the policies already expected. Rejected (owner,
  2026-09-28): the IDs are what stop an organization or repository re-created
  under the same name, after a rename or deletion, from assuming either role.
- **Trust both forms.** Rejected: it keeps exactly the weakness the immutable
  form removes.

## Consequences

- The staging account's owner re-runs `bootstrap.sh staging` once after this
  merges. Production's first run picks it up.
- Transferring the repository or re-creating it changes an ID, and both roles
  stop trusting it until `bootstrap.sh` is updated and re-run. That is the
  intended failure.
- CON-INF-007's paragraph, `docs/deployment.md`, and `terraform.yml`'s comments
  quote the immutable form.
