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
- creation timestamp and the revision it supersedes, when any;
- a nullable `deleted` timestamp.

A question's choices are not part of any revision. A single-select,
multi-select, or type-ahead question owns one ordered list of choices that an
Administrator edits in place: adding, rewording, reordering, or removing one
never creates a revision and never retires the question, even once it has been
answered. A fork carries the whole list, removed choices and reporter-added
marks included, to the replacement. A removed choice is hidden from the form,
never erased
([ADR-0095](../../docs/decisions/ADR-0095-a-question-owns-its-choices-outside-its-revisions.md)).
A reporter-added choice holds only the language it was typed in until an
Administrator supplies the other, and is offered in the language it has.

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
- Mutating a revision, reviving a retired question, or any edit that loses the
  wording an answer was given against.
- Saving a question in one language.
- Shared choice lists, or reusing one question's choices on another in any
  form ([ADR-0095](../../docs/decisions/ADR-0095-a-question-owns-its-choices-outside-its-revisions.md)).
- A reporter editing, curating, or removing a choice. A reporter may add a
  missing choice to a type-ahead; an Administrator curates it in the question
  editor ([ADR-0063](../../docs/decisions/ADR-0063-a-reporter-may-add-a-type-ahead-choice.md)).
- A reporter adding a choice to a single-select or multi-select question.
- A record of exactly which choices a reporter was shown. The answer stores
  the reporter's own words (ADR-0072).
- Machine-translating a reporter-added choice's missing language.
- An administrator authoring, seeing, or recoding an option code. A new
  choice's code is derived from its English wording, and a reworded choice
  keeps the code it has (`REQ-QB-092`).
- An administrator authoring, seeing, or changing a question key. A new
  question's key is derived from its English wording and never reuses a key any
  question holds, retired ones included (`REQ-QB-096`). Only an imported
  Typeform draft carries a key of its own, and the editor does not show it.
  Renaming an existing key is not built.
- Correcting any other seeded question's wording by migration. Once a database
  is seeded, an Administrator owns its wording, and the attachment question's
  correction (`REQ-QB-105`) is not a pattern for re-seeding.
