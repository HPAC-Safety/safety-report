---
title: A host that refused to start where the ADR said it would
description: Staging deployed, but every API request returned 502, because the API threw at startup without an identity provider. ADR-0158 said such an environment still serves its public pages, and the smoke test called a health route the API never mapped.
type: lesson
date: 2026-09-29
issue: 647
status: accepted
---

# Lesson 0037 — A host that refused to start where the ADR said it would

## Symptom

Release run 36514257110 deployed all of staging. The site answered 200 at
its CloudFront address, but every API request returned 502 "Internal Server
Error", through CloudFront and at the Function URL alike. The smoke test on
`/api/health` failed.

## Root cause

- `AddHpacSafetyAuthentication` threw outside Development when
  `HpacSafety:Authentication:Authority` was empty. No environment sets it,
  because the identity provider is not chosen yet (ADR-0064). The Lambda Web
  Adapter never became ready. ADR-0158 and `docs/deployment.md` said such an
  environment still serves its public pages. No scenario claimed that, so
  nothing tested it, and the code drifted from the decision.
- The same ADR sentence said "public pages and submission". Filing a report
  is member-only (ADR-0067), so that half was never true.
- The release's smoke test calls `/api/health`. CloudFront forwards `/api/*`
  unchanged, and the API mapped only `/health`. The smoke test could never
  have passed.

## Spec delta

- REQ-MOD-156: an environment with no identity provider starts, answers its
  health routes, and refuses every bearer token.
- ADR-0158, `docs/deployment.md`, `.spec/infrastructure-and-operations.md`,
  and `manage-hpac-infrastructure` now say that filing a report also needs
  the provider.
- The API maps `/api/health` beside `/health`.

## Scenario

REQ-MOD-156.

## Skill

None; the claim is the remedy.
