---
title: GitHub workflows
description: What each workflow under .github/workflows is responsible for.
type: readme
---

# GitHub workflows

| Workflow | Responsibility |
|---|---|
| `ci.yml` | Build, tests, coverage, web, localization, skill/agent validation |
| `linked-issue.yml` | Check each PR body: a closing issue reference (`linked-issue`), no agent session link (`no-session-link`), and screenshots or a reason for none on a rendered web change (`screenshots`) |
| `feature-coverage.yml` | Require a scenario for a behavior change, or a citation of the claims it preserves |
| `i18n-translate.yml` | Prepare French application-catalogue changes only, and report each run that calls the provider to open `verify:translation-run` issues (ADR-0103) |
| `issue-traceability.yml` | Daily and on push to `main`: keep one drift issue open while `docs/issue-traceability.md` misses an open issue or lists a closed one. Never gates a PR |
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

## Merge queue

Pull requests merge through GitHub's merge queue, which tests each one on a
`gh-readonly-queue/main/*` branch holding `main` and the pull requests ahead of
it ([ADR-0147](../../docs/decisions/ADR-0147-pull-requests-merge-through-a-merge-queue.md)).

- A workflow that reports a required context triggers on `merge_group`:
  `ci.yml`, `linked-issue.yml`, `feature-coverage.yml`, and `terraform.yml`.
  A new required context needs `merge_group` too, or the queue stalls.
- Each required job reports there under its own id. A job with nothing to
  check on a merge group runs a notice step and passes; it is never left out.
- The body checks (`linked-issue`, `no-session-link`, `screenshots`) pass
  through: a merge group has no pull request body.
- `feature-coverage` checks each queued squash commit, whose message is its
  pull request's body, against the merged matrix.
- Nothing comments, pushes, or deploys on a merge group. The coverage
  baseline is only a `push` run on `main`.
- A required-check workflow never lets a cancelled run be a context's latest
  result: `linked-issue.yml` and `feature-coverage.yml` queue runs instead of
  cancelling them.

Run `actionlint` after workflow edits. If a required job ID changes, update the
repository ruleset in the same PR. Every PR body must contain an actual closing
keyword such as `Closes #78`.
