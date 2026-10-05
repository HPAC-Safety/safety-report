---
title: Tests
description: What each test project covers and the conventions every test in this repository follows.
type: readme
---

# Tests

CI runs .NET unit/integration/contract tests, JavaScript tests, and browser
journeys. Use xUnit, Shouldly, Given/When/Then structure, Testcontainers,
`node:test`, and Playwright.

```bash
dotnet test HpacSafety.slnx
dotnet test HpacSafety.slnx --filter "Category!=Integration"
node --test $(find tests/js -name '*.test.ts')
npm --prefix tests/e2e test   # bddgen, then playwright test
```

Integration tests require Docker. Use deterministic model fakes and synthetic
identities, reports, locations, and attachments; never commit real report data.

`HpacSafety.Acceptance.Tests` runs the `.spec/features/**/*.feature` scenarios
directly via Reqnroll on xUnit v3 ([ADR-0198](../.spec/decisions/ADR-0198-the-dotnet-tests-run-on-xunit-v3.md)),
as part of the same `dotnet test HpacSafety.slnx` run. A scenario carries
`@ignore` until its behavior is implemented; implementing it means writing its
step definitions and removing that tag in the same PR.

A scenario tagged `@ui` is the exception: its step definitions are TypeScript
in [`e2e/steps`](e2e/steps) and it executes under `npm --prefix tests/e2e test`, which runs `bddgen` before
`playwright test`, never
here ([ADR-0053](../.spec/decisions/ADR-0053-ui-scenarios-execute-via-playwright-bdd.md)).
The acceptance project's settings file filters every `@ui` scenario out of a
default run, as CI does, and a hook skips any that slip through, so the bare
`dotnet test HpacSafety.slnx` above is the whole command: no category filter
is needed, and nothing is reported as skipped
([CONV-010](../.spec/conventions/CONV-010-every-acceptance-run-filters-out-ui-scenarios-and-a-hook-skips-any-that-slip-through.md)).

Target tests protect complete immutable questions, consent-only required
behavior, final multipart mapping and atomicity, rate limiting, one
strict bilingual model call, whole-identity role replacement, image/video
derivatives, private non-anonymized documents, authentication/audit, pair
approval, universal soft deletion, and exact public DTO allowlists. See
[`../.spec/testing-and-quality.md`](../.spec/testing-and-quality.md).

Some current tests intentionally describe the legacy schema, field encryption,
pre-submit storage, or multi-stage AI design. Update or remove those tests when
the owning target migration is implemented; do not preserve obsolete behavior
merely to keep a historical test green.
