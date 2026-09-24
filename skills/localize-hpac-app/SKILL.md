---
name: localize-hpac-app
description: Keep HPAC Safety application chrome, database questions, validation, and bilingual summary behavior aligned in English and French. Use for locale, copy, question, or summary-language changes.
---

# Localize HPAC Safety

## Application chrome

- Chrome lives in reviewed `en-CA` and `fr-CA` catalogues with matching keys.
  CI translation tooling applies only to those catalogues.
- Add every new string to `locales/en-CA.json` and read it through `t(...)`;
  never a literal in markup (`tools/check-hardcoded-strings.mjs` enforces it).
- **Never hand-author `fr-CA.json`.** Only CI translates it; `DEEPL_API_KEY`
  lives only in CI (ADR-0021).
- `npm run dev` / `npm run build` in `src/web` first run
  `tools/stub-missing-translations.mjs`: a key missing from either file gets
  the other's text prefixed `#` (`#Contact`), visibly untranslated instead of
  silently English, until CI replaces it after merge (ADR-0054).
- A committed `#`-prefixed value fails `translate-locale.mjs --check` and must
  never reach `main`.

## Provenance

- A record of where a value came from covers **the value**, not the input it
  was derived from. `fr-CA.meta.json` hashes the French as well as the English,
  so a hand-edited French value is recorded as a correction instead of being
  overwritten on the next run
  ([lesson 0002](../../docs/lessons/0002-provenance-that-hashes-only-one-side-of-a-pair.md),
  [ADR-0070](../../docs/decisions/ADR-0070-a-hand-edited-french-value-is-a-recorded-correction.md)).
- Apply the same test to any provenance you add: hash what you claim
  authorship of.

## Database questions

- Every immutable revision stores English and French label, help, and option
  text. Administrators author and review both.
- While authoring, Translate drafts the other language through
  `POST /api/admin/translate`, which calls `ITranslator` server-side. The result
  is an ordinary editable field; Save stays disabled until both languages are
  present (ADR-0062).
- Nothing translates a question outside that screen, and no reporter content —
  narrative, answer, or summary — is ever machine-translated.

## Runtime behavior

- Resolve locale: explicit choice, then browser preference, then English.
  Persist the explicit choice, set the document language, keep form answers when
  toggling.
- API problem details and validation messages use stable codes plus localized
  display text, never echoing private input.
- The one Worker model call returns both `AiSummaryEn` and `AiSummaryFr`. No
  source-summary translation stage, no independent language approval.
- Role phrases are stable and generic — “the pilot” / “le pilote” — with no
  gender or identity detail added.

## Never

Automatically translate raw reports, attachments, documents, model input, or
database questions.
