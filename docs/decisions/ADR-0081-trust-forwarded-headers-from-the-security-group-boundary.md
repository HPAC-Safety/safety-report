---
title: Trust forwarded headers because the security group is the trust boundary, not a static proxy list
description: Trust X-Forwarded-For/X-Forwarded-Proto unconditionally at the application layer, because the network layer already guarantees the direct connection is the ALB.
type: adr
status: accepted
date: 2026-09-22
decision-makers: Chase Florell
keywords: rate limiting, RateLimiter, ForwardedHeaders, X-Forwarded-For, ALB, trusted proxy
---

# ADR-0081 — Trust forwarded headers because the security group is the trust boundary, not a static proxy list

## Context

Issue #15 requires rate-limiting `POST /api/v1/reports` by the reporter's real
client IP, "trusting forwarding headers only from the configured load
balancer/CDN." ASP.NET Core's `ForwardedHeadersMiddleware` is the standard way
to turn `X-Forwarded-For` into `HttpContext.Connection.RemoteIpAddress`, and
its documented safe default requires `KnownProxies`/`KnownNetworks` to name
exactly which upstream addresses are trusted to set that header, rejecting
the header otherwise.

`infra/alb.tf` puts exactly one AWS Application Load Balancer directly in
front of the API — `infra/cdn.tf`'s CloudFront distribution serves the static
web bundle only, never proxies to the API. `infra/security-groups.tf`'s
`api_from_alb` rule admits inbound traffic to the API's container only from
the ALB's security group; nothing else in or outside the VPC can reach the
container's port directly. An ALB's IP addresses are not static and are not
published as a fixed list — AWS explicitly reserves the right to change them
— so a `KnownProxies` entry naming specific addresses would need to be kept
in sync with infrastructure that does not publish what to sync it to.

## Decision

**Trust `X-Forwarded-For`/`X-Forwarded-Proto` unconditionally at the
application layer, because the network layer already guarantees the direct
connection is the ALB.** `KnownNetworks`/`KnownProxies` are cleared rather
than populated: whatever reaches this process on its listening port already
passed through the security group, so there is no additional address to
check for — checking one would either duplicate the security group's job
with a list that goes stale, or require an IP range wide enough to be
meaningless.

This is standard guidance for exactly this topology (a single AWS
Elastic/Application Load Balancer directly in front of a container with a
security group restricting ingress to it) — the trust boundary is the
security group, and `ForwardedHeadersMiddleware`'s address allowlist exists
for topologies where the network layer does not already guarantee who the
direct peer is.

## Consequences

- If a second reverse proxy or CDN is ever placed in front of the ALB (unlike
  today, where CloudFront serves only the static site), this decision needs
  revisiting: an extra untrusted hop between the client and the ALB would let
  that hop forge `X-Forwarded-For` and this middleware would still trust it.
- If the security group rule (`api_from_alb`) is ever loosened to admit
  direct internet traffic to the API, this decision's premise breaks
  silently — nothing here would detect that. The security-group rule is the
  load-bearing control, not the C# in `Program.cs`.
- The client IP is used only in memory, for the sliding-window rate-limiter
  partition key. It is never persisted on a report or written to a log line
  (product invariant #8, and #15's own acceptance criteria).

## Alternatives rejected

**Populate `KnownProxies` with the ALB's current IPs.** Rejected: ALB
addresses are not static or published as a stable list; this would need an
out-of-band sync mechanism (e.g. querying AWS for current ALB ENIs at
startup) to avoid silently breaking every time AWS rotates them, for a check
the security group already makes redundant.

**Read `X-Forwarded-For` directly in the rate-limiter partition key,
bypassing `ForwardedHeadersMiddleware` entirely.** Rejected: it would still
need the same trust decision (which hop's header value to believe) made by
hand, with none of the framework's existing parsing/format handling, for no
benefit over configuring the middleware once and reading
`HttpContext.Connection.RemoteIpAddress` everywhere downstream (including
anywhere else in this codebase that later wants the caller's address).

## Related

- [issue #15](https://github.com/HPAC-Safety/safety-report/issues/15) — the rate-limiting work this supports
- `infra/alb.tf`, `infra/security-groups.tf` — the actual trust boundary
- [ADR-0068](ADR-0068-the-member-token-replaces-turnstile-on-submission.md) — the member token is submission's primary abuse control; this is the secondary, per-IP layer
