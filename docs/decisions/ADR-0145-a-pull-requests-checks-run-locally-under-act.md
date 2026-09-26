---
title: A pull request's checks run locally under act, against CI's own baseline
description: tools/ci-local.sh runs the pull request workflows under nektos/act in a pinned local image, with a synthetic event that takes every fork branch, so the coverage ratchet and the body checks give CI's verdict before the pull request is opened.
type: adr
status: accepted
date: 2026-09-26
decision-makers: Chase Florell
keywords: act, local CI, coverage gate, ratchet, pull request checks, Testcontainers, Docker Desktop, GitHub Actions
---

# ADR-0145 — A pull request's checks run locally under act, against CI's own baseline

**Status:** Accepted. Amends
[ADR-0014](ADR-0014-coverage-gate.md) (where the pre-pull-request ratchet
runs) and
[ADR-0073](ADR-0073-a-ui-scenario-is-skipped-by-reqnroll-itself.md) (a rule
enforced by a CI flag now also runs, flag and all, before the pull request).

## Context

Two scripts, `tools/coverage-check.sh`
([lesson 0010](../lessons/0010-a-coverage-gate-found-in-ci-not-before-the-pull-request.md))
and `check-coverage.sh`, each re-implemented CI's coverage ratchet on macOS.
Their verdict often differed from CI's: another baseline, another operating
system, another ffmpeg, and an SDK chosen by `rollForward` rather than the one
`global.json` names. Every other pull request check was run by hand or not at
all.

## Decision

**`tools/ci-local.sh --body <pr-body.md>` runs the pull request workflows under
[nektos/act](https://github.com/nektos/act), version pinned in `.act-version`,
and is the pre-pull-request gate.**

- **What runs**: `linked-issue.yml` (`linked-issue`, `no-session-link`,
  `screenshots`), `feature-coverage.yml`, `terraform.yml -j infra`, and every
  `ci.yml` job, stopping at the first failure. The body checks go first
  because they fail in seconds.
- **What never runs**: `traceability.yml`, `i18n-translate.yml`, terraform
  `plan` and `apply`, `deploy-*`, `terraform-relock`. They push commits, call
  a translation provider, or assume an AWS role. The wrapper's allow list
  cannot name them.
- **The event**: a synthetic `pull_request` with the draft body, `base.sha` at
  the fetched `origin/main`, `head.sha` at `HEAD`, and
  `head.repo.full_name = "local/act"`. Every write guarded by "same
  repository" (the coverage comment) takes its fork branch.
- **The token**: only `GITHUB_TOKEN`, from the environment, never argv. It is
  `HPAC_ACT_TOKEN`, ideally a fine-grained read-only token for this
  repository (Actions, Contents, Metadata: read), else `gh auth token` with a
  warning. `.actrc` points act's secret, variable, and env files at
  `/dev/null`; `.secrets`, `.vars`, and `.actrc.local` are gitignored.
- **The image**: `tools/act/Dockerfile`, built locally, is
  `catthehacker/ubuntu:act-24.04` pinned by digest plus `gh` and `shellcheck`
  from Ubuntu's archive and, on arm64, the x86-64 loader and libc. It runs
  natively on each host: arm64 on Apple Silicon, never amd64 emulation for
  the .NET jobs. Docker Desktop runs the workflows' linux-amd64 downloads
  (terraform, tflint, skillfile) through Rosetta.
- **The checkout**: act runs in a throwaway clone of `HEAD`, because a
  worktree's `.git` is a file that points outside the copy act makes.
- **Coverage parity**: the `coverage` job runs as on GitHub, on Ubuntu 24.04
  with the exact SDK, and downloads the same artifact from main's last green
  run. Lesson 0010's same-machine baseline is dropped: it existed only because
  the branch was measured on macOS.
- **Testcontainers** keep Ryuk on
  ([lesson 0008](../lessons/0008-containers-outlive-the-worktree-that-started-them.md)).
  Under Docker Desktop the wrapper sets
  `TESTCONTAINERS_HOST_OVERRIDE=host.docker.internal`, because a published
  port reaches the VM's host network about two seconds after the container
  starts, and Testcontainers connects before then.

### The `env.ACT` carve-outs in `ci.yml`

act sets `ACT=true`; on GitHub `env.ACT` is empty, so each carve-out is a
no-op there.

- **paths-filter**: `token: ${{ !env.ACT && github.token || '' }}`. No pull
  request exists for the API to list, and tokenless, the filter diffs the
  event's `base.sha` with git. (`env.ACT && '' || github.token` would not
  work: `''` is falsy, so it always yields the token.)
- **Artifacts**: act 0.2.89 rejects `upload-artifact@v7` and
  `download-artifact@v8` (nektos/act#6022). The two uploads and the one
  download skip under act, and one act-only step prints the gate, the test
  counts, the per-assembly summary, and the Cobertura totals to the log, where
  the wrapper finds them. Remove all four when a pinned act release accepts
  those versions.

## Rejected

- **[wrkflw](https://github.com/bahdotsh/wrkflw)**: it emulates
  `setup-dotnet` from `dotnet-version` only (defaulting to 7.0, so the SDK 10
  build fails), mounts no Docker socket (no Testcontainers), flattens the event
  so `pull_request.body` is null, runs dependents of a failed job, and
  hard-codes `ubuntu:latest`. Each is a blocker here.
- **Keeping a local re-implementation**: two already disagreed with CI.
- **The catthehacker `full` image**: tens of gigabytes, and not native on
  arm64.
- **act built from an unmerged artifact fix**: an unpinned binary, run with a
  token.

## Consequences

- One implementation of the ratchet: `tools/coverage-check.sh` and
  `check-coverage.sh` are deleted.
- Local green is necessary, not sufficient. GitHub stays the authority; the
  ruleset, `concurrency`, `environment:` protection, and the bot workflows do
  not run locally.
- A full run holds a per-machine lock, because port 4173 and the Docker VM are
  shared.
- Upgrading act is a pull request that changes `.act-version`;
  `init-dev.sh --check` reports a mismatch.
