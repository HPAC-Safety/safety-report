---
title: A question's choices may depend on another question's answer
description: A single-select or type-ahead question may take another single-select or type-ahead as its parent; each of its choices then names one parent choice, the form offers only those under the parent's answer, and the links live outside revisions and follow the parent through replace, merge, and fork when those happen.
type: adr
status: accepted
date: 2026-09-26
decision-makers: Chase Florell
keywords: choices, dependent choices, parent choice, type-ahead, single-select, question bank, revisions, fork, replace, merge, ADR-0095, ADR-0128, ADR-0129, ADR-0132
---

# ADR-0145 — A question's choices may depend on another question's answer

## Status

Accepted. This ADR:

- **amends**
  [ADR-0095](ADR-0095-a-question-owns-its-choices-outside-its-revisions.md):
  a choice may name a choice of another question, its parent choice. Like
  every other choice edit, linking one never revises or forks either question,
  and a fork copies each choice with its link.
- **amends**
  [ADR-0129](ADR-0129-a-type-ahead-value-is-edited-in-place-merged-and-reviewed.md):
  on a dependent type-ahead, a typed value is matched only among the values
  under the parent's answer, and a new one is offered under that answer. A
  reviewer may change a value's link. Two values merge only when they sit under
  the same parent choice.
- **amends** AGENTS.md invariant 1 (the choice rules).

It builds on
[ADR-0128](ADR-0128-an-answer-names-its-choice-and-a-picker-option-is-fixed-or-replaced.md),
under which an answer names its choice by ID. It sits beside
[ADR-0060](ADR-0060-conditional-questions-depend-on-a-boolean-question.md),
[ADR-0074](ADR-0074-a-single-select-parent-may-enable-a-conditional-question.md),
and [ADR-0132](ADR-0132-a-condition-follows-its-parent-through-a-fork.md)
without changing them.

## Context

A reporter picking a paraglider's make and model sees every model of every
make. Once they say Niviuk, the model question should offer Niviuk's models
only. A question's choices (ADR-0095) had no tie to another question's
choices, so nothing could narrow one list by another's answer (#520).

A condition cannot do it. A condition decides whether a whole question is
shown, and there is one model question, not one per make.

## Decision

### A dependency, and a link on each choice

- A single-select or type-ahead question (the *child*) may name another
  single-select or type-ahead question (the *parent*). Each live choice of the
  child then names exactly **one** parent choice. A model sold under two makes
  is entered twice, and a catch-all such as "Other" is entered once under each
  make it applies to. The same wording twice under one parent choice is
  refused.
- The form offers only the child choices under the parent's answer. The child
  is disabled until the parent is answered. A disabled child never blocks Next
  or Submit, even when it is required, and neither does a single-select child
  with nothing under the parent's answer.
- **One level only.** A child is nobody's parent, and a parent depends on
  nothing. Multi-select takes no part on either side.
- **The parent comes first** on the form. This is checked when the dependency
  is saved and on every reorder.
- A question may be both conditional and dependent. The two are independent.

### Outside revisions

The dependency lives on `questions`, and each link on its `question_choices`
row, as the choices themselves do (ADR-0095). Setting, changing, or clearing
either never revises or forks either question, answered or not.

Clearing the parent keeps every link, which then stops filtering. A parent the
form does not ask (deactivated, or deleted rather than forked) filters nothing
either, just as a condition whose parent is missing hides nothing. The API
then does not check the link.

### A link follows its parent, and is re-pointed when the parent changes

- A **replaced picker** parent choice passes its links to its replacement
  (ADR-0128).
- A **merged type-ahead** parent value passes them to the value it was merged
  into (ADR-0129).
- A **forked parent** passes its dependents to the question that replaced it,
  and each link to the copy of its choice (ADR-0071).

Each of these re-points the links when it happens, in the same save. Readers
then compare identifiers and resolve nothing.

Conditions resolve a fork on read instead (ADR-0132). That was necessary
because a condition is a revision field, so re-pointing it would revise, and
for an answered dependent fork, the dependent. A link is not a revision field.
It is editable state outside revisions and outside answers, so re-pointing it
rewrites nothing that must stay as it was. Doing it once, at the write, spares
every reader the resolution (the form, the submission check, the editor, the
review page, and the removal check). It also keeps the database's references
naming live rows.

A parent choice that a live child choice is offered under cannot be removed.
It is replaced (picker) or merged (type-ahead) instead, as ADR-0095 holds for
the required choice of a condition.

### Reporter-added values

On a dependent type-ahead, typed words are matched only among the values under
the parent's answer, and a new value is offered under that answer. When the
parent's answer is itself a new value typed in the same report, the new child
value is offered under it. A value typed while the parent is off the form has
no link until a reviewer gives it one.

A Safety Officer or an Administrator may change a value's link on the
type-ahead review page, but never clear it. Merging two values under different
parent choices is refused: answers naming a Niviuk model would otherwise read
an Ozone one. The reviewer changes the link first.

### Answers and the submission

An answer is unchanged. It names its choice by ID (ADR-0128), and nothing
about the parent is copied into it. Before writing anything, the API refuses a
child choice not offered under the parent's answer, and a child answered while
its parent is not, naming both questions by key.

### Where each rule is enforced

- **The database** holds the two references as restricted foreign keys, and a
  `CHECK` on each refuses a self-reference.
- **The domain and the API** check the rest: which types take part, one level
  only, that a link names a live choice of the parent, that every live choice
  of a child is linked, and form order. These read the parent's current
  revision and choices, so a constraint could only enforce them through a
  trigger. ADR-0060 and ADR-0074 rejected that for conditions, as a rule
  hidden from everyone reading the C#. The one class that checks them is
  `ChoiceDependencies`.

## Rejected alternatives

- **Resolve on read, as ADR-0132 does for conditions.** Every reader would
  need both questions loaded and the same chain of copy, replacement, and
  merge. The case that forced it for conditions, a revision field, does not
  apply to a link.
- **Keep links on the revision.** Each new make's models would revise the
  model question, and fork it once it is answered. ADR-0095 rejected this for
  choices themselves.
- **A child choice under several parent choices.** This needs a join table
  and a harder editor. Entering a shared model twice was the owner's choice.
- **Unlinked choices shown under every parent answer.** Every catch-all would
  appear under every make without the Administrator ever choosing that. The
  owner chose one entry per make.
- **Chains (make → model → size), and multi-select parents or children.** The
  owner had no need for them, and each multiplies what the form must clear
  when an answer changes.
- **Enforce types and depth with triggers.** This hides a rule from the C#,
  which ADR-0060 and ADR-0074 already rejected.
- **Refuse to deactivate or delete a parent while a child depends on it.** An
  Administrator would have to detach every child before retiring a question.
  A condition does not ask that, and the owner chose to mirror conditions.

## Consequences

- `questions` gains `choices_depend_on_question_id`, and `question_choices`
  gains `parent_choice_id`. Both are restricted foreign keys with a `CHECK`
  against a self-reference.
- Saving a parent's choices, merging a parent's values, and forking a parent
  re-point their dependents' links in the same save.
- The public form's questions carry each child's parent (only while that
  parent is on the form) and each choice's parent choice. The browser filters
  them.
- A new audit action records a reviewer changing a value's link.
- A value a reporter types while the parent is off the form is the one
  unlinked choice a child can hold. The next save of that question in the
  editor asks for its link.
- Typeform import and export carry no dependency.

## Related

- [#520](https://github.com/HPAC-Safety/safety-report/issues/520).
- [ADR-0095](ADR-0095-a-question-owns-its-choices-outside-its-revisions.md), [ADR-0129](ADR-0129-a-type-ahead-value-is-edited-in-place-merged-and-reviewed.md) — amended.
- [ADR-0128](ADR-0128-an-answer-names-its-choice-and-a-picker-option-is-fixed-or-replaced.md), [ADR-0132](ADR-0132-a-condition-follows-its-parent-through-a-fork.md), [ADR-0140](ADR-0140-a-type-ahead-is-a-combobox-the-form-draws.md).
