---
title: Moderation, authentication, and publication
description: Supporting detail for the member authentication, review, and public feed scenarios.
type: spec
area: moderation-authentication-and-publication
---

# Moderation, authentication, and publication

Supporting detail for
[`moderation-authentication-and-publication.feature`](moderation-authentication-and-publication.feature)
that doesn't fit Gherkin.

## Authentication boundary

Identity arrives as a signed JWT, presented as `Authorization: Bearer <jwt>`.
The API validates the signature, the issuer, the audience `hpac-safety-api`,
and the lifetime, then reads exactly two claims: the subject and the role.
Nothing else — not a name, not an email address, not a picture
([ADR-0064](../../docs/decisions/ADR-0064-jwt-bearer-authentication-with-three-roles.md)).

There is no allowlist and no user table. **This system stores no user records
at all** ([ADR-0065](../../docs/decisions/ADR-0065-no-user-records-identity-is-the-token-subject.md)).
Where a report's approver or an audit entry's actor is recorded, it is the
token subject as an opaque string that joins to nothing. Revoking access is the
identity provider's decision, and it takes effect when a token stops being
issued or expires.

The role claim's **name** is configuration; its **values** are the invariant
codes `user`, `safety_officer`, and `administrator`. A claim may be a string or
an array, and the highest role present wins. A validated token with no
recognized role authenticates as `User`.

The production provider is not yet chosen — Auth0 and AWS Cognito are the
candidates, and any provider emitting the claim shape above satisfies the
contract. In development the API issues its own genuinely signed token and
validates it through the same middleware, so only the issuer and the key differ
([ADR-0066](../../docs/decisions/ADR-0066-a-development-identity-provider-signed-with-a-dev-key.md)).
The third-party sign-in option is production-only; the browser learns whether
to offer it from `GET /api/auth/config`, never from a build flag.

A bearer token carries no ambient authority, so state-changing admin requests
need no CSRF protection.

## Roles

| Role | Capabilities |
|---|---|
| User | Proves HPAC membership. May submit an occurrence report. Nothing else — no review, authoring, or publication capability. |
| SafetyOfficer | View the review queue and private report material; view safe image/video derivatives and download validated unredacted documents; edit the bilingual summary pair; approve, reject, publish, and soft-delete reports. |
| Administrator | Every SafetyOfficer capability, plus create question revisions and curate each question's choices. |

Submission is a membership capability rather than a privileged one, so any of
the three roles may file a report — and the report records nothing about who
did ([ADR-0067](../../docs/decisions/ADR-0067-a-reporter-must-be-a-member-and-is-not-recorded.md)).

`Administrator` is not a superuser. No Administrator, migration, background
worker, or direct API caller can bypass a publication guard.

## Public DTO edge state

The requested UI locale may determine which text is displayed first but is
edge state, not extra report data.

## Out of scope

What not to build here. The global list in
[system overview](../../docs/system-overview.md) still holds; this narrows it
to this area ([ADR-0083](../../docs/decisions/ADR-0083-specification-driven-development.md)).

- A user table, an allowlist, an allowlist-management screen, or a session
  store. Roles come from the token
  ([ADR-0065](../../docs/decisions/ADR-0065-no-user-records-identity-is-the-token-subject.md)).
- Handling a member's password, outside the one Development-only carve-out
  ([ADR-0079](../../docs/decisions/ADR-0079-a-development-login-may-verify-against-the-live-members-site.md)).
- CSRF machinery or Turnstile. A bearer token carries no ambient authority
  ([ADR-0068](../../docs/decisions/ADR-0068-the-member-token-replaces-turnstile-on-submission.md)).
- Email, push, or chat notification of a reviewer, a reporter, or anyone else.
- Any publication channel besides the HPAC public feed.
- Automatic approval or publication, including "approve if the model is
  confident."
- A per-reporter rate limit, which would mean identifying the reporter.
