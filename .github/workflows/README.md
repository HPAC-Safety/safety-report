---
title: GitHub workflows
description: What each workflow under .github/workflows is responsible for.
type: readme
---

# GitHub workflows

| Workflow | Responsibility |
|---|---|
| `ci.yml` | Build, tests, coverage, web, localization, skill/agent validation |
| `linked-issue.yml` | Require a closing issue reference in each PR |
| `feature-coverage.yml` | Require a scenario for a behavior change, or a citation of the claims it preserves |
| `i18n-translate.yml` | Prepare French application-catalogue changes only, and report each run that calls the provider to open `verify:translation-run` issues (ADR-0103) |
| `traceability.yml` | Commit the regenerated `docs/traceability.md` onto a same-repo PR's branch |
| `terraform.yml` | Validate/plan/apply infrastructure |
| `deploy-api.yml` | Publish API image and run explicit migrations |
| `deploy-worker.yml` | Publish Worker image |
| `deploy-web.yml` | Publish static sites |

Pull-request workflows must be safe for forks: use `pull_request`, do not
expose secrets, and never make live AI or translation calls. Two workflows use
`pull_request_target` to commit onto a PR's own branch, and both are gated to
same-repo pull requests: `i18n-translate.yml` (ADR-0057) and
`traceability.yml`, which runs only the base branch's generator over the head's
files (ADR-0101). Neither ever pushes to `main`. `i18n-translate.yml` also holds
`issues: write`, only to comment on and close issues labelled
`verify:translation-run` (ADR-0103). Catalogue generation does not
translate database questions or summaries.

Deployments run only from successful tested `main` commits or explicit manual
dispatch, use GitHub OIDC rather than AWS access keys, and are protected by the
production environment. A migration completes before new API traffic.

The current deployment workflows still reflect legacy combined-site and email
infrastructure. Align them with issue #30 and
[`../../docs/infrastructure-and-operations.md`](../../docs/infrastructure-and-operations.md)
before production use.

Run `actionlint` after workflow edits. If a required job ID changes, update the
repository ruleset in the same PR. Every PR body must contain an actual closing
keyword such as `Closes #78`.
