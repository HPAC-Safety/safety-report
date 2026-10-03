---
title: Authentication and roles
description: Supporting detail for member authentication, the three roles, the Admin menu and its pending counts, and access control.
type: spec
area: authentication-and-roles
prefix: REQ-AUTH
---

# Authentication and roles

Supporting detail for [`authentication-and-roles.feature`](authentication-and-roles.feature)
that doesn't fit Gherkin.

## Authentication boundary

Identity arrives as a signed JWT, presented as `Authorization: Bearer <jwt>`.
The API validates the signature, the issuer, the audience `hpac-safety-api`,
and the lifetime, then reads exactly two claims: the subject and the role.
Nothing else — not a name, not an email address, not a picture
([ADR-0064](../../decisions/ADR-0064-jwt-bearer-authentication-with-three-roles.md)).

There is no allowlist and no user table. **This system stores no user records
at all** ([ADR-0065](../../decisions/ADR-0065-no-user-records-identity-is-the-token-subject.md)).
Where a report's approver or an audit entry's actor is recorded, it is the
token subject as an opaque string that joins to nothing. Revoking access is the
identity provider's decision, and it takes effect when a token stops being
issued or expires.

The role claim's **name** is configuration; its **values** are the invariant
codes `user`, `safety_officer`, and `administrator`. A claim may be a string or
an array, and the highest role present wins; none means `User`
(`REQ-MOD-017`).

The production provider is not yet chosen — Auth0 and AWS Cognito are the
candidates, and any provider emitting the claim shape above satisfies the
contract. In development the API issues its own genuinely signed token and
validates it through the same middleware, so only the issuer and the key differ
([ADR-0066](../../decisions/ADR-0066-a-development-identity-provider-signed-with-a-dev-key.md)).
Development may also sign a real member in by checking their password against
the live members site, for that one call only
([ADR-0079](../../decisions/ADR-0079-a-development-login-may-verify-against-the-live-members-site.md)).
The third-party sign-in option is production-only; the browser learns whether
to offer it from `GET /api/auth/config`, never from a build flag.

A bearer token carries no ambient authority, so state-changing admin requests
need no CSRF protection.

**No identity provider configured is a stated limitation, not a startup
failure.** Outside Development, until `HpacSafety:Authentication:Authority`
is set for an environment, that environment still starts and serves its
public endpoints — `/health`, public questions, the public feed and
submission — but no bearer token can ever validate: sign-in, review, and
administration cannot work there
([ADR-0158](../../decisions/ADR-0158-two-aws-accounts-staged-and-promoted-by-approval.md)).
The host logs one warning at startup and registers a bearer scheme with no
authority, no signing keys, and an issuer no real token will ever carry, so
every authorization-protected endpoint refuses every token with 401
(REQ-MOD-156). A configured Authority keeps today's behavior exactly.

**Staging is a temporary exception.** Behind
`HpacSafety:Authentication:InterimIssuer:Enabled` — never set alongside an
`Authority`, and never in Development or production — this API becomes its
own small RS256 identity provider: `GET /api/auth/interim/.well-known/openid-configuration`
and `GET /api/auth/interim/jwks` are mapped alongside `POST /api/auth/token`,
which reuses Development's own members-site credential check and hard-coded
administrator allowlist (ADR-0079) — **`FixedAccountCredentialSource` is not
registered here**, so the fixed development accounts
(`admin`/`admin`, `officer`/`officer`, `user`/`user`) do not exist outside
Development; only a real members-site login, checked against
`MembersSiteLoginOptions`' allowlists, signs a member in on staging.
Validation is pinned in-process to this host's own key and issuer,
`urn:hpac-safety:interim-issuer` — no metadata fetch, same as Development.
With the flag off, none of these three routes exist (404, not 401)
(REQ-MOD-157, REQ-MOD-158, REQ-MOD-159). Every part of this is deleted once a
real identity provider is chosen
([ADR-0172](../../decisions/ADR-0172-a-temporary-interim-issuer-signs-staging-tokens-until-a-real-provider-exists.md)).

## Roles

| Role | Capabilities |
|---|---|
| User | Proves HPAC membership. May submit an occurrence report. Nothing else — no review, authoring, or publication capability. |
| SafetyOfficer | View the review queue and private report material; view safe image/video derivatives and download validated unredacted documents; edit the bilingual summary pair; publish, unpublish, and soft-delete reports; keep private notes on a report ([ADR-0133](../../decisions/ADR-0133-staff-keep-private-notes-on-a-report.md)); add, download, and remove a report's private attachments ([ADR-0135](../../decisions/ADR-0135-staff-add-private-attachments-to-a-report.md)); review type-ahead values (approve, correct, merge, remove) ([ADR-0129](../../decisions/ADR-0129-a-type-ahead-value-is-edited-in-place-merged-and-reviewed.md)). |
| Administrator | Every SafetyOfficer capability, plus create question revisions and author each question's choices, including fixing or replacing a picker option ([ADR-0128](../../decisions/ADR-0128-an-answer-names-its-choice-and-a-picker-option-is-fixed-or-replaced.md)). |

Submission is a membership capability rather than a privileged one, so any of
the three roles may file a report — and the report records nothing about who
did ([ADR-0067](../../decisions/ADR-0067-a-reporter-must-be-a-member-and-is-not-recorded.md)).

`Administrator` is not a superuser (`REQ-MOD-035`).

## Pending counts on the Admin menu (#418)

The Admin menu shows how much work is waiting, so a reviewer sees it without
opening each page. **Manage reports** carries the number of reports the
*Needs action* filter lists, and **Type-ahead values to review** carries the
number in that queue. The closed **Admin** button carries the total of the
counts the member can see. A count of zero shows no badge. The badge is a
filled brand-red pill ([design system](../../../docs/design-system.md)).

`GET /api/admin/counts` answers any reviewer, and carries no report content,
so it is not audited. It still gives an Administrator the number of answers
waiting for the Worker's automatic translation
([REQ-MOD-084..086](../admin-report-search/admin-report-search.feature)), an
operational signal only — there is no page or nav option to act on it, since
nothing but the Worker ever fills that second language (ADR-0174). The counts
are read from the same database views as the list and the queue, so they
cannot disagree with them. The menu refetches on each navigation.

## Out of scope

What not to build here. The global list in
[system overview](../../system-overview.md) still holds; this narrows it
to this area ([ADR-0083](../../decisions/ADR-0083-specification-driven-development.md)).

- A live-updating or polling count on the Admin menu. The counts refresh when
  the member navigates.
- A count on Manage questions, or a per-status breakdown of the reports
  needing action.
- A user table, an allowlist, an allowlist-management screen, or a session
  store. Roles come from the token
  ([ADR-0065](../../decisions/ADR-0065-no-user-records-identity-is-the-token-subject.md)).
- A revocation endpoint or a member record to revoke. Access is granted and
  revoked at the identity provider; a revoked member's token simply stops being
  issued, and audit rows keep the opaque subject they were written with.
- Handling a member's password, outside Development's carve-out and its
  temporary staging extension
  ([ADR-0079](../../decisions/ADR-0079-a-development-login-may-verify-against-the-live-members-site.md),
  [ADR-0172](../../decisions/ADR-0172-a-temporary-interim-issuer-signs-staging-tokens-until-a-real-provider-exists.md)).
- CSRF machinery or Turnstile. A bearer token carries no ambient authority
  ([ADR-0068](../../decisions/ADR-0068-the-member-token-replaces-turnstile-on-submission.md)).
- A per-reporter rate limit, which would mean identifying the reporter.
