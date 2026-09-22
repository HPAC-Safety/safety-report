---
title: Testing conventions
description: How tests in this repository are named, structured, and written.
type: guide
---

# Testing conventions

Use xUnit and Shouldly for .NET, `node:test` for JavaScript, Playwright for
browser journeys, and Testcontainers for PostgreSQL/storage integration tests.
`Xunit.Assert` is analyzer-banned.

Name a .NET test as **three PascalCase segments joined by single underscores**,
each opening with `Given`, `When`, or `Then`
([ADR-0069](decisions/ADR-0069-scannable-given-when-then-test-names.md)), and
mark those three sections in the body with comments:

```csharp
// good
GivenMigratedDatabase_WhenActorColumnIsRead_ThenWidenedStringWithLookupIndex

// bad — the clauses disappear into the underscores
Given_a_migrated_database_When_the_actor_column_is_read_Then_it_is_a_widened_string_with_a_lookup_index
```

Drop articles (`a`, `an`, `the`) and empty predicates (`it is`, `there is`).
Keep technical terms spelled as they are everywhere else — `TinyId`, `DTO`,
`EXIF` — and keep an auxiliary that carries the voice, so
`WhenActorColumnIsRead` keeps its `Is`.

This governs C# identifiers only. A `node:test` or Playwright title is a
display string printed to a human, so those stay readable prose.

Use synthetic identities, sites, reports, and attachments. Never commit real
report content. Model tests use deterministic fakes or controlled fixtures and
assert schema, privacy properties, and preserved safety facts rather than exact
prose.

Prioritize boundaries described in
[`testing-and-quality.md`](testing-and-quality.md): immutable
question selection, multipart mapping/atomicity, token validation and rate
limits, one-call bilingual output, role replacement with no identity fragments,
attachment derivatives/private documents, the three-role authorization matrix,
audit, pair approval, soft deletion, and exact public DTO allowlists.

Authentication fixtures mint a real development-issuer token through the booted
host rather than faking a principal, so a test exercises the same validation
production runs.

Common commands:

```bash
dotnet test HpacSafety.slnx
dotnet test HpacSafety.slnx --filter "Category!=Integration"
node --test $(find tests/js -name '*.test.mjs')
npx playwright test
```

Integration suites require Docker. Coverage retains the repository floor and
added-code ratchet, but privacy and behavior assertions matter more than a high
percentage.

A UI behavior change ships with a Playwright test and, when it touches or
relies on API behavior, a server-side test — see
[ADR-0045](decisions/ADR-0045-ui-changes-require-playwright-and-server-tests.md).
