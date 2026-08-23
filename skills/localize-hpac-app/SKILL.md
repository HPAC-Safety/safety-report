---
name: localize-hpac-app
description: Keep HPAC Safety application chrome, database questions, validation, and bilingual summary behavior aligned in English and French. Use for locale, copy, question, or summary-language changes.
---

# Localize HPAC Safety

- Application chrome lives in reviewed `en-CA` and `fr-CA` catalogues with
  matching keys. CI translation tooling applies only to those stable catalogues.
- Every immutable database question revision stores both English and French
  label/help/option text. Administrators author and review both; no runtime or
  authoring-time translation service fills question text.
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
