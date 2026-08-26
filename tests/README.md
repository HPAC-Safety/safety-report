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

Target tests protect complete immutable questions, consent-only required
behavior, final multipart mapping and atomicity, Turnstile/rate limiting, one
strict bilingual model call, whole-identity role replacement, image/video
derivatives, private non-anonymized documents, authentication/audit, pair
approval, universal soft deletion, and exact public DTO allowlists. See
[`features/testing-and-quality/testing-and-quality.md`](../features/testing-and-quality/testing-and-quality.md).

Some current tests intentionally describe the legacy schema, field encryption,
pre-submit storage, or multi-stage AI design. Update or remove those tests when
the owning target migration is implemented; do not preserve obsolete behavior
merely to keep a historical test green.
