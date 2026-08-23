---
name: test-hpac-safety
description: Apply HPAC safety-report test conventions and privacy-focused test design. Use when adding, changing, or reviewing .NET tests, JavaScript tests, Playwright journeys, fixtures, adapter contract tests, redaction assertions, or coverage-sensitive code.
---

# Test safety behaviour first

Use upstream `test-driven-development`: write the test, watch it fail for the
intended reason, make it pass, then tidy. Read `docs/testing-conventions.md`
before changing the suite.

## Follow the repository test language

- Use Shouldly for every .NET assertion. `Xunit.Assert` is banned by
  `tests/BannedSymbols.txt`; add future bans there.
- Name tests `Given_<scenario>_When_<action>_Then_<assertion>` and mark the
  Given, When, and Then blocks in the body.
- Use Node's built-in `node:test` with nested `describe` blocks that produce the
  same sentence. Reserve Playwright for end-to-end tests.
- Run the end-to-end user journey in both English and French.

## Test guarantees, not implementations

- Define one abstract contract suite per port and run it unchanged against
  every production adapter and development stand-in. A stand-in may not weaken
  the production contract.
- Never commit real report content as a fixture. Invent every name, phone
  number, email, member number, site, and aircraft brand; use RFC-reserved
  domains. A fixture remains in repository history and every clone.
- Assert that redaction and metadata-removal fixtures contained the sensitive
  input before asserting it is absent afterward. A redaction assertion must be
  able to fail.
- Assert the absence of each identifier or identifying token, not an exact
  generated sentence. Exact wording is appropriate only for a human-decided
  invariant such as the pinned English/French role words.
- Generate binary fixtures at runtime. Commit only the smallest synthetic file
  for a format the runtime cannot encode, document its provenance beside it,
  and document how to regenerate it.
- Add architecture tests when a guarantee depends on every caller using one
  enforcement type.
- Assert safety properties such as absence of an identifier, not exact model
  prose that will drift.

Coverage has an 80% line and 70% branch floor plus a ratchet against `main`.
Treat it as a floor, not a target. Never dilute a safety assertion to satisfy a
coverage check.
