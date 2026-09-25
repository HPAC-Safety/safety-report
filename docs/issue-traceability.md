---
title: Issue traceability
description: Every open GitHub issue and how it relates to the target specification.
type: guide
---

# Issue traceability

This page lists every open issue and how it stands against the
[specification](../features/README.md), as of 2026-09-25 (#437). Closed issues
are not listed: their history is in GitHub, and what they decided lives in the
ADRs and `/features`. Issue #444 adds the check that fails when an open issue
has no row here, or a row names a closed one.

| Issue | Area | Disposition |
|---|---|---|
| [#27 — End-to-end report and review journey in both UI languages](https://github.com/HPAC-Safety/safety-report/issues/27) | CI | Open, unblocked. The capstone journey test. Its body predates the JSON submission of claimed uploads (ADR-0096), authored required questions (ADR-0061), and published media and documents (ADR-0117, ADR-0119); those parts are corrected when it is picked up. It blocks #30. |
| [#30 — Deploy the minimal application to AWS](https://github.com/HPAC-Safety/safety-report/issues/30) | Infrastructure | Open, blocked by #27. Deploys the topology in [infrastructure and operations](infrastructure-and-operations.md). #441 and #443 bring the Terraform to it first. |
| [#31 — Replace the Typeform with the new report form](https://github.com/HPAC-Safety/safety-report/issues/31) | Infrastructure | Open, phase 2. The cut-over after deployment. |
| [#47 — Dependency Dashboard](https://github.com/HPAC-Safety/safety-report/issues/47) | — | Renovate's standing dashboard, not a task. |
| [#387 — Evaluate AWS Bedrock as the summarization provider](https://github.com/HPAC-Safety/safety-report/issues/387) | AI | Open research spike. Summaries use Gemini today ([ADR-0104](decisions/ADR-0104-summaries-are-generated-by-gemini-through-a-paid-key.md)); the provider is configuration behind `IAiChatClient`. |
| [#413 — Identify commenters by name and HPAC number once OIDC lands](https://github.com/HPAC-Safety/safety-report/issues/413) | Security | Open, waiting on the real identity provider ([ADR-0064](decisions/ADR-0064-jwt-bearer-authentication-with-three-roles.md)). Comments show "Member" until then ([ADR-0114](decisions/ADR-0114-members-may-comment-on-a-published-report.md)). |
| [#427 — Show report attachments as a thumbnail strip with a lightbox, and a viewer-scoped count](https://github.com/HPAC-Safety/safety-report/issues/427) | Web, API | Open, unblocked; the owner's decisions are recorded on the issue. Builds on ADR-0117 and ADR-0119. |
| [#437 — Reconcile skill statements that contradict accepted ADRs](https://github.com/HPAC-Safety/safety-report/issues/437) | Documentation | In progress. The full audit of instructions and documentation against the ADRs; its decisions are recorded on the issue. |
| [#441 — Remove Terraform that contradicts the ADRs](https://github.com/HPAC-Safety/safety-report/issues/441) | Infrastructure | Open. The unused migrate task (ADR-0055) and the SES leftovers (CON-INF-002). |
| [#443 — Run the API and the Worker on Lambda](https://github.com/HPAC-Safety/safety-report/issues/443) | Infrastructure | Open. The Terraform, the deploy workflows, and the Worker's drain-once Lambda host ([ADR-0042](decisions/ADR-0042-lambda-hosted-api-with-fargate-migration-path.md), [ADR-0123](decisions/ADR-0123-the-worker-runs-on-lambda-and-the-website-on-s3-and-cloudfront.md)). |
| [#444 — Check that the source inventory and issue traceability stay current](https://github.com/HPAC-Safety/safety-report/issues/444) | Documentation | Open. The CI check for this page and the [source inventory](source-inventory.md). |
| [#446 — Bind the @ignore scenarios whose behavior is already built](https://github.com/HPAC-Safety/safety-report/issues/446) | CI | Open. Scenarios the #437 audit found built but still tagged `@ignore`, so the matrix reports them as uncovered. |
