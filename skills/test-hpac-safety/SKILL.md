---
name: test-hpac-safety
description: Test HPAC Safety privacy, immutable questions, multipart submission, Worker summarization, attachments, moderation, deletion, and publication. Use for test changes or behavior that needs verification.
---

# Test HPAC Safety

Use xUnit and Shouldly. JavaScript uses `node:test`; browser journeys use
Playwright. Generate synthetic report and file fixtures and never use real
personal data.

Name a C# test as three PascalCase segments joined by single underscores, each
opening with `Given`, `When`, or `Then`
([ADR-0069](../../docs/decisions/ADR-0069-scannable-given-when-then-test-names.md)):

```csharp
// good
GivenMigratedDatabase_WhenActorColumnIsRead_ThenWidenedStringWithLookupIndex

// bad
Given_a_migrated_database_When_the_actor_column_is_read_Then_it_is_a_widened_string_with_a_lookup_index
```

Drop articles and empty predicates (`a`, `the`, `it is`); keep technical terms
exact and keep an auxiliary that carries the voice. Mark the three sections in
the body with `// Given`, `// When`, `// Then` comments. **C# identifiers
only** — `node:test` and Playwright titles are display strings and stay
prose.

Scenarios in `features/**/*.feature` without `@ui` execute directly as
xUnit tests via Reqnroll (`tests/HpacSafety.Acceptance.Tests`, ADR-0049). A
scenario tagged `@ui` asserts browser-observable behavior and executes
instead through `playwright-bdd` in `tests/e2e/steps` — Reqnroll has no
browser to assert against, so it is never the right tool for a `@ui`
scenario (ADR-0053). The acceptance suite skips a `@ui` scenario itself,
through a `[BeforeScenario("ui")]` hook, so one is never attempted there
wherever `dotnet test` runs; `.github/workflows/ci.yml`'s category filter
is a second line of defence, not the mechanism (ADR-0073). An
unimplemented scenario carries `@ignore`; implementing its behavior means
writing its step definitions — Reqnroll for a plain scenario,
`tests/e2e/steps` for an `@ui` one — and removing that tag in the same PR —
never leave a scenario both un-ignored and unimplemented. A scenario
blending a client-observable assertion with a server-authoritative one
splits into an `@ui` scenario and an untagged one rather than carrying both
concerns together.

Write a step definition from the scenario, not from the conversation that
produced it. If a binding needs a fact the scenario does not state, the
scenario is incomplete — amend it rather than encoding the missing fact in C#
or TypeScript
([ADR-0083](../../docs/decisions/ADR-0083-specification-driven-development.md)).

**`@ignore` means "not built yet," never "no longer true."** When a decision
supersedes what a scenario asserts, **delete the scenario** in the pull request
that records the decision. Do not park an obsolete scenario behind `@ignore` —
that leaves the repository stating something false and waiting for an
implementation that will never come, which is exactly the contradiction
[ADR-0047](../../docs/decisions/ADR-0047-feature-files-must-not-contradict-adrs.md)
forbids. Git history preserves the deleted text if the reasoning is ever
needed. `@ignore` is only ever a promise that somebody is coming back to
implement the scenario as written.

Most untagged scenarios execute against the domain directly — no host, no
database. The ones that describe what the API *refuses* boot it instead, via
`BootedApi` in the acceptance project: "the API rejects the operation
regardless of what the UI would have shown" cannot be shown by calling a
domain method. `BootedApi` starts on first use rather than at test-run start,
so a domain-only run still pays nothing.

A Reqnroll step string is a **Cucumber Expression**, not a regex. Parentheses
mean "optional text", so `(User|SafetyOfficer|Administrator)` matches nothing
and the scenario reports as pending. Use a `{word}` parameter. A literal `/`
is alternation and needs escaping as `\/`.

Authentication fixtures mint a real token through the booted host rather than
faking a `ClaimsPrincipal`, so the test exercises the validation production
runs. Cover the three-role matrix at every admin endpoint, the refusal of an
unsigned, foreign-key-signed, tampered, expired, wrong-audience or `alg: none`
token, and the assertion that a stored report holds no reference to the member
who filed it.

Test observable contracts:

- complete question revisions are immutable; latest-revision selection cannot
  resurrect an older active revision; only consent is required;
- unfinished answers/revision IDs stay in browser storage for 15 days and no
  report, file, reserved ID, or database state exists before final submission;
- one multipart request maps answers and file indexes exactly, accepts known
  superseded revisions, rejects unknown/deleted ones, and commits report,
  answers, files, and outbox work atomically;
- the Worker sends answered fields in the correct `report_content` or
  `private_context` section, invokes the model once, and accepts only strict
  nonblank English/French JSON;
- a synthetic private identity repeated in narrative becomes the exact role in
  both languages with no fragment left, while eligible safety facts survive;
- attachment count/size/type checks stream safely; image/video derivatives
  remove metadata; documents remain private originals and never reach AI or
  public output;
- consent, non-deletion, and current pair approval are all required for public
  visibility; editing clears approval; soft deletion stops every flow;
- credentials, report content, model payloads, client filenames, and URLs never
  enter logs or exceptions.

Use deterministic fakes at model and service boundaries. Do not assert exact
generated prose beyond strict schema and required role phrases. Integration
tests use the supported PostgreSQL version through Testcontainers.
