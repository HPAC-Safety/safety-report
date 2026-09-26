---
title: Question bank and form
description: Supporting detail for the immutable bilingual question and form assembly scenarios.
type: spec
area: question-bank-and-form
---

# Question bank and form

Supporting detail for [`question-bank-and-form.feature`](question-bank-and-form.feature)
that doesn't fit Gherkin.

## Revision fields

Each revision contains:

- a unique revision identifier, stable question key, and monotonically
  increasing revision number;
- English and French label text and optional English and French help text;
- question type;
- form sort order and optional section/group key;
- `is_private`, `is_active`, `is_system`, and `is_required` flags;
- `allow_future_dates`, `false` unless an administrator allows future dates.
  Only a date question may set it, and changing it is a revision like any
  other field
  ([ADR-0138](../../docs/decisions/ADR-0138-a-date-question-allows-future-dates-only-when-it-says-so.md));
- creation timestamp and the revision it supersedes, when any;
- a nullable `deleted` timestamp.

A question's choices are not part of any revision. A single-select,
multi-select, or type-ahead question owns one list of choices, edited in
place: adding, changing, pinning, or removing one never creates a revision and
never retires the question, even once it has been answered. A fork carries a
copy of the whole list, removed choices, pins, and reporter-added marks
included, to the replacement
([ADR-0095](../../docs/decisions/ADR-0095-a-question-owns-its-choices-outside-its-revisions.md)).

The list has no order of its own. Wherever choices are shown — the report
form's single-select, multi-select, and type-ahead, the question editor's
options, the required-option control, the type-ahead review page, and a
multi-select answer on a report — they are listed alphabetically in the
reader's language, ignoring accents and case, so the English and French lists
may differ in order. An Administrator may pin a choice **first** or **last**;
by default it is not pinned. The list shows three groups in turn — pinned
first, not pinned, pinned last — each alphabetical, with a separator between
groups wherever the control can draw one. A type-ahead's suggestions cannot,
so they only keep the group order. A value a reporter adds is not pinned and
takes its alphabetical place at once. The editor re-sorts its options when it
opens, never while the Administrator is typing
([ADR-0136](../../docs/decisions/ADR-0136-choices-are-listed-alphabetically-in-the-readers-language.md)).

An answer names its choice by identifier and copies none of its wording; both
languages are read from the choice. A removed choice is hidden from the form,
never erased, and every answer that named it still names it and reads its
wording ([ADR-0128](../../docs/decisions/ADR-0128-an-answer-names-its-choice-and-a-picker-option-is-fixed-or-replaced.md)).

- **Picker options** (single-select, multi-select) are authored by an
  Administrator in both languages. Changing an option's wording, they choose
  to **fix it in place** (same choice; every answer reads the fix) or
  **replace it** (the old choice is retired, still named by every earlier
  answer, and a new choice takes its place). A condition naming a replaced
  choice follows it to its replacement ([ADR-0128](../../docs/decisions/ADR-0128-an-answer-names-its-choice-and-a-picker-option-is-fixed-or-replaced.md)).
  A condition also follows its parent when the parent forks: the dependent
  question is not revised, and the form and the editor name the live question
  that replaced the parent and its copy of the choice
  ([ADR-0132](../../docs/decisions/ADR-0132-a-condition-follows-its-parent-through-a-fork.md)).
- **Type-ahead values** are corrected in place for every answer that names
  them, removed by soft delete, and merged: merging B into A retires B, and
  answers naming B read A without being rewritten. A value a reporter adds is
  flagged for review, offered at once in the language it was typed, and given
  its other language by the Worker. A Safety Officer or an Administrator
  reviews it ([ADR-0129](../../docs/decisions/ADR-0129-a-type-ahead-value-is-edited-in-place-merged-and-reviewed.md)).

A single-select or multi-select question always keeps at least one live
choice: one with none could not be answered, so saving it, retyping a question
into it without choices, or removing its last choice is refused. A type-ahead
may start with none, because reporters add to it. A Typeform field imported
with no choices opens as a draft the Administrator completes before saving.

The two consent questions are always private. Their answers are left out of
summary input by their key, not by their privacy
([REQ-AI-009](../ai-anonymization/ai-anonymization.feature)); privacy is the
second guard, and an edit cannot remove it.

## Current form query

The query the API uses to assemble the form is a read DTO; it does not expose
persistence entities. It includes the revision ID, key, type, section, flags,
bilingual copy, and bilingual options needed to render and validate the form.
The response carries both translations so a locale toggle never has to replace
the question identities already shown.

## Question types

The answer shapes are short text, long text, email, phone, date, time, number,
single select, multi-select, type-ahead, yes/no, checkbox, and file upload. A
statement and a group are display-only and produce no answer.
Dropdowns versus radio buttons are presentation choices for the same
single-select domain type.

A statement is shown to administrators as **Instructional text**. It is a
title and a description, not a question and help text, so the editor labels
its wording that way and gives each description several lines (`REQ-QB-141`).
Both still live in the revision's label and help-text fields; only the editor's
labels differ by type. The description is stored exactly as typed, line breaks
included (`REQ-QB-142`), and the reporter's form shows it with its paragraphs
wherever the statement appears: as the introduction, on a page of its own, or
under a group (`REQ-QB-143`).

The Typeform-derived question set is seed/import input, not hardcoded form
logic. The database remains authoritative after initial seeding.

## Correcting seeded wording

The seeded attachment question departs from Typeform on purpose. Typeform took
one file and asked for the rest by email. This form takes several, so the
question reads "Photos or videos:" and asks for photos, videos, or documents
(`REQ-QB-104`).

A database seeded before that change is corrected by a migration that follows
the same rule as an Administrator's edit. An unanswered question gets a new
revision (`REQ-QB-105`), and an answered one forks (`REQ-QB-106`). The
migration acts only while the question still carries the exact seeded wording,
so it never overwrites an Administrator's own edit (`REQ-QB-107`).

## The group page contract

A `group`-typed question and its `grouped_under_question_id` children render
together as one page/step, not as separate steps: `fieldset`/`legend` around
the group's own label and the input controls for each visible child, in
revision order. A child renders inside its group's page even if it also
carries its own conditional dependency — grouping and conditional dependency
are independent (ADR-0076) — and the group page itself is skipped only if
every one of its children is currently hidden by an unmet condition.

## Current implementation divergence

Main currently has a stable `Question` whose order, active flag, privacy, and
role can change, while wording/type/options live below `QuestionVersion`. It
also projects several ordinary answers onto typed report properties. Both
choices are superseded by the complete-revision model in the feature file. See
[implementation status](../../docs/implementation-status.md).

## Out of scope

What not to build here. The global list in
[system overview](../../docs/system-overview.md) still holds; this narrows it
to this area ([ADR-0083](../../docs/decisions/ADR-0083-specification-driven-development.md)).

- A general-purpose form builder: scoring, surveys, quizzes, form templates, or
  arbitrary branching. A question may be conditional on a yes/no question or on
  a single-select question naming a required option, and that is the whole of
  it ([ADR-0060](../../docs/decisions/ADR-0060-conditional-questions-depend-on-a-boolean-question.md),
  [ADR-0074](../../docs/decisions/ADR-0074-a-single-select-parent-may-enable-a-conditional-question.md)).
- Machine translation on the submission path. Translation is administrator-
  initiated while authoring, or Worker-run off the submission path
  ([ADR-0080](../../docs/decisions/ADR-0080-every-answer-gets-a-worker-translated-second-language.md)).
- A translate button on each choice, or keeping a brand name untranslated.
  Translate choices fills every one-language choice's missing side in one
  press, never overwrites a written side, and the administrator corrects the
  draft before saving (`REQ-QB-154`–`REQ-QB-158`).
- Mutating a revision, reviving a retired question, or any edit that loses the
  wording an answer was given against.
- Saving a question in one language.
- Accepting a date, time, yes/no, or checkbox answer in any shape but its
  stored form, then converting it. The API refuses it instead (`REQ-QB-118`):
  no seconds on a time, no locale date format, no prose, and no string —
  `yes`, `oui`, or `true` — for a yes/no or checkbox, which is a JSON boolean.
  The form's own inputs already send the stored form (ADR-0072, ADR-0130).
- Storing a yes/no or checkbox answer as words, or giving it a second
  language. It is `true` or `false` in `value_boolean`; only the interface
  turns it into Yes / Oui or No / Non
  ([ADR-0130]../../docs/decisions/ADR-0130-a-yes-or-no-answer-is-stored-as-a-boolean.md)).
- Rewriting any other answer. Converting the stored yes/no words to booleans
  (`REQ-QB-137`) was a one-time migration, not a precedent.
- Shared choice lists, or reusing one question's choices on another in any
  form ([ADR-0095](../../docs/decisions/ADR-0095-a-question-owns-its-choices-outside-its-revisions.md)).
- A reporter editing, curating, or removing a choice. A reporter may add a
  missing value to a type-ahead; a Safety Officer or an Administrator reviews
  it ([ADR-0129](../../docs/decisions/ADR-0129-a-type-ahead-value-is-edited-in-place-merged-and-reviewed.md)).
- A reporter adding a choice to a single-select or multi-select question.
- A record of exactly which choices a reporter was shown. The answer names the
  choice it was given under ([ADR-0128](../../docs/decisions/ADR-0128-an-answer-names-its-choice-and-a-picker-option-is-fixed-or-replaced.md)).
- Holding a reporter's new type-ahead value back until it is approved. It is
  offered at once and reviewed afterwards ([ADR-0129](../../docs/decisions/ADR-0129-a-type-ahead-value-is-edited-in-place-merged-and-reviewed.md)).
- Merging picker options, or a Safety Officer editing one. A picker option is
  fixed or replaced by an Administrator ([ADR-0128](../../docs/decisions/ADR-0128-an-answer-names-its-choice-and-a-picker-option-is-fixed-or-replaced.md)).
- Replacing a type-ahead value, or un-merging one. A type-ahead value is only
  ever corrected in place ([ADR-0129](../../docs/decisions/ADR-0129-a-type-ahead-value-is-edited-in-place-merged-and-reviewed.md)).
- Rewriting an answer to name a different choice, on merge, replacement, or
  migration. Readers follow the link instead.
- Ordering choices by hand: dragging them, moving them up or down, or letting
  a question choose between the order they were written in and alphabetical
  order. Choices are alphabetical, apart from pinning
  ([ADR-0136](../../docs/decisions/ADR-0136-choices-are-listed-alphabetically-in-the-readers-language.md)).
- Sorting choices on the server by language. The server returns each group in
  a stable order and the reader's browser collates it, because only the reader
  knows their language.
- Copying a choice's wording onto an answer, in either language.
- An administrator authoring, seeing, or recoding an option code. A new
  choice's code is derived from its English wording, and a choice fixed in
  place keeps the code it has (`REQ-QB-092`).
- An administrator authoring, seeing, or changing a question key. A new
  question's key is derived from its English wording and never reuses a key any
  question holds, retired ones included (`REQ-QB-096`). Only an imported
  Typeform draft carries a key of its own, and the editor does not show it.
  Renaming an existing key is not built.
- Formatting in a statement's description: no Markdown, rich text, or links.
  Line breaks are the only structure it keeps (`REQ-QB-143`).
- Renaming a statement's "Ask this question" behaviour checkbox, or relabelling
  a group's fields. Only a statement's wording labels differ (`REQ-QB-141`).
- Correcting any other seeded question's wording by migration. Once a database
  is seeded, an Administrator owns its wording, and the attachment question's
  correction (`REQ-QB-105`) is not a pattern for re-seeding.
