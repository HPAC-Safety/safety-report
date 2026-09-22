---
title: Reqnroll executes the .feature files
description: "features/*.feature files are the canonical specification (features/README.md) — Gherkin scenarios that describe target behavior."
type: adr
status: accepted
date: 2026-09-19
decision-makers: Chase Florell
keywords: Reqnroll, Gherkin, feature files, acceptance tests, xUnit
---

# ADR-0049 — Reqnroll executes the `.feature` files

**Status:** Accepted

## Context

`features/*.feature` files are the canonical specification
([`features/README.md`](../../features/README.md)) — Gherkin scenarios that
describe target behavior. Until now they were checked for syntax only
(`tools/gherkin/verify.mjs`, using `@cucumber/gherkin`) and had no step
implementations; the comment at the top of that script said so explicitly.
Most scenarios describe behavior that isn't built yet.

The goal is to make each scenario an executable acceptance test as its
behavior ships, without moving the `.feature` files out of `features/` (they
are the specification, read by humans and agents first) and without doing
more work today than standing up the infrastructure requires.

Reqnroll fails a scenario outright when no step definition matches — there is
no built-in "pass as pending" for a scenario with zero bindings. With every
canonical scenario currently unimplemented, wiring them straight into
`dotnet test` would turn CI red immediately.

## Decision

Add one new test project, `tests/HpacSafety.Acceptance.Tests`, using
`Reqnroll.xUnit` (xUnit is already the repository's test framework) plus
`Reqnroll.Tools.MsBuild.Generation` (the actual feature-file-to-code-behind
compiler; `Reqnroll.xUnit` alone does not pull it in transitively). The
project references the existing `features/**/*.feature` files in place via a
`ReqnrollFeatureFile` MSBuild item pointing outside the project's own folder —
no copy, move, or symlink. `ReqnrollUseIntermediateOutputPathForCodeBehind` is
set `true` so generated `*.feature.cs` code-behind lands in `obj/`, not next
to the physical `.feature` file, keeping `features/` free of generated output.

Every scenario in every current `.feature` file is tagged `@ignore`. Reqnroll
turns that into a skipped test at the generator level (no step definition is
invoked, so an unimplemented scenario never needs one). The project is
registered in `HpacSafety.slnx`, so the existing `dotnet test HpacSafety.slnx`
CI step already runs it — no new CI job.

Implementing a scenario's behavior means writing its Reqnroll step
definitions and removing its `@ignore` tag, in the same pull request that
implements the behavior (already implied by `deliver-hpac-change`'s rule that
a `.feature` file is touched in the same PR that changes its behavior).

## Why this choice

**No file moves.** Reqnroll does not require `.feature` files to live inside
the test project; code-behind generation follows wherever the file is
declared, which is why the out-of-cone `ReqnrollFeatureFile` include works
without disturbing `features/`'s layout or its role as the canonical spec
index.

**`@ignore` over stub step definitions.** Writing a `[Given]`/`[When]`/`[Then]`
stub for every step across ~113 scenarios purely to throw
`PendingStepException` is meaningfully more work than a one-line tag per
scenario, for the same outcome (a build that doesn't assert anything yet).
Tagging is also the natural unit that maps 1:1 to "this scenario is
implemented" going forward — a diff on one line proves a scenario just went
live.

**Reuse the existing `dotnet test HpacSafety.slnx` pipeline.** Every other
`.Tests` project is discovered this way (`ADR-0011`); a Reqnroll-specific CI
job would duplicate that mechanism for no reason.

## Alternatives

- **A separate demo/smoke feature file, deferring the 7 canonical files.**
  Rejected: proves the wiring but leaves the actual specification
  unconnected to the tooling until each of 7 future PRs remembers to wire its
  own file in; `@ignore` gets the whole specification connected today at
  mechanical cost.
- **Generate real step stubs for every step now (Reqnroll's "Define Steps"
  codegen).** Rejected as more work than the infrastructure needs today; see
  "Why this choice" above.
- **Symlink `features/` into the test project.** Rejected: still resolves to
  the same physical path for code-behind output purposes, adds a symlink to
  reason about, and buys nothing over an out-of-cone MSBuild item.

## Consequences

- A PR that implements a scenario's behavior removes that scenario's
  `@ignore` tag and adds its step definitions in the same PR — enforced the
  same way `deliver-hpac-change` already enforces touching `/features` when
  behavior changes.
- `tools/gherkin/verify.mjs`'s syntax check stays useful and unchanged
  alongside Reqnroll; its header comment is updated to stop claiming there is
  no step implementation to run scenarios against.
- Generated `*.feature.cs` files live under `obj/`, already gitignored
  repository-wide; nothing new to ignore.

## Related

- [`features/README.md`](../../features/README.md)
- [`ADR-0011`](ADR-0011-ci-contexts-precede-their-checks.md) — `dotnet test HpacSafety.slnx` as the shared test-discovery mechanism
- [`ADR-0047`](ADR-0047-feature-files-must-not-contradict-adrs.md)
- [`skills/deliver-hpac-change/SKILL.md`](../../skills/deliver-hpac-change/SKILL.md)
