---
title: A term list holds the French translator to a word
description: locales/terms.json names English terms, the French each must become, and the forms it must never become; the CI translator is told every term inline with each request, and --check fails on any French value that uses a forbidden form, whoever wrote it.
type: adr
status: accepted
date: 2026-09-23
decision-makers: Chase Florell
keywords: translation, terminology, DeepL, custom_instructions, glossary, fr-CA, téléverser, ADR-0021, ADR-0022, ADR-0070
---

# ADR-0102 — A term list holds the French translator to a word

## Context

DeepL, the CI translator (ADR-0022), rendered every "upload" in the UI
catalogue as *télécharger*, which Canadian French reads as **download**. #378
corrected nine strings by hand, and ADR-0070 records each correction so the
next run doesn't overwrite it. That protects the nine strings. It doesn't
protect the next English string that says "upload", which DeepL would
translate the same way. Nothing would object: the French would carry a valid
hash, and a person might approve it without noticing.

`locales/glossary.json` pins the official French for a **key**. That's the
right tool for a whole string HPAC words itself, such as the consent question
or the severity scale. It can't express "this word, wherever it appears".

## Decision

1. **Terms live in `locales/terms.json`**, separate from `glossary.json`.
   Each entry names an English term, the French it must become (`fr-CA`), and
   the stems it must never use (`forbidden`). `upload` → *téléverser*, never
   *télécharg…*.
2. **Every translation request carries the terms as instructions.** Each
   term becomes one plain-language sentence: what to use and what never to
   use.
   - DeepL receives the sentences as `custom_instructions`, inline in the
     request. DeepL's documentation lists this parameter for French "and its
     variants".
   - A chat-completions provider receives the same sentences in its system
     prompt.
   - The adapter refuses more than 10 terms, or an instruction longer than
     300 characters, which is DeepL's limit, rather than drop one silently.
3. **`--check` enforces the terms, whoever wrote the French.** When an English
   value uses a term (matched at the start of a word, so "uploads",
   "uploaded", and "uploading" count), its French must not contain a
   forbidden stem. The check is case-insensitive and ignores whether the stem
   is conjugated or used as a noun. This is a correctness rule, not a
   provenance rule. A hand-edited value is still recorded as a correction
   (ADR-0070), but it still has to use the term correctly. No workflow can
   fix a violation, so it blocks even on a branch: a person corrects the
   French by hand.
4. **`--generate` still writes what the provider returned** and warns about
   any violation. The pull request carries the French to a reviewer, and
   `--check` fails on it there.

## Alternatives rejected

- **A stored DeepL glossary** (`glossary_id`, v2 or v3). DeepL documents that
  a glossary with a base target language works with that language's variants
  for `EN`, `PT`, and `ZH` only. It says nothing about using an `FR`
  dictionary with the `FR-CA` target this repository requires, and a mismatch
  there is an HTTP 400 that loses the whole run. A glossary is also account
  state that the job would have to create, find, keep in step with the file,
  and clean up. The owner asked for a glossary; this design keeps the list in
  the repository and passes it inline instead.
- **Replacing the forbidden word after translation.** French conjugates and
  agrees: *téléverser*, *téléversé*, *téléversés*, *téléversement*. A string
  substitution produces French that is wrong in a new way, and nobody reads it
  as a translation.
- **Pinning every string that contains the term in `glossary.json`.** This
  protects only the strings that exist today, which is exactly the gap.
- **DeepL's `context` parameter alone.** Context influences a translation but
  is not an instruction, and nothing would check the result.

## Consequences

- Adding a term is a one-line change to `locales/terms.json`. The next
  `--check` then covers every existing French value too, so adding a term can
  immediately fail on wording that was already merged. That is intended.
- An instruction isn't a guarantee. If DeepL ignores one, the check catches
  it on the translation pull request, and a person corrects it.
- **Unverified until the next real translation run:** whether DeepL accepts
  `custom_instructions` alongside `tag_handling: xml` and `formality` for
  `FR-CA`. If it answers 400, the run fails loudly, as any provider error
  does, and this ADR's DeepL mechanism needs revisiting. #381 tracks it, and
  the translation run itself settles it:
  [ADR-0103](ADR-0103-a-translation-run-reports-to-the-issues-waiting-on-it.md)
  has the run comment on, and close, the issue.
- The server-side `ITranslator`, used by question authoring (ADR-0062) and
  Worker answer translation (ADR-0080), is not held to the term list. Its
  output is either reviewed by an administrator or is an answer's second
  language.
