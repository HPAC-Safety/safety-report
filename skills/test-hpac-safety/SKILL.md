---
name: test-hpac-safety
description: Test HPAC Safety privacy, immutable questions, uploads and submission, Worker summarization, attachments, moderation, deletion, and publication — extends the generic test-from-scenarios skill. Use for test changes or behavior that needs verification in this repository.
---

# Test HPAC Safety

Extends [`test-from-scenarios`](../test-from-scenarios/SKILL.md); read that
first. This skill holds only what is specific to this repository, under the
same section names.

## Tools and data

- .NET: xUnit and Shouldly. JavaScript tools: `node:test`. Web logic
  (`src/web`): Vitest with Testing Library and jsdom, tests beside the code as
  `Foo.test.tsx`, held to 100% coverage
  (a component's logic lives in `Foo.tsx` and its markup in `Foo.view.tsx`;
  [`build-hpac-web-ui`](../build-hpac-web-ui/SKILL.md)). Browser journeys:
  Playwright.
- Test code is C# or TypeScript.
- Synthetic fixtures only: people, locations, reports, and attachments.
- Seeded rows: the consent questions and the seeded question bank; a test that finds a consent question by key seeds
  that key.
- Integration tests use the supported PostgreSQL version through
  Testcontainers.

## Naming C# tests

Three PascalCase segments joined by single underscores, opening with `Given`,
`When`, `Then`
(the scannable-test-names decision):

```csharp
// good
GivenMigratedDatabase_WhenActorColumnIsRead_ThenWidenedStringWithLookupIndex

// bad
Given_a_migrated_database_When_the_actor_column_is_read_Then_it_is_a_widened_string_with_a_lookup_index
```

- Drop articles and empty predicates (`a`, `the`, `it is`); keep technical
  terms exact and any auxiliary that carries the voice.
- Mark the body with `// Given`, `// When`, `// Then`.
- **C# only.** `node:test`, Vitest and Playwright titles are display strings
  and stay prose.

## Scenarios

### Which runner

- **Untagged** scenarios run as xUnit v3 tests via `Reqnroll.xUnit.v3` in
  `tests/HpacSafety.Acceptance.Tests` (the Reqnroll-acceptance decision).
  - Reqnroll.xUnit.v3 does not turn `@xunit:collection(Name)` into
    `[Collection("Name")]`. `XunitCollectionBindings.cs` adds it to each tagged
    feature's generated partial class, and `XunitCollectionBindingTests` fails
    when one is missing. Tag another feature, add its partial.
  - A call that takes a `CancellationToken` passes
    `TestContext.Current.CancellationToken` (`xUnit1051`).
- **`@ui`** scenarios run through `playwright-bdd` in `tests/e2e/steps`.
  Reqnroll has no browser, so it never runs a `@ui` scenario (the playwright-bdd decision).
  - The acceptance suite skips `@ui` itself, through a
    `[BeforeScenario("ui")]` hook, wherever `dotnet test` runs. The hook throws
    an exception whose message starts with `DynamicSkipToken.Value`, xUnit v3's
    dynamic skip, since `Assert.Skip` is banned.
    `.github/workflows/ci.yml`'s category filter is the backstop (the UI-scenario-is-skipped-by-Reqnroll-itself decision).
- The booted host is `BootedApi`.

### `@ignore`

- Step definitions for a `@ui` scenario live in `tests/e2e/steps`.
- A scenario that leads its code carries `@ignore @issue-<N>`, naming the open
  issue that will build it; the pull request that builds it removes both tags.
  `feature-coverage` runs `node tools/spec/check-ignored-claims.ts` (the
  scenario-counts-only-in-its-own-area convention).
- A built claim fails the `coverage` job unless every pickle of it passed in
  its engine's run (the built-claim-counts-only-when-its-scenario-passed
  decision). Both suites write Cucumber Messages under `artifacts/claims/`: Playwright on
  every run, Reqnroll when `REQNROLL_FORMATTERS` asks. To judge your own runs:

  ```sh
  REQNROLL_FORMATTERS='{"formatters":{"message":{"outputFilePath":"'"$PWD"'/artifacts/claims/reqnroll.ndjson"}}}' \
    dotnet test tests/HpacSafety.Acceptance.Tests --filter "Category!=ui"
  (cd tests/e2e && CI=1 npm test)
  node tools/spec/check-claim-results.ts \
    --results Reqnroll=artifacts/claims/reqnroll.ndjson \
    --results playwright-bdd=artifacts/claims/playwright-bdd.ndjson
  ```

  A filtered run judges only what it ran, so judge a whole suite.
- A superseded scenario left behind `@ignore` is the contradiction the
  feature-files-must-not-contradict-decisions rule forbids.

### Step definitions

- Write from the scenario (specification-driven development); never encode a missing fact in C# or TypeScript.
- Which file binds each step of a claim, and which steps nothing binds yet, is
  in that claim's entry in the generated claims file;
  `node tools/spec/generate-traceability.ts` fails a built claim with an
  unbound step.
- A claim proven only in the domain layer, or by a step that asserts nothing,
  is not covered: bind a request-level claim through `BootedApi` and assert the
  response (an internal identifier leaked into the authoring screen while a
  hollow step reported it covered; a choice code nobody could supply and a
  reporter choice nobody recorded).
- Start from the claim's entry in the generated claims file: its steps with no
  `files` are the definitions to write. Remove `@ignore` and its `@issue-<N>`
  once the entry says `"staleIgnore": true` and the scenario passes; from then
  on CI fails the claim unless it passes in every run.
- A key comes from its Examples cell through `tests/e2e/steps/keys.ts` (the
  one-behavior-per-scenario convention).
- Reqnroll steps are Cucumber Expressions:
  `(User|SafetyOfficer|Administrator)` matches nothing; use `{word}`.

## Authentication

- Never fake a `ClaimsPrincipal`.
- The roles are `User`, `SafetyOfficer`, and `Administrator`; cover all three
  at every admin endpoint.
- Cover a stored report holding no reference to the member who filed it.

## External providers

- Example: a provider language code (the kept, dormant DeepL adapter's
  `EN-CA`). Exercise French to English as well as English to French;
  a code the provider never offered fails only in one direction.
- Required phrases are the role phrases.

## Contracts to cover

- **Questions**: complete revisions are immutable; latest-revision selection
  cannot resurrect an older active revision. The consents are always required
  when asked; an administrator may require any other question (the administrators-may-require-any-question decision).
- **Before submission**: unfinished answers and revision IDs stay in browser
  storage for 15 days; no report, reserved ID, or database state exists before
  final submission. An upload is minted with a PUT signed for its declared
  type and exact size, is sniffed and validated when claimed, names no member,
  and is erased by its delete.
- **Submission**: maps answers and upload IDs exactly; refuses missing uploads
  by ID; accepts known superseded revisions; rejects unknown or deleted ones;
  commits report, answers, files, and outbox work atomically.
- **Worker**: sends each answered field in the right `report_content` or
  `private_context` section, calls the model once, and accepts only strict
  nonblank English/French JSON.
- **Anonymity**: a synthetic private identity repeated in narrative becomes the
  exact role in both languages with no fragment left; eligible safety facts
  survive.
- **Attachments**: count, per-kind size, and type checks read only what they
  need; image and video derivatives remove metadata; documents are kept as unchanged originals and
  never reach AI. One is public only as a short-lived forced download under a
  server-minted name, when validated, unhidden, and `consent_documents` is true
  (the published-report-offers-documents decision).
- **Publication**: consent, non-deletion, and an approved revision are all
  required; an edit on a report that is not live is an unapproved draft, and one
  on a Published report is approved and public at once (the append-only-summary-revisions decision); soft deletion
  stops every flow.
- **Logs**: credentials, report content, model payloads, client filenames, and
  URLs never enter logs or exceptions.

## Test containers

- Suspect the local image cache first: it can hide an upstream image that was
  withdrawn.
- The S3-compatible server is pinned once, in `tests/Shared/S3Emulator.cs` (the
  RustFS-replaces-MinIO decision).
