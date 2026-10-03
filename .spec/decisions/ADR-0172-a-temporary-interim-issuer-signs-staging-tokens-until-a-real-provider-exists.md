---
title: A temporary interim issuer signs staging tokens until a real provider exists
description: The API runs its own RS256 identity provider in staging, behind one flag, so staging is usable end to end before an identity provider is chosen. Deleted in one sweep once one is.
type: adr
status: accepted
date: 2026-09-28
decision-makers: Chase Florell
keywords: interim issuer, RS256, JWKS, staging, temporary, identity provider, ADR-0064, ADR-0079, ADR-0158
---

# ADR-0172 — A temporary interim issuer signs staging tokens until a real provider exists

**Status:** Accepted. **Temporary** — every part of this decision is deleted,
not superseded, once [ADR-0064](ADR-0064-jwt-bearer-authentication-with-three-roles.md)'s
identity provider choice is made. This ADR amends
[AGENTS.md](../../AGENTS.md) invariant 7 and its "Not built" section, and
[ADR-0079](ADR-0079-a-development-login-may-verify-against-the-live-members-site.md),
to carve staging as a second place the Development-only members-site login
and administrator allowlist run.

## Context

Staging ([ADR-0158](ADR-0158-two-aws-accounts-staged-and-promoted-by-approval.md))
has no identity provider. [ADR-0064](ADR-0064-jwt-bearer-authentication-with-three-roles.md)
defers that choice, and [ADR-0158's amendment](ADR-0158-two-aws-accounts-staged-and-promoted-by-approval.md)
("`AN environment with no identity provider configured still starts")
makes that a stated limitation rather than a startup failure: staging starts
and serves its public endpoints, but no bearer token can ever validate there,
so nobody can sign in, file a report as a member, review, or administer.

The owner wants staging usable end to end now (issue #648), through a
temporary mechanism **in the API**, not a separate service, deleted once a
real provider exists.

## Decision

Behind one configuration flag, `HpacSafety:Authentication:InterimIssuer:Enabled`
— honored only outside Development, and mutually exclusive with a configured
`Authority` (a real provider replaces this; the host refuses to start with
both set) — this API becomes its own small RS256 identity provider:

- **Signing key**: an RSA private key PEM, held in Secrets Manager as
  `hpac-safety/interim-issuer-signing-key`, resolved by ARN at cold start the
  same way the CloudFront origin secret and the DeepL key already are (#597).
  Terraform creates the entry only, with no
  `aws_secretsmanager_secret_version` and no `prevent_destroy` — unlike every
  other secret this repository manages, this one is meant to be deleted
  outright, entry included, once the feature is. `deploy-environment.yml`
  generates the one value with `openssl` and puts it only if the entry has no
  `AWSCURRENT` version yet; the deploy role can put a secret value but never
  get one (ADR-0171), so it is never echoed or read back.
- **Issuer**: the fixed string `urn:hpac-safety:interim-issuer`
  (`InterimTokenIssuer.IssuerName`) — a URN, not a URL, because nothing ever
  fetches provider metadata from it. Validation is pinned in-process to this
  issuer and this host's own public key, exactly like the Development
  issuer's `TokenValidationParameters` — no `Authority`, no metadata fetch,
  and so no CloudFront/Lambda Terraform cycle.
- **Endpoints**, mapped only where the flag is on:
  - `GET /api/auth/interim/.well-known/openid-configuration` — issuer,
    `jwks_uri`, `token_endpoint`, and `id_token_signing_alg_values_supported:
    ["RS256"]`. Exists for a client that wants a discovery document; this
    API's own validation never reads it.
  - `GET /api/auth/interim/jwks` — the public key only (`kty`, `n`, `e`,
    `kid`), never the private exponent.
  - `POST /api/auth/token` — the **same route** Development already maps,
    reusing **only `MembersSiteCredentialSource`** (ADR-0079): the password is
    checked live against the members site, and role comes from
    `MembersSiteLoginOptions`' email allowlists. The owner's words: "We only
    use the hard-coded accounts to determine administrators" — meaning those
    allowlists, not the fixed accounts themselves.
    **`FixedAccountCredentialSource` is never registered here.** Development's
    `admin`/`admin`, `officer`/`officer`, and `user`/`user` accounts exist only
    on a developer's own machine; registering them for the interim issuer
    would let anyone reach staging's public address and sign in as
    Administrator with a well-known password. A shared `IMemberTokenIssuer`
    abstraction lets `AuthEndpoints`'s handler serve either issuer without
    knowing which one is registered, but the two issuers' credential sources
    are not the same list.
- **Terraform**: `interim_issuer_enabled` (bool), `false` by default,
  `true` in `staging.tfvars`, explicitly `false` in `production.tfvars`. When
  true, the API Lambda function's role gains `secretsmanager:GetSecretValue`
  on the one new secret, and its environment carries the flag, the secret's
  ARN, and the two `MembersSiteLogin` email lists as plain (non-secret)
  environment variables — the same two accounts already committed, in plain
  text, in `appsettings.Development.json`. No change to `infra/bootstrap.sh`:
  the deploy role's existing `ManageOurSecretsOnly` statement already covers
  `Describe*`/`PutSecretValue` on `hpac-safety/*`, and the deploy role is
  already denied `GetSecretValue` on everything but the CloudFront origin
  secret (ADR-0171) — this secret needs no new grant either way.
- **Naming**: every symbol this touches — options, types, endpoints, the
  Terraform variable, the secret, the workflow step — says "Interim." Finding
  everything to delete, later, is a search for that word, not an audit.

## Why

**In the API, not a separate service.** A second Lambda function, or a
hosted OIDC-as-code product, is more than a one-flag-and-one-secret stopgap
justifies for an association receiving dozens of reports a year, and it would
still need deleting later; this way there is nothing to decommission but
configuration and one file tree.

**The same members-site login as Development, minus the fixed accounts**,
because ADR-0079 already answered every question a members-site-backed login
raises (cookie handling, generic failure messages, the down-site distinction,
the allowlist's shape) and reusing `MembersSiteCredentialSource` costs
nothing. `FixedAccountCredentialSource` is deliberately left out: its three
memorable passwords are fine on a developer's own machine, reachable only
from `localhost`, but staging is a public address, and registering them there
would let anyone sign in as Administrator. This ADR does not reopen
ADR-0079's reasoning; it only extends where the members-site mechanism is
allowed to run, and narrows which of its two credential sources comes along.

**RS256 with a real JWKS**, not HS256 with a shared secret, so validation
looks like it will against a real provider — an `Authority`-less, in-process
`TokenValidationParameters` pinned to a public key, not a shared symmetric
key baked into every consumer. Nothing about switching to a real provider
later has to change shape.

**Mutually exclusive with `Authority`.** The moment a real provider is
configured, it must win outright — allowing both would either silently
prefer one (surprising) or double the validated issuers (a bigger attack
surface for no reason). Refusing to start is the same posture
`DevelopmentSigningKey`'s absence already takes in Development.

## Considered options

- **A hosted OIDC-as-code product for staging.** Rejected: more
  infrastructure than a temporary stopgap justifies, and it still needs
  deleting later.
- **Reuse the Development HS256 issuer and key outside Development.**
  Rejected: a shared symmetric key baked into a deployed Lambda function's
  environment is a materially worse posture than an asymmetric key whose
  private half never leaves Secrets Manager and the function's own memory,
  and it would not exercise the RS256/JWKS shape a real provider uses.
- **A brand-new credential check for staging**, independent of ADR-0079.
  Rejected: it would re-litigate decisions ADR-0079 already made, for no
  functional difference — the owner explicitly asked for the same login.

## Consequences

- `AGENTS.md` invariant 7's "Development-only exception" bullet is amended to
  say the members-site login and its administrator allowlist also run in
  staging, behind this flag, until this ADR's removal plan below completes.
  "Not built"'s carved exception is amended the same way.
- `ADR-0079` and `ADR-0064` each gain a short status note pointing here;
  neither's own decision changes.
- `docs/deployment.md` and the moderation feature's README record that
  staging sign-in now works, and that production still needs
  `AUTH_AUTHORITY`.
- New scenarios in
  `.spec/features/moderation-authentication-and-publication/moderation-authentication-and-publication.feature`:
  `@REQ-MOD-157` (sign-in works end to end with the flag on, and an
  allowlisted account is Administrator), `@REQ-MOD-158` (the interim
  endpoints and `/api/auth/token` do not exist with the flag off), and
  `@REQ-MOD-159` (the JWKS publishes only a public key).

### Removal plan

Once ADR-0064's provider is chosen and configured in staging:

1. Set `interim_issuer_enabled = false` in `staging.tfvars` and remove the two
   email-list variables' values (or delete the variables outright once
   nothing sets them).
2. Delete `aws_secretsmanager_secret.interim_issuer_signing_key` from
   `secrets.tf`, `interim_issuer_signing_key_secret` from `outputs.tf`, and
   the interim block from `iam.tf` and `lambda.tf`.
3. Delete `InterimIssuerOptions.cs`, `InterimTokenIssuer.cs`,
   `InterimIssuerSigningKey.cs`, `InterimIssuerEndpoints.cs`, and
   `IMemberTokenIssuer.cs` (folding `AuthEndpoints.Token` back onto
   `DevelopmentTokenIssuer` directly, or keeping the interface if a future
   provider still benefits from it).
4. Remove the interim branch from `AuthenticationServiceCollectionExtensions`
   and the `interimIssuerEnabled` parameter from `AuthEndpoints.MapAuth`.
5. Delete the "Generate the interim issuer signing key" step from
   `deploy-environment.yml`.
6. Revert this ADR's amendments to `AGENTS.md`, and mark this ADR superseded
   by whichever ADR records the chosen provider.

## Related

- [ADR-0064](ADR-0064-jwt-bearer-authentication-with-three-roles.md) — the deferred provider choice this stands in for
- [ADR-0079](ADR-0079-a-development-login-may-verify-against-the-live-members-site.md) — the credential check and allowlist this reuses unchanged
- [ADR-0158](ADR-0158-two-aws-accounts-staged-and-promoted-by-approval.md) — staging existing at all, and "no identity provider is a stated limitation"
- [ADR-0171](ADR-0171-terraform-reads-back-only-the-origin-secret-and-log-groups-are-guarded-by-name.md) — why the deploy role never reads this secret's value
- [`.spec/features/moderation-authentication-and-publication`](https://github.com/HPAC-Safety/safety-report/blob/35f00aca/.spec/features/moderation-authentication-and-publication/moderation-authentication-and-publication.feature)
