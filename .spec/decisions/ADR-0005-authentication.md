---
title: Credential proxy for admin authentication
description: "Admin authentication proxies the member site's login rather than storing credentials, since reporting itself is anonymous."
type: adr
status: superseded
date: 2026-08-22
decision-makers: Chase Florell
keywords: authentication, IMemberAuthenticator, credential proxy, HPAC membership
---

# ADR-0005 — Credential proxy for admin authentication

**Status:** Superseded by
[ADR-0064](ADR-0064-jwt-bearer-authentication-with-three-roles.md). The
credential proxy was never built and never will be: production authentication
uses a standards-based OAuth/OIDC provider, and this system never sees a
member's password. `IMemberAuthenticator` is deleted rather than implemented.
The premise below that "reporting is anonymous" is also reversed — submission
now requires a member, who is not recorded
([ADR-0067](ADR-0067-a-reporter-must-be-a-member-and-is-not-recorded.md)) — and
`admin_users` no longer exists
([ADR-0065](ADR-0065-no-user-records-identity-is-the-token-subject.md)).

## Context

Reporting is anonymous; only the admin surface needs authentication, for a
handful of safety officers.

`members.hpac.ca` was investigated directly and has **no OAuth**. It is a Rails
application using form/session authentication. `/.well-known/openid-configuration`,
`/.well-known/oauth-authorization-server`, `/oauth/authorize`,
`/users/auth/saml`, and `/api` all return 404. There is no SSO to integrate with.

HPAC is being asked to migrate to OAuth, but that is not in our control.

## Decision

`IMemberAuthenticator` with `HpacMembersProxyAuthenticator`: fetch `/login` for
the CSRF token, POST the credentials, treat a redirect plus session cookie as
success. `OidcAuthenticator` slots in unchanged when upstream OAuth exists.

Authorization is entirely ours: an `admin_users` allowlist. A successful
upstream login with no matching row is rejected, because membership is not the
same as being a safety officer.

## Risks, stated plainly

This application handles real member passwords for a system we do not own. A bug
or a log leak here exposes the upstream member database, not just ours. It also
depends on the exact markup of a page we do not control.

Mitigations are mandatory, not advisory: credentials exist in a local variable
for one call, are never persisted or cached, never logged at any level, scrubbed
from exception paths, sent over TLS to a hardcoded host, rate-limited with
lockout, and switchable off by one config flag.

## Alternatives

- **Local accounts.** No third-party dependency, but this project then owns
  password storage, reset, and breach risk for volunteers.
- **External IdP (Google/Microsoft).** Cleanest, but adds a second identity for
  officers who already have HPAC credentials.

## Consequences

- Migration to OIDC changes authentication only. `admin_users` is untouched.
- If anything outside `Infrastructure` needs editing at migration time, the
  abstraction leaked and that is a bug to fix first.
