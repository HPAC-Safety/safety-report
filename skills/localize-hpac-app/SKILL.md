---
name: localize-hpac-app
description: Preserve HPAC safety-report's English/French parity and localization boundaries. Use when changing user-facing strings, validation or rejection messages, question wording, invariant domain codes, summaries, translation, locale detection, seed wording, or end-to-end journeys.
---

# Keep both official languages complete

Treat English and French as two first-class halves of the application. A change
that works in only one locale is incomplete. Read `docs/localization.md` and,
for question or summary data, [`incident-domain-model`](../incident-domain-model/SKILL.md).

## Separate UI chrome from authored content

- Put every user-facing UI string, including `aria-label`, validation,
  rejection, and email subject text, in `locales/en-CA.json` and reference a
  stable key.
- Let CI generate `locales/fr-CA.json`; never hand-edit it. Preserve terms in
  `locales/glossary.json` exactly.
- Return invariant error codes from the domain and localize them at the edge.
  Developer-facing exception messages stay English and must not contain user
  input.
- Store domain values as invariant codes, never display text.
- Store question and option wording per locale in the database. Translate it at
  authoring time in either direction through `ITranslator`; do not put it in
  `locales/`.

## Preserve parity guarantees

- Never interpret `is_source` as the canonical locale. It records which locale
  a human authored first.
- Do not activate a question without its counterpart. Mark machine-translated
  wording instead of hiding it.
- Require and approve summaries as an EN/FR pair. Never publish one while the
  other waits.
- Apply deterministic redaction in both languages.
- Exercise end-to-end flows in both locales.

Raw reports remain in their submitted language and are never translated. Only
an already-anonymized summary may cross the translation boundary.
