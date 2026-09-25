---
name: test-hpac-safety
description: Test HPAC Safety privacy, immutable questions, uploads and submission, Worker summarization, attachments, moderation, deletion, and publication. Use for test changes or behavior that needs verification.
---

# Test HPAC Safety

## Tools and data

- .NET: xUnit and Shouldly. JavaScript: `node:test`. Browser journeys:
  Playwright.
- Synthetic report and file fixtures only; never real personal data.
- Deterministic fakes at model and service boundaries.
- A rule over seeded rows (the consent questions, the seeded question bank) is
  tested against the rows the migrations seed, loaded from the migrated
  database, never against a stand-in built with a domain factory. A seeded row
  can differ from the factory's in key, flags, or wording, and a test on the
  stand-in proves nothing about the real one
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
- **`@ui`** scenarios assert browser-observable behavior and run through
  `playwright-bdd` in `tests/e2e/steps`. Reqnroll has no browser, so it never
  runs a `@ui` scenario (ADR-0053).
  - The acceptance suite skips `@ui` itself, through a
    `[BeforeScenario("ui")]` hook, wherever `dotnet test` runs.
    `.github/workflows/ci.yml`'s category filter is a backstop, not the
    mechanism (ADR-0073).
- A scenario mixing a client-observable and a server-authoritative assertion
  splits into one `@ui` and one untagged scenario.
- Most untagged scenarios call the domain directly — no host, no database.
  Scenarios about what the API **refuses** boot it via `BootedApi` instead: a
  domain call cannot show "the API rejects it regardless of the UI".
  `BootedApi` starts on first use, so a domain-only run pays nothing.

### `@ignore`

- **`@ignore` means "not built yet", never "no longer true."**
- Implementing a scenario means writing its step definitions (Reqnroll, or
  `tests/e2e/steps` for `@ui`) and removing `@ignore` in the same pull request.
  Never leave a scenario un-ignored and unimplemented.
- **A decision that supersedes a scenario deletes it**, in the pull request
  that records the decision. Parking it behind `@ignore` leaves the repository
  stating something false — the contradiction
  [ADR-0047](../../docs/decisions/ADR-0047-feature-files-must-not-contradict-adrs.md)
  forbids. Git history keeps the text.

### Step definitions

- **Write from the scenario, not the conversation.** A binding that needs a
  fact the scenario does not state means the scenario is incomplete — amend it
  rather than encoding the fact in C# or TypeScript
  ([ADR-0083](../../docs/decisions/ADR-0083-specification-driven-development.md)).
- **A step that asserts nothing proves nothing.**
  - An empty `Then` is allowed only when an earlier step in the same scenario
    already made that assertion.
  - Never point at another suite ("covered by Api.Tests"). Prove it here, or
    remove the sentence.
  - A claim proven only against a domain method does not prove the endpoint
    calls it. When the scenario describes what a submission or request does,
    one binding goes through `BootedApi`
    ([lesson 0006](../../docs/lessons/0006-an-internal-identifier-leaked-into-the-authoring-screen.md)).
- **Reqnroll steps are Cucumber Expressions, not regex.**
  - Parentheses mean optional text: `(User|SafetyOfficer|Administrator)`
    matches nothing and the scenario reports pending. Use `{word}`.
  - A literal `/` is alternation; escape it as `\/`.

## Authentication

- Mint a real token through the booted host; never fake a `ClaimsPrincipal`.
- Cover:
  - the three-role matrix at every admin endpoint;
  - refusal of an unsigned, foreign-key-signed, tampered, expired,
    wrong-audience, or `alg: none` token;
  - a stored report holding no reference to the member who filed it.

## External providers

- The expected value of what a provider is sent (a DeepL language code, a model
  parameter, a request field) comes from the provider's documentation, never
  from the code under test.
- Exercise every translation direction — French to English as well as English
  to French
  ([lesson 0019](../../docs/lessons/0019-a-language-code-the-provider-never-offered.md)).
- Never assert exact generated prose beyond the strict schema and required role
  phrases.

## Contracts to cover

- **Questions**: complete revisions are immutable; latest-revision selection
  cannot resurrect an older active revision. The consents are always required
  when asked; an administrator may require any other question (ADR-0061).
- **Before submission**: unfinished answers and revision IDs stay in browser
  storage for 15 days; no report, reserved ID, or database state exists before
  final submission. An upload is validated before it is stored, names no
  member, and is erased by its delete.
- **Submission**: maps answers and upload IDs exactly; refuses missing uploads
  by ID; accepts known superseded revisions; rejects unknown or deleted ones;
  commits report, answers, files, and outbox work atomically.
- **Worker**: sends each answered field in the right `report_content` or
  `private_context` section, calls the model once, and accepts only strict
  nonblank English/French JSON.
- **Anonymity**: a synthetic private identity repeated in narrative becomes the
  exact role in both languages with no fragment left; eligible safety facts
  survive.
- **Attachments**: count, size, and type checks stream safely; image and video
  derivatives remove metadata; documents are kept as unchanged originals and never
  reach AI. One is public only as a short-lived forced download under a
  server-minted name, when validated, unhidden, and `consent_documents` is true
  (ADR-0119).
- **Publication**: consent, non-deletion, and current pair approval are all
  required; editing clears approval; soft deletion stops every flow.
- **Logs**: credentials, report content, model payloads, client filenames, and
  URLs never enter logs or exceptions.

## Test containers

- Pin each image to a version, from an upstream that is still maintained and
  publishes CI's architectures. A pinned tag guards against change, not
  disappearance.
- A pull that fails in CI but passes locally: suspect the local cache first.
  `docker rmi` and pull again, or query the registry's token endpoint
  anonymously, before blaming the change
  ([lesson 0014](../../docs/lessons/0014-a-local-image-cache-hides-a-withdrawn-upstream.md)).
- The S3-compatible server is pinned once, in `tests/Shared/S3Emulator.cs`
  ([ADR-0110](../../docs/decisions/ADR-0110-rustfs-replaces-minio-as-the-development-s3-server.md)).
