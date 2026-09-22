---
status: superseded by ADR-0079
date: 2026-09-21
decision-makers: Chase Florell
keywords: development, mock authentication, JWT, signing key, environment configuration
---

# ADR-0066 — A development identity provider, signed with a dev key

**Status:** Superseded by [ADR-0079](ADR-0079-a-development-login-may-verify-against-the-live-members-site.md)
for the "no external dependency" alternative below — a fourth,
members-site-verified development login now exists alongside the three
fixed accounts described here, which remain unchanged.

## Context

Until now, signing in during development meant clicking a button. Both the
credential form and the third-party button on the login page called the same
no-op, which wrote a marker into `sessionStorage`; the API accepted any
non-empty header echoing it. Nothing could be tested, because there was nothing
to test — least of all the difference between an administrator and anyone else.

With three roles
([ADR-0064](ADR-0064-jwt-bearer-authentication-with-three-roles.md)), a
developer needs to be able to *be* each of them. Waiting on a provider account
to do that would make local work depend on a decision that has not been made.

## Decision

In Development, the API issues its own tokens.

`POST /api/auth/token` accepts three fixed credential pairs and returns a
**genuinely signed** JWT — HS256 over a symmetric key from configuration, with
the same issuer, audience, subject, role, and lifetime shape any provider must
produce:

| Username | Password | Role | Subject |
|---|---|---|---|
| `admin` | `admin` | Administrator | `dev:admin` |
| `officer` | `officer` | SafetyOfficer | `dev:officer` |
| `user` | `user` | User | `dev:user` |

The API then validates that token through **the same middleware, the same
`TokenValidationParameters`, and the same authorization policies** that run in
production. Only the issuer and the key differ. A forged, expired,
wrong-audience, or `alg: none` token is refused in development exactly as it
would be in production, and the host refuses to start if the development key is
missing or shorter than thirty-two bytes.

Outside Development the endpoint **is not mapped at all** — the route returns
404, not 401. There is no flag that turns it on in a deployed environment,
because there is no code path that maps it there.

The third-party sign-in button is production-only. In development the login
page does not render it.

### How the web application knows which it is

`GET /api/auth/config` — anonymous, present in every environment. It reports
the authentication mode and whether a third-party provider is configured, and
the browser renders accordingly.

Not a build flag. `src/web` has no `import.meta.env` usage and one built
artifact is promoted across environments, so a build-time switch would mean
either a second build or a bundle that lies about where it is running.

## Why

A mock that skips validation proves that the mock works. Signing the token for
real means the development path exercises signature verification, issuer and
audience checks, expiry, claim extraction, and policy evaluation — every part
of the mechanism except which key signed it. When the provider is chosen, the
only thing that changes is configuration.

Refusing to map the endpoint outside Development, rather than guarding it with
a flag, is deliberate. A flag can be set wrong. A route that was never mapped
cannot be reached.

## Alternatives

- **A bypass filter that trusts a header in development.** Rejected. It is the
  stub we are removing, and it exercises none of the real path.
- **A `VITE_` build flag for the third-party button.** Rejected. It bakes the
  environment into the bundle.
- **A hand-pasted token from a real provider.** Rejected. It makes local work
  depend on a provider account and a decision not yet made.
- **A development refresh flow.** Rejected. Refresh is the provider's job in
  production, so building a development-only version of it is drift. The
  development token simply lasts a working day.

## Consequences

- A symmetric development signing key lives in
  `appsettings.Development.json`. It is a throwaway for local use, following
  the same pattern already set by the committed development encryption key, and
  it is never a production secret.
- Production needs a real signing configuration — the provider's issuer and
  JWKS — recorded with the other secrets.
- End-to-end tests stub the token endpoint rather than running the API, and API
  tests mint real tokens through the booted host.

## Related

- [ADR-0064](ADR-0064-jwt-bearer-authentication-with-three-roles.md) — the claim contract both issuers satisfy
- [ADR-0015](ADR-0015-one-shell-script-for-development-setup.md) — development setup
- [ADR-0020](ADR-0020-seeding-by-migration.md) — the development administrator this replaces
- [ADR-0043](ADR-0043-react-typescript-vite-web-front-end.md) — the front end that reads the config endpoint
- [ADR-0079](ADR-0079-a-development-login-may-verify-against-the-live-members-site.md) — supersedes the "hand-pasted token from a real provider" rejection below for a members-site-verified login
