# ADR-0014 — Coverage: an absolute floor plus a ratchet, from main's last artifact

**Status:** Accepted

## Context

Coverage was documented and never measured. The `coverage` CI job existed as a
required check that collected a report and asserted nothing —
[ADR-0011](ADR-0011-ci-contexts-precede-their-checks.md) named that debt and
committed to paying it down.

Three things had to be decided to make it real.

## Decision 1 — exclude generated code, and treat that as load-bearing

Measured before excluding anything, `HpacSafety.Api` reported **4.8% line
coverage while `Program.cs` was at 100%**. The gap is the OpenApi source
generator, which emits roughly 440 lines of XML-comment support into `obj/` that
no test will ever execute.

A gate reading that number would have been measuring the .NET SDK. So
`coverlet.runsettings` excludes `**/obj/**`, `*.g.cs`, `*.designer.cs`,
`Migrations/*.cs`, anything carrying `[ExcludeFromCodeCoverage]` or
`[GeneratedCode]`, and the test assemblies themselves.

With those exclusions the same solution measures **100% of 12 coverable lines**.
The exclusions are not tidying; they are the difference between a gate and a
random number.

## Decision 2 — a floor *and* a ratchet, not either

- **Floor:** 80% line, 70% branch. Stops the number sinking over years.
- **Ratchet:** fail if coverage is below main's. Stops a single pull request
  adding a large body of untested code while staying just above the floor.

Neither is sufficient alone. The floor alone permits a slow slide to exactly
80%; the ratchet alone permits whatever the first commit happened to measure to
become permanent.

A 0.1 percentage-point tolerance absorbs the arithmetic noise of a refactor that
deletes one covered and one uncovered line. It is far too small to hide a
deleted test.

## Decision 3 — the baseline comes from main's last green artifact

The `coverage` job uploads its merged Cobertura report. A pull request finds
main's most recent successful CI run, downloads that artifact, and compares.

**Rejected: recompute main's coverage in the same run.** Always accurate and no
artifact plumbing, but it roughly doubles the job — including re-running the
Testcontainers suite — to recompute a number already known. Correctness was
equal; cost was not.

**Rejected: commit a `coverage-baseline.json`.** A drop would be visible in the
diff, which is genuinely attractive. But every pull request would touch the
file, a forgotten update fails confusingly, and — decisively — the number
becomes hand-editable. A gate whose threshold the gated change can rewrite is
not a gate.

Two failure modes are handled by continuing rather than failing: no successful
main run yet, and an expired or absent artifact. Both emit a `::notice::`, skip
the ratchet, and **still apply the floor**. Failing closed here would block every
pull request for a reason unrelated to the change.

## Consequences

- Artifact retention bounds the ratchet. If main goes quiet past the retention
  window, the ratchet skips with a notice until main next runs green.
- The job needs `actions: read` to download the artifact and
  `pull-requests: write` to comment.
- **The comment is skipped on fork pull requests.** Their token is read-only by
  design, and this repository takes fork contributions. The job summary carries
  the identical table, so a failing gate stays fully diagnosable from the run
  page — nothing is lost but convenience.
- JavaScript coverage merges automatically once `tests/js` exists. The step
  emits a notice and skips until then; ReportGenerator already consumes lcov
  alongside Cobertura, so #8 adds a suite and nothing here changes.

## The thing this must not become

Coverage is a floor, not a goal. This repository could sit at 95% and still
publish someone's phone number.

`HpacSafety.Anonymization.Tests` is what actually protects a reporter. The gate
exists to catch a slide, not to be optimised for, and the failure message says
so: *"Add the test that pins down the behaviour this change introduced — not a
test that raises the number."*
