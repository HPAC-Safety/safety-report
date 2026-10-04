---
title: Issue traceability
description: Every open GitHub issue with its milestone, labels, and parent, generated from GitHub.
type: guide
---

# Issue traceability

> **Generated file — do not edit by hand.**
> Regenerate with `node tools/spec/generate-issue-traceability.ts`, which reads
> the open issues with `GITHUB_TOKEN`, `GH_TOKEN`, or the `gh` login
> ([ADR-0191](../.spec/decisions/ADR-0191-each-rule-is-stated-once-and-no-status-page-is-written-by-hand.md)).

Every open issue, with its milestone, labels, and parent. Closed issues are
not listed: their history is in GitHub, and what they decided lives in the
ADRs and `.spec/features`. What an issue asks for is in the issue itself.

Nothing checks this page on a pull request. `.github/workflows/issue-traceability.yml`
compares it with GitHub daily and on every push to `main`, and keeps one
"Issue traceability drift" issue open while they differ.

| Issue | Milestone | Labels | Parent |
|---|---|---|---|
| [#31 — Replace the Typeform with the new report form](https://github.com/HPAC-Safety/safety-report/issues/31) | Phase 2 — Publish | `area:infra`, `phase:2` | — |
| [#47 — Dependency Dashboard](https://github.com/HPAC-Safety/safety-report/issues/47) | — | — | — |
| [#387 — Evaluate AWS Bedrock as the summarization provider](https://github.com/HPAC-Safety/safety-report/issues/387) | Phase 3 — After launch | — | — |
| [#413 — Identify commenters by name and HPAC number once OIDC lands](https://github.com/HPAC-Safety/safety-report/issues/413) | Phase 3 — After launch | `area:security` | — |
| [#566 — Import historical Typeform report exports from Manage reports](https://github.com/HPAC-Safety/safety-report/issues/566) | Phase 2 — Publish | `area:api`, `area:i18n`, `area:web`, `enhancement`, `phase:2` | [#31](https://github.com/HPAC-Safety/safety-report/issues/31) |
| [#607 — Deploy HPAC-Safety to production: HPAC's AWS account at safety.hpac.ca and securite.acvl.ca, promoted on approval](https://github.com/HPAC-Safety/safety-report/issues/607) | Phase 2 — Publish | `area:infra`, `enhancement`, `phase:2` | — |
| [#661 — Post a published report to HPAC's social channels (WhatsApp first)](https://github.com/HPAC-Safety/safety-report/issues/661) | Phase 3 — After launch | `area:api`, `area:i18n`, `area:infra`, `area:web`, `area:worker`, `enhancement` | — |
| [#662 — Spike: how the safety bot reaches the WhatsApp group](https://github.com/HPAC-Safety/safety-report/issues/662) | Phase 3 — After launch | `area:infra`, `area:worker`, `enhancement` | [#661](https://github.com/HPAC-Safety/safety-report/issues/661) |
| [#690 — Staging: verify the 250 MB video upload and the 26 MB refusal once sign-in works](https://github.com/HPAC-Safety/safety-report/issues/690) | Phase 2 — Publish | `area:infra`, `enhancement`, `phase:2` | — |
