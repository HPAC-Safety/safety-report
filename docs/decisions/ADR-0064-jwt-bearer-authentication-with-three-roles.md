---
title: JWT bearer authentication with three roles
description: "Identity arrives as a signed JWT, presented as Authorization: Bearer <jwt>."
type: adr
status: accepted
date: 2026-09-21
decision-makers: Chase Florell
keywords: authentication, JWT, bearer token, OIDC, roles, authorization, claims
---

# ADR-0064 — JWT bearer authentication with three roles

**Status:** Accepted. Supersedes
[ADR-0005](ADR-0005-authentication.md).

## Context

[ADR-0005](ADR-0005-authentication.md) investigated `members.hpac.ca` directly,
found no OAuth, and chose to proxy member credentials to its Rails login form.
That adapter was never built. `IMemberAuthenticator` has sat in Core with zero
implementations, and the admin endpoints have been gated by a header stub whose
own doc comment says it "proves nothing about who is asking."

The intent has changed. Production authentication will use a standards-based
OAuth/OIDC provider, and this application will never see a member's password.
That removes the single largest risk ADR-0005 accepted — handling real
credentials for a system we do not own.

A third role is also needed. Reporting is no longer open
([ADR-0067](ADR-0067-a-reporter-must-be-a-member-and-is-not-recorded.md)), so
there is now a meaningful identity that proves HPAC membership without
conferring any review capability.

## Decision

Identity arrives as a signed JWT, presented as `Authorization: Bearer <jwt>`.
The API validates it and reads exactly two things: the subject and the role.

Three roles, ordered:

| Role | Claim value | What it grants |
|---|---|---|
| User | `user` | Proves HPAC membership. May submit a report. Nothing else. |
| SafetyOfficer | `safety_officer` | Review queue, private report material, summary editing, approve/reject/publish/soft-delete. |
| Administrator | `administrator` | Every SafetyOfficer capability, plus question and choice-list authoring. |

The role **claim name** is configuration, defaulting to `roles`. The role
**claim values** are the invariant codes above. A claim may be a single string
or an array; the highest role present wins. A validated token carrying no
recognized role authenticates as `User` — membership is proven, and no admin
capability follows from it.

Validation requires a signature, an exact issuer match, the audience
`hpac-safety-api`, and a current lifetime with thirty seconds of clock skew.
`TokenValidationParameters.RoleClaimType` is set to the configured name so the
framework's own role checks work without a custom handler.

**The API reads `sub` and the role claim and nothing else.** Not `email`, not
`name`, not `given_name`, not `picture`. The subject is opaque: never parsed,
never split, never assumed to be an address.

Authorization is expressed as three policies — `Member`, `Reviewer`,
`Administrator` — applied at the endpoint. The API is the authorization
boundary; the delivery path never was
([ADR-0048](ADR-0048-one-website-admin-as-a-route.md)).

The concrete provider is **not decided here.** Auth0 and AWS Cognito are the
candidates. Any provider that can emit the claim shape above satisfies this
decision, and a later ADR records the choice.

## Why

Two issuers, one contract. Development mints its own token
([ADR-0066](ADR-0066-a-development-identity-provider-signed-with-a-dev-key.md))
and production takes one from the provider, but both travel the same middleware
and the same policies. The path a developer exercises is the path that runs in
production, differing only in issuer and key.

Making the claim *name* configurable while fixing the claim *values* is what
keeps the provider choice genuinely open: Auth0 wants a namespaced claim and
Cognito emits `cognito:groups`, but neither dictates what this application
calls a role.

Reading only the subject and the role is a privacy decision, not an oversight.
An identity provider will happily hand over a name and an email address. This
system has no use for either, and the narrowest possible read is the one that
cannot leak.

## Alternatives

- **The credential proxy (ADR-0005).** Rejected. It means holding real member
  passwords for a system we do not own, and depending on the exact markup of a
  page we do not control.
- **Local accounts.** Rejected. This project would then own password storage,
  reset, and breach risk for volunteers.
- **Session cookies with CSRF.** Rejected. A bearer token carries no ambient
  authority, so there is nothing for a cross-site request to forge, and a
  stateless token suits the Lambda-hosted path
  ([ADR-0042](ADR-0042-lambda-hosted-api-with-fargate-migration-path.md)).
- **Deciding the provider now.** Rejected as premature. Nothing in the
  application code changes between the candidates, so the choice can wait for
  the operational discussion it actually depends on — including the
  `ca-central-1` residency constraint, which is a point in Cognito's favour but
  not a decisive one.

## Consequences

- `IMemberAuthenticator`, `AdminRole`, and the credential-proxy design are
  deleted rather than adapted.
- A JWT signing key and, later, a provider client secret become secrets this
  system must handle.
- Choosing the provider changes configuration and a login redirect. If it
  requires touching domain code, the abstraction leaked and that is a bug to
  fix first ([ADR-0033](ADR-0033-third-party-libraries-behind-owned-abstractions.md)).

## Related

- [ADR-0005](ADR-0005-authentication.md) — superseded by this decision
- [ADR-0065](ADR-0065-no-user-records-identity-is-the-token-subject.md) — where identity is stored, which is nowhere
- [ADR-0066](ADR-0066-a-development-identity-provider-signed-with-a-dev-key.md) — the development issuer
- [ADR-0067](ADR-0067-a-reporter-must-be-a-member-and-is-not-recorded.md) — why a third role exists
- [`features/moderation-authentication-and-publication`](../../features/moderation-authentication-and-publication/moderation-authentication-and-publication.feature)
