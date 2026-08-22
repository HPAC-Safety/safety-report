# ADR-0005 — Credential proxy for admin authentication

**Status:** Accepted, with a planned replacement

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
