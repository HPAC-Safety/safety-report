# Testing conventions

## Shouldly, always

Not `Assert.*`, not FluentAssertions. Shouldly is pinned in
`Directory.Packages.props`, and **`Xunit.Assert` is banned outright** — using it
is a build error, in the editor and in a local `dotnet build`, not just in CI:

```
error RS0030: The symbol 'Assert' is banned in this project: Use Shouldly.
value.ShouldBe(expected), not Assert.Equal(expected, value).
```

The reason is legibility in a log. Shouldly names the expression under test in
its failure message, which matters when the reader is an agent reading CI output
rather than a person sitting at a debugger:

| | on failure |
|---|---|
| `summary.Text.ShouldNotContain("Vince")` | prints the text that contained it |
| `Assert.False(summary.Text.Contains("Vince"))` | `Expected: False, Actual: True` |

The mechanism is `Microsoft.CodeAnalysis.BannedApiAnalyzers` reading
[`tests/BannedSymbols.txt`](../tests/BannedSymbols.txt), wired up in
`Directory.Build.props` for every `*.Tests` project. Add future bans to that
file. Why an analyzer rather than a CI grep:
[ADR-0013](decisions/ADR-0013-ban-assert-rather-than-grep-for-it.md).

If a test ever legitimately needs it, `#pragma warning disable RS0030` with a
comment saying why. That is visible in review, which is the point.

```csharp
summary.Text.ShouldNotContain("403-555-0134");
result.Status.ShouldBe(ReportStatus.PendingReview);
Should.Throw<TurnstileVerificationException>(() => verifier.Verify(token));
```

## Given / When / Then

In the test name and marked in the body, in that order:

```csharp
[Fact]
public async Task Given_a_report_naming_the_pilot_When_the_summary_is_generated_Then_the_name_is_absent()
{
    // Given
    var report = ReportBuilder.Default().WithDescription("Sarah spiralled in from 200 feet");

    // When
    var summary = await _pipeline.RunAsync(report);

    // Then
    summary.Text.ShouldNotContain("Sarah");
}
```

It reads as a sentence in test output, which is exactly what a reviewer sees.

JavaScript uses `node:test` with nesting that produces the same sentence:

```js
describe('Given a browser advertising fr-CA', () => {
  describe('When the locale is resolved', () => {
    it('Then it selects fr-CA', () => { /* ... */ })
  })
})
```

## Projects

| Project | Scope |
|---|---|
| `HpacSafety.Core.Tests` | Pure unit. No database, no network. The deterministic scrub lives here. |
| `HpacSafety.Api.Tests` | `WebApplicationFactory` + Testcontainers Postgres |
| `HpacSafety.Worker.Tests` | Outbox claiming, retry, poison handling; recorded model fixtures |
| `HpacSafety.Anonymization.Tests` | Golden-file PII suite |
| `tests/js` | `node --test` for i18n, api-client, form logic |
| `tests/e2e` | Playwright, both locales |

### Integration tests need Docker

`HpacSafety.Api.Tests` starts a real PostgreSQL container through
Testcontainers, pinned to `postgres:17-alpine` — a database version that moves
underneath the suite is a failure nobody can reproduce.

Those tests carry `[Trait("Category", "Integration")]`, so a machine without a
running Docker daemon can skip them:

```bash
dotnet test --filter "Category!=Integration"
```

CI runs everything. Do not make the skip automatic: a test that silently skips
itself is a test that stops running and nobody notices.

## Testing anything that calls a model

**Never assert on an exact generated sentence.** Models drift and the test
becomes noise that gets muted, which is worse than no test.

Assert on:

- **Absence of a specific identifier** — the phone number, the name, the site.
- Structural properties — language, length bounds, required sections present.
- Behaviour around the call — retry, backoff, what happens on a failure.

Recorded fixtures rather than live calls, so the suite runs on a fork PR with no
API key.

## Coverage

`coverlet.collector` plus ReportGenerator on .NET,
`node --test --experimental-test-coverage` on JS, merged into one Cobertura
report.

- CI fails below **80% line / 70% branch**.
- It **ratchets**: the threshold may rise, never fall, and CI compares against
  the `main` baseline so a PR cannot quietly dilute it.
- Bootstrap, migrations, and DTOs are excluded so the number reflects logic.

Coverage is a floor, not a goal. This repository could sit at 95% and still
publish someone's phone number — the anonymization suite is what actually
protects a reporter. The PR template asks what behaviour a new test pins down,
not whether the number went up.

## Related

- `skills/hpac-safety-conventions/SKILL.md`
- `docs/anonymization-policy.md`
