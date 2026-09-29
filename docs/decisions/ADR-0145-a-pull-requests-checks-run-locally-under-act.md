---
title: A pull request's checks run locally under act, against CI's own baseline
description: tools/ci-local.sh runs the pull request workflows under nektos/act in a pinned local image, with a synthetic event that takes every fork branch, so the coverage ratchet and the body checks give CI's verdict before the pull request is opened.
type: adr
status: accepted
date: 2026-09-26
decision-makers: Chase Florell
keywords: act, local CI, coverage gate, ratchet, pull request checks, Testcontainers, Docker Desktop, GitHub Actions, no token, gh login, traceability matrix, pending translation, coverage parity, parallel runs, no lock, E2E_PORT, container names
---

# ADR-0145 — A pull request's checks run locally under act, against CI's own baseline

**Status:** Accepted. Relates to [ADR-0014](ADR-0014-coverage-gate.md) and
[ADR-0073](ADR-0073-a-ui-scenario-is-skipped-by-reqnroll-itself.md);
supersedes the script
[lesson 0010](../lessons/0010-a-coverage-gate-found-in-ci-not-before-the-pull-request.md)
added. ADR-0014's gate is unchanged: this runs it before the pull request, as
CI does. ADR-0073 still holds: running CI's flags locally does not make a
guard that lives only in a CI flag a real guard.

**Amended 2026-09-26 (#554):** act receives no token. The `HPAC_ACT_TOKEN`
fine-grained token and `--allow-gh-token` are removed; the coverage baseline
is downloaded on the host with the developer's `gh` login. See "The token"
and "The baseline" below.

**Amended 2026-09-27 (#546):** the wrapper stands in for the two bot commits
CI sees on a same-repository pull request (the regenerated matrix, and the
French under act's `i18n` job), and the coverage merge reads only the
per-project attachment copies. See "The bots' commits" and "Coverage parity".

**Amended 2026-09-29 (#675):** the per-machine lock is removed. Runs from
different worktrees proceed in parallel, even at the same time, kept apart by
per-run ports and container names instead of by queueing. See "The lock is
removed" below, which replaces "Exit codes"' lock clause and the
"Testcontainers" and "Consequences" sections' mentions of it.

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
- **The token** (amended, #554): act receives none. The repository is public,
  and measured on 2026-09-26, listing runs and artifacts, the setup actions,
  `gh release download`, and skillfile's fetch of public skills all work
  anonymously, within the 60-an-hour per-IP quota.
  - act fills a missing `GITHUB_TOKEN` secret from `gh auth token` (act 0.2.89,
    `cmd/root.go`), which would hand every job and action the developer's
    full-scope login. So the wrapper passes `-s GITHUB_TOKEN=`, explicitly
    empty, and unsets `GITHUB_TOKEN` and `GH_TOKEN` in act's environment.
  - The coverage job's act-only baseline step prints whether `github.token` is
    empty, and the wrapper fails a run that does not report it empty.
  - A job that hits the anonymous rate limit fails; the wrapper says so and
    suggests waiting. It never falls back to giving act a token.
  - This replaces `HPAC_ACT_TOKEN`, a fine-grained read-only token each
    developer had to create, and `--allow-gh-token`: a token inside act
    reaches every job and third-party action, because act does not enforce a
    workflow's `permissions:`.
- **The baseline** (#554): GitHub answers an anonymous artifact download with
  401, even on a public repository, and the ratchet needs main's last green
  `coverage-report`.
  - Before act starts, the wrapper finds the run CI's "Fetch the main
    baseline" step picks (the last successful push run of `CI` on `main`) and
    downloads its `Cobertura.xml` on the host with the developer's `gh` login.
    It prints the run ID; the job prints it again.
  - The file goes into the clone's `.ci-local/baseline/`, gitignored, so the
    jobs' `git status` checks never see it; act runs with
    `--use-gitignore=false` so it is copied. The coverage job's act-only step
    reads it, and its `gh` download step skips under act.
  - No green run on `main`, or none carrying the artifact: the floor alone, as
    in CI.
  - **Without a `gh` login**, a run that includes `coverage` exits 2 before act
    starts, naming `gh auth login`. A floor-only pass with a notice was
    rejected: it is a local verdict CI would not give, the disagreement this
    ADR exists to remove. A run of other jobs alone (`--job linked-issue`)
    needs no login.
- **Other inputs**: the wrapper passes `--secret-file`, `--var-file`, and
  `--env-file /dev/null` on act's command line, over `.actrc` and any
  user-level actrc (`~/.actrc`, `$XDG_CONFIG_HOME/act/actrc`,
  `~/Library/Application Support/act/actrc`), and warns when one exists,
  because act still merges its other flags.
- **Action cache**: `--use-new-action-cache`. With act's default cache,
  parallel jobs using the same action re-checked-out one shared working tree,
  and a job intermittently found `setup-node@v7`'s `dist/cache-save/index.js`
  missing.
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
- **The bots' commits** (#546): on a same-repository pull request,
  `traceability.yml` commits the regenerated matrix and `i18n-translate.yml`
  the French before CI's verdict settles. Neither runs locally, so a branch
  that changed a scenario failed `docs`, and one that added an English key
  failed `i18n`, although CI passed. Neither stand-in changes what GitHub
  runs.
  - **The matrix**: the wrapper runs `node tools/traceability.mjs` in the clone
    and, when the matrix changed, commits it there and moves `HEAD`,
    `origin/<branch>`, and the event's `head.sha` to that commit: the commit
    `traceability.yml` would push. Like that workflow, it skips a branch that
    changes the generator, whose author regenerates by hand. A failing
    generator leaves the clone alone, so the `docs` job reports why.
  - **The French**: it needs a translation provider, which nothing local may
    call. So `ci.yml`'s check runs
    `translate-locale.mjs --check ${ACT:+--allow-pending-translation}`: under
    act, French still pending as a `#` stub, or English reworded since it was
    translated, is a notice. That is the pre-commit hook's rule on a branch.
    `ACT` is unset on GitHub, so the flag never reaches CI, where a stub still
    cannot merge.
  - Rejected: patching the workflow in the clone, which would run a copy of
    CI rather than CI; a wrapper flag that skips `docs` and `i18n`, which
    would hide every other failure in those jobs; and adding
    `--allow-pending-translation` to `translate-locale.mjs`'s own defaults
    under `ACT`, which puts a CI-runner rule in a tool that also runs in the
    hook and on `main`.
- **Coverage parity**: the `coverage` job runs as on GitHub, on Ubuntu 24.04
  with the exact SDK, and downloads the same artifact from main's last green
  run. Lesson 0010's same-machine baseline is dropped: it existed only because
  the branch was measured on macOS. The act-only coverage step lists each
  per-project Cobertura report and the assemblies it carries, and the wrapper
  fails a run whose report count is not the number of test projects: a lost
  report can move the verdict either way.
  - **One report per project** (#546): vstest copies each project's report
    into a per-run `<machine>_<timestamp>/In/` directory named to the second,
    besides its GUID attachment directory. Two projects finishing in the same
    second share the `In/` directory, and vstest names the second copy
    `coverage.cobertura[1].xml`, which the old `**/coverage.cobertura.xml`
    glob did not match. So the merge read 11 or 12 files. No coverage was
    lost, because every `In/` copy duplicates an attachment. The merge now
    reads `./artifacts/coverage/*/coverage.cobertura.xml`, the attachments
    alone, on GitHub too.
  - **Measured differences settled** (#546): #545, #553, #548, and #555
    measured local and CI totals for the same code, within 0.05 point, with
    equal gate verdicts. Every difference traced to one of three causes, and
    each is now pinned:
    - `BlobKey`'s report-id alphabet check: suites use random ids, so whether
      a run reached `-` or `_` varied (93.7% to 96.8% branch coverage across
      CI runs of identical code). A test now covers each character class and
      each boundary.
    - `PrivateNoteEndpoints`' commit-time conflict: two lines reached only
      when a racing edit met the unique index rather than the in-memory
      check. A test now holds two edits at `SaveChanges` until both have
      loaded.
    - `tools/adr-numbers.mjs`'s unreadable-file path: its test skipped as
      root, and act runs jobs as root. The test now uses a directory in place
      of a tracked file, which no user can read as a file.
- **Memory**: a full run peaked at about 5 GiB of container memory
  (`docker stats` summed every 5 s over a 694 s run, Docker Desktop VM of
  7.75 GB), because `build`, `test`, `e2e`, and then `coverage` run side by
  side with their Testcontainers. The recommendation is at least 7 GB for
  Docker; `init-dev.sh` reports the VM's size against it.
- **Installing act**: `init-dev.sh` downloads the release asset for the OS
  and architecture at `.act-version`, checks its SHA-256 against
  `.act-checksums`, and installs it to `~/.local/bin`, again whenever the act
  on `PATH` is another version. No package manager installs an exact act.
- **Exit codes**: 0 passed, 1 a job failed, 2 a precondition or setup step
  failed.
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
- **Pending French** (permanent, #546): the `i18n` job's translation check
  adds `--allow-pending-translation` when `ACT` is set, through the shell's
  `${ACT:+…}`. See "The bots' commits".
- **Artifacts** (temporary): act 0.2.89 rejects `upload-artifact@v7` and
  `download-artifact@v8` (nektos/act#6022). The two uploads and the one
  download skip under act, and one act-only step prints the gate, the test
  counts, the per-assembly summary, and the Cobertura totals to the log, where
  the wrapper finds them. Each carries a comment citing nektos/act#6022.
  **Remove all four when act ships the fix**, in the pull request that moves
  `.act-version` to that release.

## The lock is removed (#675)

A full run held a per-machine `mkdir`-based lock, so only one ran at a time on
a given machine, because port 4173 and the Docker VM were shared. With several
agents working from different worktrees, runs queued for up to an hour behind
each other, and a run killed while holding the lock blocked every other run
until someone removed it by hand — which happened on 2026-09-29 (#666, #674).

**Decision: there is no lock.** Every run is independent, even if that means
several Docker containers at once; the owner accepts the resource cost. What
the lock protected is now kept apart per run instead of serialized:

- **Port 4173.** act puts jobs on the Docker VM's host network, so two
  concurrent `e2e` jobs would otherwise collide on Vite's fixed preview port,
  or one would test the other's build. `tests/e2e/playwright.config.ts` now
  uses `E2E_PORT` when a caller sets it, or else picks one free port,
  synchronously, the first time the file is evaluated — the Playwright main
  process, before it forks test workers. Workers re-evaluate the same file but
  inherit `process.env` from the process that forked them, so `E2E_PORT` is
  already set by the time they read it; a port picked per evaluation would
  instead differ between workers, which would serve and test against
  different builds. Both `webServer` and `baseURL` read the same value, and
  `tests/e2e/steps/locale.steps.ts`'s hostname navigations do too. `ci-local.sh`
  needs nothing extra: it never named 4173 itself. GitHub CI may still land on
  a shared default port, since nothing else runs on its runner.
- **act's job container names.** act names a job container deterministically
  from the workflow's top-level `name:` and the job's own name (act 0.2.89,
  `pkg/runner/run_context.go`: `jobContainerName` hashes
  `"<workflow name>/<job name>"`), so two concurrent runs of the same job
  hashed to the same container name and either collided or removed each
  other's container mid-run. `ci-local.sh` now writes a retagged copy of each
  workflow file — its `name:` line suffixed with this run's pid and the clone
  directory's random suffix — to a directory *outside* the clone, and points
  act's `-W` at that copy instead of the clone's own `.github/workflows/`.
  act's `-W` accepts a file anywhere; the directory act is invoked from (the
  clone) is a separate concept from the file `-W` names, and still supplies
  the checkout act copies into each job container, so nothing else about what
  runs changes (verified: `act -l -W <external file>` reports the retagged
  name, and a full run against an external `-W` succeeds with the tag in its
  container name). The clone's git history is never touched.
  - **An earlier version of this fix committed the retag onto the clone's
    HEAD instead**, and was caught in review before merging: `linked-issue.yml`
    (`git diff --name-only "$BASE_SHA"...HEAD`), `feature-coverage.yml`
    (`check "$BASE_SHA...HEAD"`), and `ci.yml`'s `changes` job (dorny's
    `paths-filter`, reading `pull_request.base.sha`) all diff with `...`
    (from the merge-base), not a plain two-dot diff. Committing the retag onto
    HEAD alone left `merge-base(origin/main, HEAD)` at the untagged upstream
    commit, so every one of those diffs still saw all four workflow files as
    changed on every run, regardless of what the pull request touched — a
    manifest-only or documentation-only change would have failed
    `feature-coverage` or `linked-issue` locally while GitHub passed it. A
    second attempt mirrored the same retag onto a synthetic copy of the base
    commit, built with plumbing and never checked out, so a plain two-dot diff
    against that synthetic base showed nothing extra — but `merge-base` finds
    the *real* upstream commit as the common ancestor regardless, since the
    synthetic base is the real base's child and HEAD does not descend from it,
    so the same `...` diffs were unaffected. Writing the retagged copies
    outside the clone and leaving `BASE_SHA` and `HEAD_SHA` exactly as they
    were removes the problem instead of working around it: nothing in the
    clone's history differs from the real branch, so every diff any job or
    filter computes — two-dot or three-dot — is the one CI would compute.
- **Everything else audited and found already safe**: no workflow run by
  `ci-local.sh` declares a `services:` container or another fixed port; none
  uses `actions/cache`, so its random-by-default cache-server port is never
  exercised; the artifact server never starts, because the wrapper passes it
  no path, and the four `upload-artifact`/`download-artifact` steps already
  skip under act 0.2.89 regardless (see "Artifacts" above); Testcontainers
  already binds random host ports; `mktemp`'s random suffix keeps each run's
  clone under its own `$TMPDIR` path; and the Dockerfile-hash image tag is
  content-addressed, so two concurrent identical builds can't clobber each
  other — only the unversioned `:local` convenience alias can be overwritten
  by a concurrent run with a different Dockerfile hash, and nothing in
  `ci-local.sh` reads that alias back, so a stale one only misleads a
  hand-run `act`, never this script's own verdict. act's own action cache
  (`--action-cache-path`, default `~/.cache/act`) stays shared machine-wide, a
  known, accepted residual: it is a read-mostly cache keyed by action and ref,
  and none of this repository's workflows contend on a single action that
  isn't already cached from an earlier run.
- `dev-up.sh` and the dev compose project are unchanged: they keep their
  fixed ports on purpose, so the app is always at the same address in
  development. This ADR's lock existed only for `ci-local.sh`'s test runs.

**Proof**: two full `tools/ci-local.sh --body pr-body.md` runs, from two
separate worktrees on the same branch, started together, both passed with
correct verdicts, and neither tested the other's build (logs in the pull
request that made this change).

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
- No lock (#675): runs from different worktrees proceed in parallel, even at
  the same time, kept apart by per-run ports and container names. See "The
  lock is removed".
- Upgrading act is a pull request that changes `.act-version`;
  `init-dev.sh --check` reports a mismatch.
