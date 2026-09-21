# Tests

CI runs .NET unit/integration/contract tests, JavaScript tests, and browser
journeys. Use xUnit, Shouldly, Given/When/Then structure, Testcontainers,
`node:test`, and Playwright.

```bash
dotnet test HpacSafety.slnx
dotnet test HpacSafety.slnx --filter "Category!=Integration"
node --test $(find tests/js -name '*.test.mjs')
npx playwright test
```

Integration tests require Docker. Use deterministic model fakes and synthetic
identities, reports, locations, and attachments; never commit real report data.

`HpacSafety.Acceptance.Tests` runs the `features/**/*.feature` scenarios
directly via Reqnroll ([ADR-0049](../docs/decisions/ADR-0049-reqnroll-for-executable-gherkin-scenarios.md)),
as part of the same `dotnet test HpacSafety.slnx` run. A scenario carries
`@ignore` until its behavior is implemented; implementing it means writing its
step definitions and removing that tag in the same PR.

A scenario tagged `@ui` is the exception: its step definitions are TypeScript
in [`e2e/steps`](e2e/steps) and it executes under `npx playwright test`, never
here ([ADR-0053](../docs/decisions/ADR-0053-ui-scenarios-execute-via-playwright-bdd.md)).
The acceptance suite skips every `@ui` scenario itself, so the bare
`dotnet test HpacSafety.slnx` above is the whole command — no category filter
is needed to get a true result
([ADR-0073](../docs/decisions/ADR-0073-a-ui-scenario-is-skipped-by-reqnroll-itself.md)).

Target tests protect complete immutable questions, consent-only required
behavior, final multipart mapping and atomicity, rate limiting, one
strict bilingual model call, whole-identity role replacement, image/video
derivatives, private non-anonymized documents, authentication/audit, pair
approval, universal soft deletion, and exact public DTO allowlists. See
[`../docs/testing-and-quality.md`](../docs/testing-and-quality.md).

Some current tests intentionally describe the legacy schema, field encryption,
pre-submit storage, or multi-stage AI design. Update or remove those tests when
the owning target migration is implemented; do not preserve obsolete behavior
merely to keep a historical test green.
