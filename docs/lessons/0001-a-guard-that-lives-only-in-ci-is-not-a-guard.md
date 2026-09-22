---
title: A guard that lives only in CI is not a guard
description: "Thirty-eight @ui scenarios failed on every fresh clone while CI stayed green, because the rule was a --filter argument rather than code."
type: lesson
date: 2026-09-21
issue: 219
status: accepted
---

# Lesson 0001 — A guard that lives only in CI is not a guard

## Symptom

A fresh clone looked broken. `dotnet test HpacSafety.slnx` — the command
`tests/README.md` and `init-dev.sh` both tell a developer to run — produced 38
failures across four feature files, every one a
`XUnitPendingStepException: Test pending: No matching step definition found`
for a `@ui` scenario that was never meant to have a C# binding at all.

CI was green the entire time.

## Root cause

[ADR-0053](../decisions/ADR-0053-ui-scenarios-execute-via-playwright-bdd.md)
decided that a `@ui` scenario is "never attempted through Reqnroll," and
implemented that decision as a category filter on CI's `dotnet test` step:
`--filter "Category!=ui"`.

The filter only exists where it is typed. Reqnroll's generator reads
`features/**/*.feature` with no tag exclusion, so it still emitted an xUnit test
for every `@ui` scenario, and the generated code-behind skipped only on
`@ignore`. Anywhere the filter was absent — a bare `dotnet test`, an IDE "run
all tests" — every un-ignored `@ui` scenario ran and failed.

The decision was right. The mechanism was in the wrong place: a rule about what
the test suite does, enforced by an argument on one command line.

## Spec delta

[ADR-0073](../decisions/ADR-0073-a-ui-scenario-is-skipped-by-reqnroll-itself.md)
moved the mechanism into the code. A `[BeforeScenario("ui")]` hook in
`tests/HpacSafety.Acceptance.Tests` calls `SkipScenarioAsync()`, so the scenario
reports as skipped wherever the suite runs. The CI filter stays as a second line
of defence, not as the mechanism.

The general rule that came out of it, and that this repository now applies to
every new check: **a rule enforced only by a flag in a workflow file holds only
in that workflow.** If it should be true on a developer's machine, it lives in
the code, the hook, or the tool — with CI as the backstop.

## Scenario

No scenario. This is a property of the test suite rather than of the system, so
nothing in `features/` can assert it. `UiScenarioHooksTests` guards the hook
directly, precisely because CI's filter would hide its removal — which is the
same failure mode one level up.

## Skill

[`hpac-safety-conventions`](../../skills/hpac-safety-conventions/SKILL.md)
carries the general rule: put a rule where it runs, not only where it is
checked — the CI step is the backstop, never the mechanism.
