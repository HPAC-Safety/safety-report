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

**Status:** Accepted. Relates to [ADR-0014](ADR-0014-coverage-gate.md) and
[ADR-0073](ADR-0073-a-ui-scenario-is-skipped-by-reqnroll-itself.md);
supersedes the script
[lesson 0010](../lessons/0010-a-coverage-gate-found-in-ci-not-before-the-pull-request.md)
added. ADR-0014's gate is unchanged: this runs it before the pull request, as
CI does. ADR-0073 still holds: running CI's flags locally does not make a
guard that lives only in a CI flag a real guard.

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
  `HPAC_ACT_TOKEN`, a fine-grained read-only token for this repository
  (Actions, Contents, Metadata: read). act does not enforce a workflow's
  `permissions:`, so every job and third-party action receives the token as
  is. Without `HPAC_ACT_TOKEN` the wrapper exits 2; the developer's
  full-scope `gh auth token` is used only on an explicit `--allow-gh-token`
  (or `HPAC_ACT_ALLOW_GH_TOKEN=1`), with a warning.
- **Other inputs**: the wrapper passes `--secret-file`, `--var-file`, and
  `--env-file /dev/null` on act's command line, over `.actrc` and any
  user-level actrc (`~/.actrc`, `$XDG_CONFIG_HOME/act/actrc`), and warns when
  a user-level actrc exists, because act still merges its other flags.
  `.secrets`, `.vars`, and `.actrc.local` are gitignored.
- **The image**: `tools/act/Dockerfile`, built locally and never pushed.
  - Base: `catthehacker/ubuntu:act-24.04`, pinned by digest. It lacks `gh`
    (without it the coverage job's baseline step silently skips the ratchet)
    and `shellcheck`.
  - `gh`: GitHub's release tarball, pinned by version (2.101.0) and SHA-256
    per architecture. Ubuntu's `gh` 2.45.0 lacks the `gh run download`
    path-traversal fix (CVE-2024-54132, fixed in 2.63.1), which matters
    because the coverage job runs that command; and an exact pin in the
    -updates pocket stops resolving after the next update.
  - `shellcheck` 0.9.0-1 from noble's release pocket, which never changes,
    and is the version GitHub's runner ships.
  - On arm64, the x86-64 loader, libc, and libgcc_s, copied from a
    digest-pinned `ubuntu:24.04`.
  - Tagged `hpac-safety-act:<hash of the Dockerfile>`, so a changed Dockerfile
    builds a new image rather than reusing a stale one; the wrapper maps
    `ubuntu-latest` to that tag.
  - Native on each host: arm64 on Apple Silicon, never amd64 emulation for the
    .NET jobs. Docker Desktop runs the workflows' linux-amd64 downloads
    (terraform, tflint, skillfile) through Rosetta.
- **The checkout**: act runs in a throwaway clone of `HEAD`, because a
  worktree's `.git` is a file that points outside the copy act makes. The
  clone is made from a bundle of `HEAD` alone, so no other branch's refs come
  along (CI's checkout has none; a tool whose tests read the repository's
  refs measured differently with them), and its `origin/main` and
  `origin/<branch>` refs are set to the fetched base and `HEAD`.
- **Coverage parity**: the `coverage` job runs as on GitHub, on Ubuntu 24.04
  with the exact SDK, and downloads the same artifact from main's last green
  run. Lesson 0010's same-machine baseline is dropped: it existed only because
  the branch was measured on macOS. The act-only coverage step lists each
  per-project Cobertura report and the assemblies it carries, and the wrapper
  fails a run whose report count is not the number of test projects: a lost
  report can move the verdict either way.
- **Exit codes**: 0 passed, 1 a job failed, 2 a precondition or setup step
  failed, 3 the lock timed out. The lock records its holder's pid and start
  time, reports whether that pid is alive, and is never cleared by another
  run.
- **Testcontainers** keep Ryuk on
  ([lesson 0008](../lessons/0008-containers-outlive-the-worktree-that-started-them.md)).
  act puts each job on the Docker VM's host network. Under Docker Desktop a
  published port reaches that network about two seconds after the container
  starts, and Testcontainers connects before then, so Ryuk's handshake is
  refused. The wrapper detects Docker Desktop (`docker info` reports it as the
  operating system) and only then passes
  `TESTCONTAINERS_HOST_OVERRIDE=host.docker.internal`, which answers at once.
  A native Linux engine gets nothing.

### The `env.ACT` carve-outs in `ci.yml`

act sets `ACT=true`; on GitHub `env.ACT` is empty, so each carve-out is a
no-op there.

- **paths-filter** (permanent):
  `token: ${{ !env.ACT && github.token || '' }}`. No pull request exists for
  the API to list; tokenless, the filter diffs the event's `base.sha` with
  git, so no `base:` input is needed. The form first proposed,
  `env.ACT && '' || github.token`, always yields the token, because `''` is
  falsy.
- **Artifacts** (temporary): act 0.2.89 rejects `upload-artifact@v7` and
  `download-artifact@v8` (nektos/act#6022). The two uploads and the one
  download skip under act, and one act-only step prints the gate, the test
  counts, the per-assembly summary, and the Cobertura totals to the log, where
  the wrapper finds them. Each carries a comment citing nektos/act#6022.
  **Remove all four when act ships the fix**, in the pull request that moves
  `.act-version` to that release.

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
