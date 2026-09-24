---
title: Only answers that need it get a second language; a picker's comes from its choice
description: Machine translation is limited to free text an administrator marked as needing it; select answers copy their choice's other-language label at submission; every other answer type never gets a second language.
type: adr
status: accepted
date: 2026-09-24
decision-makers: Chase Florell
keywords: translation, answers, DeepL, ITranslator, worker, choices, question bank, ADR-0080, ADR-0072
---

# ADR-0112 — Only answers that need it get a second language; a picker's comes from its choice

**Status:** Accepted. Amends
[ADR-0080](ADR-0080-every-answer-gets-a-worker-translated-second-language.md):
not every answer is machine-translated, and a select answer's second language
is no longer machine-made. Amends the product invariant that "there is no
answer type this ever skips".

## Context

ADR-0080 sent every answer with a value to DeepL. On a real report, that means
first and last names, phone numbers, email addresses, dates, and times all go
to a translation provider, and the admin report view prints a "Translation:"
line under each that repeats it. None of those values has a second language.
Sending them costs money, puts private contact details in front of one more
processor, and makes a reviewer read every value twice.

Select answers had the opposite problem. Their choices are already written in
both official languages when an administrator authors the question (with the
editor's Translate button as a drafting aid, ADR-0062). Machine-translating
the English label of a choice produces a guess at a French label that already
exists in the database.

## Decision

**How an answer gets its second language is decided by its question, and
recorded on the answer at submission.** Each `report_answers` row carries a
`translation_mode`:

| Mode | Applies to | Second language |
|---|---|---|
| `machine` | long or short text whose revision is marked **Auto-translate answer**; a type-ahead value that names no choice written in both languages; a select value whose choice has one language only | The Worker, through `ITranslator`, as ADR-0080 describes (`auto`) |
| `choice` | a single-select, multi-select, or type-ahead value naming a choice written in both languages | That choice's other-language label, copied at submission (`choice`) |
| `none` | everything else: text not marked, email, phone, date, time, number, yes/no, checkbox, file | None, ever |

**Auto-translate answer is a question-revision field** (`is_translatable`), like required and
private. The editor offers it only for long and short text. It defaults on
for long text and off for short text, and marking any other type as needing
translation is rejected. Changing it revises or, once the question has been
answered, forks the question (ADR-0071), so every answer keeps the setting it
was submitted under.

**Copying a choice's label is not translation.** It is a lookup in the
question's own choices, done by the API as it stores the answer. No
translation provider is called on the submission path, so AGENTS.md's
"machine translation never runs on the submission path" still holds. The
copied label is fixed on the answer: relabelling the choice later does not
rewrite old answers, just as it does not rewrite their `value` (ADR-0072).

**Nothing shows a second language for a `none` answer.** The admin report view
returns none for it, even where an older row still holds one from before this
decision. The Worker never sends it, and the answers-awaiting-translation
queue and the administrator's correction both skip it.

## Rejected alternatives

- **Decide by question type alone, with no checkbox.** Simpler, but short text
  covers both "first name" and "what went wrong, in a sentence". The owner
  wants the administrator who writes the question to say which.
- **Keep the flag on the question, outside revisions, like choices (ADR-0095).**
  Toggling it would then change how answers already given are shown. A
  revision field keeps each answer under the setting it was submitted with.
- **Resolve a picker's other label when the report is displayed.** No stored
  copy, but a later relabel of the choice would silently change what an old
  report says the reporter chose.
- **Translate a type-ahead value by DeepL always**, because a reporter may
  have typed it. When the value names a choice already written in both
  languages, the administrator's wording is better than a machine's; the
  Worker is kept for values that name no such choice.

## Consequences

- `question_revisions` gains `is_translatable`. Existing long-text revisions
  are backfilled true and everything else false.
- `report_answers` gains `translation_mode`, backfilled from each answer's
  revision. Answers already given to select and type-ahead questions are
  backfilled `machine`, because their stored second language came from the
  Worker.
- `translation_source` gains `choice`.
- DeepL sees less report content: only free text an administrator marked,
  and type-ahead values it cannot resolve.
