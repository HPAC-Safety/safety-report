---
name: localize-hpac-app
description: Keep HPAC Safety application chrome, database questions, validation, and bilingual summary behavior aligned in English and French. Use for locale, copy, question, or summary-language changes.
---

# Localize HPAC Safety

- Application chrome lives in reviewed `en-CA` and `fr-CA` catalogues with
  matching keys. CI translation tooling applies only to those stable catalogues.
- Every new chrome string is added to `locales/en-CA.json` and read through
  `t(...)` — never a literal in markup (`tools/check-hardcoded-strings.mjs`
  enforces this). Never hand-author `fr-CA.json`: only CI translates it
  (`DEEPL_API_KEY` lives only in CI, ADR-0021). `npm run dev`/`npm run build`
  in `src/web` run `tools/stub-missing-translations.mjs` first, which fills
  any key missing from either locale file with the other's text prefixed
  `#` — so a new key is visibly untranslated (`#Contact`) rather than
  silently falling back to English, until CI replaces it for real after
  merge (ADR-0054). A committed `#`-prefixed value fails
  `translate-locale.mjs --check` and must never reach main.
- Every immutable database question revision stores both English and French
  label/help/option text. Administrators author and review both. While
  authoring they may press Translate to draft the other language through
  `POST /api/admin/translate`, which calls `ITranslator` server-side; the
  result is an ordinary editable field and Save stays disabled until both
  languages are present (ADR-0062). Nothing translates a question outside that
  screen, and no reporter content — narrative, answer, or summary — is ever
  machine-translated.
- Resolve locale in order: explicit user choice, browser preference, English.
  Persist the explicit choice, set the document language, and keep form answers
  when toggling.
- API problem details and validation messages use stable codes plus localized
  display text without echoing private input.
- The one Worker model call returns both `AiSummaryEn` and `AiSummaryFr`. There
  is no source-summary translation stage or independent language approval.
- Use stable generic role phrases such as “the pilot” / “le pilote” without
  adding gender or identity detail.

Do not translate raw reports, attachments, documents, model input, or database
questions automatically.
