---
title: A question owns its choices, outside its revisions
description: Every choice-bearing question keeps one editable list of its own choices beside its revision chain; shared lists and per-revision option snapshots are removed, and their tables dropped after a forward copy.
type: adr
status: accepted
date: 2026-09-22
decision-makers: Chase Florell
keywords: question bank, choices, option sets, type-ahead, reporter-added, revisions, fork, migration, drop table
---

# ADR-0095 — A question owns its choices, outside its revisions

## Status

Accepted. This ADR:

- **supersedes** [ADR-0058](ADR-0058-shared-option-sets-with-a-revision-snapshot.md);
- **amends** [ADR-0063](ADR-0063-a-reporter-may-add-a-type-ahead-choice.md),
  [ADR-0071](ADR-0071-an-answered-question-forks-instead-of-revising.md),
  [ADR-0074](ADR-0074-a-single-select-parent-may-enable-a-conditional-question.md) and
  [ADR-0077](ADR-0077-typeform-json-import-and-export.md).

Amended by [ADR-0128](ADR-0128-an-answer-names-its-choice-and-a-picker-option-is-fixed-or-replaced.md) (an answer names its choice by ID; an
Administrator fixes a picker option in place or replaces it) and
[ADR-0129](ADR-0129-a-type-ahead-value-is-edited-in-place-merged-and-reviewed.md) (a type-ahead value is edited in place, merged, and
reviewed by a Safety Officer or Administrator; the Worker translates a
reporter-added value), and by
[ADR-0136](ADR-0136-choices-are-listed-alphabetically-in-the-readers-language.md)
(the list has no order of its own: choices are listed alphabetically in the
reader's language, apart from those an Administrator pins first or last, so
"reordering" a choice below means pinning it), and by
[ADR-0145](ADR-0145-a-choice-list-may-depend-on-another-questions-answer.md)
(a choice may name one choice of a parent question it is offered under; the
link sits outside revisions too, and a fork copies it).

It carries the second argued exception to AGENTS.md invariant 8's ban on
`DROP TABLE`, after
[ADR-0065](ADR-0065-no-user-records-identity-is-the-token-subject.md)'s
`admin_users`.

## Context

A choice could live in two places:

- a revision's own `question_revision_options` rows, copied from the editor;
- a shared `option_sets` list, snapshotted into each revision that used it
  (ADR-0058).

The question bank seeded from the Typeform fixtures uses only the first. So the
"Where" question offers five sites, while `/admin/choice-lists` — the page an
Administrator is sent to for curating choices — is empty. No question in the
bank shares a list, and the page was solving a problem the owner does not have.

The per-revision snapshot has a cost that ADR-0071 makes visible. Choices
belong to an immutable revision, so any change to them is a new revision. Once
the question has an answer, that change is a fork: the question is retired and
replaced. Adding one aerodrome to an answered type-ahead retired the question.

What the snapshot bought was a record of the complete set of choices a reporter
was offered (ADR-0058, "Amended by ADR-0072"). The owner does not need that
record. The answer already stores the reporter's own words (ADR-0072), and that
is what a reviewer reads.

## Decision

**Each question owns one editable list of choices, `question_choices`, beside
its revision chain.** A single-select, multi-select or type-ahead question
carries its choices there. A revision no longer holds options. There are no
shared lists.

- **Editing choices never revises or forks.** Adding, rewording, reordering or
  removing a choice changes the question's list in place, on any type, answered
  or not. A new revision — or, for an answered question, a fork (ADR-0071) — is
  created only when a field of the revision itself changes: wording, help, type,
  flags, dependency or grouping.
- **A fork copies every choice.** The replacement receives every row of the
  retired question's list, including removed choices (still removed) and
  reporter-added marks.
- **A removed choice is hidden, never erased.** It carries `Deleted` and
  disappears from the form. A reporter typing its wording again never revives
  it; an Administrator writing it again does, deliberately.
- **A choice a live question depends on cannot be removed.** The parent's
  required option (ADR-0074) resolves against the parent's live choices, so
  removing it would silently disable the child.
- **Reporter additions are type-ahead only.** A value a reporter types that a
  type-ahead does not offer joins that question's own list, marked
  `AddedByReporter`, in the same transaction as the report. It holds only the
  language it was typed in, with the other label empty and `NeedsTranslation`
  set. A single-select or multi-select never accepts an unlisted value. The
  multi-select "allow reporter additions" flag ADR-0077 introduced is removed.
- **A one-language choice is offered in the language it has.** A reporter
  reading the form in French sees an English-only reporter-added choice in
  English rather than not at all. A choice an Administrator saves still needs
  both languages (AGENTS.md invariant 1), so only a reporter-added choice can
  be one-language. Supplying its missing label completes it and clears
  `NeedsTranslation`.
- **Curation is in the question editor.** A reporter-added choice is marked
  there, and the questions list counts the ones still awaiting review.

### Dropping the old tables

The migration first copies every question's current choices into
`question_choices`:

- from its current revision's options; or,
- for a type-ahead whose current revision used a live shared list, from that
  list's items, keeping removed items removed and keeping the reporter flags.

A reporter-added item keeps only the label in the language it was typed in.

It then drops `option_sets`, `option_set_items` and `question_revision_options`,
and the columns `question_revisions.option_set_id` and
`question_revisions.allows_reporter_additions`.

This is argued on its own facts, as invariant 8 requires:

- **What was lost is copied first.** Nothing a reporter or Administrator can
  see is lost. Every live choice and every reporter addition moves to
  `question_choices` before the drop, in the same migration.
- **What is not copied is the per-revision record of which choices were
  offered.** The owner has decided not to keep that record. The answers it
  would have explained store their own words.
- **Keeping the tables would keep two homes for choices,** which is the defect
  this ADR removes. An unused table invites the next reader to write to it.
- **The owner authorised the drop** on 2026-09-22, while unsure whether any
  deployed environment held list data. The forward copy is why that
  uncertainty is acceptable.

The exception does not generalize. Any future `DROP TABLE` still needs its own
argument.

## Consequences

- A question's choices can change without its revision number moving. A
  revision no longer describes everything a reporter saw.
- The Typeform export reads the question's live choices. The import creates no
  lists and ignores a multi-select's `allow_other_choice`.
- `/admin/choice-lists`, its API and its Admin menu entry are removed.
- A report cannot reconstruct the exact list its reporter chose from. This is
  accepted, not an oversight.

## Alternatives rejected

- **Keep shared lists as templates copied into a question.** This keeps two
  homes for choices and a page nobody uses, and a template edit reaching no
  question is the same surprise the empty page was.
- **Snapshot the offered choices onto each report at submission.** This
  preserves the record ADR-0058 valued, at the cost of copying every choice
  onto every report. The owner does not want the record.
- **Edit only reporter-added choices in place, and fork on any other choice
  edit.** This makes two rules for one list. Adding an aerodrome would still
  retire an answered question.
- **Keep the old tables unused.** This was rejected for the reasons under
  "Dropping the old tables".

## Related

- [ADR-0058](ADR-0058-shared-option-sets-with-a-revision-snapshot.md) — superseded.
- [ADR-0063](ADR-0063-a-reporter-may-add-a-type-ahead-choice.md) — reporter additions land on the question and are curated in place.
- [ADR-0071](ADR-0071-an-answered-question-forks-instead-of-revising.md) — choices sit outside the fork rule, and a fork copies them.
- [ADR-0072](ADR-0072-every-answer-is-stored-as-a-string.md) — why the offered-set record is not needed.
- [ADR-0074](ADR-0074-a-single-select-parent-may-enable-a-conditional-question.md) — the required option resolves against live choices.
- [ADR-0077](ADR-0077-typeform-json-import-and-export.md) — multi-select additions and import-created lists removed.
- [Lesson 0009](../lessons/0009-a-choice-list-nobody-could-see.md).
