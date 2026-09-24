---
title: No translation stand-in in any environment; Development needs a real key
description: The Development echo translator is removed. With no DeepL credential, translation is unavailable everywhere, because a stand-in that returns its input unchanged gets stored as an answer's translation.
type: adr
status: accepted
date: 2026-09-24
decision-makers: Chase Florell
keywords: translation, DeepL, EchoTranslator, stand-in, development, ITranslator, ADR-0062, ADR-0080
---

# ADR-0109 — No translation stand-in in any environment; Development needs a real key

**Status:** Accepted. Supersedes the Development stand-in in
[ADR-0062](ADR-0062-administrators-may-machine-translate-question-text.md).
The rest of ADR-0062 stands.

## Context

ADR-0062 gave Development an `EchoTranslator`: with no DeepL credential, the
translation port returned every string unchanged, so the authoring screen's
Translate button worked locally. The screen said the text was copied, and the
only thing the stand-in could fill was a draft an administrator would read
before saving.

[ADR-0080](ADR-0080-every-answer-gets-a-worker-translated-second-language.md)
then gave the same port a second caller, one that nobody reads before it
writes. The Worker translates every answer and stores the result as its
`auto` second language. In Development, with no key, the Worker got the
stand-in too. An English report was then stored with "translations" that
repeated the English word for word, marked `auto`, and shown on the admin
report view as `Translation: …`. Because the Worker only picks up answers
whose second language is still unset, the copy was never replaced once a key
was configured. A developer reading that report sees a translator translating
English into English, not a stand-in.

## Decision

**There is no translation stand-in, in Development or anywhere else.**
`EchoTranslator` is removed. `AddHpacSafetyTranslation` takes no environment
switch and always registers the DeepL adapter. With no credential, that adapter
reports itself unconfigured and refuses to translate:

- the authoring screen's Translate control is disabled and says translation
  is unavailable, as it already did outside Development;
- the Worker's answer-translation message fails, backs off, and retries, and
  after its retries it is marked failed. The answers' second language stays
  unset rather than holding a copy of the first.

The availability response drops its `standIn` flag, and the screen drops its
"copied unchanged" message.

**A developer who wants translation, or a summary, sets a real key.** The API
and Worker read `DEEPL_API_KEY` and `GEMINI_API_KEY` from the environment.
`dev-up.sh` passes the primary checkout's `.env` (gitignored) to Compose, so
the keys are written once and reach every worktree's containers.

## Rejected alternatives

- **Keep the stand-in, but have it write a marked fake** (for example
  `[fr-CA] …`). The marker would still be stored as an answer's translation,
  marked `auto`, and never replaced. It fixes how the copy looks, not the fact
  that a stored translation is fake.
- **Keep the stand-in for authoring only, and give the Worker none.** Two
  registrations of one port, chosen by caller, to keep a local convenience that
  a free DeepL key already provides.
- **Leave the translation unset when the stand-in is in use.** Hides the
  missing key rather than reporting it, and the same effect comes for free
  from having no stand-in.

## Consequences

- Development behaves like an unconfigured production server until the keys
  are set. A report submitted without them has its summary and translations
  marked failed after the retries, and those messages are not re-queued by
  themselves.
- Tests never used the stand-in as a fake: they have their own
  `ITranslator` stubs, which are unchanged.
