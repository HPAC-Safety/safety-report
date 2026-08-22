# tests

**Not deployable.** Run in CI on every pull request and on merge to `main`.

| Project | Scope |
|---|---|
| `HpacSafety.Core.Tests` | Pure unit. No database, no network. |
| `HpacSafety.Api.Tests` | `WebApplicationFactory` + Testcontainers Postgres |
| `HpacSafety.Infrastructure.Tests` | Adapters. Blob storage against MinIO and the filesystem; EXIF stripping and content sniffing |
| `HpacSafety.Worker.Tests` | Outbox claiming, retry, poison handling; recorded model fixtures |
| `HpacSafety.Anonymization.Tests` | Golden-file PII suite |
| `js/` | `node --test` — the coverage gate, i18n, api-client, form logic |
| `e2e/` | Playwright — submit → summarize → review → approve, both locales |

## The one that matters most

`HpacSafety.Anonymization.Tests` is the suite that actually protects a reporter.
Each case is a fixture report seeded with known personal information and an
assertion that the specific token is absent from the output.

Add a fixture **before** changing a redaction rule.

## Running

```bash
dotnet test                              # all .NET suites — needs Docker
dotnet test --filter "Category!=Integration"   # no Docker daemon
node --test $(find tests/js -name '*.test.mjs')   # JavaScript units
npx playwright test                      # E2E (needs the stack running)
```

`HpacSafety.Api.Tests` starts a real `postgres:17-alpine` container and
`HpacSafety.Infrastructure.Tests` starts a real MinIO one, both through
Testcontainers, so a Docker daemon has to be running. CI always runs the full
set.

## Conventions

- **Shouldly**, not `Assert.*` — `Xunit.Assert` is banned by an analyzer, so it
  is a build error in the editor, not a CI surprise. See
  [`BannedSymbols.txt`](BannedSymbols.txt) and
  [ADR-0013](../docs/decisions/ADR-0013-ban-assert-rather-than-grep-for-it.md).
- **`Given_..._When_..._Then_...`**, with the sections marked in the body.
- **Never assert on exact model output.** Models drift, the test becomes noise,
  and noisy tests get muted. Assert absence of the identifier, and structural
  properties.
- **Never commit real report content** as a fixture. Invent plausible data.
- **One contract suite per port, not one per adapter.** Where a port has a
  production adapter and a development stand-in — `IBlobStore` today — the
  guarantees live in an abstract suite that both subclass, so the stand-in
  cannot quietly be the weaker one. See
  [ADR-0026](../docs/decisions/ADR-0026-presigned-urls-and-private-blob-storage.md).
- **Generate binary fixtures at run time** rather than committing them. The
  EXIF suite builds its own JPEG with GPS tags attached, which is both smaller
  in the repository and impossible to mistake for a real photograph.

## Coverage

`dotnet test --settings ../coverlet.runsettings --collect:"XPlat Code Coverage"`,
merged with ReportGenerator, gated by `tools/coverage-gate.mjs`: an 80% line /
70% branch floor, plus a ratchet that fails if coverage drops below `main`.

Generated code and migrations are excluded — without that the API measures 4.8%
while `Program.cs` is at 100%. See
[ADR-0014](../docs/decisions/ADR-0014-coverage-gate.md).

It is a floor, not a goal. The suite above it is what matters.

Full detail: [`docs/testing-conventions.md`](../docs/testing-conventions.md).
