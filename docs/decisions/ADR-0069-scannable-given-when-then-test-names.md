---
status: accepted
date: 2026-09-21
decision-makers: Chase Florell
keywords: testing, naming, conventions, Given/When/Then, xUnit
---

# ADR-0069 — Scannable Given/When/Then test names

**Status:** Accepted. Narrows the naming half of
[ADR-0013](ADR-0013-ban-assert-rather-than-grep-for-it.md)'s test conventions;
everything else about how tests are written is unchanged.

## Context

.NET test methods have been named as one long snake_case sentence:

```csharp
Given_a_migrated_database_When_the_actor_column_is_read_Then_it_is_a_widened_string_with_a_lookup_index
```

The Given/When/Then structure earns its place — a name that states the
precondition, the action, and the expected outcome is the difference between a
failing test you can act on and one you have to open. The spelling is what does
not. At that length the three clauses run together, a test explorer truncates
the part that matters, and a CI failure line wraps. Roughly a third of the
characters are articles.

## Decision

A .NET test method is named as **three PascalCase segments joined by single
underscores**, each opening with the literal `Given`, `When`, or `Then`:

```csharp
GivenMigratedDatabase_WhenActorColumnIsRead_ThenWidenedStringWithLookupIndex
```

The underscores are the only separators, and they fall exactly on the clause
boundaries — so the three parts are visible at a glance and a search for
`_When` finds every test.

**Drop filler.** Articles (`a`, `an`, `the`) and empty predicates (`it is`,
`there is`) carry no information and go. Keep technical terms exactly as they
are spelled elsewhere — `TinyId`, `DTO`, `EXIF`, `SqlState` — and keep an
auxiliary that carries the voice, so `WhenActorColumnIsRead` keeps its `Is`.

**This governs C# identifiers only.** A TypeScript or JavaScript test title is
a display string that the runner prints to a human, and Playwright and
`node:test` output reads as prose by design. Those titles stay sentences. The
rule is about identifiers, and only C# test names are identifiers here.

## Why

The three clauses are the whole value of the name, so the spelling should make
them the most visible thing in it rather than the least. PascalCase segments do
that with no extra ceremony, and they match how every other identifier in the
codebase is already written.

Dropping articles is not terseness for its own sake. A name is read in a
failure message, in a test explorer, and in a diff, and in all three the useful
signal competes with the length. `ThenWidenedStringWithLookupIndex` says
everything `Then_it_is_a_widened_string_with_a_lookup_index` says, in a form
that survives truncation.

## Alternatives

- **Keep the long snake_case form.** Rejected: it is the status quo whose cost
  prompted this, and no reader has ever been helped by the articles.
- **Plain PascalCase with no underscores** (`GivenMigratedDatabaseWhenActorColumnIsReadThenWidenedString`).
  Rejected: it loses the clause boundaries, which are the one thing worth
  keeping. The underscores are load-bearing.
- **A `[DisplayName]` attribute carrying prose.** Rejected: it puts the useful
  name somewhere a grep and a stack trace will not find it, and leaves two
  names per test to keep in agreement.
- **Applying the same form to TypeScript titles.** Rejected: those strings are
  printed to a human by a runner built to print sentences, and an identifier
  there would be consistency bought at the reader's expense.

## Consequences

- Every test written from now on uses the new form.
- The 419 existing names are renamed separately (issue #197), as a
  rename-only change with no body or assertion edits, so the diff stays
  reviewable.
- Reqnroll step-definition methods are not test names and are unaffected.

## Related

- [ADR-0013](ADR-0013-ban-assert-rather-than-grep-for-it.md) — Shouldly, and the wider test conventions
- [ADR-0049](ADR-0049-reqnroll-for-executable-gherkin-scenarios.md) — scenarios, whose names come from the feature file
- [`docs/testing-conventions.md`](../testing-conventions.md)
