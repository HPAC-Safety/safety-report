---
title: Pull requests merge through a merge queue
description: Pull requests reach main through GitHub's merge queue, which tests each one on top of main and the pull requests ahead of it; every required check reports on merge_group, the pull-request-body checks pass through there, and required-check workflows never let a cancelled run be a context's latest result.
type: adr
status: accepted
date: 2026-09-26
decision-makers: Chase Florell
keywords: merge queue, merge_group, ruleset, required status checks, up to date, auto-merge, concurrency, cancel-in-progress, coverage baseline, paths-filter, pull request body
---

# ADR-0147 — Pull requests merge through a merge queue

**Status:** Accepted.

## Context

The `main` ruleset requires a branch to be up to date before it merges
(`strict_required_status_checks_policy: true`). With several agent pull
requests open at once, every merge sends the others `BEHIND`, and each one is
rebased and re-run by hand before it can land
([lesson 0011](../lessons/0011-a-branch-rebased-before-its-push-is-behind-by-the-time-it-is-green.md)).

Turning the rule off would be unsafe here, because many conflicts in this
repository are not textual. Two pull requests can each pass alone and still
break `main` once both merge:

- both claim the same ADR, lesson, or `REQ` number
  ([ADR-0091](ADR-0091-an-adr-number-is-verified-not-assumed.md));
- the traceability matrix goes stale
  ([ADR-0101](ADR-0101-ci-regenerates-the-traceability-matrix.md));
- the coverage baseline shifts
  ([ADR-0014](ADR-0014-coverage-gate.md));
- one pull request removes a claim that another's `feature-coverage` exemption
  cites ([ADR-0090](ADR-0090-an-exemption-cites-the-claims-it-preserves.md)).

A second problem showed up while PR #544 was merging. Two runs of a required
workflow started on the same commit within a second of each other: a body edit
and a push. The `cancel-in-progress` concurrency group cancelled one of them,
and its "cancelled" result became the latest one for the required
`linked-issue` context. The pull request stayed blocked until someone re-ran
the cancelled run.

### What GitHub's merge queue does

These are GitHub's documented facts that this decision relies on:

- A merge queue tests each queued pull request on a temporary
  `gh-readonly-queue/<base>/…` branch: `main`, plus the pull requests ahead of
  it, plus this one. It merges each pull request in order once that branch
  passes every required check, and the author never updates the branch by
  hand ([Managing a merge queue][manage]).
- GitHub Actions report on that branch only for a workflow triggered by
  `merge_group`; otherwise the required checks never report and the merge
  fails ([Managing a merge queue][manage];
  [Events that trigger workflows, `merge_group`][events]).
- The `merge_group` payload carries a `merge_group` object with `base_sha`,
  `base_ref`, `head_sha`, `head_ref`, and `head_commit`. It carries no
  `pull_request`, so `github.event.pull_request.*` is empty there and no pull
  request body is available ([Webhook events, `merge_group`][payload]).
- A pull request leaves the queue when a required check fails or does not
  report within the timeout, and its timeline records why
  ([Merging a pull request with a merge queue][merging]).
- With auto-merge enabled, a pull request joins the queue once its own
  required checks pass ([Merging a pull request with a merge queue][merging]).
- The queue's squash commit uses the repository's default squash message,
  which here is the pull request body (`squash_merge_commit_message: PR_BODY`)
  ([community discussion #111224][squash]; confirmed on the first queued
  merge, #547).
- The ruleset's `merge_queue` rule takes `merge_method`, `grouping_strategy`
  (`ALLGREEN` or `HEADGREEN`), `max_entries_to_build`,
  `min_entries_to_merge`, `max_entries_to_merge`,
  `min_entries_to_merge_wait_minutes`, and `check_response_timeout_minutes`
  ([Repository rules REST API][rules]).
- `dorny/paths-filter@v4` detects changes on `merge_group` itself: it diffs
  the payload's `merge_group.base_sha` against its `head_sha` with git, and
  needs no token or pull request
  ([paths-filter `src/main.ts`][pathsfilter]).

## Decision

**Pull requests merge through a merge queue.** `main` stays protected by the
up-to-date rule, and the queue, not the author, keeps each pull request up to
date.

### Every required check reports on `merge_group`

- `ci.yml`, `linked-issue.yml`, `feature-coverage.yml`, and `terraform.yml`
  trigger on `merge_group` (`checks_requested`, base `main`).
- Each required context reports under its own job id there, never a missing
  job: a job that has nothing to check runs a notice step and passes.
- `terraform.yml`: only `infra` runs. `plan` requires `pull_request` and
  `apply` requires `push` or `workflow_dispatch`, so neither runs on a merge
  group.

### The pull request body checks pass through

`linked-issue`, `no-session-link`, and `screenshots` read only the pull
request body. On `merge_group` each runs a notice step and passes. This is
safe for three reasons:

- each one already passed against the body on the pull request, and
  auto-merge queues a pull request only after its required checks pass;
- the body becomes the squash commit message unchanged;
- no body check depends on another pull request, so no collision can show up
  only on the merged tree.

### Collision-sensitive checks really run on the merged tree

- `docs` (ADR numbers, frontmatter, the traceability matrix), `cucumber`,
  `i18n`, `build`, `test`, `web`, `e2e`, `coverage`, `agent-config`, and
  `infra` run on the merge group exactly as on a pull request.
- `feature-coverage` runs once per queued squash commit. Each run uses that
  commit's own diff, its message (the pull request body), and the merged
  tree's matrix. That catches an exemption citing a claim that a pull request
  ahead in the queue removed.
- **Two pull requests claiming the same ADR number:** the first merges. The
  second's merge group holds both records, so `adr-numbers.mjs` fails in
  `docs`, the second pull request leaves the queue, and `main` never sees the
  collision. If both files share one name, the queue cannot build the group,
  and the second pull request leaves the queue the same way.

### `changes` decides on the merge group, or runs everything

`changes` runs paths-filter on `merge_group` as it does on a pull request. If
the filter step fails, every gated job runs: a job skipped by a broken filter
would be a check that silently passed.

### Coverage

- The ratchet runs on the merge group against `main`'s baseline, because that
  tree is the one that becomes `main`.
- The pull request comment step is skipped: a merge group has no pull request.
- The baseline is only ever a successful `push` run on `main`
  (`gh run list --branch main --event push`). A merge group's
  `coverage-report` artifact never sets the bar.

### No comment, bot push, or deploy on a merge group

- `traceability.yml` and `i18n-translate.yml` use `pull_request_target`, which
  a merge group never fires. `i18n-translate.yml`'s `push` trigger is filtered
  to `main`. Neither workflow ever pushes to a `gh-readonly-queue/*` branch.
- The deploy workflows run on `workflow_dispatch`, and their re-enabled path
  requires a `push` run on `main`.
- Terraform `plan` comments and `apply` never run on a merge group.

### A cancelled run is never a required context's latest result

- `ci.yml` and `terraform.yml` cancel an in-progress run only for a newer push
  to the same pull request. Every job in the newer run is always scheduled, so
  it reports every context again, on a newer commit.
- `linked-issue.yml` and `feature-coverage.yml` never cancel in progress. They
  queue instead: a body edit and a push can land on the same commit, and these
  jobs take seconds, so the newer run waits and always finishes last.
- On `merge_group` each group's ref is its own queue branch, so queue runs
  never share a concurrency group and never cancel each other.

### The ruleset

The `main` ruleset gains a `merge_queue` rule, recorded in
`docs/github-ruleset.json`:

| Parameter | Value | Why |
|---|---|---|
| `merge_method` | `SQUASH` | the repository's only merge method |
| `grouping_strategy` | `ALLGREEN` | every pull request's own queued commit must pass, so each one landing on `main` was tested as it lands, and a failure names the pull request that caused it |
| `max_entries_to_build` | 5 | several agent pull requests can be tested at once without one full CI run each waiting in line |
| `min_entries_to_merge` | 1 | a lone pull request never waits for company |
| `max_entries_to_merge` | 5 | with `ALLGREEN`, merging up to five green entries in one push costs no attribution and saves a `main` CI run for each |
| `min_entries_to_merge_wait_minutes` | 5 | GitHub's default; no effect while the minimum is 1 |
| `check_response_timeout_minutes` | 60 | CI's slowest path is under 15 minutes; an hour leaves room for runner queuing and ejects only a run that has truly hung |

- `strict_required_status_checks_policy` stays `true`.
- **Rollout:** the workflow changes merge first. An administrator applies the
  rule only after `main` carries them; otherwise the first queued pull request
  stalls on checks that never report.

### How an author reads the queue

- A pull request that is `BEHIND` needs no rebase. Only a real conflict does.
- A pull request removed from the queue shows why in its timeline. A failed
  check on the merge group means it collides with something ahead of it:
  rebase onto `main`, fix the collision, and push. Auto-merge queues it again.

## Alternatives considered

- **Turn off "require branches to be up to date".** Rejected by the owner:
  each pull request is green alone, and `main` breaks when two collide on a
  number, the matrix, or coverage.
- **Keep rebasing by hand.** This is the cost the queue removes. It scales
  with the number of open pull requests, and each rebase re-runs all of CI.
- **Run the body checks on the merge group by reading each pull request
  through the API.** Rejected: the queue ref names only the last pull request
  in a group, and these checks cannot collide, so the extra machinery would
  buy nothing. `feature-coverage` is the exception, because its exemption
  cites claims that another pull request can remove, and it reads the squash
  commit message, which is already on the queue branch.
- **Pass `feature-coverage` through as well.** Rejected: that would leave the
  exemption's citations unchecked against the tree that becomes `main`, which
  is exactly where they can break.
- **`HEADGREEN`.** Rejected: it tests only the head of a group, so a failure
  would not say which pull request caused it, and a pull request in the
  middle of a group would land untested as itself.
- **Queue size 1.** Rejected: with `ALLGREEN`, every pull request is still
  tested alone on its exact base, so merging green entries together only
  saves `main` runs.
- **Keep `cancel-in-progress: true` on the body checks, grouped by commit.**
  Rejected: a body edit on the same commit must still supersede the older
  body's result. Queuing the runs keeps both correct at a cost of a few
  seconds.

## Consequences

- Agents enable auto-merge as before, and the pull request enters the queue
  once its checks pass. A pull request that is only `BEHIND` needs no rebase.
- Each queued pull request runs CI twice: once on its branch, and once on the
  merge group.
- A collision between two pull requests ejects the later one from the queue
  instead of breaking `main`.
- The `feature-coverage` check on a merge group relies on the queue's squash
  message being the pull request body. If GitHub ever changes that, the check
  fails loudly and ejects the pull request, and nothing merges silently.
- The rule takes effect when an administrator applies it after this change
  merges. The first queued pull request is the test that every required check
  reports, recorded on #547.

[manage]: https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/configuring-pull-request-merges/managing-a-merge-queue
[merging]: https://docs.github.com/en/pull-requests/collaborating-with-pull-requests/incorporating-changes-from-a-pull-request/merging-a-pull-request-with-a-merge-queue
[events]: https://docs.github.com/en/actions/reference/workflows-and-actions/events-that-trigger-workflows#merge_group
[payload]: https://docs.github.com/en/webhooks/webhook-events-and-payloads#merge_group
[rules]: https://docs.github.com/en/rest/repos/rules#update-a-repository-ruleset
[squash]: https://github.com/orgs/community/discussions/111224
[pathsfilter]: https://github.com/dorny/paths-filter/blob/v4/src/main.ts
