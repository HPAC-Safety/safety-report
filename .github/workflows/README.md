---
title: GitHub workflows
description: What each workflow under .github/workflows is responsible for.
type: readme
---

# GitHub workflows

Each `run:` step is one command; its logic is a tested script under
[`tools/`](../../tools/README.md)
([ADR-0189](../../.spec/decisions/ADR-0189-a-workflow-step-runs-one-command-and-tools-is-grouped-by-domain.md)).

| Workflow | Responsibility |
|---|---|
| `ci.yml` | Build, tests, coverage, web, localization, skill/agent validation |
| `linked-issue.yml` | Check each PR body: a closing issue reference (`linked-issue`), no agent session link (`no-session-link`), and screenshots or a reason for none on a rendered web change (`screenshots`) |
| `feature-coverage.yml` | Require a scenario for a behavior change, or a citation of the claims it preserves |
| `i18n-translate.yml` | Prepare French application-catalogue changes only, and report each run that calls the provider to open `verify:translation-run` issues (ADR-0103) |
| `issue-traceability.yml` | Daily and on push to `main`: keep one drift issue open while `docs/issue-traceability.md` misses an open issue or lists a closed one. Never gates a PR |
| `traceability.yml` | Commit the regenerated `.spec/traceability.md`, `.spec/bindings.md`, and `.spec/README.md` onto a same-repo PR's branch |
| `terraform.yml` | Validate Terraform on every pull request (credential-free); plan it against both AWS accounts on a same-repo pull request. Never applies. |
| `release.yml` | **Release**, run by hand on `main`: create the next `YYYY.MM.DD-N` tag and a GitHub Release with notes generated from the pull requests merged since the last one (`.github/release.yml`), build the API image, the Worker image, and the web bundle once, and deploy them to `hpac-safety-staging` (issues #619, #621, ADR-0168). Never deploys to production (issue #466, ADR-0158, ADR-0166, CON-INF-011..013) |
| `promote.yml` | Run by hand on a release tag: deploy that tag's already-built, staging-green artifacts to `hpac-safety-production` after `hpac-safety-admins` approves — the same artifacts, never a rebuild (issue #609, ADR-0166, CON-INF-012) |
| `deploy-environment.yml` | Reusable (`workflow_call` only, no trigger of its own): the one set of deploy steps `release.yml` (staging) and `promote.yml` (production) both call, so the two environments can never drift apart |

Pull-request workflows must be safe for forks: use `pull_request`, do not
expose secrets, and never make live AI or translation calls. Two workflows use
`pull_request_target` to commit onto a PR's own branch, and both are gated to
same-repo pull requests: `i18n-translate.yml` (ADR-0057) and
`traceability.yml`, which runs only the base branch's generator over the head's
files (ADR-0101). Neither ever pushes to `main`. `i18n-translate.yml` also holds
`issues: write`, only to comment on and close issues labelled
`verify:translation-run` (ADR-0103). Catalogue generation does not
translate database questions or summaries.

**Only `release.yml` and `promote.yml` (and, through them,
`deploy-environment.yml`) ever deploy to AWS**: `release.yml` to staging only,
when a maintainer runs it on `main`; `promote.yml` to production only, when
a maintainer dispatches it on a tag already green on staging — never on push, merge, or pull_request.
Each holds its own concurrency group, so a promotion waiting on approval
never holds a staging release (lesson 0026). They use GitHub OIDC rather than
AWS access keys, one role per account (`hpac-safety-deploy`), and
`hpac-safety-production` is protected by the `hpac-safety-admins` team's
required approval. `terraform.yml`'s `plan` job also reaches both
accounts, read-only, through a separate `hpac-safety-plan` role per account —
see its own comments for why that cannot be the same GitHub Environment the
deploy jobs use. Every third-party action `release.yml`, `promote.yml`, and
`deploy-environment.yml` call is pinned to a full commit SHA, kept current by
Renovate. See [`../../docs/deployment.md`](../../docs/deployment.md) for the
release, rollback, and configuration story in full.

## Merge queue

Pull requests merge through GitHub's merge queue, which tests each one on a
`gh-readonly-queue/main/*` branch holding `main` and the pull requests ahead of
it ([ADR-0147](../../.spec/decisions/ADR-0147-pull-requests-merge-through-a-merge-queue.md)).

- A workflow that reports a required context triggers on `merge_group`:
  `ci.yml`, `linked-issue.yml`, `feature-coverage.yml`, and `terraform.yml`.
  A new required context needs `merge_group` too, or the queue stalls.
- Each required job reports there under its own id. A job with nothing to
  check on a merge group runs a notice step and passes; it is never left out.
- `linked-issue` and `screenshots` pass through: a merge group has no pull
  request body, and both already passed on the pull request.
- `no-session-link` and `feature-coverage` check each queued squash commit,
  whose message is its pull request's body; `feature-coverage` checks it
  against the merged matrix.
- Nothing comments, pushes, or deploys on a merge group. The coverage
  baseline is only a `push` run on `main`.
- A required-check workflow never lets a cancelled run be a context's latest
  result: `linked-issue.yml` and `feature-coverage.yml` queue runs instead of
  cancelling them.

Run `actionlint` after workflow edits. If a required job ID changes, update the
repository ruleset in the same PR. Every PR body must contain an actual closing
keyword such as `Closes #78`.
