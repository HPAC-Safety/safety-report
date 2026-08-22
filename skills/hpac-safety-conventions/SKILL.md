---
name: hpac-safety-conventions
description: Repository conventions for HPAC safety-report — Mermaid-only diagrams, Shouldly assertions, Given/When/Then test naming, no hardcoded user-facing strings, PR workflow, and the project layout. Use before writing or reviewing any code, test, or documentation in this repository.
---

# Conventions

## Diagrams — Mermaid, never ASCII

Every diagram in this repository is a Mermaid fence: READMEs, skills, docs,
ADRs, PR descriptions. GitHub renders Mermaid natively, and an agent can edit a
Mermaid graph in a way it cannot edit a box-drawing diagram. CI fails on stray
box-drawing characters in markdown.

## Tests

**Shouldly, always.** Not `Assert.*`, not FluentAssertions. Shouldly's failure
messages name the expression under test, which matters when the reader is an
agent looking at a CI log rather than a person at a debugger.

**Given/When/Then**, in the name and marked in the body:

```csharp
[Fact]
public async Task Given_a_report_with_no_publication_consent_When_it_is_approved_Then_it_is_not_publishable()
{
    // Given
    var report = ReportBuilder.Default().WithConsent(false);
    // When
    await _moderation.ApproveAsync(report.Id);
    // Then
    report.IsPublishable.ShouldBeFalse();
}
```

JavaScript uses `node:test` with nested `describe` blocks producing the same
sentence. Playwright is E2E only.

Coverage is gated at 80% line / 70% branch and ratchets upward — but it is a
floor, not a goal. This repository can be at 95% and still publish someone's
phone number. The anonymization suite is what actually matters.

## Strings

**No user-facing literal ever appears in code.** Not in the admin UI, not in a
validation message, not in an `aria-label`, not in an email subject line. Add a
key to `locales/en-CA.json`.

French is generated in CI and reviewed by a human — never hand-edit
`locales/fr-CA.json`. Terms in `locales/glossary.json` are pinned and must not
be machine-translated.

## Layout

| Path | Contains |
|---|---|
| `src/HpacSafety.Core` | Domain and interfaces. **Depends on nothing.** |
| `src/HpacSafety.Infrastructure` | EF Core, HTTP clients, Anthropic, blob storage |
| `src/HpacSafety.Api` | ASP.NET Core Web API |
| `src/HpacSafety.Worker` | Outbox consumer: summarize, translate, notify |
| `src/web` | Static HTML/JS. No SPA framework, no bundler. |
| `locales/` | The single source of user-facing strings |
| `docs/` | Design, policy, ADRs |
| `skills/`, `agents/` | Sources for `skillfile install` |

If a dependency arrow would point out of `Core`, the abstraction belongs in
`Core` and the implementation in `Infrastructure`.

## Pull requests

- `main` is protected. Every change goes through a PR with one approval from a
  repository administrator.
- Squash merge only; the PR title becomes the commit message.
- One issue per PR where practical.
- The PR template asks whether the change touches anonymization or PII handling.
  Answer it honestly — that question routes reviewer attention.
- No `Co-Authored-By` trailers.
- Do not create a `CODEOWNERS` file. Approver management lives on the repository
  access page precisely so that changing it does not require a PR.

## Generated files — never hand-edit

| File | Regenerate with |
|---|---|
| `docs/form-spec.md` | `tools/extract-typeform.py` |
| `locales/fr-CA.json` | `tools/translate-locale.mjs` (in CI) |
| `.claude/skills/`, `.claude/agents/` | `skillfile install` |
| `src/web/styles/site.css` | `tools/build-css.sh` |

## Related

- `AGENTS.md` — the invariants, which outrank anything here
- `docs/testing-conventions.md`
- `docs/localization.md`
