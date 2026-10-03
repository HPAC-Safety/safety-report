---
title: "A workflow step runs one command, and tools/ is grouped by domain"
description: "Every run: in .github/ is one command; logic lives in a tested Node script under tools/<group>/ with its test under tests/js/<group>/. tools/ is grouped by the domain each script serves, and entry points are named check-, generate-, guard-, build-, find-/read-, or report-. A check enforces the rule."
type: adr
status: accepted
date: 2026-10-02
decision-makers: Chase Florell
keywords: type stripping, TypeScript, erasableSyntaxOnly, tsc, GitHub Actions, workflow, run step, inline shell, tools, scripts, node:test, naming convention, grouping, check-workflow-steps, lesson 0001, ADR-0039, ADR-0052, ADR-0090, ADR-0101, ADR-0113, ADR-0145, ADR-0147, ADR-0183
---

# ADR-0189 — A workflow step runs one command, and `tools/` is grouped by domain

**Status:** Accepted. Decided by the owner on 2026-10-02 in
[#778](https://github.com/HPAC-Safety/safety-report/issues/778). The workflow
counterpart of [ADR-0052](ADR-0052-no-inline-script-typescript-only.md) (no
inline script in HTML); generalizes what
[ADR-0090](ADR-0090-an-exemption-cites-the-claims-it-preserves.md) did for one
gate ("the judgement lives in a tested tool rather than in inline workflow
shell"); enforced as code per
[lesson 0001](../lessons/0001-a-guard-that-lives-only-in-ci-is-not-a-guard.md).
Supersedes nothing.

## Context

The workflows held about 45 multi-line `run: |` blocks — branching, loops,
`jq`/`sed` parsing, retries, `gh`/`git`/`aws` sequences. None was unit-tested;
each could be exercised only by running its workflow or replaying it under act
([ADR-0145](ADR-0145-a-pull-requests-checks-run-locally-under-act.md)). The rest
of the CI logic already followed a better pattern: a script under `tools/`, a
`node:test` file under `tests/js/`, run by `ci.yml` with its coverage in the
ratchet. `tools/` itself had grown into a flat list of about 35 scripts with
mixed names.

## Decision

1. **A `run:` is one command.** It may continue across lines with a trailing
   backslash and may use a pipe, a redirect, or `$(...)`. It holds no second
   command line, no shell control flow (`if`, `for`, `while`, `until`, `case`,
   `function`), and no command list (`&&`, `||`, `;`). Plain sequential commands
   become consecutive steps; anything with logic becomes a script.
2. **Logic lives in a Node script under `tools/<group>/`**, dependency-free,
   with pure exported functions, a `main()` that returns an exit code, and its
   effects (`exec`, `env`, outputs, summary) injectable through
   `tools/lib/actions.ts`. Its test is `tests/js/<group>/<name>.test.ts`.
   Inputs reach it through the step's `env:`, never `${{ }}` interpolated into
   script text.
3. **`tools/` is grouped by the domain a script serves:** `lib/`, `spec/`,
   `docs/`, `web/`, `i18n/`, `coverage/`, `build/`, `github/`, `infra/`, `dev/`,
   and `gherkin/`. Tests mirror the groups.
4. **An entry point's name says what it does:** `check-*` is a read-only gate
   that exits 1 on failure; `generate-*` writes a tracked generated file;
   `guard-*` is a hook that refuses an action; `build-*` produces an artifact;
   `find-*`/`read-*` hands a value to a later step; `report-*` writes summary or
   comment markdown; any other action is verb-object. A noun name
   (`translator`, `spec-paths`, `adr-numbers`) is an importable module or a
   multi-mode command. [`tools/README.md`](../../tools/README.md) lists the
   groups.
5. **`tools/github/check-workflow-steps.ts` enforces rule 1** in the
   pre-commit hook and the `docs` job.

What the extraction keeps unchanged: path gates include each workflow's scripts
([ADR-0039](ADR-0039-path-gated-required-checks.md)); the bot workflows still run
only trusted base-branch code with write credentials
([ADR-0101](ADR-0101-ci-regenerates-the-traceability-matrix.md),
[ADR-0113](ADR-0113-a-bot-pushing-onto-a-pull-request-replays-past-another-bot.md));
merge-group checks still judge each queued commit
([ADR-0147](ADR-0147-pull-requests-merge-through-a-merge-queue.md)).

## Consequences

- Every workflow decision is unit-tested and counted by the coverage ratchet.
- A workflow reads as wiring: inputs, conditions, and one command per step.
- The move edited historical ADR and lesson text only where it named a moved
  path, as [ADR-0183](ADR-0183-the-specification-lives-in-a-spec-directory.md)
  did, so every reference resolves.
- Git hooks were copies installed by `init-dev.sh` into the shared hooks
  directory, so they read the new paths only once re-installed, and a worktree
  still on the old layout needed a rebase. The last amendment below replaces the
  copies with a shim.

## Considered options

- **A new `.github/scripts/` directory.** Rejected: it splits CI logic across two
  homes and re-wires test discovery and coverage that `tools/` already has.
- **Bash scripts tested with bats.** Rejected: a new dependency, a second test
  runner, and no line in the coverage ratchet.
- **Extract only the logic-heavy blocks.** Rejected by the owner: a threshold
  invites drift, and a check needs a crisp rule.

## Amendment (2026-10-03) — tools and tests are TypeScript, run by type stripping ([#798](https://github.com/HPAC-Safety/safety-report/issues/798))

Applies on the date of the pull request that closes #798. It replaces
"Node script" and `.mjs` in decisions 2 and 4 above; everything else stands.

1. **Every script under `tools/` and every test under `tests/js/` is a `.ts`
   file** that Node runs directly: no build step, no flag, no `tsx`. Node 24
   strips the types (unflagged since 22.18); CI pins 24 with `setup-node`, and
   the act runner image carries 24.19. `node tools/<group>/<name>.ts` is the
   command a workflow step, a hook, or `ci-local.sh` runs.
2. **Only erasable syntax.** Type stripping replaces annotations with blanks
   and cannot run `enum`, `namespace`, or a constructor parameter property.
   `tsconfig.json` sets `erasableSyntaxOnly` (so tsc refuses them),
   `verbatimModuleSyntax` (a type is imported with `import type`, which stripping
   needs), and `allowImportingTsExtensions` with `noEmit` (a sibling is imported
   with its `.ts` extension, as Node resolves it).
3. **Node does not check types, so something else must.** `npm run typecheck`
   (`tsc -p tsconfig.json` over `tools` and `tests/js`) runs in CI's `lint` job,
   one command per step; typescript-eslint's `strict-type-checked` preset runs
   over the same files through the root `tsconfig.json` (ADR-0188 amendment).
   Both need the Gherkin parser's types, so the `lint` job and pre-commit also
   install `tools/gherkin` (`npm --prefix tools/gherkin ci`).
4. **A job that runs a script has Node 24.** Five jobs (`ci.yml` `build` and
   `agent-config`, `terraform.yml` `infra` and `plan`, `terraform-relock.yml`,
   `deploy-environment.yml` `deploy`) relied on the runner's preinstalled Node,
   which cannot strip types; each gains a `setup-node` step. The deploy job's
   sparse checkout of `tools` still carries the root `package.json`
   (`"type": "module"`), because cone mode keeps root files.
5. **`eslint.config.mjs` stays JavaScript.** ESLint 9 loads a TypeScript config
   only through `jiti` or an unstable Node flag; a config is the one file that
   cannot be linted by a tool it has not loaded yet. It is the only `.mjs` in
   the repository, and `tools/gherkin/` keeps its own `package.json`.
6. **Coverage is unchanged.** `node --test --experimental-test-coverage` reads
   the stripped files; stripping keeps line numbers, so the lcov names the same
   `tools/**/*.ts` and `tests/js/**/*.ts` paths the ratchet merges.
   `run-js-tests` finds `*.test.ts`.

Consequences: a script's signature is its documentation, and a fake in a test
must satisfy the same `Exec` and `Env` types as the real thing. A script
cannot use `enum` or `namespace`; the check is `npm run typecheck`, not review.
The move edited historical ADR and lesson text only where it named a moved path.

## Amendment (2026-10-03) — the installed hooks are a shim ([#796](https://github.com/HPAC-Safety/safety-report/issues/796))

The move above left every installed hook calling the old flat `tools/*.mjs`
paths until `./init-dev.sh` was run again. A copy goes stale whenever anything
it calls moves, and nothing says so.

- **`./init-dev.sh` installs `tools/dev/git-hook-shim.sh` under each hook name**
  (`pre-commit`, `commit-msg`, `post-merge`, `post-rewrite`), instead of a copy
  of `.githooks/<name>`.
- **The shim holds no logic.** It runs `.githooks/<its own name>` from the
  worktree git is working in (`git rev-parse --show-toplevel`), with the hook's
  arguments, standard input and exit status. A tool that moves, or a hook that
  changes, is therefore live in every worktree with no re-install, and a
  worktree runs its own branch's hooks, not the main checkout's.
- **A missing tracked hook runs nothing and succeeds**, so a branch from before
  a hook existed is never blocked. The tracked file need not be executable: the
  shim runs it with `sh`.
- **It is tested** (`tests/js/dev/git-hook-shim.test.ts`): a real repository
  and a real worktree commit through it. `init-dev.sh` replaces a full copy
  installed earlier, because it compares each installed hook with the shim.
- **Where the shim cannot help:** a repository whose installed hooks are still
  the old copies until `./init-dev.sh` runs once. That one run is the last
  re-install a move will need.
