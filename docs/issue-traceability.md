---
title: Issue traceability
description: Every open GitHub issue and how it relates to the target specification.
type: guide
---

# Issue traceability

This page lists every open issue and how it stands against the
[specification](../features/README.md), as of 2026-09-26 (#444). Closed issues
are not listed: their history is in GitHub, and what they decided lives in the
ADRs and `/features`. A pull request that closes an issue removes its row.

`tools/issue-traceability.mjs` compares this page with the open issues. It
never fails a pull request: `.github/workflows/issue-traceability.yml` runs it
daily and on every push to `main`, and keeps one "Issue traceability drift"
issue open while an open issue has no row here or a row names a closed one
([ADR-0143](decisions/ADR-0143-issue-traceability-drift-opens-an-issue-and-gates-nothing.md)).

| Issue | Area | Disposition |
|---|---|---|
| [#30 — Deploy the minimal application to AWS](https://github.com/HPAC-Safety/safety-report/issues/30) | Infrastructure | Open. Deploys the topology in [infrastructure and operations](infrastructure-and-operations.md). #441 and #443 bring the Terraform to it first. |
| [#31 — Replace the Typeform with the new report form](https://github.com/HPAC-Safety/safety-report/issues/31) | Infrastructure | Open, phase 2. The cut-over after deployment. |
| [#47 — Dependency Dashboard](https://github.com/HPAC-Safety/safety-report/issues/47) | — | Renovate's standing dashboard, not a task. |
| [#387 — Evaluate AWS Bedrock as the summarization provider](https://github.com/HPAC-Safety/safety-report/issues/387) | AI | Open research spike. Summaries use Gemini today ([ADR-0104](decisions/ADR-0104-summaries-are-generated-by-gemini-through-a-paid-key.md)); the provider is configuration behind `IAiChatClient`. |
| [#413 — Identify commenters by name and HPAC number once OIDC lands](https://github.com/HPAC-Safety/safety-report/issues/413) | Security | Open, waiting on the real identity provider ([ADR-0064](decisions/ADR-0064-jwt-bearer-authentication-with-three-roles.md)). Comments show "Member" until then ([ADR-0114](decisions/ADR-0114-members-may-comment-on-a-published-report.md)). |
| [#427 — Show report attachments as a thumbnail strip with a lightbox, and a viewer-scoped count](https://github.com/HPAC-Safety/safety-report/issues/427) | Web, API | Open, unblocked; the owner's decisions are recorded on the issue. Builds on ADR-0117 and ADR-0119. |
| [#441 — Remove Terraform that contradicts the ADRs](https://github.com/HPAC-Safety/safety-report/issues/441) | Infrastructure | Open. The unused migrate task (ADR-0055) and the SES leftovers (CON-INF-002). |
| [#443 — Run the API and the Worker on Lambda](https://github.com/HPAC-Safety/safety-report/issues/443) | Infrastructure | Open. The Terraform, the deploy workflows, and the Worker's drain-once Lambda host ([ADR-0042](decisions/ADR-0042-lambda-hosted-api-with-fargate-migration-path.md), [ADR-0123](decisions/ADR-0123-the-worker-runs-on-lambda-and-the-website-on-s3-and-cloudfront.md)). |
| [#461 — Record the deployment shape: staging and production accounts, release promotion, CloudFront to a Function URL, two hostnames](https://github.com/HPAC-Safety/safety-report/issues/461) | Infrastructure | Open, phase 2. Documents the deployment topology before #464–#467 build it. |
| [#463 — Let the hostname set a first-time visitor's language: securite.acvl.ca opens in French](https://github.com/HPAC-Safety/safety-report/issues/463) | Web, localization | Open, phase 2. The hostname joins the initial-locale order. |
| [#464 — Bootstrap each AWS account from CloudShell with per-environment OIDC roles](https://github.com/HPAC-Safety/safety-report/issues/464) | Infrastructure, security | Open, phase 2. |
| [#465 — Make the Terraform build staging and production, grouped as HPAC-Safety, with CloudFront routing /api to the API](https://github.com/HPAC-Safety/safety-report/issues/465) | Infrastructure | Open, phase 2. |
| [#466 — Deploy by release: build once, deploy to staging, promote to production on approval](https://github.com/HPAC-Safety/safety-report/issues/466) | Infrastructure, CI | Open, phase 2. |
| [#467 — Emit the operational metrics, alarm on them, and write the runbooks](https://github.com/HPAC-Safety/safety-report/issues/467) | Worker, infrastructure | Open, phase 2. |
| [#520 — Let a picker or type-ahead's choices depend on another picker or type-ahead's answer](https://github.com/HPAC-Safety/safety-report/issues/520) | API, web, localization | Open, phase 1. |
| [#522 — Translate stays disabled after editing a bilingual question's wording, and has no direction switch](https://github.com/HPAC-Safety/safety-report/issues/522) | Web, localization | Open bug, phase 1. |
| [#535 — Round-trip Allow future dates through Typeform, and fix the #517 review findings](https://github.com/HPAC-Safety/safety-report/issues/535) | API, web | In progress, phase 1. |
| [#540 — Run the pull request workflow suite locally under act, with coverage matching CI](https://github.com/HPAC-Safety/safety-report/issues/540) | CI | In progress. |
