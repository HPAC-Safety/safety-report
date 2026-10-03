---
title: Choices and type-ahead values
description: Supporting detail for a question's choices: their identity, order and pinning, and the review of type-ahead values reporters add.
type: spec
area: choices-and-type-ahead
prefix: REQ-CTA
---

# Choices and type-ahead values

Supporting detail for [`choices-and-type-ahead.feature`](choices-and-type-ahead.feature)
that doesn't fit Gherkin.

## Choices

A question's choices are not part of any revision. A single-select,
multi-select, or type-ahead question owns one list of choices, edited in
place: adding, changing, pinning, or removing one never creates a revision and
never retires the question, even once it has been answered. A fork carries a
copy of the whole list, removed choices, pins, and reporter-added marks
included, to the replacement
([ADR-0095](../../decisions/ADR-0095-a-question-owns-its-choices-outside-its-revisions.md)).

The list has no order of its own. Wherever choices are shown — the report
form's single-select, multi-select, and type-ahead, the question editor's
options, the required-option control, the type-ahead review page, and a
multi-select answer on a report — they are listed alphabetically in the
reader's language, ignoring accents and case, so the English and French lists
may differ in order. An Administrator may pin a choice **first** or **last**;
by default it is not pinned. The list shows three groups in turn — pinned
first, not pinned, pinned last — each alphabetical, with a separator between
groups. A value a reporter adds is not pinned and takes its alphabetical place
at once. The editor re-sorts its options when it opens, never while the
Administrator is typing
([ADR-0136](../../decisions/ADR-0136-choices-are-listed-alphabetically-in-the-readers-language.md)).

An answer names its choice by identifier and copies none of its wording; both
languages are read from the choice. A removed choice is hidden from the form,
never erased, and every answer that named it still names it and reads its
wording ([ADR-0128](../../decisions/ADR-0128-an-answer-names-its-choice-and-a-picker-option-is-fixed-or-replaced.md)).

- **Picker options** (single-select, multi-select) are authored by an
  Administrator in both languages. Changing an option's wording, they choose
  to **fix it in place** (same choice; every answer reads the fix) or
  **replace it** (the old choice is retired, still named by every earlier
  answer, and a new choice takes its place). A condition naming a replaced
  choice follows it to its replacement ([ADR-0128](../../decisions/ADR-0128-an-answer-names-its-choice-and-a-picker-option-is-fixed-or-replaced.md)).
  A condition also follows its parent when the parent forks: the dependent
  question is not revised, and the form and the editor name the live question
  that replaced the parent and its copy of the choice
  ([ADR-0132](../../decisions/ADR-0132-a-condition-follows-its-parent-through-a-fork.md)).
- **Type-ahead values** are corrected in place for every answer that names
  them, removed by delete, and merged: merging B into A retires B, and
  answers naming B read A without being rewritten. A value a reporter adds is
  flagged for review, offered at once in the language it was typed, and given
  its other language by the Worker. A reviewer
  reviews it ([ADR-0129](../../decisions/ADR-0129-a-type-ahead-value-is-edited-in-place-merged-and-reviewed.md)).
  A merged value's wording is not only resolved at submission: the survivor
  carries it as an **alias**, so the form offers the survivor while a
  reporter is still typing it, with a hint naming the alias, in either
  language and following a chained merge with nothing for the client to
  chase (`REQ-QB-233`–`REQ-QB-236`). The type-ahead review page lists each
  value's aliases too, read-only — there is still no un-merge
  (`REQ-QB-237`, ADR-0129 amendment).

A single-select or multi-select question always keeps at least one live
choice: one with none could not be answered, so saving it, retyping a question
into it without choices, or removing its last choice is refused. A type-ahead
may start with none, because reporters add to it. A Typeform field imported
with no choices opens as a draft the Administrator completes before saving.

## Out of scope

What not to build here. The global list in
[system overview](../../system-overview.md) still holds; this narrows it
to this area ([ADR-0083](../../decisions/ADR-0083-specification-driven-development.md)).

- Shared choice lists, or reusing one question's choices on another in any
  form ([ADR-0095](../../decisions/ADR-0095-a-question-owns-its-choices-outside-its-revisions.md)).
- A reporter editing, curating, or removing a choice. A reporter may add a
  missing value to a type-ahead; a reviewer reviews
  it ([ADR-0129](../../decisions/ADR-0129-a-type-ahead-value-is-edited-in-place-merged-and-reviewed.md)).
- A reporter adding a choice to a single-select or multi-select question.
- A record of exactly which choices a reporter was shown. The answer names the
  choice it was given under ([ADR-0128](../../decisions/ADR-0128-an-answer-names-its-choice-and-a-picker-option-is-fixed-or-replaced.md)).
- Holding a reporter's new type-ahead value back until it is approved. It is
  offered at once and reviewed afterwards ([ADR-0129](../../decisions/ADR-0129-a-type-ahead-value-is-edited-in-place-merged-and-reviewed.md)).
- Merging picker options, or a Safety Officer editing one. A picker option is
  fixed or replaced by an Administrator ([ADR-0128](../../decisions/ADR-0128-an-answer-names-its-choice-and-a-picker-option-is-fixed-or-replaced.md)).
- Replacing a type-ahead value, or un-merging one. A type-ahead value is only
  ever corrected in place ([ADR-0129](../../decisions/ADR-0129-a-type-ahead-value-is-edited-in-place-merged-and-reviewed.md)).
- Rewriting an answer to name a different choice, on merge, replacement, or
  migration. Readers follow the link instead.
- Ordering choices by hand: dragging them, moving them up or down, or letting
  a question choose between the order they were written in and alphabetical
  order. Choices are alphabetical, apart from pinning
  ([ADR-0136](../../decisions/ADR-0136-choices-are-listed-alphabetically-in-the-readers-language.md)).
- Sorting choices on the server by language. The server returns each group in
  a stable order and the reader's browser collates it, because only the reader
  knows their language.
- Copying a choice's wording onto an answer, in either language.
- Sorting of the type-ahead review page other than grouped by question and
  alphabetical within each group; the API's own order is otherwise unchanged.
  What each review action does on the server is unchanged too — only how the
  page renders and refetches its list changed
  ([#651](https://github.com/HPAC-Safety/safety-report/issues/651)).
