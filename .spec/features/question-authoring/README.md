---
title: Question authoring
description: Supporting detail for the immutable bilingual question revisions, question types, conditions, groups, and the question editor.
type: spec
area: question-authoring
prefix: REQ-QAU
---

# Question authoring

Supporting detail for [`question-authoring.feature`](question-authoring.feature)
that doesn't fit Gherkin.

## Revision fields

Each revision contains:

- a unique revision identifier, stable question key, and monotonically
  increasing revision number;
- English and French label text and optional English and French help text;
- question type;
- form sort order and optional section/group key;
- `is_private`, `is_active`, `is_system`, and `is_required` flags;
- `allow_future_dates`, `false` unless an Administrator allows future dates.
  Only a date question may set it, and changing it is a revision like any
  other field
  ([ADR-0138](../../decisions/ADR-0138-a-date-question-allows-future-dates-only-when-it-says-so.md));
- creation timestamp and the revision it supersedes, when any;
- a nullable `deleted` timestamp.

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

A statement is shown to Administrators as **Instructional text**. It is a
title and a description, not a question and help text, so the editor labels
its wording that way and gives each description several lines (`REQ-QB-141`).
Both still live in the revision's label and help-text fields; only the editor's
labels differ by type. The description is stored exactly as typed, line breaks
included (`REQ-QB-142`), and the reporter's form shows it with its paragraphs
wherever the statement appears: as the introduction, on a page of its own, or
under a group (`REQ-QB-143`).

The Typeform-derived question set is seed/import input, not hardcoded form
logic. The database remains authoritative after initial seeding.

## A label has no closing colon

A question's label is stored without a colon, and the interface draws it
([ADR-0181](../../decisions/ADR-0181-a-one-time-migration-trims-label-colons-in-place.md)):

- **Where and how.** After an answerable question's label: `Label:` in en-CA,
  `Label :` in fr-CA, where French typography asks for a space before the
  colon. None after a statement, a group, or a label that ends in `?`. It
  shows on the reporter form, the admin report detail, and the question bank
  previews, which show each language's label in that language's style
  (`REQ-QB-240`, `REQ-QB-241`, `REQ-QB-242`).
- **New labels.** The editor refuses a label ending in `:` (`REQ-QB-243`), and
  so does the API, with a problem worded in both languages (`REQ-QB-244`).
  Typeform import strips the colon from a title (`REQ-TF-024`).
- **Stored labels.** One migration removed the trailing colon from every stored
  label, in place, creating no revision (`REQ-QB-245`). A clean database holds
  none (`REQ-QB-246`). This is the one exception to the rule that an answered
  question forks instead of being revised.

## The group page contract

A `group`-typed question and its `grouped_under_question_id` children render
together as one page/step, not as separate steps: `fieldset`/`legend` around
the group's own label and the input controls for each visible child, in
revision order. A child renders inside its group's page even if it also
carries its own conditional dependency — grouping and conditional dependency
are independent (ADR-0076) — and the group page itself is skipped only if
every one of its children is currently hidden by an unmet condition.

A group that is deleted, or retyped to anything but `group`, ungroups its live
children in the same save (`REQ-QB-052`, ADR-0076). An answered child forks and
an unanswered child gets a new revision, like any edit (ADR-0071). The children
take the group's slot in their existing order, and every later question shifts down only as far as it must;
a retyped group keeps its own slot and the children follow it. Each is
audited as a question edit.

A group that is edited and stays a group gives each live child a new revision in
the same save, still grouped under it (`REQ-QB-248`); an answered child forks, and
its replacement is grouped under the group too.

## Out of scope

What not to build here. The global list in
[system overview](../../system-overview.md) still holds; this narrows it
to this area ([ADR-0083](../../decisions/ADR-0083-specification-driven-development.md)).

- A label colon stored in the wording, or configured per question. The colon is
  drawn by the interface in the reader's locale, and no other punctuation is
  added.
- Markdown in a question's help text or a statement's description, or a
  Markdown editor, toolbar, or hint in the form. A reporter's paragraph answer
  is stored as typed and read as Markdown by reviewers
  ([ADR-0180](../../decisions/ADR-0180-a-summary-is-markdown-with-one-section-per-public-paragraph-question.md)).
- A general-purpose form builder: scoring, surveys, quizzes, form templates, or
  arbitrary branching. A question may be conditional on a yes/no question or on
  a single-select question naming a required option, and that is the whole of
  it ([ADR-0060](../../decisions/ADR-0060-conditional-questions-depend-on-a-boolean-question.md),
  [ADR-0074](../../decisions/ADR-0074-a-single-select-parent-may-enable-a-conditional-question.md)).
- Mutating a revision, reviving a retired question, or any edit that loses the
  wording an answer was given against.
- An Administrator authoring, seeing, or recoding an option code. A new
  choice's code is derived from its English wording, and a choice fixed in
  place keeps the code it has (`REQ-QB-092`).
- An Administrator authoring, seeing, or changing a question key. A new
  question's key is derived from its English wording and never reuses a key any
  question holds, retired ones included (`REQ-QB-096`). Only an imported
  Typeform draft carries a key of its own, and the editor does not show it.
  Renaming an existing key is not built.
- Formatting in a statement's description: no Markdown, rich text, or links.
  Line breaks are the only structure it keeps (`REQ-QB-143`).
- Renaming a statement's "Ask this question" behaviour checkbox, or relabelling
  a group's fields. Only a statement's wording labels differ (`REQ-QB-141`).
