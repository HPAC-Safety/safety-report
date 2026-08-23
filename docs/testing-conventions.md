# Testing conventions

Use xUnit and Shouldly for .NET, `node:test` for JavaScript, Playwright for
browser journeys, and Testcontainers for PostgreSQL/storage integration tests.
Name .NET tests `Given_..._When_..._Then_...` and mark those sections in the
body. `Xunit.Assert` is analyzer-banned.

Use synthetic identities, sites, reports, and attachments. Never commit real
report content. Model tests use deterministic fakes or controlled fixtures and
assert schema, privacy properties, and preserved safety facts rather than exact
prose.

Prioritize boundaries described in
[`spec/testing-and-quality.md`](../spec/testing-and-quality.md): immutable
question selection, multipart mapping/atomicity, Turnstile/rate limits, one-call
bilingual output, role replacement with no identity fragments, attachment
derivatives/private documents, authentication/audit, pair approval, soft
deletion, and exact public DTO allowlists.

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
