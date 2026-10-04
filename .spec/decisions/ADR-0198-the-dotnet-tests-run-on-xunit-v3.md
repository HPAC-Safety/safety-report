---
title: The .NET tests run on xUnit v3, and Reqnroll executes the .feature files through Reqnroll.xUnit.v3
description: Every .NET test project moves from the deprecated xunit 2.9.3 to xunit.v3.mtp-off 4.0.1 with VSTest kept, Reqnroll.xUnit.v3 is the adapter, a @ui scenario skips through xUnit v3's dynamic-skip token, and the run-alone collections are bound by hand.
type: adr
status: accepted
date: 2026-10-04
decision-makers: Chase Florell
keywords: xUnit, xunit.v3, Reqnroll, Reqnroll.xUnit.v3, VSTest, SkipException, dynamic skip, collections, ADR-0049, ADR-0073, ADR-0195
---

# ADR-0198 — The .NET tests run on xUnit v3, and Reqnroll executes the `.feature` files through `Reqnroll.xUnit.v3`

**Status:** Accepted. Decided by Chase Florell on 2026-10-04 in
[#852](https://github.com/HPAC-Safety/safety-report/issues/852). Supersedes
[ADR-0049](ADR-0049-reqnroll-for-executable-gherkin-scenarios.md).

## Context

NuGet marks `xunit` 2.9.3 deprecated: "This package will only be updated for
security issues. All future feature work has moved onto v3", with `xunit.v3` as
the alternate. Renovate lists it under "Deprecations / Replacements" (#47).

[ADR-0049](ADR-0049-reqnroll-for-executable-gherkin-scenarios.md) names
`Reqnroll.xUnit` as the adapter, and that adapter targets xUnit v2. The adapter
for v3 is `Reqnroll.xUnit.v3`. Licences were checked: `xunit.v3` and its
sub-packages are Apache-2.0, `Reqnroll.xUnit.v3` is BSD-3-Clause.

The suite relies on four things a framework move could break:

- `dotnet test` on VSTest, with coverlet's collector, the `trx` logger, and
  `tools/coverage/report-test-counts.ts` reading the trx;
- a `@ui` scenario reporting **Skipped** with Reqnroll's message formatter on
  or off ([ADR-0073](ADR-0073-a-ui-scenario-is-skipped-by-reqnroll-itself.md),
  [ADR-0195](ADR-0195-a-built-claim-counts-only-when-its-scenario-passed-in-the-run.md));
- `Xunit.Assert` staying banned
  ([ADR-0013](ADR-0013-ban-assert-rather-than-grep-for-it.md));
- a feature tagged `@xunit:collection(Name)` running alone (REQ-MED-024,
  [ADR-0185](ADR-0185-a-submission-answers-only-current-revisions-and-the-browser-drops-the-rest.md)).

## Considered options

- **TUnit, MSTest, or NUnit.** Rejected: each rewrites about 1,460 test
  attributes and every fixture. xUnit v3 keeps `[Fact]`, `[Theory]`, and
  `[InlineData]` source-compatible.
- **Stay on xunit 2.9.3.** Rejected: deprecated, security fixes only.
- **xUnit v3 on Microsoft Testing Platform (`xunit.v3`).** Rejected: it would
  move `dotnet test`, coverlet, the trx logger, and the count report with it.
- **xUnit v3 with VSTest kept (`xunit.v3.mtp-off`)** — chosen.

## Decision

- Every `tests/*/*.csproj` references `xunit.v3.mtp-off` **4.0.1**,
  `xunit.runner.visualstudio` 4.0.0, and, in the acceptance project,
  `Reqnroll.xUnit.v3` **3.3.4** with `Reqnroll.Tools.MsBuild.Generation`.
  `xunit.v3.mtp-off` keeps `dotnet test` on VSTest and sets `OutputType` to
  `Exe`. Reqnroll.xUnit.v3 3.3.4 and CI's `dotnet test` arguments ran clean on
  4.0.1, so the 3.2.2 line was not needed.
- Renovate moves `Reqnroll.xUnit.v3` in the `xunit` group.
- A fixture's `InitializeAsync` and `DisposeAsync` return `ValueTask`. A call
  that takes a `CancellationToken` passes `TestContext.Current.CancellationToken`
  (`xUnit1051`, an error; no blanket `NoWarn`).
- The `@ui` hook throws an `InvalidOperationException` whose message starts with
  `Xunit.v3.DynamicSkipToken.Value`, xUnit v3's contract for a dynamic skip,
  which the generated `[Fact]` reports as skipped. `Assert.Skip` does the same
  but is banned with `Xunit.Assert`, so no suppression of the ban is needed.
  This replaces the `SkipException` of `Xunit.SkippableFact` named in ADR-0195's
  Consequences; the decision in ADR-0073 and ADR-0195 is unchanged.
  `UiScenarioHooksTests` pins it, and a run with the filter `Category=ui`
  reports every `@ui` scenario Skipped with `REQNROLL_FORMATTERS` set and unset.
- Reqnroll.xUnit.v3 3.3.4 turns `@xunit:collection(Name)` into a `Category`
  trait and not into `[Collection("Name")]`, which `Reqnroll.xUnit` did. Without
  the attribute those features ran in parallel and failed the allocation
  measurement and the question-bank scenarios. A hand-written partial class per
  tagged feature (`XunitCollectionBindings.cs`) adds the attribute, and
  `XunitCollectionBindingTests` fails when a tagged feature's class lacks the
  collection its tag names.

### What still holds from ADR-0049

- Reqnroll executes the `.spec/features/**/*.feature` files in place, through a
  `ReqnrollFeatureFile` item, with the code-behind in `obj/`.
- `Reqnroll.Tools.MsBuild.Generation` is the feature-file compiler.
- `tests/HpacSafety.Acceptance.Tests` runs in the same `dotnet test
  HpacSafety.slnx` step; no separate CI job.
- A scenario carries `@ignore @issue-<N>` until it is built, and the pull
  request that builds it removes the tags
  ([CONV-001](../conventions/CONV-001-a-scenario-counts-only-in-its-own-area-and-an-ignored-one-names-its-issue.md)).

## Consequences

- No `.feature` scenario changes; the diff touches no behavior-bearing path.
- Tagging another feature `@xunit:collection(...)` needs its partial class in
  `XunitCollectionBindings.cs`; the guard test names the omission.
- `UiScenarioHooksTests` and `XunitCollectionBindingTests` add one hand-written
  test to the acceptance assembly.
- Test order differs from v2, which surfaced two order-dependent tests: one read
  only the first page of the shared database's report list, and one read the
  audit log without an order. Both were made order-independent.
- If a later Reqnroll.xUnit.v3 emits `[Collection]` itself, the partial classes
  duplicate the attribute and the build fails with `CS0579`, which prompts
  deleting `XunitCollectionBindings.cs`.

## Related

- [ADR-0049](ADR-0049-reqnroll-for-executable-gherkin-scenarios.md): superseded.
- [ADR-0053](ADR-0053-ui-scenarios-execute-via-playwright-bdd.md),
  [ADR-0073](ADR-0073-a-ui-scenario-is-skipped-by-reqnroll-itself.md),
  [ADR-0195](ADR-0195-a-built-claim-counts-only-when-its-scenario-passed-in-the-run.md):
  unchanged; only the skip mechanism moved.
- [ADR-0013](ADR-0013-ban-assert-rather-than-grep-for-it.md): `Xunit.Assert` stays banned.
- [#852](https://github.com/HPAC-Safety/safety-report/issues/852), #47.
