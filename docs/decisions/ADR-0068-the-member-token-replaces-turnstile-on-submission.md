---
status: accepted
date: 2026-09-21
decision-makers: Chase Florell
keywords: Turnstile, abuse control, rate limiting, submission, bot protection
---

# ADR-0068 — The member token replaces Turnstile on submission

**Status:** Accepted.

## Context

`POST /api/v1/reports` was specified to require a Cloudflare Turnstile response
token alongside the report. That made sense for an endpoint anyone on the
internet could call: Turnstile was the only thing standing between an open
write endpoint and a script.

[ADR-0067](ADR-0067-a-reporter-must-be-a-member-and-is-not-recorded.md) closes
that endpoint to non-members. The population that can reach it is now a few
hundred known people who hold credentials with an identity provider.

## Decision

Turnstile is removed from report submission. The member bearer token is the
abuse control, alongside the per-IP rate limiting that was already specified.

The validation order becomes request size, multipart shape, trusted client IP,
rate limit, **token validation**, then the report's own rules.

## Why

Turnstile answers "is this a human?" A member token answers "is this one of our
members?", which for this endpoint is the stronger question and the one the
programme actually cares about. Running both means a reporter — often filing
something stressful, sometimes on a phone in a field — solves a challenge to
prove something the token already proved.

There is also a plain dependency argument. Turnstile means every reporter's
browser contacts Cloudflare while filing an occurrence report. This repository
already rejected Google Fonts on exactly that reasoning
([ADR-0023](ADR-0023-pinned-and-vendored-web-assets.md)); a third-party
challenge on the submission path is a larger version of the same objection, and
it is no longer buying anything that the token does not.

## Alternatives

- **Keep both gates.** Defensible — a compromised or shared member credential
  could script submissions, and Turnstile would blunt that. Rejected because
  the marginal protection is small against a known population, the cost falls
  on every honest reporter at the worst moment, and it keeps a third-party
  dependency on the most privacy-sensitive page in the application.
- **Keep Turnstile only for signed-out visitors.** Rejected: there are none.
  Signed-out visitors cannot reach the form at all.

## Consequences

- **The accepted risk, stated plainly:** one leaked member credential can
  script this endpoint. What remains is per-IP rate limiting, request-size and
  multipart-shape limits, and the fact that revoking access at the identity
  provider stops it. There is no per-reporter throttle, because per-reporter
  would mean identifying the reporter, which ADR-0067 forbids.
- Turnstile site and secret configuration is removed from the submission path.
- The scenarios asserting a Turnstile token on submission are removed; the
  per-IP rate-limiting scenarios stay and gain the assertion that the
  authenticated subject is never stored on the report.

## Related

- [ADR-0067](ADR-0067-a-reporter-must-be-a-member-and-is-not-recorded.md) — the decision that made this possible
- [ADR-0064](ADR-0064-jwt-bearer-authentication-with-three-roles.md) — the token doing the work
- [ADR-0023](ADR-0023-pinned-and-vendored-web-assets.md) — the third-party-request principle
- [`features/report-submission`](../../features/report-submission/report-submission.feature)
