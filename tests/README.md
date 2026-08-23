# tests

**Not deployable.** CI runs these on every pull request and merge to `main`.

| Project | Scope |
|---|---|
| `HpacSafety.Core.Tests` | Pure domain and port tests; no database or network |
| `HpacSafety.Api.Tests` | `WebApplicationFactory` and Testcontainers PostgreSQL |
| `HpacSafety.Infrastructure.Tests` | Migrations, mapping, encryption, seed data, storage, and media adapters |
| `HpacSafety.Worker.Tests` | Outbox claim/retry/poison handling and recorded provider behaviour |
| `HpacSafety.Anonymization.Tests` | Question-to-model privacy boundary and anonymization safety properties |
| `js/` | Node built-in test runner |
| `e2e/` | Playwright submission-to-approval journeys in both locales |

## Privacy tests

The anonymization suite does not imitate the model with deterministic regexes.
It protects the contract around the model:

- new questions default private and expose no reclassification path;
- report answers snapshot question privacy;
- `SummarizationInput.Partition` puts non-private fields in `report_content` and
  private fields in `private_context`, never both;
- only the summarizer can accept private context;
- controlled or recorded model cases contain known synthetic identifiers in
  input, omit each identifier from output, and preserve important non-private
  safety details;
- summary-only PII audit, translation, and human approval remain mandatory;
- English and French exercise the same safety properties.

Never commit real report content. Use invented names, reserved-domain contact
details, synthetic sites and brands, and runtime-generated binary fixtures.

## Running

```bash
dotnet test
dotnet test --filter "Category!=Integration"
node --test $(find tests/js -name '*.test.mjs')
npx playwright test
```

The integration suites use Testcontainers and need Docker. Infrastructure tests
share containers but create isolated databases. CI always runs the complete set.

## Conventions

- Use Shouldly; `Xunit.Assert` is analyzer-banned.
- Name tests `Given_..._When_..._Then_...` and mark those sections in the body.
- Test ports with one abstract contract suite run against every adapter.
- Assert generated-output safety properties, not exact prose.
- Prove sensitive input was present before asserting it is absent; also prove
  useful content survived so an empty output cannot pass.
- Generate binary fixtures at runtime except for the smallest documented format
  the runtime cannot encode.

Coverage has an 80% line / 70% branch floor plus a ratchet against `main`. It is
a floor, not a target. See `docs/testing-conventions.md`.
