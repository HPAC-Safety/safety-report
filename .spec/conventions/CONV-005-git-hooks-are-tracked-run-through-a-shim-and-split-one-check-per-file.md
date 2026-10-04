---
title: Git hooks are tracked, run through a shim, and split one check per file
description: Every git hook is a tracked file under .githooks run by the shim init-dev.sh installs, never through core.hooksPath or a hook manager such as Husky; each pre-commit check is its own file under .githooks/pre-commit.d.
type: convention
status: accepted
date: 2026-10-03
---

# CONV-005 — Git hooks are tracked, run through a shim, and split one check per file

## Rule

- A git hook is a tracked file, `.githooks/<hook name>`. `./init-dev.sh`
  installs `tools/dev/git-hook-shim.sh` under each name in its hook loop, into
  `$(git rev-parse --git-path hooks)`; the shim runs the current worktree's
  `.githooks/<name>`. Edit the tracked file, never the installed one; a
  changed hook or a moved tool needs no re-install.
- Never set `core.hooksPath`.
- graphify owns `post-checkout` and `post-commit`; `graphify hook install`
  writes them. Never add a `.githooks/` file of either name.
- A new hook name is a new `.githooks/<name>` and a new name in
  `init-dev.sh`'s hook loop, which `./init-dev.sh --check` then audits.
- A new pre-commit check is a new file, `.githooks/pre-commit.d/NN-<check>.sh`,
  numbered for where it must run, never a block in `.githooks/pre-commit`.
  The check file:
  - opens with a header comment saying what it guards, which ADR or
    convention, and which CI job is its backstop;
  - gates itself on the staged paths that wake it, reading `$STAGED` (the
    added, copied, modified, or renamed paths, one per line) or asking git
    for another `--diff-filter`;
  - says so when a tool it needs is missing, unless it is advisory without
    it, in which case its header says it is skipped;
  - exits non-zero to fail.
- `.githooks/pre-commit` stays a runner: it reads the staged files and the
  branch (`$BRANCH`, empty on a detached HEAD) once, runs every
  `pre-commit.d/*.sh` in lexical order with `sh`, and fails at the end,
  naming each check that failed. One failing check never skips another.
- Order matters where a check re-stages: the locale stub re-stages before the
  parity check reads the index, and `dotnet format` runs last.
- A script two hooks share lives under `.githooks/lib/`, sourced, never
  installed: `post-merge` and `post-rewrite` share
  `lib/regenerate-spec.sh`, and each keeps its own skip-on-`main` (#802) and
  rewrite-type logic.
- The agent-tooling install is the second thing a hook runs on `main`, after
  the graph merge. `post-merge` and `post-rewrite` share
  `lib/install-agent-tooling.sh` with `init-dev.sh`:
  - it wakes only when `git diff --name-only ORIG_HEAD HEAD` touches
    `Skillfile`, `Skillfile.lock`, `agents/`, or `skills/`, and is silent when
    `skillfile` is not on `PATH`;
  - it runs `skillfile install`, then deletes every `.claude/agents/*.md` and
    `.claude/skills/<dir>` that `skillfile list --names-only` does not name;
  - it is allowed on `main` because its output under `.claude/` is gitignored
    (#849), like the graph merge. `skillfile install` can rewrite the tracked
    `Skillfile.lock`, so on `main` the script restores it, and elsewhere it
    says to include it in the next commit.
- No hook manager: not Husky, lint-staged, lefthook, or the pre-commit
  framework.

## Why

The owner asked whether to adopt Husky to keep the hooks maintainable, and
decided against it in
[#818](https://github.com/HPAC-Safety/safety-report/issues/818). The cost was
the shape of a ~270-line `pre-commit` holding a dozen checks, not how hooks are
installed.

Husky and lint-staged were considered and rejected:

- **Husky works by setting `core.hooksPath`** (to `.husky/_`). That repoints
  git at one directory for every hook, which would silently stop graphify's
  `post-checkout` and `post-commit`, written straight into
  `$(git rev-parse --git-path hooks)`.
- **Husky installs from an npm `prepare` script.** This repository is .NET
  first, and the root `package.json` is lint tooling only: a contributor who
  never ran `npm ci` at the root would get no hooks. `./init-dev.sh` already
  installs them for every contributor, and `./init-dev.sh --check` audits
  them.
- **The shim already gives what Husky offers**: tracked hooks, no re-install
  when one changes, per-worktree hooks (#796), and a missing hook never
  blocking a commit. lint-staged's staged-file gating is what each check file
  does by hand.
- Either would add a dependency and a second convention, and remove no
  problem.

Splitting the checks one per file puts each check's reason next to its code,
and keeps the "every check runs" rule in one place: a single script's
`set -e` once let a locale failure skip `dotnet format` (#207).

## Enforced by

- `tests/js/dev/pre-commit-runner.test.ts`: every check file is numbered,
  executable, and opens with a header; the runner holds no check of its own,
  runs every file in order, and still runs later checks after one fails.
- `tests/js/dev/git-hook-shim.test.ts`: the shim runs the worktree's tracked
  hook and nothing under a check directory, and `init-dev.sh` installs it
  under the four hook names only.
- `./init-dev.sh --check` reports any hook that is not the shim.
- CI's `build` job shellchecks the runner, every check file, `post-merge`,
  `post-rewrite`, and `.githooks/lib/*.sh`.
- `tests/js/dev/install-agent-tooling.test.ts`: the wake gate, the prune, and
  the `Skillfile.lock` guard, with a fake `skillfile` on `PATH`.
- Never setting `core.hooksPath` and adopting no hook manager are written,
  not checked.
