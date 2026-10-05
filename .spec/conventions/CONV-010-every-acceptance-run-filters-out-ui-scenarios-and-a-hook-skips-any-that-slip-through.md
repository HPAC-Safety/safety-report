---
title: Every acceptance run filters out @ui scenarios, and a hook skips any that slip through
description: The acceptance project's own settings file filters @ui scenarios out of every default run, local or IDE, as CI's filter does; the UiScenarioHooks skip stays as the backstop for a run whose settings replace that file.
type: convention
status: accepted
date: 2026-10-04
---

# CONV-010 — Every acceptance run filters out `@ui` scenarios, and a hook skips any that slip through

## Rule

- **The filter is the mechanism.**
  `tests/HpacSafety.Acceptance.Tests/acceptance.runsettings` sets
  `RunConfiguration/TestCaseFilter` to `Category!=ui`, and the project names it
  in `RunSettingsFilePath`. A default `dotnet test`, Visual Studio, and Rider all
  read that property. A `@ui` scenario's generated test is therefore not run
  at all, and a local run reports what CI reports.
- **A command-line `--filter` combines with it.** It does not replace it, so
  `--filter "Category!=Integration"` still leaves the `@ui` tests out.
- **The hook is the backstop.** `UiScenarioHooks` still skips a `@ui` scenario
  in any run whose `--settings` replaces the project's file, as CI's
  `--settings coverlet.runsettings` does. `UiScenarioHooksTests` keeps the hook
  from being removed unnoticed. With the filter on every default path, a
  missing hook would otherwise show up nowhere.
- **CI keeps its own `--filter "Category!=ui"`** on the `dotnet test` and
  coverage steps, because its settings file replaces the project's.
- **A `@ui` scenario still executes only through `playwright-bdd`** in
  `tests/e2e` (ADR-0053). A non-`@ui` scenario with a missing binding still
  fails here: both the filter and the hook are scoped to the tag.

- **To exercise the hook**, replace the project's settings file:
  `--settings coverlet.runsettings --filter "Category=ui"` reports every `@ui`
  scenario as skipped. With the project's file in place, `--filter "Category=ui"`
  matches nothing, because the two filters combine.

### Carried from the record this supersedes

- **A guard that exists only as a CI flag is not a guard.** A rule held only by
  a command-line option in a workflow holds only where that option is typed; it
  belongs where every run reads it, such as the code or a file the project
  itself names. Records and comments that cite ADR-0073 for this rule now rest
  on this convention.
- A developer's first `dotnet test` on a fresh clone is green, with no filter
  typed.
- Excluding `@ui` feature files from the Reqnroll glob stays rejected. The tag
  belongs to a scenario, not a file, and every such file also holds scenarios
  that must execute here.

## Why

[ADR-0073](../decisions/ADR-0073-a-ui-scenario-is-skipped-by-reqnroll-itself.md)
made the hook the mechanism, so a local run reported every `@ui` test as
skipped: 635 of them, against 0 in CI. The owner read that as hundreds of
ignored tests. ADR-0073 rejected a settings file only because `dotnet test`
did not read one from the repository root. `RunSettingsFilePath` in the
project file removes that objection: `dotnet test` reads it too, so the
documented command and the working command stay the same
([#860](https://github.com/HPAC-Safety/safety-report/issues/860)).

This convention supersedes ADR-0073. It also overrides the passage in
[ADR-0195](../decisions/ADR-0195-a-built-claim-counts-only-when-its-scenario-passed-in-the-run.md)
that says a bare `dotnet test` still skips every `@ui` scenario: that run now
filters them out instead. How an acceptance run declines `@ui`
scenarios is a tooling rule, and tooling rules are conventions
([ADR-0192](../decisions/ADR-0192-an-accepted-adr-is-immutable-and-process-rules-are-conventions.md)).

## Enforced by

- `UiScenarioHooksTests` asserts the hook exists and is scoped to the `ui` tag.
- `UiScenarioHooksTests` also asserts that the project names
  `acceptance.runsettings` and that the file's `TestCaseFilter` is
  `Category!=ui`. CI never reads that file, so this test is the only check
  that notices if either is removed.
