---
title: "A workflow step runs one command, and tools/ is grouped by domain"
description: "Every run: in .github/ is one command; logic lives in a tested Node script under tools/<group>/ with its test under tests/js/<group>/. tools/ is grouped by the domain each script serves, and entry points are named check-, generate-, guard-, build-, find-/read-, or report-. A check enforces the rule."
type: adr
status: accepted
date: 2026-10-02
decision-makers: Chase Florell
keywords: GitHub Actions, workflow, run step, inline shell, tools, scripts, node:test, naming convention, grouping, check-workflow-steps, lesson 0001, ADR-0039, ADR-0052, ADR-0090, ADR-0101, ADR-0113, ADR-0145, ADR-0147, ADR-0183
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
   `tools/lib/actions.mjs`. Its test is `tests/js/<group>/<name>.test.mjs`.
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
5. **`tools/github/check-workflow-steps.mjs` enforces rule 1** in the
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
- Git hooks are copies installed by `init-dev.sh` into the shared hooks
  directory; they read the new paths once re-installed, and a worktree still on
  the old layout needs a rebase.

## Alternatives considered

- **A new `.github/scripts/` directory.** Rejected: it splits CI logic across two
  homes and re-wires test discovery and coverage that `tools/` already has.
- **Bash scripts tested with bats.** Rejected: a new dependency, a second test
  runner, and no line in the coverage ratchet.
- **Extract only the logic-heavy blocks.** Rejected by the owner: a threshold
  invites drift, and a check needs a crisp rule.
