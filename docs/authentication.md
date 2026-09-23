---
title: Authentication and authorization
description: Who must sign in, what a token carries, and what each role may do.
type: guide
---

# Authentication and authorization

Reading the public feed and a public report detail requires no account.
**Everything else — filing a report, and every review or administration
action — requires a signed-in HPAC member.**

## The token is the identity

Identity arrives as a signed JWT in `Authorization: Bearer <jwt>`. The API
validates the signature, an exact issuer match, the audience
`hpac-safety-api`, and the lifetime with thirty seconds of clock skew.

It then reads **two claims and no others**: the subject, and the role. Not an
email address, not a name, not a picture. The subject is opaque — never parsed,
never split, never assumed to be an address.

The role claim's *name* is configuration, defaulting to `roles`, because
providers disagree about where roles live. Its *values* are this repository's
invariant codes:

| Role | Claim value | Capabilities |
|---|---|---|
| User | `user` | Proves HPAC membership. May submit a report. Nothing else. |
| SafetyOfficer | `safety_officer` | Review queue and private report material, safe derivatives and validated documents, summary editing, approve/reject/publish/soft-delete. |
| Administrator | `administrator` | Every SafetyOfficer capability, plus question revisions and curating each question's choices. |

A claim may be a string or an array; the highest role present wins. A validated
token carrying no recognized role authenticates as `User` — membership is
proven, and no administrative capability follows from it.

There is no CSRF machinery. A bearer token carries no ambient authority, so
there is nothing for a cross-site request to forge.

## No user records

**This system stores no user details of any kind**
([ADR-0065](decisions/ADR-0065-no-user-records-identity-is-the-token-subject.md)).
There is no `admin_users` table, no allowlist, no role column, and no active
flag. Where a summary's approver or an audit entry's actor is recorded, it is
the token subject as an opaque `varchar(256)` string that joins to nothing.

Revoking somebody's access is the identity provider's decision, and it takes
effect when their token expires or stops being issued. This system has no
record of them to revoke.

## Two issuers, one contract

**Production** uses a standards-based OAuth/OIDC provider. The concrete
provider is not yet chosen — Auth0 and AWS Cognito are the candidates, and any
provider that emits the claim shape above satisfies the contract
([ADR-0064](decisions/ADR-0064-jwt-bearer-authentication-with-three-roles.md)).
This system never sees a member's password.

**Development** mints its own genuinely signed token from a symmetric key and
validates it through the same middleware, the same validation parameters, and
the same policies. Only the issuer and the key differ
([ADR-0066](decisions/ADR-0066-a-development-identity-provider-signed-with-a-dev-key.md)).

Sign in with a real HPAC membership: `POST /api/auth/token` verifies the
username and password against the live members site
(`https://members.hpac.ca`) for that one call, never logging or storing the
password. Role comes from two Development-only email allowlists in
configuration — `MembersSiteLogin:AdministratorEmails` and
`MembersSiteLogin:SafetyOfficerEmails` — falling back to `User` for any other
verified member
([ADR-0079](decisions/ADR-0079-a-development-login-may-verify-against-the-live-members-site.md)).

The development token endpoint is **not mapped outside Development** — the
route returns 404 rather than 401, because there is no code path that maps it
there. The third-party sign-in option is production-only; the browser learns
whether to offer it from `GET /api/auth/config`, never from a build flag.

## Filing a report

Submission requires a member and records nothing about them
([ADR-0067](decisions/ADR-0067-a-reporter-must-be-a-member-and-is-not-recorded.md)).
No report, answer, file, outbox message, audit entry, or log line holds the
submitter's subject, and no column, join table, or hash links a report to the
member who filed it. Authentication answers one question — *is this an HPAC
member?* — and its answer is not kept. The form tells the reporter so.

Turnstile is not used. The member token is the abuse control, alongside per-IP
rate limiting
([ADR-0068](decisions/ADR-0068-the-member-token-replaces-turnstile-on-submission.md)).

## Auditing

Raw-report views, attachment access, summary edits, approval, rejection,
publication, deletion, and question changes are audited by acting subject and
time, without copying report content into the audit entry.

See
[`features/moderation-authentication-and-publication/moderation-authentication-and-publication.feature`](../features/moderation-authentication-and-publication/moderation-authentication-and-publication.feature)
for the normative role and endpoint rules.
