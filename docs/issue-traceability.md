---
title: Issue traceability
description: Every open GitHub issue and how it relates to the target specification.
type: guide
---

# Issue traceability

This page lists every open issue and how it stands against the
[specification](../.spec/features/README.md), as of 2026-10-02 (#550). Closed issues
are not listed: their history is in GitHub, and what they decided lives in the
ADRs and `.spec/features`. A pull request that closes an issue removes its row.

`tools/spec/check-issue-traceability.mjs` compares this page with the open issues. It
never fails a pull request: `.github/workflows/issue-traceability.yml` runs it
daily and on every push to `main`, and keeps one "Issue traceability drift"
issue open while an open issue has no row here or a row names a closed one
([ADR-0143](../.spec/decisions/ADR-0143-issue-traceability-drift-opens-an-issue-and-gates-nothing.md)).

| Issue | Area | Disposition |
|---|---|---|
| [#31 — Replace the Typeform with the new report form](https://github.com/HPAC-Safety/safety-report/issues/31) | Infrastructure | Open, phase 2. The cut-over after deployment. |
| [#47 — Dependency Dashboard](https://github.com/HPAC-Safety/safety-report/issues/47) | — | Renovate's standing dashboard, not a task. |
| [#387 — Evaluate AWS Bedrock as the summarization provider](https://github.com/HPAC-Safety/safety-report/issues/387) | AI | Open research spike. Summaries use Gemini today ([ADR-0104](../.spec/decisions/ADR-0104-summaries-are-generated-by-gemini-through-a-paid-key.md)); the provider is configuration behind `IAiMediator`. |
| [#413 — Identify commenters by name and HPAC number once OIDC lands](https://github.com/HPAC-Safety/safety-report/issues/413) | Security | Open, waiting on the real identity provider ([ADR-0064](../.spec/decisions/ADR-0064-jwt-bearer-authentication-with-three-roles.md)). Comments show "Member" until then ([ADR-0114](../.spec/decisions/ADR-0114-members-may-comment-on-a-published-report.md)). |
| [#566 — Import historical Typeform report exports from Manage reports](https://github.com/HPAC-Safety/safety-report/issues/566) | API, web, i18n | Open, phase 2. An administrator imports HPAC's English and French Typeform `.xlsx` exports from **Manage reports**, re-runnable as newer exports arrive; needed before the cut-over in #31. |
| [#607 — Deploy HPAC-Safety to production: HPAC's AWS account at safety.hpac.ca and securite.acvl.ca, promoted on approval](https://github.com/HPAC-Safety/safety-report/issues/607) | Infrastructure | Open, phase 2, blocked by #606. Replaces the production half of #30: the code, Terraform, and workflows are built, and what remains is the one-time setup in `infra/SETUP.md` Part 3, DNS, and the first approved promotion ([infrastructure and operations](../.spec/infrastructure-and-operations.md)). |
| [#661 — Post a published report to HPAC's social channels (WhatsApp first)](https://github.com/HPAC-Safety/safety-report/issues/661) | API, web, Worker, infrastructure, i18n | Phase 3, after launch. Designs the specification for social channels; until it lands, [system overview](../.spec/system-overview.md) `CON-SO-009` and [interfaces and data flow](../.spec/interfaces-and-data-flow.md) `CON-IF-002` keep a publication channel out of scope. |
| [#662 — Spike: how the safety bot reaches the WhatsApp group](https://github.com/HPAC-Safety/safety-report/issues/662) | Worker, infrastructure | Phase 3 spike under #661. Its answer decides the sender adapter and where it is hosted. |
| [#690 — Staging: verify the 250 MB video upload and the 26 MB refusal once sign-in works](https://github.com/HPAC-Safety/safety-report/issues/690) | Infrastructure | Open, phase 2. The one acceptance check left from #606, waiting on sign-in in staging ([ADR-0172](../.spec/decisions/ADR-0172-a-temporary-interim-issuer-signs-staging-tokens-until-a-real-provider-exists.md)). |
