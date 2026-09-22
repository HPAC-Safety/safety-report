---
title: Host the API on Lambda, built for a later Fargate migration
description: The API is hosted on Lambda, written so a later move to Fargate needs no application change.
type: adr
status: accepted
date: 2026-09-18
decision-makers: Chase Florell
keywords: AWS, Lambda, API, hosting, Docker, ALB, Fargate migration
---

# ADR-0042 — Host the API on Lambda, built for a later Fargate migration

**Status:** Accepted. Supersedes the API row of
[ADR-0009](ADR-0009-hosting-on-aws.md) ("ECS Fargate service behind an ALB").
The Worker row of ADR-0009 is unchanged — the Worker stays ECS Fargate.

## Context

ADR-0009 put both the API and the Worker on ECS Fargate, reasoning that running
both on one primitive was worth more than splitting them across services. That
reasoning still holds for the Worker: it polls continuously and holds database
connections, so it cannot go event-driven.

The API is different. Actual traffic is a handful of report submissions and
review actions a day — sparse, bursty, with long idle stretches. An
always-on Fargate task bills for those idle hours whether or not a request
arrives. A national association receiving dozens of reports a year does not
need a warm API process at 3am.

The organization may outgrow this traffic profile — more member associations
onboarding, sustained review-queue activity, or a latency requirement Lambda's
cold start can't meet. When that happens, the API should move to Fargate
without a rewrite.

## Decision

Host the API as a Docker container image on **AWS Lambda**, fronted by the
same ALB already required for the static sites and (per ADR-0031) the
`/admin/` route — using the ALB's **Lambda target group** support rather than
adding API Gateway. Traffic pattern is unchanged from the browser's
perspective: HTTPS → ALB → API.

Inside the container, use the **AWS Lambda Web Adapter** (a small extra layer
added in the Dockerfile) instead of `Amazon.Lambda.AspNetCoreServer`. The
adapter translates ALB/Lambda events into ordinary HTTP requests against the
container's Kestrel listener. The ASP.NET Core application has **zero
AWS Lambda SDK dependency** — it is the same `WebApplication` pipeline that
would run on Fargate, ECS, or a developer's machine.

| Concern | Service |
|---|---|
| API | Lambda, container image, behind the ALB via a Lambda target group |
| Worker | ECS Fargate service, no load balancer (unchanged, ADR-0009) |

## Why these choices

**Authentication stays stateless.** A bearer token validated per request
([ADR-0064](ADR-0064-jwt-bearer-authentication-with-three-roles.md)) carries no
server-side session, so nothing here depends on a warm instance or a shared
session store. That is a point in this hosting choice's favour that was not
available when it was written.

**ALB → Lambda target group, not API Gateway.** The ALB already exists for
the static sites and the admin route. Adding API Gateway would mean a second
public entry point with its own domain mapping, WAF association, and access
logging to keep in parity with the ALB's. An ALB target group can point at a
Lambda function directly; migrating the API to Fargate later is then
**swapping the target group's type**, not re-routing DNS or CDN origins.

**Lambda Web Adapter, not `Amazon.Lambda.AspNetCoreServer`.** The
`AspNetCoreServer` package couples the application's hosting model to
Lambda's request/response lifecycle — the app that runs must be built as a
Lambda handler. The Web Adapter instead runs the ordinary container process
and translates events into loopback HTTP calls, per
[ADR-0033](ADR-0033-third-party-libraries-behind-owned-abstractions.md)'s
preference for keeping third-party coupling at the edge. The same
`Dockerfile`, minus one `COPY --from=` layer for the adapter binary, builds
the image that later runs on Fargate.

**Lambda over Fargate for the API today.** Fargate bills for a running task
whether or not it serves a request; at this traffic volume that is
near-total waste. Lambda's cold start (roughly hundreds of milliseconds for a
.NET container image, longer on a cold start with SnapStart unavailable for
container-packaged functions) is an acceptable trade for an API with no
sub-second SLA.

**Lambda over Fargate for the API, revisited later.** Reasons to move: a
review-queue latency requirement Lambda's cold start can't meet, WebSocket or
long-lived connections, or traffic dense enough that Lambda's per-invocation
pricing stops beating a small always-on task. Any of these triggers a new
ADR, not a silent drift.

**Not App Runner.** App Runner would fit the API alone but does not give the
ALB-target-group migration path — moving off App Runner means a new public
endpoint, the same DNS/CDN churn this decision exists to avoid.

## Consequences

- Terraform: `aws_lambda_function` with `package_type = "Image"`, pointing at
  the same ECR repository pattern ADR-0009 already established for
  Fargate/ECS images. The ALB's API target group has `target_type = "lambda"`
  with `aws_lambda_permission` granting the ALB invoke rights.
- The application project has no reference to `Amazon.Lambda.AspNetCoreServer`
  or any Lambda-specific hosting package. The Dockerfile adds the Lambda Web
  Adapter as a build stage / `COPY` layer, configured entirely through
  environment variables (port, readiness path) — no application code branches
  on "am I running in Lambda."
- Lambda's hard 15-minute execution ceiling and no-WebSocket-upgrade
  constraint apply to every API request. Nothing in the current API design
  (`features/README.md`) needs either.
- **Migrating to Fargate later** means: build an ECS service and task
  definition from the existing image (the same one already in ECR), attach
  it as a second ALB target group, cut the target group weight over, then
  delete the Lambda function and its target group. No application code
  change, no new Docker image, no DNS or CloudFront change.
- Cost shifts from ADR-0009's "two always-on Fargate tasks" to "one always-on
  Fargate task (Worker) plus a near-zero-cost Lambda API" for the traffic
  volumes in view.

## Related

- [ADR-0009](ADR-0009-hosting-on-aws.md) — superseded for the API row only
- [ADR-0031](ADR-0031-terraform-shape-and-topology.md) — single ALB, admin as
  a route
- [ADR-0033](ADR-0033-third-party-libraries-behind-owned-abstractions.md) —
  third-party coupling stays at the edge
- `docs/infrastructure-and-operations.md`
