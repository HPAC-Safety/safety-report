---
title: Testing conventions
description: How tests in this repository are named, structured, and written.
type: guide
---

# Testing conventions

Use xUnit and Shouldly for .NET, `node:test` for the JavaScript tools,
Vitest with Testing Library for web logic, Playwright for
browser journeys, and Testcontainers for PostgreSQL/storage integration tests.
`Xunit.Assert` is analyzer-banned.

Name a .NET test as **three PascalCase segments joined by single underscores**,
each opening with `Given`, `When`, or `Then`
([ADR-0069](../.spec/decisions/ADR-0069-scannable-given-when-then-test-names.md)), and
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

This governs C# identifiers only. A `node:test`, Vitest or Playwright title is a
display string printed to a human, so those stay readable prose.

Use synthetic identities, sites, reports, and attachments. Never commit real
report content. Model tests use deterministic fakes or controlled fixtures and
assert schema, privacy properties, and preserved safety facts rather than exact
prose.

Prioritize boundaries described in
[`testing-and-quality.md`](../.spec/testing-and-quality.md): immutable
question selection, JSON submission mapping and atomicity with claimed uploads,
token validation and rate limits, one-call bilingual output, role replacement
with no identity fragments, attachment derivatives and document publication
only under media consent, the three-role authorization matrix,
audit, pair approval, soft deletion, and exact public DTO allowlists.

Authentication fixtures mint a real development-issuer token through the booted
host rather than faking a principal, so a test exercises the same validation
production runs.

Common commands:

```bash
dotnet test HpacSafety.slnx
dotnet test HpacSafety.slnx --filter "Category!=Integration"
node --test $(find tests/js -name '*.test.ts')   # tests/js/gherkin needs npm --prefix tools/gherkin ci
node tools/gherkin/lint-scenarios.ts      # scenarios describe behavior, one each (CONV-004, CONV-006)
npm run typecheck                        # tsc over tools and tests/js (Node strips their types, never checks them)
npm --prefix src/web run test:coverage   # Vitest, 100% on split components and tested helpers
npm --prefix src/web run typecheck
node tools/web/check-component-split.ts
npm ci && npm --prefix src/web ci && npm --prefix tests/e2e ci && npm --prefix tools/gherkin ci && npm run lint   # ESLint (strict, type-checked): src/web, tools, tests/js, tests/e2e
npm run typecheck:e2e   # tsc over tests/e2e: Playwright transpiles without checking
npm --prefix tests/e2e test   # bddgen, then playwright test
```

Web logic is unit-tested with Vitest and Testing Library, in a `Foo.test.tsx`
beside the code, and held to 100% line, branch, function and statement coverage
([ADR-0188](../.spec/decisions/ADR-0188-a-components-logic-lives-in-foo-tsx-and-its-markup-in-foo-view-tsx-and-web-logic-is-unit-tested.md)).
Which files are held to it follows the files on disk, so a pull request that
splits a component edits no configuration; see
[`build-hpac-web-ui`](../skills/build-hpac-web-ui/SKILL.md). Test code is never
part of a release: `tools/web/check-web-bundle.ts` fails a build that carries any.
Every `.ts` helper under `src/web/src` has a colocated test, so all web logic is
under the gate; `api/` tests mock `fetch` and `XMLHttpRequest`. A `v8 ignore` or
`istanbul ignore` hint is for a branch no input can reach, with its reason in the
comment directly above, and the split guard fails one without it. Every
TypeScript and JavaScript file is linted by ESLint at the repository root
(`eslint.config.mjs`, CI's `lint` job, pre-commit); the rules are errors, and
for TypeScript they are typescript-eslint's strict-type-checked set. Test code
gets no relaxation: a value a test needs is narrowed with `present(...)`, not
asserted with `!`, and a spy is held in a variable, not read off an object.

Integration suites require Docker. Coverage retains the repository floor and
added-code ratchet, but privacy and behavior assertions matter more than a high
percentage.

A UI behavior change ships with a Playwright test and, when it touches or
relies on API behavior, a server-side test — see
[ADR-0045](../.spec/decisions/ADR-0045-ui-changes-require-playwright-and-server-tests.md).
