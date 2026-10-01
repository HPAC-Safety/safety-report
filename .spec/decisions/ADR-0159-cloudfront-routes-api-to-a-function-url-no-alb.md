---
title: CloudFront routes /api/* to the API's Function URL — no ALB
description: The API is reached only through the same CloudFront distribution that serves the website, on the path /api/*, forwarding to a Lambda Function URL guarded by a CloudFront-injected secret header. There is no Application Load Balancer. Client IP for rate limiting comes from CloudFront-Viewer-Address, revisiting ADR-0081's forwarded-header trust.
type: adr
status: accepted
date: 2026-09-27
decision-makers: Chase Florell
keywords: CloudFront, Lambda Function URL, ALB, origin secret, CloudFront-Viewer-Address, rate limiting, forwarded headers, ADR-0042, ADR-0081
---

# ADR-0159 — CloudFront routes `/api/*` to the API's Function URL — no ALB

## Status

Accepted. **Amends** [ADR-0042](ADR-0042-lambda-hosted-api-with-fargate-migration-path.md):
the API stays on Lambda, but it is no longer fronted by an ALB. Its "ALB →
Lambda target group" entry path, and the ALB row it assumed existed for the
static sites and the `/admin/` route, do not carry forward.
**Supersedes** [ADR-0081](ADR-0081-trust-forwarded-headers-from-the-security-group-boundary.md):
the trust boundary it reasoned about (a single ALB directly in front of the
API, admitted by a security group) no longer exists, and the header it named
is replaced.

## Context

ADR-0042 put the API on Lambda behind an ALB, reasoning that the ALB already
existed for the static website and the `/admin/` route and that a Lambda
target group made a later Fargate migration a target-group swap. ADR-0123
then moved the website itself off that ALB, onto S3 and CloudFront directly
— the ALB ADR-0042 assumed for "the static sites" stopped being a
prerequisite for anything but the API.

An ALB costs a fixed amount whether or not it is used (roughly US$16–20/month
plus data processing, in each account, so twice that across staging and
production) and needs its own security group, target group health checks,
and certificate, duplicating work CloudFront already does for the website.
Nothing about the API's traffic (ADR-0042: "a handful of report submissions
and review actions a day") needs an ALB's load-balancing; it needs one public
entry point in front of one Lambda function.

ADR-0081 reasoned that trusting `X-Forwarded-For`/`X-Forwarded-Proto`
unconditionally was safe because the network layer already guaranteed the
one direct peer was the ALB, admitted by a security group rule
(`api_from_alb`) that let nothing else reach the container's port. Removing
the ALB removes that security-group boundary, so ADR-0081's argument needs
re-examining, not silently carrying forward onto a new topology it never
described.

## Decision

### One entry point: CloudFront, `/api/*`

The same CloudFront distribution that serves the website (ADR-0123) also
routes `/api/*` to the API. There is **no ALB** anywhere in front of the API,
in either account.

- **Origin:** the API's Lambda **Function URL**, auth type `NONE` — CloudFront
  cannot itself sign SigV4 requests to an `AWS_IAM`-authenticated Function
  URL, so the URL is unauthenticated at the AWS layer and the application
  layer below is what actually restricts who may call it.
- **The CloudFront-injected secret origin header.** CloudFront adds a custom
  header (`X-Origin-Verify`, value a Secrets-Manager-held random string) to
  every request it forwards to the Function URL. The API rejects any request
  missing that header or carrying the wrong value, before any other
  middleware runs. This is what stops someone calling the Function URL
  directly, bypassing CloudFront and any cache, WAF, or logging on the way:
  the Function URL itself is still internet-reachable — Lambda offers no way
  to make it CloudFront-only — but a request that arrives without the
  correct secret is refused before it does anything else.
- **The website's origin is unchanged**: CloudFront's default behavior still
  serves the S3 site bucket through its origin access control. `/api/*` is an
  additional cache behavior with caching disabled (`CachingDisabled`
  policy) and all headers/cookies/query strings forwarded, because API
  responses are per-request and never cacheable at the edge.
- **Deep links** still fall back to `index.html` for any path CloudFront does
  not recognize as `/api/*` or a static asset (ADR-0123).

### Client IP for rate limiting: `CloudFront-Viewer-Address`

`POST /api/v1/reports` rate-limits by the reporter's real client IP
([issue #15](https://github.com/HPAC-Safety/safety-report/issues/15)).
CloudFront sets **`CloudFront-Viewer-Address`** (`ip:port`) on every request
it forwards to its origin, always, and a client cannot set or override it —
unlike `X-Forwarded-For`, which CloudFront otherwise passes through from
whatever the viewer sent. The API reads the client IP from
`CloudFront-Viewer-Address`'s host part, not `X-Forwarded-For`.

### ADR-0081, revisited

ADR-0081's "trust unconditionally, because the security group already
guarantees the one direct peer" argument assumed an ALB. It no longer applies
to this topology two ways:

- The direct peer at the Function URL is CloudFront's fleet, whose IP ranges
  are published but broad and change over time — not a security group the
  API's own infrastructure controls the way `api_from_alb` did.
- `X-Forwarded-For`/`X-Forwarded-Proto` are no longer the signal to trust. A
  request that skipped CloudFront (calling the Function URL directly) could
  set an arbitrary `X-Forwarded-For` itself; `CloudFront-Viewer-Address` and
  the origin secret header are the two facts a bypassing caller cannot forge
  cheaply, because they are values CloudFront itself sets or an origin-only
  secret it was configured with, not values it re-forwards from the viewer's
  own request.

ADR-0081 is not superseded outright: `ForwardedHeadersMiddleware`'s address
allowlist is still unnecessary here, for a different reason than before —
the origin secret header, not a security group, is now the check that a
request genuinely passed through CloudFront. `Program.cs` clears
`KnownNetworks`/`KnownProxies` as ADR-0081 already decided, and additionally
rejects any request without the correct origin secret before that middleware
runs.

## Alternatives considered

- **Keep the ALB, put CloudFront in front of it instead of the Function
  URL.** Adds a second load balancer to operate and pay for without adding
  any capability — the ALB would exist only to be CloudFront's origin, which
  a Function URL already can be directly.
- **API Gateway instead of a Function URL.** ADR-0042 already rejected API
  Gateway as a second public entry point with its own domain mapping and
  access logging to keep in parity; a Function URL behind CloudFront's
  existing distribution avoids that duplication entirely, and needs no
  domain mapping of its own.
- **`AWS_IAM` auth type on the Function URL, with CloudFront signing the
  request (`OriginAccessControl` for Lambda Function URLs).** Not available
  for Function URLs at the time of this decision — CloudFront's Lambda
  Function URL origin support does not sign requests the way it does for S3.
  The origin secret header is the documented alternative for this exact
  topology.
- **Trust `X-Forwarded-For` from CloudFront's published IP range list**,
  the way ADR-0081 rejected trusting an ALB's unpublished addresses. CloudFront's
  range list is published and stable enough to use, but it only proves the
  direct peer is *some* CloudFront distribution, not this one — anyone
  could point their own distribution at the Function URL and forward
  whatever `X-Forwarded-For` they chose. The origin secret header proves the
  request came through *this* distribution specifically; the IP range alone
  does not.

## Consequences

- Terraform: no `aws_lb`, `aws_lb_target_group`, `aws_lb_listener`, or
  `aws_lb_target_group_attachment` for the API. `infra/cdn.tf` gains an
  `/api/*` origin and cache behavior pointed at
  `aws_lambda_function_url` on the API function; the Function URL's
  `authorization_type` is `NONE`; a Secrets Manager entry holds the origin
  secret, read by both the CloudFront custom-header config and the API's
  configuration.
- `.spec/infrastructure-and-operations.md`'s topology diagrams, and
  `docs/deployment.md`, drop the ALB and show `/api/*` on the CloudFront
  entry.
- Rate-limiting code reads `CloudFront-Viewer-Address` instead of
  `X-Forwarded-For`/`Connection.RemoteIpAddress` for the client IP; this is
  implementation work for #443/#465, not decided here beyond naming the
  header.
- One certificate, one distribution, one set of access logs cover both the
  website and the API, instead of a second certificate and log stream on an
  ALB.
- Cost: an ALB in each account is removed from the estimate in issue #30;
  CloudFront's per-request cost is already accounted for by the website.
