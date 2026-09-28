---
title: The CloudFront origin secret is Terraform-generated, the one exception to "entries, never values"
description: Terraform originates the CloudFront origin-verify header's value itself, with random_password, and writes it into both CloudFront's origin configuration and a Secrets Manager version — the one secret this repository lets Terraform create a value for, because both its readers are configuration Terraform itself owns.
type: adr
status: accepted
date: 2026-09-27
decision-makers: Chase Florell
keywords: CloudFront, origin secret, Secrets Manager, random_password, Terraform state, CON-INF-005, ADR-0159
---

# ADR-0163 — The CloudFront origin secret is Terraform-generated, the one exception to "entries, never values"

## Status

Accepted — including holding this one value in Terraform state, confirmed by
the owner on issues #30 and #465. Narrows
[CON-INF-005](../infrastructure-and-operations.md) (secret values live in
Secrets Manager and never in Terraform state) for exactly one entry: this
routing token, and nothing else. Every other secret — Gemini, DeepL, and the
database credentials (the RDS-managed master password, never read by
Terraform at all) — stays out of state, unchanged. Implements the mechanism
[ADR-0159](ADR-0159-cloudfront-routes-api-to-a-function-url-no-alb.md) named
but did not specify: "a CloudFront-injected secret origin header, value from
Secrets Manager."

**Why accepting this in state is a narrow exception, not a crack in the
rule:**

- **CloudFront's own distribution config holds this value in state
  regardless of how it is generated.** `aws_cloudfront_distribution.site`'s
  `origin.custom_header.value` is a plain resource argument; Terraform
  stores every resource argument in state whether Terraform or a human chose
  it. Not using `random_password` would not keep this value out of state —
  it would only move the choice of value to a human typing it into two
  places (see "Context" below for why that is worse), while the
  value still ends up in the same state file either way.
- **It protects no data by itself.** The header only proves a request
  reached the API through this specific CloudFront distribution rather than
  by calling the Function URL directly; every actual authorization decision
  — which member, which role, which report — is still made by the JWT and
  role checks behind it. Leaking this value lets an attacker skip the
  "came through CloudFront" check, not skip authentication or authorization.
- **State access is already restricted to the two roles that need it.** The
  state bucket (`infra/bootstrap.sh`) is encrypted, versioned, and blocks
  public access; only `hpac-safety-deploy` (read/write) and
  `hpac-safety-plan` (read-only) can reach it, in the one account it
  belongs to. This is the same access boundary every other Terraform-managed
  configuration value in this system already relies on — ARNs, bucket names,
  security group rules — none of which are secret, but none of which are
  meant for public exposure either.

## Context

Every other secret this system holds — the connection string, the identity
provider's client secret, the Gemini and DeepL keys — has exactly one
consumer that needs the value at runtime (a Lambda function's environment,
populated by the deploy workflow from Secrets Manager), and a human is the
only party who can reasonably choose it: nobody but a person can type in a
vendor's issued API key. `infra/secrets.tf` creates the entry; an operator
supplies the value out of band; `aws_secretsmanager_secret_version` never
appears in this directory for any of them.

The CloudFront origin-verify secret is a different shape. It has two
consumers, not one: CloudFront's own origin configuration (`infra/cdn.tf`),
which sends it as a literal custom header on every `/api/*` request, and the
API, which reads it from Secrets Manager (via the deploy workflow, the same
as every other secret) and refuses any request that doesn't carry the
matching value. Both consumers must agree on the exact same string, and
CloudFront cannot read Secrets Manager at request time to check — its custom
header value is configuration, not a runtime lookup.

If a human chose this value, they would have to type it correctly into two
places at once (`aws secretsmanager put-secret-value` and a CloudFront
distribution update) with no way for either side to notice a mismatch until
the API starts refusing every CloudFront-forwarded request. Worse, this
system's design principles (ADR-0158, "design principles carried forward
from #30") require that a release re-plans after it applies and fails on any
drift — so if an operator later corrected a typo by editing CloudFront's
custom header directly in the console (matching Secrets Manager), the next
Terraform plan would either revert the fix (if Terraform still held its own,
different literal) or, if the two values were made to `ignore_changes`,
Terraform would stop being able to manage that origin at all, since
`ignore_changes` only applies to the whole `origin` block list, not one
nested field within it.

This value has no vendor and needs no human judgment: it exists solely to
prove a request reached the API through this specific CloudFront distribution
rather than by calling the Function URL directly. Nothing about it resembles
the case CON-INF-005 was written for.

## Decision

Terraform originates this one value itself, with `random_password`, and
writes it to both places that need it in the same apply:

- `infra/cdn.tf`'s `/api/*` origin's `custom_header` value is
  `random_password.cloudfront_origin_secret.result` directly — a literal
  Terraform manages every apply, never a placeholder and never
  `ignore_changes`.
- `infra/secrets.tf` creates `aws_secretsmanager_secret_version` for this ONE
  entry, from the same `random_password` resource. This is the sole
  `aws_secretsmanager_secret_version` resource anywhere in `infra/`; if a
  second one is ever added for a different secret, that is very likely a
  defect — see `infra/secrets.tf`'s header comment, which states the rule and
  points here for the one exception.

Consequence accepted: this value lives in Terraform state, unlike every other
secret in this system. State access is already something to guard (the state
bucket is private, encrypted, and reached only by the two OIDC roles); this
is one more reason it needs to stay that way, not a new one.

## Alternatives considered

- **A human-chosen value, entry only (the CON-INF-005 default).** Rejected
  for the reasons above: two places to keep in sync by hand, with drift that
  either breaks the API silently or fights every subsequent Terraform plan.
- **CloudFront reads Secrets Manager at request time.** Not something
  CloudFront's origin configuration can do; a custom header value is part of
  the distribution's own configuration, not a runtime lookup.
- **`AWS_IAM` authorization on the Function URL, with CloudFront signing the
  request.** ADR-0159 already rejected this: CloudFront does not support
  SigV4-signing requests to a Lambda Function URL origin the way it does for
  S3.

## Consequences

- `infra/secrets.tf`'s header comment states the general rule ("entries,
  never values") and this one exception, with a pointer here.
- [CON-INF-005](../infrastructure-and-operations.md) is narrowed to name this
  as the one entry whose value Terraform creates.
- A rotation of this value is a plan-and-apply, same as any other Terraform
  change — there is no separate manual rotation step for it, unlike the
  vendor keys.
