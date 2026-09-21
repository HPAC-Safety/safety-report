---
status: accepted
date: 2026-09-21
decision-makers: Chase Florell
keywords: Reqnroll, Playwright, playwright-bdd, hooks, tags, CI, developer experience
---

# ADR-0073 — A `@ui` scenario is skipped by Reqnroll itself, not by a CI filter

**Status:** Accepted, amends the mechanism in
[ADR-0053](ADR-0053-ui-scenarios-execute-via-playwright-bdd.md)

## Context

[ADR-0053](ADR-0053-ui-scenarios-execute-via-playwright-bdd.md) decided that a
`@ui` scenario executes through `playwright-bdd` and is "never attempted
through Reqnroll". It implemented that with a category filter on CI's
`dotnet test` step: `--filter "Category!=ui"`.

The filter only exists where it is typed. Reqnroll's generator reads
`features/**/*.feature` with no tag exclusion, so it still emits an xUnit test
for every `@ui` scenario, and the generated code-behind skips only on
`@ignore`. Anywhere the filter is absent — a bare `dotnet test HpacSafety.slnx`,
which both `tests/README.md` and `init-dev.sh` tell a developer to run, or an
IDE "run all tests" — every un-ignored `@ui` scenario runs and throws
`XUnitPendingStepException: Test pending: No matching step definition found`,
for want of a C# binding it is never meant to have.

That reached 38 failures across four feature files once
[#202](https://github.com/HPAC-Safety/safety-report/pull/202) moved seven
scenarios from `@ui @ignore` to `@ui`. CI was green the whole time. A fresh
clone was not.

ADR-0053 rejected the permanent-`@ignore` alternative on the grounds that a
scenario should "simply never be attempted there". A CI-only filter delivers
that only in CI.

## Decision

The acceptance suite skips a `@ui` scenario itself. A `[BeforeScenario("ui")]`
hook in `tests/HpacSafety.Acceptance.Tests` resolves `ITestRunner` and calls
`SkipScenarioAsync()`. Reqnroll's generator already emits a
`[SkippableFact]` per scenario, so the scenario reports as **Skipped**
wherever the suite runs — bare CLI, IDE, Debug, Release, CI, with or without a
filter.

The category filter stays on CI's `dotnet test` and coverage steps as a second
line of defence, not as the mechanism.

ADR-0053's decision is unchanged: `@ui` scenarios still execute through
`playwright-bdd` in `tests/e2e`, and implementing one still means writing its
TypeScript step definitions and removing `@ignore` in the same PR. Only *how*
Reqnroll declines to attempt them changes.

## Consequences

- `tests/HpacSafety.Acceptance.Tests/UiScenarioHooks.cs` holds the hook.
- `UiScenarioHooksTests` asserts the hook exists and is scoped to the `ui` tag.
  Without it a removed hook would be invisible in CI, which still filters the
  category out, and would surface only on a developer's machine — the failure
  mode this ADR exists to end.
- A developer's first `dotnet test` on a fresh clone is green.
- A non-`@ui` scenario with a genuinely missing binding still fails, because
  the hook is scoped to the tag.
- ADR-0053's Decision and Consequences sections are corrected in the same PR to
  point here for the mechanism, per
  [ADR-0047](ADR-0047-feature-files-must-not-contradict-adrs.md).

## Alternatives

- **A `.runsettings` at the repository root carrying the filter.** Rejected:
  Rider and Visual Studio auto-detect it, but `dotnet test` does not — the bare
  command in `tests/README.md` and `init-dev.sh` would still fail, so the
  documented command and the working command would stay different.
- **Exclude `@ui` feature files from the Reqnroll glob.** Rejected: the tag is
  per scenario, not per file. Every feature file with a `@ui` scenario also
  holds non-`@ui` ones that must execute here.
- **Document the filter and leave the failures.** Rejected: it makes every
  developer memorize a flag to see a true result, and a wall of red on a fresh
  clone trains people to ignore the suite.
- **Drop the CI filter now that the hook exists.** Rejected for this change,
  not forever: keeping both means a hook that misbehaves under one
  configuration cannot turn CI red for a reason unrelated to the change under
  test. Removing the filter is a separate decision on its own evidence.

## Related

- [ADR-0045](ADR-0045-ui-changes-require-playwright-and-server-tests.md)
- [ADR-0049](ADR-0049-reqnroll-for-executable-gherkin-scenarios.md)
- [ADR-0050](ADR-0050-ui-tag-for-scenarios-needing-playwright.md)
- [ADR-0053](ADR-0053-ui-scenarios-execute-via-playwright-bdd.md)
