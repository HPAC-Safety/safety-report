# ADR-0007 — Bilingual, with CI-time translation

**Status:** Narrowed to application UI catalogues by the
[localization specification](../../spec/web-localization-and-design.md).
Database questions are manually bilingual and one model call returns both
summary texts; the question/runtime translation clauses below are superseded.

## Context

HPAC is a national bilingual association and the existing Typeform is titled
"(English)", implying a French counterpart. Both official languages must be
first-class.

## Decision

`en-CA` and `fr-CA`. Detection order: query param, stored preference,
`navigator.languages`, then English. A visible toggle is always present.

**No hardcoded user-facing strings anywhere** — one shared set of JSON locale
files consumed by both the web apps and the API, enforced by a CI lint.

French is generated **in CI**, not at runtime, using **GitHub Models** on the
free tier (`permissions: models: read`, no API key) — *retired since; see
ADR-0022*. Change detection is a
content hash per key; only new or changed keys are sent, batched into one
request. The job **opens a PR** rather than pushing to `main`.

Terms in `glossary.json` — the injury severity scale, rating names,
certification classes, the consent question — are pinned and never
machine-translated.

Raw reports are never translated. Summaries are generated in the report's own
language and then translated, so both versions exist for every report.

## Alternatives

- **Runtime translation.** Per-visit latency and cost, a third-party request
  from a reporter's browser, and French that cannot be reviewed in a diff.
- **DeepL.** Better raw French, but adds a vendor and a key for a job that runs
  a few times a month.
- **`actions/ai-inference@v3`.** Officially blessed, but v3 is Copilot-CLI-only
  and needs a Copilot seat — not free.
- **Human translation only.** Correct but blocking; the machine draft plus human
  review gets the same result without stalling development.

## Consequences

- "Serious injury (secondary medical aid)" is close to a defined term, so its
  French wording must come from HPAC, not a model.
- The worker cannot use GitHub Models — that free tier depends on the Actions
  token, which does not exist in production. It translates through the Anthropic
  client it already holds. One `ITranslator`, two registrations.
- `reports.language` and one `summaries` row per language are required.
