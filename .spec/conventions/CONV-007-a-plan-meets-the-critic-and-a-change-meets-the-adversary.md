---
title: A plan meets the critic and a change meets the adversary
description: A plan is challenged by the critic, one pass and at most one recheck, before it is final; a change is attacked by the adversary at contract boundaries, after repeated test failures, and before any pull request or merge; the hook only reminds; the auditor runs on demand, never per change, and only reports.
type: convention
status: accepted
date: 2026-10-04
---

# CONV-007 — A plan meets the critic and a change meets the adversary

## Rule

- **The critic runs on a plan before it is final**: a plan-mode plan before
  `ExitPlanMode`, and an issue or epic plan before `gh issue create`.
  - **One pass, then at most one recheck.** The author revises, or rebuts each
    finding in writing. The recheck covers only the prior findings and raises
    no new one.
  - The plan is then final. A finding still open goes to the owner; there is
    no third round.
  - The critic is read-only and judges conflicts with the specification,
    accepted ADRs, product invariants, and out-of-scope lines, and a simpler
    alternative.
- **The adversary runs on a change**:
  - at a contract boundary (an interface, schema, wire format, or persisted
    shape the change alters);
  - after repeated test failures;
  - before any pull request or merge.
  - It is read-only and reports one line per finding:
    `path:line, severity, problem, fix`. The author fixes; the adversary never
    does.
- **The auditor runs on demand, never per change.** It audits the whole
  repository at rest for drift no single diff caught, is read-only, reports in
  the adversary's finding format, and files nothing; the owner decides which
  findings become issues
  ([#853](https://github.com/HPAC-Safety/safety-report/issues/853)).
- **The hook only reminds.** A `PreToolUse` hook on `ExitPlanMode` and on a
  Bash `gh issue create` adds a reminder to run the critic, and never blocks.
  The adversary and the auditor have no hook.
- Each agent owns its own artifact: the critic judges a plan before it is
  built, the adversary attacks a change for bugs, security, privacy leaks,
  contract violations, and missing tests, `spec-reviewer` judges the diff
  after, against claims and ADRs, and `auditor` judges the whole repository
  between changes. `backend`, `ux`, and `infrastructure`
  design and build, and still cite claims.

## Why

A plan was finalized with nobody challenging it, and code reached a pull
request with nobody trying to break it
([#842](https://github.com/HPAC-Safety/safety-report/issues/842)). The bound
keeps the critic from looping: a second pass on the same plan finds new
nitpicks forever, so the recheck verifies only what was already found.

The advisor tool (`advisorModel` in `.claude/settings.json`) is complementary:
it is consulted whenever the model chooses; the critic is an explicit gate
before a plan is final.

## Enforced by

- `tools/github/remind-critic.ts`, wired in `.claude/settings.json`: a
  reminder, never a block.
- Nothing else. No pull-request body or CI check looks for a critic or
  adversary run.
