---
name: test-hpac-safety
description: Apply HPAC test conventions and privacy-focused test design. Use when adding, changing, or reviewing .NET, JavaScript, Playwright, adapter-contract, question-privacy, model-input, anonymization, or coverage-sensitive tests.
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
- Test that a new question defaults private, privacy has no public mutation,
  and a recorded answer snapshots the flag. Test the owned partitioner rather
  than reconstructing its behaviour in a Worker test.
- Assert that every private field lands only in `private_context` and every
  eligible fact lands only in `report_content`. Add architecture tests proving
  translation, PII-audit, and public ports cannot receive private context.
- Use recorded provider responses or a controlled model test double for textual
  anonymization; do not recreate model behaviour with a regex scrubber in the
  test suite. Assert each synthetic private token exists in the request and is
  absent from the candidate summary.
- Assert the absence of each identifier or identifying token, not an exact
  generated sentence. Also assert important non-private details survive so an
  empty summary cannot pass. Exact wording is appropriate only for a
  human-decided invariant such as English/French role words.
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
