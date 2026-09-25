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

- Every immutable revision stores English and French label and help text. A
  question's choices live outside its revisions, one editable list per question
  (ADR-0095). Administrators author and review both.
- While authoring, Translate drafts the other language through
  `POST /api/admin/translate`, which calls `ITranslator` server-side. The result
  is an ordinary editable field; Save stays disabled until both languages are
  present (ADR-0062).
- Nothing translates a question outside that screen.
- Reporter content is machine-translated only off the submission path, in these
  cases:
  - an answer that needs a second language, by the Worker (ADR-0112);
  - a summary language a reviewer asks to draft from the other (ADR-0108);
  - each revision of a member's comment (ADR-0114).
- A select answer copies its choice's other label instead, which is a lookup,
  not a translation.

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

- Translate anything on the submission path.
- Translate attachments, documents, or model input.
- Translate an answer that does not need a second language (ADR-0112).
- Translate database questions outside the authoring screen.
- Produce the Worker's summary pair with a translation provider. It comes from
  the one model call.
