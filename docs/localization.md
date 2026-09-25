---
title: Localization
description: How Canadian English and Canadian French are kept in step across chrome, questions, and summaries.
type: guide
---

# Localization

HPAC Safety supports Canadian English and Canadian French.

## Application chrome

Stable UI strings live in reviewed `locales/en-CA.json` and
`locales/fr-CA.json` catalogues with matching keys. Existing CI generation may
help prepare a catalogue PR, but runtime pages never call a translation service
and generated French still receives human review.

English is the source of truth and `fr-CA.json` is generated. Editing the
French by hand is not the intended path, but it is possible, so it is
recorded rather than absorbed: an edit whose English is unchanged becomes a
**human correction**, stamped as human-authored and never machine-translated
again. An edit to both languages of one key at once is recorded the same way:
whoever edited both edited both on purpose
([ADR-0070](decisions/ADR-0070-a-hand-edited-french-value-is-a-recorded-correction.md)).

Resolve locale in this order: explicit user selection, browser preference,
English fallback. Persist the explicit selection, set the HTML `lang`, and keep
form answers/revision IDs when switching language.

## Database questions

Each complete immutable question revision stores its English and French label
and help text. A question's bilingual choices live on the question itself,
outside its revisions
([ADR-0095](decisions/ADR-0095-a-question-owns-its-choices-outside-its-revisions.md)).
An answer naming a choice reads both languages from it
([ADR-0128](decisions/ADR-0128-an-answer-names-its-choice-and-a-picker-option-is-fixed-or-replaced.md));
the Worker supplies a reporter-added type-ahead value's missing language
([ADR-0129](decisions/ADR-0129-a-type-ahead-value-is-edited-in-place-merged-and-reviewed.md)).
Administrators provide and review both versions. Question text is not generated
from UI catalogues and is never translated at render time. While authoring, an
administrator may ask for a machine-translated draft and saves only what they
reviewed
([ADR-0062](decisions/ADR-0062-administrators-may-machine-translate-question-text.md)).

## Reports and summaries

A submitted answer is never changed. An answer that needs a second language
gets one beside it, made by the Worker's machine translation off the
submission path
([ADR-0112](decisions/ADR-0112-only-answers-that-need-it-get-a-second-language.md)).
Attachments are never translated. The Worker's one model
call returns both `AiSummaryEn` and `AiSummaryFr` for the same eligible facts.
There is no source-summary translation stage or per-language approval; a safety
officer reviews and approves the pair.

Validation and problem details use stable machine codes plus localized safe
copy. Never echo private input merely to localize an error.

See [`features/web-localization-and-design/web-localization-and-design.feature`](../features/web-localization-and-design/web-localization-and-design.feature).
