---
title: Roles come from application-owned role assignments, and the provider proves membership only
description: Administrator and Safety Officer come from this application's own role assignments (an email, a role, and who granted and revoked it when), under every issuer and permanently; removal is a soft revoke recording when and by whom, access is re-checked on every request, nobody removes their own, and an unreadable store fails closed to User.
type: adr
status: accepted
date: 2026-10-16
decision-makers: Chase Florell
keywords: role assignment, roles, Administrator, Safety Officer, revoke, soft delete, allowlist, user record, interim issuer, ADR-0064, ADR-0065, ADR-0079, ADR-0172, ADR-0200
---

# ADR-0199 — Roles come from application-owned role assignments, and the provider proves membership only

**Status:** Accepted. Decided by Chase Florell on 2026-10-16 in
[#909](https://github.com/HPAC-Safety/safety-report/issues/909). Supersedes
[ADR-0064](ADR-0064-jwt-bearer-authentication-with-three-roles.md),
[ADR-0065](ADR-0065-no-user-records-identity-is-the-token-subject.md),
[ADR-0079](ADR-0079-a-development-login-may-verify-against-the-live-members-site.md),
and [ADR-0172](ADR-0172-a-temporary-interim-issuer-signs-staging-tokens-until-a-real-provider-exists.md).
"What still holds" below lists what of each stands.

## Context

Staging's interim issuer
([ADR-0172](ADR-0172-a-temporary-interim-issuer-signs-staging-tokens-until-a-real-provider-exists.md))
resolves Administrator and Safety Officer from email lists committed in
`infra/staging.tfvars`. The repository is public, so adding a person
publishes their email and ships a build.
[ADR-0064](ADR-0064-jwt-bearer-authentication-with-three-roles.md) takes the
role from the token's claim, and
[ADR-0065](ADR-0065-no-user-records-identity-is-the-token-subject.md) stores no
user record and no allowlist. Both assume an identity provider that knows
this system's roles. The owner has decided that no provider ever will: the
association's members site, and any future OAuth provider, prove membership,
and this application alone decides who reviews and who administers.

## Decision drivers

- No person's email in the repository, a build, a log, or a plan comment.
- Adding or removing a reviewer is an Administrator's action on a page, not
  a deploy.
- Revoked access stops at once, not when a token expires.
- The smallest record that does the job: no name, no profile.
- Nothing is physically deleted (`AGENTS.md` invariant 8).

## Considered options

- **Keep email lists in configuration (tfvars or a secret).** Rejected: every
  change is a deploy, and a committed list publishes the email.
- **Take roles from a future provider's claims.** Rejected by the owner:
  OAuth never decides who administers here, under any issuer.
- **Resolve the role once at sign-in and trust the token until it expires.**
  Rejected: a removed reviewer would keep access for the token's lifetime.
- **Hard-delete a removed assignment.** Rejected: it erases who granted and
  revoked access, and breaks invariant 8.
- **Application-owned role assignments, re-checked on every request** —
  chosen.

## Decision

- **The identity provider proves membership only.** A validated token makes
  its bearer a member with the User role. Administrator and Safety Officer
  come only from this application's **role assignments**, or from the
  Super Administrator
  ([ADR-0200](ADR-0200-a-permanent-super-administrator-comes-from-configuration.md)).
  This holds under every issuer: Development's, the interim issuer, and any
  real provider. It is permanent, not interim.
- **Development only keeps its own lists as well.** In Development, and
  nowhere else, the fixed accounts and the `MembersSiteLogin` email lists in
  `appsettings.Development.json`
  ([ADR-0079](ADR-0079-a-development-login-may-verify-against-the-live-members-site.md))
  stay a direct role source, checked alongside role assignments on every
  request; the highest role either gives wins. They seed nothing. Every other
  environment uses the Super Administrator and role assignments only.
- **A role assignment holds an email address and one role**, Safety Officer
  or Administrator, with when it was granted and the token subject of the
  Administrator who granted it, and nothing else about the person: no name,
  no profile, no members-site identifier. Once revoked, it also holds when it
  was revoked and the token subject of the Administrator who revoked it. The
  email need not already be registered on the members site, and assigning one
  raises no warning.
- **A token subject can hold an email.** Under staging's interim issuer a
  token subject is `member:<email>`, so the granting and revoking subjects,
  and an audit entry's actor, can hold an Administrator's email in the
  database. That is stored, never logged (CON-IF-011).
- **One active assignment per email, in any role.** An email with an active
  assignment cannot be assigned again; to change its role, remove it and add
  it again. Administrator already includes every Safety Officer capability.
- **Emails match case-insensitively.** An email is stored normalised, trimmed
  and lower-cased, and every lookup, duplicate check, and revoke compares the
  normalised form.
- **An email is ASCII.** Assigning an email address with any non-ASCII
  character is refused, and a sign-in name with any non-ASCII character never
  matches an assignment. Lower-casing turns some non-ASCII characters into
  ASCII ones, such as the Kelvin sign (U+212A) into `k`, so without this a
  look-alike name could match another member's assignment.
- **Removal is a revoke, never a delete.** Revoking stamps the time and the
  revoking Administrator's token subject on the assignment. The database
  refuses a `DELETE` of a role assignment, and any update other than that
  one revoke of an active assignment. Re-adding the same email creates a new
  assignment. The revoke stamp is the table's retirement: it has no separate
  `deleted` column and no default live-row filter; the active assignments are
  read through a view.
- **Access is re-checked on every request.** Every request that needs the
  Reviewer or Administrator policy resolves the member's role from their
  active role assignment (or the Super Administrator) at that moment, not
  from the role a token was issued with. A revoked member loses that access
  on their next request.
- **No Administrator removes their own assignment.** The request is refused,
  so an Administrator cannot lock themselves out by accident.
- **Fail closed.** When role assignments cannot be read, a member resolves
  to User. The Super Administrator still resolves to Administrator
  ([ADR-0200](ADR-0200-a-permanent-super-administrator-comes-from-configuration.md)).
- **Administrators only.** Listing, adding, and revoking role assignments
  needs the Administrator policy. A Safety Officer can neither open the page
  nor call its endpoints. Every add and every revoke is audited, naming the
  assignment as its target and never the email.
- **No email in a log.** Neither an assignment's email nor the interim
  issuer's `member:<email>` token subject reaches a log. That includes a
  constraint violation's detail, which echoes the offending key.
- **Staging's lists leave the repository.** The `interim_issuer_*_emails`
  tfvars values and variables are removed. People are added through the page
  after release.
- **Outside ADR-0172's removal plan.** Role assignments, their page, and
  their endpoints are permanent. Deleting the interim issuer, as ADR-0172's
  removal plan describes, deletes none of them.

### What still holds

- [ADR-0064](ADR-0064-jwt-bearer-authentication-with-three-roles.md): JWT
  bearer validation (signature, issuer, audience, lifetime); the three
  ordered roles and their codes; reading only the token subject and the role
  claim (REQ-MOD-013–018); the deferred provider choice. Its role claim no
  longer grants Administrator or Safety Officer.
- [ADR-0065](ADR-0065-no-user-records-identity-is-the-token-subject.md): the
  `admin_users` drop and its narrow exception to invariant 8; approvers and
  audit actors as opaque token subjects. No user record is kept beyond a role
  assignment.
- [ADR-0079](ADR-0079-a-development-login-may-verify-against-the-live-members-site.md):
  Development's members-site credential check, its generic failures, and the
  members-site-down distinction, and, in Development only, its fixed accounts and email lists as a role source (REQ-MOD-019–022).
- [ADR-0172](ADR-0172-a-temporary-interim-issuer-signs-staging-tokens-until-a-real-provider-exists.md):
  the whole interim issuer (flag, signing key, issuer, endpoints, no fixed
  accounts, its removal plan) except where its role comes from
  (REQ-MOD-157–159, REQ-AUTH-009–011).

## Consequences

- One new table and its trigger.
- `AGENTS.md` invariant 7 and "Not built" change: a role-assignment record
  and its management page now exist.
- CON-DP-005, CON-IF-004, CON-IF-005, and CON-SO's user-record line are
  rewritten. New CON lines cover the database's refusals and email in logs.
- Production ships the code dormant: `interim_issuer_enabled` stays `false`,
  and this change creates no production environment, secret, or promotion.
- Mapping a real provider's token to an email is
  [#911](https://github.com/HPAC-Safety/safety-report/issues/911)'s, and not
  decided here.
- Claims: REQ-AUTH-012 onward in
  [`authentication-and-roles.feature`](../features/authentication-and-roles/authentication-and-roles.feature).

## Related

- [ADR-0200](ADR-0200-a-permanent-super-administrator-comes-from-configuration.md) — the break-glass Administrator
- [ADR-0067](ADR-0067-a-reporter-must-be-a-member-and-is-not-recorded.md) — a reporter is still not recorded
- [ADR-0178](ADR-0178-the-database-refuses-changes-to-the-reporters-account-and-to-summary-revisions.md) — the trigger pattern the refusals follow
