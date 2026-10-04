---
name: localize-hpac-app
description: Keep HPAC Safety application chrome, database questions, validation, and bilingual summary behavior aligned in English and French. Use for locale, copy, question, or summary-language changes.
---

# Localize HPAC Safety

## Application chrome

- Chrome lives in reviewed `en-CA` and `fr-CA` catalogues with matching keys.
  CI translation tooling applies only to those catalogues.
- Add every new string to `locales/en-CA.json` and read it through `t(...)`;
  never a literal in markup (`tools/web/check-hardcoded-strings.ts` enforces it).
- **Never add or generate `fr-CA.json` keys by hand.** Only
  `i18n-translate.yml` runs `translate-locale.ts --generate` (the CI-translation-opens-a-pull-request and same-repo-pull-requests-translate-in-PR decisions). Correcting an existing French value by hand is allowed: it is
  recorded as a human correction and never machine-translated again
  (the decision that a hand-edited French value is a recorded correction). A developer's `.env` holds a `GEMINI_API_KEY` for the API and
  Worker (the no-translation-stand-in and Gemini-translates-everything decisions), and a dormant `DEEPL_API_KEY` that nothing
  reads while DeepL is unregistered; no local tool uses either to write the
  catalogue.
- `npm run dev` / `npm run build` in `src/web` first run
  `tools/i18n/stub-missing-translations.ts`: a key missing from either file gets
  the other's text prefixed `#` (`#Contact`), visibly untranslated instead of
  silently English, until CI replaces it: on a same-repo pull request's own
  branch, or after merge for a fork's (the local-build-stubs-missing-translations decision).
- A committed `#`-prefixed value fails `translate-locale.ts --check` and must
  never reach `main`.

## Provenance

- A record of where a value came from covers **the value**, not the input it
  was derived from. `fr-CA.meta.json` hashes the French as well as the English,
  so a hand-edited French value is recorded as a correction instead of being
  overwritten on the next run (the decision that a hand-edited French value is a recorded correction; a
  provenance that hashes only one side of a pair misses the edit).
- Apply the same test to any provenance you add: hash what you claim
  authorship of.

## Database questions

- A label is stored without its closing colon; the interface adds it in the
  locale's style (`Label:` / `Label :`), and the editor and API refuse a label
  that ends in one (the label-colon-trim decision).
- Every immutable revision stores English and French label and help text. A
  question's choices live outside its revisions, one editable list per question
  (the choices-outside-revisions decision). Administrators author and review both.
- While authoring, Translate drafts the other language through
  `POST /api/admin/translate`, which calls `ITranslator` server-side. The result
  is an ordinary editable field; Save stays disabled until both languages are
  present (the administrators-may-machine-translate decision).
- Nothing translates a question outside that screen.
- Reporter content is machine-translated only off the submission path, in these
  cases:
  - an answer that needs a second language, by the Worker, once — never by a
    human, and never overwritten (the only-answers-that-need-it and written-once-by-the-Worker decisions);
  - a summary language a reviewer asks to draft from the other (the reviewer-may-machine-translate-a-summary decision);
  - each revision of a member's comment (the members-may-comment decision).
- A select answer naming a choice written in both languages copies that
  choice's other label instead, which is a lookup, not a translation. One
  naming a one-language choice is translated by the Worker (the only-answers-that-need-it decision).

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
- Translate an answer that does not need a second language (the only-answers-that-need-it decision).
- Let a human supply or correct an answer's second language. Only the Worker
  ever writes one, and only once (the written-once-by-the-Worker decision).
- Translate database questions outside the authoring screen.
- Produce the Worker's summary pair with a translation provider. It comes from
  the one model call.
