---
name: test-hpac-safety
description: Test HPAC Safety privacy, immutable questions, multipart submission, Worker summarization, attachments, moderation, deletion, and publication. Use for test changes or behavior that needs verification.
---

# Test HPAC Safety

Use xUnit, Shouldly, and `Given_..._When_..._Then_...` names. JavaScript uses
`node:test`; browser journeys use Playwright. Generate synthetic report and file
fixtures and never use real personal data.

Scenarios in `features/**/*.feature` without `@ui` execute directly as
xUnit tests via Reqnroll (`tests/HpacSafety.Acceptance.Tests`, ADR-0049). A
scenario tagged `@ui` asserts browser-observable behavior and executes
instead through `playwright-bdd` in `tests/e2e/steps` — Reqnroll has no
browser to assert against, so it is never the right tool for a `@ui`
scenario (ADR-0053); `.github/workflows/ci.yml`'s `dotnet test` step
excludes `@ui` by category filter so one is never attempted there. An
unimplemented scenario carries `@ignore`; implementing its behavior means
writing its step definitions — Reqnroll for a plain scenario,
`tests/e2e/steps` for an `@ui` one — and removing that tag in the same PR —
never leave a scenario both un-ignored and unimplemented. A scenario
blending a client-observable assertion with a server-authoritative one
splits into an `@ui` scenario and an untagged one rather than carrying both
concerns together.

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
