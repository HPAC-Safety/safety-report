---
title: A coverage gate found in CI, not before the pull request
description: A pull request opened with every test green failed CI's branch-coverage ratchet, because the gate was never run locally and "tests pass" was taken to mean "checks pass".
type: lesson
date: 2026-09-22
issue: 352
status: superseded
---

# Lesson 0010 — A coverage gate found in CI, not before the pull request

**Status:** Superseded by
[lesson 0025](0025-a-local-gate-that-re-implemented-ci-disagreed-with-it.md)
and [ADR-0145](../decisions/ADR-0145-a-pull-requests-checks-run-locally-under-act.md).
The rule stands — pass the coverage gate before the pull request — but the
script this lesson added is gone: `tools/ci-local.sh` runs CI's own coverage
job under act, against CI's own baseline.

## Symptom

Pull request #353 was opened with every .NET, acceptance, and Playwright test
passing. CI's `coverage` job then failed it: branch coverage fell from 91.65%
on `main` to 91.13%. The owner, not the agent, found it.

## Root cause

The agent ran the test suites and treated a green run as a passing gate. The
coverage ratchet is a separate check. It compares ratios, and this change
deleted a lot of well-covered code (`OptionSet`, `OptionSetItem`, the option-set
endpoints) while adding code with untested branches. Every test passed, and the
ratio still dropped.

Nothing in the delivery workflow said to run the gate before opening the pull
request. CI's measurement also runs on a different machine, so its absolute
numbers are not reproducible locally without measuring `main` on the same
machine too. That made "check coverage first" easy to skip and awkward to do.

The branches that were uncovered were real behaviour: a French-only
reporter-added choice reaching the public form, an edit that sends no choice
list, a choice with no wording at all, a missing embedded SQL script. One was a
branch that could never run, and it was deleted rather than tested.

## Spec delta

None upstream. Coverage is a property of the delivery process, not a claim
about the product.

## Scenario

No scenario. The tests added pin down the behaviour of each uncovered branch
and cite the claims they serve (REQ-QB-099, REQ-QB-102).

## Skill

[`deliver-hpac-change`](../../skills/deliver-hpac-change/SKILL.md) "Verify and
publish" step 1 required a local coverage script to pass before a pull
request touching `src/`, `tests/`, or `tools/` was opened. The script measured
`origin/main` and the branch on the same machine with CI's own commands, and
ran `tools/coverage-gate.mjs` on the pair. Lesson 0025 replaced it.

Since #492 the general rule lives in the generic
[`deliver-change`](../../skills/deliver-change/SKILL.md) skill; the project skill named above
keeps this repository's commands, paths, and references.
