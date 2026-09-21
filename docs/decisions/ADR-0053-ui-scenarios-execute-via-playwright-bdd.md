---
status: accepted
date: 2026-09-19
decision-makers: Chase Florell
keywords: Reqnroll, Playwright, playwright-bdd, Gherkin, feature files, tags
---

# ADR-0053 — `@ui` scenarios execute via Playwright, not Reqnroll

**Status:** Accepted, partially supersedes
[ADR-0050](ADR-0050-ui-tag-for-scenarios-needing-playwright.md); its mechanism
for keeping `@ui` out of Reqnroll is amended by
[ADR-0073](ADR-0073-a-ui-scenario-is-skipped-by-reqnroll-itself.md)

## Context

[ADR-0050](ADR-0050-ui-tag-for-scenarios-needing-playwright.md) said `@ui`
scenarios "still execute through Reqnroll exactly like every other
scenario," with the tag existing only to flag that a Playwright companion
test is also needed. Implementing the first real `@ui` scenarios (issue
#140, the homepage spike) showed that claim doesn't hold up: a Reqnroll
step definition is C#, with no DOM access, no way to click a link or read a
CSS attribute, and no browser. Making an `@ui` scenario "execute through
Reqnroll" for real would mean driving a browser from .NET (a
Microsoft.Playwright-for-.NET dependency, a preview-server fixture, browser
installs in the .NET test job) in parallel with the TypeScript Playwright
suite ADR-0045 already requires — two separate browser-automation stacks
asserting the same behavior, for no benefit over one.

## Decision

Reqnroll is for scenarios a C# step definition can actually assert —
non-`@ui` scenarios, unchanged from ADR-0049. `@ui` scenarios execute
through **`playwright-bdd`** in `tests/e2e` instead: it reads the same
`features/**/*.feature` files in place (`featuresRoot`/`features` point at
`../../features`, no copy), generates Playwright specs from them filtered
to `@ui and not @ignore`, and matches each Given/When/Then to a step
definition in `tests/e2e/steps/*.ts` — real browser automation, in the same
language and tool as the rest of the Playwright suite.

An `@ui` scenario is never attempted through Reqnroll — whether or not it
carries `@ignore` — and never fails there for lack of a C# step definition.

> **Amended by
> [ADR-0073](ADR-0073-a-ui-scenario-is-skipped-by-reqnroll-itself.md).** This
> ADR originally achieved that with a category filter on
> `.github/workflows/ci.yml`'s `dotnet test` step, which only held where the
> filter was typed: a bare `dotnet test` or an IDE run still attempted every
> `@ui` scenario and failed. The suite now skips them itself, through a
> `[BeforeScenario("ui")]` hook. The category filter remains in CI as a second
> line of defence.

Implementing an `@ui` scenario's behavior means writing its step
definitions in `tests/e2e/steps/` and removing its `@ignore` tag, in the
same PR — the same "define + un-ignore together" discipline ADR-0049
established, applied to this track instead of Reqnroll's.

Non-`@ui` scenarios are unaffected: ADR-0049 governs them exactly as
before, Reqnroll executes them, and `@ignore` removal there still means
writing C# step definitions.

## Why this choice

**One browser-automation stack, not two.** `tests/e2e` already runs
Playwright against the built site (`playwright.config.ts`'s `webServer`).
`playwright-bdd` adds Gherkin parsing on top of that same setup rather than
introducing a second, parallel way to drive a browser from a different
language.

**The `.feature` files stay the single specification.** `playwright-bdd`
reads `features/**/*.feature` directly — the same files Reqnroll reads for
non-`@ui` scenarios — so there is still exactly one canonical scenario per
behavior, not a Gherkin copy duplicated into a TypeScript-only location.

**A tag-driven split, matching the shape ADR-0050 already chose.** ADR-0050
already used `@ui` to distinguish these scenarios; this ADR changes what
"needs a Playwright companion" means (from "also" to "instead of Reqnroll")
without inventing a new tag or a new file layout.

## Alternatives

- **Drive a browser from Reqnroll via Microsoft.Playwright for .NET**, so
  `@ui` scenarios genuinely execute through Reqnroll as ADR-0050 originally
  said. Rejected: a second full browser-automation stack (NuGet package,
  preview-server fixture, browser install step in the `dotnet test` CI job)
  duplicating what `tests/e2e` already does, for scenarios that read from
  the same `.feature` files either way.
- **Keep `@ui` scenarios permanently un-runnable through Reqnroll by
  leaving `@ignore` on forever**, with no CI-level exclusion. Rejected:
  fragile — nothing stops a future PR from removing `@ignore` on an `@ui`
  scenario without writing the C# step defs it would then need, and CI
  would only catch that by failing (an unimplemented-step exception),
  rather than the scenario simply never being attempted there.

## Consequences

- `tests/e2e/playwright.config.ts` configures `playwright-bdd`
  (`defineBddConfig`) and gains a second Playwright project so both
  hand-written specs (`*.spec.ts`) and generated feature specs
  (`.features-gen/`) run in the same `npm test` invocation.
- `tests/e2e/package.json`'s `test` script runs `bddgen` before
  `playwright test`.
- `.github/workflows/ci.yml`'s `dotnet test` step gains a category filter
  excluding `@ui`. (Since
  [ADR-0073](ADR-0073-a-ui-scenario-is-skipped-by-reqnroll-itself.md) that
  filter is a second line of defence rather than the mechanism.)
- `features/README.md` and `skills/test-hpac-safety/SKILL.md` are updated
  in this PR to describe the split mechanism.

## Related

- [ADR-0045](ADR-0045-ui-changes-require-playwright-and-server-tests.md)
- [ADR-0049](ADR-0049-reqnroll-for-executable-gherkin-scenarios.md)
- [ADR-0050](ADR-0050-ui-tag-for-scenarios-needing-playwright.md)
- [ADR-0073](ADR-0073-a-ui-scenario-is-skipped-by-reqnroll-itself.md)
