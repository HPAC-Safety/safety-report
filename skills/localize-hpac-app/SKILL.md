---
name: localize-hpac-app
description: Preserve HPAC safety-report's English/French parity, localization boundaries, and safe CI translation workflow. Use when changing user-facing strings, locale or provenance files, glossary pins, question wording, invariant domain codes, summaries, translation providers, locale detection, seed wording, i18n workflows, or end-to-end journeys.
---

# Keep both official languages complete

Treat English and French as two first-class halves of the application. A change
that works in only one locale is incomplete. Read `docs/localization.md` and,
for question or summary data, [`incident-domain-model`](../incident-domain-model/SKILL.md).

## Separate UI chrome from authored content

- Put every user-facing UI string, including `aria-label`, validation,
  rejection, and email subject text, in `locales/en-CA.json` and reference a
  stable key.
- Let CI generate `locales/fr-CA.json` and `locales/fr-CA.meta.json`; never
  hand-edit either. Fix the English source or pin the term in
  `locales/glossary.json`.
- Return invariant error codes from the domain and localize them at the edge.
  Developer-facing exception messages stay English and must not contain user
  input.
- Store domain values as invariant codes, never display text.
- Store question and option wording per locale in the database. Translate it at
  authoring time in either direction through `ITranslator` — an administrator
  types one language and the next step fills the other before the row is
  saved; do not require both by hand, and do not put question wording in
  `locales/`.

## Preserve parity guarantees

- Never interpret `is_source` as the canonical locale. It records which locale
  a human authored first.
- Do not activate a question without its counterpart. Mark machine-translated
  wording instead of hiding it.
- Require and approve summaries as an EN/FR pair. Never publish one while the
  other waits.
- Apply the same LLM anonymization policy in both languages. Private context is
  available only to the source-language summarizer; it never reaches the
  translation provider.
- Exercise end-to-end flows in both locales.

Report content and private context remain in the submitted language and are
never translated. Only an LLM-anonymized source summary may cross the
translation boundary.

## Generate UI French safely

- Run `translate-locale.mjs --check` on pull requests. It must remain read-only,
  construct no translator, and use no secret. Run generation only from
  `i18n-translate.yml` after a push to `main`; fork-authored code must never
  trigger inference or write a generated locale file.
- Keep DeepL behind `tools/translator.mjs`, the single provider adapter. Target
  `FR-CA`, never metropolitan `FR`. Swap providers through that adapter and
  configuration rather than spreading vendor code through the workflow.
- Remove glossary-pinned keys before calling any provider. Do not substitute a
  provider glossary: sending the source text and accepting machine-composed
  output is weaker than never sending the key.
- Require every translation to preserve the exact set of `{named}` placeholders
  from its English source. Permit reordering for French grammar; fail on missing
  or added placeholders.
- Stamp generated values in `fr-CA.meta.json`, but never let a machine set
  `reviewed: true`. That flag records a human review.
- Permit an absent generated pair only while the tracked `.fr-CA.pending`
  bootstrap marker exists. Remove it in the first generated-locale pull request
  and never recreate it; afterward, missing generated files must fail closed.
- Reject `stub` provenance in `--check`, regardless of workflow configuration,
  so an offline test double cannot reach `main`.

Read ADR-0021, ADR-0022, `docs/localization.md`, and
`.github/workflows/README.md` before modifying this boundary.
