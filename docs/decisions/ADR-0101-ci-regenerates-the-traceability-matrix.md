---
title: CI regenerates the traceability matrix onto the pull request, and the matrix check is required to merge
description: A pull_request_target workflow runs the base branch's generator over a same-repo PR's files and commits docs/traceability.md onto that PR; and the docs job becomes a required check on main, so a stale matrix can't merge.
type: adr
status: accepted
date: 2026-09-23
decision-makers: Chase Florell
keywords: traceability, generated documentation, pull_request_target, required status checks, up to date, ADR-0084, ADR-0057
---

# ADR-0101 — CI regenerates the traceability matrix onto the pull request, and the matrix check is required to merge

**Status:** Accepted. Amends
[ADR-0084](ADR-0084-stable-claim-ids-and-a-generated-traceability-matrix.md)
on one point: who regenerates `docs/traceability.md`. The matrix is still
generated, still tracked, and never hand-edited.

## Context

ADR-0084 made `docs/traceability.md` a generated, tracked file, with the
`docs` job in `ci.yml` failing when it drifts. Two things regenerate it today,
and neither runs where it matters:

- `.githooks/post-merge` and `post-rewrite` regenerate it after a merge or
  rebase **done locally**.
- `ci.yml` only **checks** it.

On 2026-09-23 #368 and #369 each added five claims, and each regenerated the
matrix against the same `main`. Both squash-merged, 22 seconds apart. The
`main` ruleset already requires branches to be up to date
(`strict_required_status_checks_policy: true`), so that rule was not the gap.
Two other things were:

- The ruleset gives the Admin role a bypass with `bypass_mode: always`, so
  #369 could merge while it was behind `main`.
- `docs`, the `ci.yml` job that checks the matrix, is not one of the required
  checks. So even a branch that was up to date and had a red `docs` job could
  merge.

When #369 merged behind `main`, Git merged the two sets of claim rows cleanly. Both PRs had rewritten the summary line to
the same `299 claims` text, though, so `main` kept that line when the
generator now says 304. `main`'s `docs` job went red, and the only remedy
available was a pull request whose whole content was a regenerated file.

A person should never open a pull request to regenerate a generated file.

## Decision

1. **`.github/workflows/traceability.yml` regenerates the matrix onto the pull
   request's own branch.** It runs on `pull_request_target` when a PR touches
   the generator's inputs (`features/**`, the five `CONSTRAINT_PAGES`, the
   matrix itself, or the generator). It is gated to same-repo PRs, like
   [ADR-0057](ADR-0057-same-repo-pull-requests-translate-in-pr.md). When the
   regenerated file differs, it commits `Regenerate the traceability matrix`
   onto the head branch. It never pushes to `main` and writes nothing for a
   fork.
2. **No code the pull request wrote runs with the write token.** The
   generator comes from the base branch and is pointed at the head commit's
   files as data. `tools/traceability.mjs` only reads the feature files and
   the five docs pages, and writes one markdown file. The head is checked out
   by SHA without persisted credentials, and nothing is installed. A PR that
   changes the generator is skipped with a notice, because the base generator
   would render the old format. Its author regenerates locally, and
   `ci.yml`'s check, run with the head's own generator, still decides.
3. **The push re-triggers CI with `TRANSLATION_PR_TOKEN`.** A push made with
   the built-in `GITHUB_TOKEN` starts no workflows, so the required checks
   would never re-run on the bot's commit. The fine-grained PAT that
   `i18n-translate.yml` already uses for the same reason
   ([ADR-0021](ADR-0021-ci-translation-opens-a-pull-request.md)) is scoped to
   this repository with Contents and Pull requests only. Reusing it adds no
   new credential. When the job runs again on its own push, it finds nothing
   to change, so it can't loop.
4. **`docs` becomes a required check on the `main` ruleset.** The ruleset
   already requires branches to be up to date. With `docs` required too, a PR
   that falls behind is updated, this workflow regenerates the matrix against
   the combined tree, and the PR can't merge until `docs` is green on that
   tree. Whether the Admin role keeps its `always` bypass, the one that let
   #369 merge while behind, is the owner's decision and is still pending. While
   it stays, an administrator's override can still land a stale matrix, and
   the next PR's run of this workflow repairs it.
5. `ci.yml`'s check stays, as the backstop rather than the fixer. So do the
   local hooks, which keep a developer's own tree right between pushes.

## Alternatives rejected

- **A pull request that regenerates the matrix after the fact**, by hand or
  opened by a push-to-main job as `i18n-translate.yml` does for French. It
  leaves `main` red until someone merges it, and it is exactly the chore this
  decision removes. French needs a human to read it; a generated index does
  not.
- **Stop tracking the file and generate it at build time.** It removes the
  drift entirely, but it reverses ADR-0084 and
  [ADR-0088](ADR-0088-the-matrix-carries-the-specification-into-the-graph.md).
  The matrix is how the specification reaches the knowledge graph, and it is
  linked from the docs and cited in PR exemptions (ADR-0090). All of that
  needs it in the tree.
- **A merge queue on its own.** It tests each merge result, so it would catch
  the drift, but it doesn't fix the file. It would still need this workflow,
  and requiring branches to be up to date, which the ruleset already does,
  with `docs` required gets the same guarantee on a
  repository of this size without the queue.
- **Running the head branch's generator.** It would follow a PR that changes
  the matrix format. It would also run PR-authored code with a write token,
  which is the risk `pull_request_target` exists to fence off.

## Consequences

- A contributor never commits the matrix by hand. The hooks and the workflow
  keep it right, and a PR can carry one extra bot commit that a local branch
  pulls before its next push.
- A PR that falls behind `main` already had to be updated before it could
  merge. Now its `docs` check must also pass on the updated tree.
- A PR that changes `tools/traceability.mjs`, and every fork PR, still
  regenerates locally. `ci.yml` says so when it fails.
- Without `TRANSLATION_PR_TOKEN` the commit still lands, but checks need a
  manual nudge. The job warns when that happens.
