---
title: An answer names its choice, and a picker option is fixed in place or replaced
description: A single-select, multi-select, or type-ahead answer references the choice it was given under by ID and copies no text; an Administrator editing a picker option either fixes it in place for every answer or replaces it with a new option, leaving old answers on the old one.
type: adr
status: accepted
date: 2026-09-25
decision-makers: Chase Florell
keywords: choices, answers, picker, single-select, multi-select, type-ahead, choice ID, replace, fix in place, conditional questions, migration, ADR-0072, ADR-0095, ADR-0112
---

# ADR-0128 — An answer names its choice, and a picker option is fixed in place or replaced

## Status

Accepted. This ADR:

- **partially supersedes**
  [ADR-0072](ADR-0072-every-answer-is-stored-as-a-string.md): its select rows
  (a picker, multi-select, or type-ahead answer stores the label as shown and
  points at nothing) and its rejection of "keep option codes". Every other
  answer type is still one string.
- **partially supersedes**
  [ADR-0112](ADR-0112-only-answers-that-need-it-get-a-second-language.md): a
  choice answer no longer copies its choice's other-language label at
  submission. Its other rows stand.
- **amends**
  [ADR-0095](ADR-0095-a-question-owns-its-choices-outside-its-revisions.md):
  "rewording a choice in place" becomes the Administrator's choice between
  fixing and replacing, below. Its other rules stand.
- **amends**
  [ADR-0074](ADR-0074-a-single-select-parent-may-enable-a-conditional-question.md):
  a condition names its parent's choice by ID and follows a replacement.
- **amends**
  [ADR-0071](ADR-0071-an-answered-question-forks-instead-of-revising.md): a
  fork's copied choices are new rows; answers to the retired question keep
  naming its rows.
- **amends** AGENTS.md invariant 1 ("Choices sit outside revisions",
  "Answers", "Second language").

[ADR-0129](ADR-0129-a-type-ahead-value-is-edited-in-place-merged-and-reviewed.md)
decides what editing, merging, and reviewing a type-ahead value means. This
ADR decides how any choice answer is stored and how a picker option changes.

## Context

ADR-0072 stored a picker answer as the label the reporter saw, so that a
relabelled or removed option could never reach an old answer. It worked by
copying: two answers naming the same option hold two copies of its words, and
nothing links either copy to the option.

That makes two things the owner wants impossible:

- **Knowing which option an answer names.** A reviewer cannot ask "who picked
  baz", only "whose stored text reads baz", and a French and an English answer
  naming the same option look unrelated.
- **Correcting an option for everybody.** A misspelled option fixed in the
  editor stays misspelled in every answer that copied it, and a type-ahead
  value (ADR-0129) can never be corrected or merged.

The owner ruled (#477) that an answer references its option, and that an
option a reporter answered is kept, with its original identity, forever.

## Decision

### An answer names its choice by ID

- **A single-select, multi-select, or type-ahead answer references one row of
  its question's `question_choices` by ID.** It stores no label. A multi-select
  stores one answer row per chosen choice, as today.
- **Both languages are read from the choice.** The label in the reporter's
  language and the one in the other language come from the choice row whenever
  the answer is read: reviewer view, Worker model input and marking pass,
  conditional checks, and export. Nothing is copied at submission, and no
  translation provider is involved.
- **A choice an answer names is never erased.** Removing it is a soft delete
  (ADR-0095): it stops being offered, and every answer that names it still
  resolves to it and shows its label.
- **Yes/no and checkbox answers are not choices.** They stay words in the
  reporter's language
  ([ADR-0127](ADR-0127-a-yes-or-no-answer-is-stored-in-the-reporters-language.md)).
- **The submission names choices by ID.** A picker or multi-select answer
  carries the chosen choices' IDs, and the API accepts only live choices of
  that question. A type-ahead answer carries either a choice's ID or, for a
  value the question does not offer, the text the reporter typed
  (ADR-0129).

### A picker option is fixed in place or replaced

When an Administrator edits the wording of an existing single-select or
multi-select option, they choose which edit it is:

- **Fix in place.** The same choice, with the same ID, gets the new wording.
  Every answer that names it, past and future, shows the fix. This is for a
  typo, an accent, or a translation correction: the option still means what it
  meant.
- **Replace.** The old choice is retired: soft-deleted, still named by every
  answer given under it, with its words unchanged. A new choice with a new ID
  takes its place in the list, and the retired one records which choice
  replaced it. This is for a change of meaning: anyone who answered "baz"
  answered "baz", forever.

Both languages are still authored together (AGENTS.md invariant 1). Adding,
reordering, and removing options work as ADR-0095 says. Neither edit revises
or forks the question.

### A condition follows a replacement

- A conditional question names its single-select parent's required choice by
  ID, not by code.
- When that choice is replaced, the condition follows the replacement link to
  the new choice. The dependent question is not revised: its saved dependency
  still names the old choice, and every reader resolves it through the link.
- A choice a live question depends on still cannot be removed without a
  replacement (ADR-0095).

### A fork copies choices as new rows

A fork (ADR-0071) gives the replacement question its own copy of every choice,
with new IDs and the same codes, removed and reporter-added marks included.
Answers to the retired question keep naming the retired question's rows. A
dependency is carried to the copy with the same code, as today.

### Existing answers are linked, not rewritten

The migration that adds the reference fills it for every existing select and
type-ahead answer, by matching the stored label and language against its
question's choices. An answer whose label matches no choice, because its
option was later relabelled or removed, gets a removed choice carrying that
label in that language, so every old answer resolves. The stored text is left
in place: answers are immutable (ADR-0080).

## Rejected alternatives

- **Keep storing the label (ADR-0072).** It cannot tell which option an answer
  names, and nothing can be corrected for everybody.
- **Store the ID and a copy of the label.** Two records of the same fact
  diverge, which is why ADR-0072 rejected keeping codes beside labels. Once an
  option is immutable when it matters (replace), the copy guards nothing.
- **Always replace on any wording change.** A misspelled option could then
  never be fixed for the answers that already name it.
- **Always fix in place.** A change of meaning would silently rewrite what
  earlier reporters answered.
- **Rewrite the dependent question when its parent choice is replaced.** An
  answered dependent would fork (ADR-0071) for an edit its Administrator never
  made.
- **Reference choices by code.** A code is derived from wording and is
  per-question; an ID is the row's identity and survives both fix and replace.

## Consequences

- `report_answers` gains a choice reference; `question_revisions` replaces
  `depends_on_option_code` with a choice reference; `question_choices` gains a
  replaced-by link.
- A choice answer's `translated_value` is no longer written; its
  `translation_mode` stays `choice`, meaning "read from the choice".
- A report can be read back exactly as its reporter answered only when options
  were replaced, not fixed. That is the point of the choice between the two.
- The submission contract's multi-select field carries IDs, not labels.
- The report form must send choice IDs, and read a one-language choice in the
  language it has (ADR-0095).

## Related

- [ADR-0129](ADR-0129-a-type-ahead-value-is-edited-in-place-merged-and-reviewed.md) — type-ahead values.
- [ADR-0072](ADR-0072-every-answer-is-stored-as-a-string.md), [ADR-0112](ADR-0112-only-answers-that-need-it-get-a-second-language.md) — partially superseded.
- [ADR-0095](ADR-0095-a-question-owns-its-choices-outside-its-revisions.md), [ADR-0074](ADR-0074-a-single-select-parent-may-enable-a-conditional-question.md), [ADR-0071](ADR-0071-an-answered-question-forks-instead-of-revising.md) — amended.
- [ADR-0127](ADR-0127-a-yes-or-no-answer-is-stored-in-the-reporters-language.md) — yes/no stays a word.
