---
title: A development login may verify against the live members site
description: "A fourth Development-only sign-in path may verify a real member's password against the live members site for the single call that checks it."
type: adr
status: accepted
date: 2026-09-21
decision-makers: Chase Florell
keywords: development, mock authentication, members site, email allowlist, credential verification
---

# ADR-0079 — A development login may verify against the live members site

**Status:** Accepted. Supersedes [ADR-0066](ADR-0066-a-development-identity-provider-signed-with-a-dev-key.md)
for this one case.

## Context

[ADR-0066](ADR-0066-a-development-identity-provider-signed-with-a-dev-key.md)
gives Development three fixed accounts — one per role — and explicitly
rejected "a hand-pasted token from a real provider" as a dependency on a
decision not yet made ([ADR-0064](ADR-0064-jwt-bearer-authentication-with-three-roles.md)).

A developer now wants a fourth path: sign in with a real HPAC membership,
verified against the live members site (`https://members.hpac.ca`, a Rails
form-login application — not an identity provider, not OAuth/OIDC), to
exercise the app as an actual member rather than a fixed synthetic account.
This is an intentional, narrow exception to ADR-0066's rejection, confined to
Development, and it does not reopen ADR-0064's deferred production
OIDC-provider choice.

Two things were checked before deciding how role would work:

- The members site's cookies (`member`, `user_id`) are sealed with Rails'
  own `MessageEncryptor`/`MessageVerifier` key, which this system does not
  have and has no legitimate way to get. They are not decodable.
- Manual inspection of a real logged-in session found no rendered page
  distinguishing an administrator or committee member from an ordinary one.
  The site simply has no concept of this system's three roles.

## Decision

A second `IDevelopmentCredentialSource` (alongside the existing fixed
accounts, extracted unchanged into `FixedAccountCredentialSource`) verifies a
login by replicating a browser session against the members site:

1. `GET /login`, scrape the `authenticity_token` from the rendered form and
   the session cookie from the response.
2. `POST /login` with `session[email]`, `session[password]`, and that token,
   carrying the cookie forward by hand (never via a shared
   `CookieContainer` — see Consequences).
3. A redirect off `/login` is success. A re-rendered `200` login page is bad
   credentials — the same one generic failure the fixed accounts already
   produce, with nothing distinguishing which source was tried. Anything
   else (a timeout, an unreachable host, an unexpected page shape) is a
   distinct `MembersSiteUnavailableException` — a down members site is not
   the same failure as a wrong password, and reporting it as one would send
   a developer chasing a credential that was never the problem.

Because the site carries no role, role for a verified login comes from two
hardcoded, Development-only email lists in configuration —
`MembersSiteLogin:AdministratorEmails` and `MembersSiteLogin:SafetyOfficerEmails`
— checked in that order, falling back to `MemberRole.User`. These are a
throwaway convenience, the same shape as the existing
`DevelopmentSigningKey` (ADR-0066): never a production concept, never
synced with the members site's own data, and naming nobody's real
authorization anywhere else in this system.

`DevelopmentTokenIssuer` tries the fixed accounts first, then this source,
and mints the same token shape either way — same issuer, signing key,
claims, and lifetime. The fixed accounts are unaffected.

## Why

An email allowlist, not a members-site signal, because there is no
members-site signal to read — its cookies are opaque to us by design, and a
logged-in page shows nothing distinguishing role. The alternative of trying
to reverse-engineer or decode the site's session state was never viable: it
would mean either breaking their encryption (not something this system
should ever attempt, against a site it does not own) or guessing at
undocumented internal structure that could change without notice.

Reporting a members-site outage distinctly from bad credentials matters
because a developer debugging "why won't this login work" needs to know
whether their credentials were actually judged.

## Alternatives

- **Decode the members site's session cookies for a role signal.** Rejected.
  They are sealed with a secret we do not have, and even if they weren't,
  inspection found no role data in them — this system's three roles are not
  a concept the members site has.
- **Map every verified login to `User`, with no allowlist.** Considered, and
  the simplest option. Rejected because it leaves no way to test
  Administrator/SafetyOfficer against a real membership — a developer would
  still need the fixed accounts for that, making the members-site path only
  ever exercise one of three roles.
- **A second allowlist keyed by something other than email (a members-site
  ID, a username).** Rejected. Email is what a developer already has and
  already typed to log in; a second identifier would need its own lookup
  with no source to look it up from.

## Consequences

- `MembersSiteCredentialSource` threads its session cookie through the
  request pipeline by hand — local values, not a
  `System.Net.CookieContainer` on the named `HttpClient`'s handler. That
  handler is pooled and shared by `IHttpClientFactory` across concurrent
  requests; a container attached to it would leak one developer's login
  session into another's concurrent attempt. This is the one place this
  task's initial "single `HttpClient` + shared `CookieContainer`" shape does
  not fit this codebase's use of `IHttpClientFactory`.
- The password is used only for the one `POST /login` call and never logged,
  stored, or included in any exception message.
- This is registered and reachable only where `DevelopmentTokenIssuer`
  already is — inside Development's `useDevelopmentIssuer` branch, with the
  `/api/auth/token` route mapped only there. There is no additional flag to
  misconfigure and no code path that reaches any of this outside
  Development.
- `AGENTS.md`'s statement that this system has no "allowlist" or "credential
  proxy" is updated in the same pull request to carve this one exception,
  the same way it already carves the `admin_users` table drop
  ([ADR-0065](ADR-0065-no-user-records-identity-is-the-token-subject.md)).
  It does not generalize — any future allowlist or credential-proxy-shaped
  code needs its own argument on its own facts.

## Related

- [ADR-0066](ADR-0066-a-development-identity-provider-signed-with-a-dev-key.md) — the token shape and issuer this reuses; superseded for this one case
- [ADR-0064](ADR-0064-jwt-bearer-authentication-with-three-roles.md) — the three roles, and the deferred production provider this does not decide
- [ADR-0065](ADR-0065-no-user-records-identity-is-the-token-subject.md) — no user records; this allowlist is config, not a user record
