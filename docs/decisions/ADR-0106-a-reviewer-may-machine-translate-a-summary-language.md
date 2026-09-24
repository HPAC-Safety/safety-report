---
title: A reviewer may machine-translate one summary language from the other
description: While editing a summary pair, a safety officer or administrator may draft one language from the other through the translation port, confirmed against a diff, and each saved language records whether it was generated, human-written, or machine-translated.
type: adr
status: accepted
date: 2026-09-23
decision-makers: Chase Florell
keywords: translation, summary, DeepL, reviewer, provenance, diff, ADR-0062, ADR-0080, ADR-0105
---

# ADR-0106 — A reviewer may machine-translate one summary language from the other

**Status:** Accepted. Amends the product invariant that machine translation
"never touches a summary", and widens
[ADR-0062](ADR-0062-administrators-may-machine-translate-question-text.md)'s
translation endpoint from administrators to reviewers.

## Context

A reviewer correcting a summary usually fixes one language. Retyping the same
correction in the other language is slow and error-prone, and the pair must
stay aligned (REQ-MOD-032). The translation port (`ITranslator`, DeepL) already
exists for question authoring (ADR-0062) and answer translation (ADR-0080), but
it was reachable only by administrators, and the invariant kept it away from
summaries so that the published pair came only from the Worker's anonymized
model call.

## Decision

**A reviewer may draft one summary language from the other.** In the summary
editor, **Translate to French** is offered once the English text was changed,
**Translate to English** once the French text was changed, and both when both
were. A language filled by an accepted translation does not count as changed.
The same buttons appear when a pair is written by hand after a failed
summarization.

**Nothing is overwritten silently.** The editor shows the current text and the
proposed translation with their word-level differences marked, and replaces the
field only when the reviewer accepts. Nothing is saved until the reviewer
saves the pair, which clears approval as any edit does (ADR-0105).

**Reviewers may reach the translation port.** `/api/admin/translate` moves from
the Administrator policy to the Reviewer policy (SafetyOfficer or
Administrator). A User is still refused. The text sent is the reviewer's own
summary draft, which is already anonymized; no raw report content is sent by
this feature.

**Each saved language records how it was produced.** `summaries` gains
`source_en` and `source_fr`: `generated` (the Worker's model call), `human`
(typed or edited by a reviewer), or `machine` (an accepted translation).
Existing rows are backfilled as `generated`, or `human` where the pair's model
is `manual`. A language the reviewer did not change keeps its source; a changed
language is `human` unless it was filled by an accepted translation and not
typed in since.

## Rejected alternatives

- **Translating automatically on save.** Hides a machine-written language
  behind a human save, and would overwrite a deliberate edit in the other
  language.
- **Asking the summarization model to re-translate.** A second model call
  outside the one-call contract, and it would re-run anonymization on already
  anonymized text.
- **One source for the whole pair.** Loses exactly the fact a later reviewer
  needs: which language a person wrote.

## Consequences

- REQ-QB-023 ("Only an Administrator may translate") is deleted; REQ-MOD-069
  replaces it.
- REQ-MOD-070..074 specify the sources, the buttons, the diff confirmation, the
  manual-pair case, and the report view's labels.
- The generated pair still comes only from the Worker's one call; nothing
  publishes a translation without a reviewer saving and approving it.
