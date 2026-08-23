---
name: anonymize-hpac-reports
description: Safely evolve or audit HPAC safety-report anonymization, deterministic PII scrubbing, sensitive-data flow, summarization and PII-audit prompts, role-word replacement, and published-detail boundaries. Use when code, tests, fixtures, prompts, APIs, logs, exceptions, translations, or documentation could affect what report content reaches a model or published summary.
---

# Protect the anonymization boundary

Read `AGENTS.md`, `docs/anonymization-policy.md`,
`src/HpacSafety.Core/Features/Anonymization/README.md`, and the applicable prompt
versions before changing this pipeline. Load [`test-hpac-safety`](../test-hpac-safety/SKILL.md)
for tests and [`localize-hpac-app`](../localize-hpac-app/SKILL.md) for either
official language.

## Preserve the closed deterministic pass

- Keep `DeterministicScrub` in dependency-free `HpacSafety.Core`: no database,
  network, model, configuration, or production runtime dependency.
- Keep every scrub stage `internal` and assemble the complete chain only in
  `DeterministicScrub`. Do not add an options object, registry, callback, or
  public stage that lets a caller omit protection. Test the assembled chain.
- Pass labelled fields in `ScrubRequest`; do not flatten structured answers
  into one text blob. Treat an unclassified field as Restricted and fail closed.
- Drop direct contact and member fields. Generalize structured locations to the
  province, carry only the already-derived occurrence date/time buckets, and
  never derive an aircraft class deterministically.
- Harvest structured identifiers before scanning narrative patterns so names,
  sites, handles, membership identifiers, manufacturers, and models repeated in
  prose are removed or replaced.
- Replace harvested names with the role vocabulary, not a generic marker. Keep
  French role words uniform masculine so grammatical agreement does not restore
  identifying information. Treat those words as human-decided pinned terms.

ADR-0027 owns the scrub design and its disclosed limits. ADR-0028 owns the role
vocabulary. Update the ADR and policy when accepting or closing a limitation;
do not let documentation promise more than the code proves.

## Keep sensitive text out of side channels

- Never include a raw field, narrative, regex subject, member credential, or
  other user input in logs, exception messages, telemetry, snapshots, or PR
  fixtures. Translate low-level exceptions into content-free domain errors.
- Keep `ScrubbedReport` unforgeable outside the deterministic feature. Model
  ports still accept `string` until issue #61 retypes that boundary; do not add
  another raw-string path, and use the proof value when completing that issue.
- Never translate a raw report. Summarize in its submitted language, translate
  only the already-anonymized summary, and approve both official-language
  versions before publication.
- Review combinations that identify someone in a small flying community even
  when no single value looks like PII: exact dates, sites, unusual aircraft,
  events, occupations, and unique roles.

## Version runtime prompts

Keep runtime redaction and model instructions under `prompts/`, never in this
skill. Add a new prompt version rather than editing an existing one. Compose
each summarization and PII-audit version with the matching redaction-rules
version, preserve absolute language such as “never,” and document the version
relationship in `prompts/README.md`.

## Prove the safety claim

- Follow red-green-refactor for every change. Start with a synthetic fixture
  that demonstrates the exact leak or over-redaction and watch it fail for that
  reason.
- Assert that each sensitive token exists in the input and is absent from the
  result. Assert important non-sensitive details survive so deleting everything
  cannot pass the suite.
- Cover English and French, case and Unicode normalization, punctuation,
  spacing, elision, compact spellings, and ordinary short names or sites.
- Assert exact text only for human-decided vocabulary. Generated prose must be
  tested by safety property rather than sentence shape.
- Keep the Core dependency architecture test proving zero runtime dependencies;
  analyzer-only packages with `PrivateAssets=all` do not weaken that claim.

Run the full anonymization suite and the repository test/coverage gates. Then
run the configured `anonymization-auditor` against the committed diff for any
redaction, prompt, PII, or publication change. Treat a substantiated leak as
blocking; the auditor reports findings but never replaces human approval.
