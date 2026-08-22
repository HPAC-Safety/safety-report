# tests

**Not deployable.** Run in CI on every pull request and on merge to `main`.

| Project | Scope |
|---|---|
| `HpacSafety.Core.Tests` | Pure unit. No database, no network. |
| `HpacSafety.Api.Tests` | `WebApplicationFactory` + Testcontainers Postgres |
| `HpacSafety.Worker.Tests` | Outbox claiming, retry, poison handling; recorded model fixtures |
| `HpacSafety.Anonymization.Tests` | Golden-file PII suite |
| `js/` | `node --test` — i18n, api-client, form logic |
| `e2e/` | Playwright — submit → summarize → review → approve, both locales |

## The one that matters most

`HpacSafety.Anonymization.Tests` is the suite that actually protects a reporter.
Each case is a fixture report seeded with known personal information and an
assertion that the specific token is absent from the output.

Add a fixture **before** changing a redaction rule.

## Running

```bash
dotnet test                              # all .NET suites
node --test tests/js                     # JavaScript units
npx playwright test                      # E2E (needs the stack running)
```

## Conventions

- **Shouldly**, not `Assert.*` — CI fails on a bare `Assert.` here.
- **`Given_..._When_..._Then_...`**, with the sections marked in the body.
- **Never assert on exact model output.** Models drift, the test becomes noise,
  and noisy tests get muted. Assert absence of the identifier, and structural
  properties.
- **Never commit real report content** as a fixture. Invent plausible data.

Full detail: [`docs/testing-conventions.md`](../docs/testing-conventions.md).
