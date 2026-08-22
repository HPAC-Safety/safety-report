# ADR-0013 — Ban `Xunit.Assert` with an analyzer, not a CI grep

**Status:** Accepted

## Context

`AGENTS.md` and `docs/testing-conventions.md` both say Shouldly is the assertion
library and `Assert.*` is not to be used. Until now nothing enforced it — the
rule was documentation, which is to say it held for exactly as long as everyone
remembered it.

The issue that raised this offered two options: an analyzer, or a CI grep for a
bare `Assert.` under `tests/`.

The rule matters more than it looks. Shouldly's failure message names the
expression under test — `summary.Text.ShouldNotContain("Vince")` fails with the
actual text — where `Assert.False(summary.Text.Contains("Vince"))` fails with
`Expected: False, Actual: True`. Most of this suite is read by an agent looking
at a CI log rather than a person at a debugger, and the difference between those
two messages is the difference between a fix and a bisect.

## Decision

**`Microsoft.CodeAnalysis.BannedApiAnalyzers`**, with `tests/BannedSymbols.txt`
listing `T:Xunit.Assert` and the reason. `RS0030` is an error.

Wired in `Directory.Build.props` under the existing
`$(MSBuildProjectName.EndsWith('.Tests'))` condition, so every current and
future test project gets it without anyone remembering to opt in.

## Why not a CI grep

A grep was the cheaper option and is worse on every axis that matters:

| | grep | analyzer |
|---|---|---|
| Fails in the editor | no | yes |
| Fails in a local `dotnet build` | no | yes |
| Matches `Assert` in a comment, string, or identifier | yes | no |
| Sees `using static Xunit.Assert;` | no | yes |
| Carries the reason to the author | in a workflow file | in the error message |

The last row is the one that decided it. A grep tells you a build failed; the
analyzer tells you *"Use Shouldly. `value.ShouldBe(expected)`, not
`Assert.Equal(expected, value)`"* at the exact character, in the editor, before
a commit exists. A rule that explains itself where it fires is a rule people
stop tripping over.

The feedback loop matters more than usual here because this codebase is written
largely by agents. An agent gets the message inline and corrects in the same
turn; a CI grep costs a full push-wait-read cycle for the same information.

## Consequences

- Test projects gain an analyzer package. It is `PrivateAssets: all`, so nothing
  ships with it.
- `tests/BannedSymbols.txt` is the place for any future ban. Two obvious
  candidates are named in the file but deliberately left unbanned, because
  neither was asked for and each is its own judgement:
  `DateTime.Now`/`UtcNow` (inject `TimeProvider`) and `Thread.Sleep` (await the
  condition).
- If a test ever legitimately needs `Assert` — a custom assertion helper, say —
  the escape is `#pragma warning disable RS0030` with a comment. That is
  deliberately visible in review, which a grep exemption in a workflow file
  would not be.
- The ban applies to test projects only. `src/` has no reason to reference
  xunit at all, so nothing is gained by extending it there.
