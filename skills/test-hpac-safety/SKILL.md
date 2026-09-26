---
name: test-hpac-safety
description: Test HPAC Safety privacy, immutable questions, uploads and submission, Worker summarization, attachments, moderation, deletion, and publication — extends the generic test-from-scenarios skill. Use for test changes or behavior that needs verification in this repository.
---

# Test HPAC Safety

Extends [`test-from-scenarios`](../test-from-scenarios/SKILL.md); read that
first. This skill holds only what is specific to this repository, under the
same section names.

## Tools and data

- .NET: xUnit and Shouldly. JavaScript: `node:test`. Browser journeys:
  Playwright.
- Synthetic report and file fixtures only.
- Seeded rows: the consent questions and the seeded question bank
  ([lesson 0021](../../docs/lessons/0021-a-consent-question-found-by-a-key-it-was-never-seeded-under.md)).
- Integration tests use the supported PostgreSQL version through
  Testcontainers.

## Naming C# tests

Three PascalCase segments joined by single underscores, opening with `Given`,
`When`, `Then`
([ADR-0069](../../docs/decisions/ADR-0069-scannable-given-when-then-test-names.md)):

```csharp
// good
GivenMigratedDatabase_WhenActorColumnIsRead_ThenWidenedStringWithLookupIndex

// bad
Given_a_migrated_database_When_the_actor_column_is_read_Then_it_is_a_widened_string_with_a_lookup_index
```

- Drop articles and empty predicates (`a`, `the`, `it is`); keep technical
  terms exact and any auxiliary that carries the voice.
- Mark the body with `// Given`, `// When`, `// Then`.
- **C# only.** `node:test` and Playwright titles are display strings and stay
  prose.

## Scenarios

### Which runner

- **Untagged** scenarios run as xUnit tests via Reqnroll in
  `tests/HpacSafety.Acceptance.Tests` (ADR-0049).
- **`@ui`** scenarios run through `playwright-bdd` in `tests/e2e/steps`.
  Reqnroll has no browser, so it never runs a `@ui` scenario (ADR-0053).
  - The acceptance suite skips `@ui` itself, through a
    `[BeforeScenario("ui")]` hook, wherever `dotnet test` runs.
    `.github/workflows/ci.yml`'s category filter is the backstop (ADR-0073).
- The booted host is `BootedApi`.

### `@ignore`

- Step definitions for a `@ui` scenario live in `tests/e2e/steps`.
- A superseded scenario left behind `@ignore` is the contradiction
  [ADR-0047](../../docs/decisions/ADR-0047-feature-files-must-not-contradict-adrs.md)
  forbids.

### Step definitions

- Write from the scenario
  ([ADR-0083](../../docs/decisions/ADR-0083-specification-driven-development.md));
  never encode a missing fact in C# or TypeScript.
- A request-level claim binds through `BootedApi`
  ([lesson 0006](../../docs/lessons/0006-an-internal-identifier-leaked-into-the-authoring-screen.md)).
- Reqnroll steps are Cucumber Expressions:
  `(User|SafetyOfficer|Administrator)` matches nothing; use `{word}`.

## Authentication

- Never fake a `ClaimsPrincipal`.
- The roles are `User`, `SafetyOfficer`, and `Administrator`; cover all three
  at every admin endpoint.
- Cover a stored report holding no reference to the member who filed it.

## External providers

- Example: a DeepL language code. Exercise French to English as well as
  English to French
  ([lesson 0019](../../docs/lessons/0019-a-language-code-the-provider-never-offered.md)).
- Required phrases are the role phrases.

## Contracts to cover

- **Questions**: complete revisions are immutable; latest-revision selection
  cannot resurrect an older active revision. The consents are always required
  when asked; an administrator may require any other question (ADR-0061).
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
  (ADR-0119).
- **Publication**: consent, non-deletion, and current pair approval are all
  required; editing clears approval; soft deletion stops every flow.
- **Logs**: credentials, report content, model payloads, client filenames, and
  URLs never enter logs or exceptions.

## Test containers

- Why the local cache is suspect first:
  [lesson 0014](../../docs/lessons/0014-a-local-image-cache-hides-a-withdrawn-upstream.md).
- The S3-compatible server is pinned once, in `tests/Shared/S3Emulator.cs`
  ([ADR-0110](../../docs/decisions/ADR-0110-rustfs-replaces-minio-as-the-development-s3-server.md)).
