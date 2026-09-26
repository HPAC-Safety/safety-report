---
title: Choices are listed alphabetically in the reader's language
description: A question's choices have no authored order; every screen lists them alphabetically in the reader's language, collated in the browser, after pinned-first and before pinned-last choices an Administrator marks.
type: adr
status: accepted
date: 2026-09-26
decision-makers: Chase Florell
keywords: question bank, choices, sorting, collation, pinning, locale, question_choices, display_order, ADR-0095
---

# ADR-0136 — Choices are listed alphabetically in the reader's language

## Status

Accepted. This ADR **amends**
[ADR-0095](ADR-0095-a-question-owns-its-choices-outside-its-revisions.md):
a question's list of choices has no order of its own, so "reordering" a choice
is replaced by pinning it. Everything else in ADR-0095 stands, including that
editing choices never revises or forks the question.

Amended by [ADR-0140](ADR-0140-a-type-ahead-is-a-combobox-the-form-draws.md)
(a type-ahead is a combobox the form draws, so it draws the separators too).

## Context

A question's choices were shown in the order an Administrator entered them.
`question_choices.display_order` held that order, and a value a reporter
added to a type-ahead went to the end (#514). No scenario or ADR said what the
order should be. A long list, such as countries or launch sites, was hard to
scan, and the order said nothing a reporter could use.

The owner decided on 2026-09-26 that every list of choices is alphabetical in
the reader's language. Some choices still belong at an end: "Canada" and
"United States" above the other countries, and "Other" or "Unknown" below
everything.

## Decision

**A question's choices are listed alphabetically in the reader's language,
after the choices pinned first and before the choices pinned last.**

- **Pin.** Each choice is pinned `first`, pinned `last`, or not pinned
  (`none`, the default), stored in `question_choices.pin`. An Administrator
  sets it with the choice's Position in the question editor. It is part of the
  choice, so setting it never revises or forks the question (ADR-0095), and a
  fork copies it.
- **Order.** Every screen lists three groups in turn: pinned first, not
  pinned, pinned last. Within each group, choices are sorted by the label the
  reader sees, with `Intl.Collator(locale, { sensitivity: "base", numeric:
  true })`. Accents and case are ignored, so "Émeu" sorts with the E's, and
  the English and French lists may come out in different orders. Ties are
  broken by choice ID. A one-language value a reporter added sorts by the
  label shown, which is the language it has.
- **The browser collates.** The API returns each question's choices grouped
  by pin, and by choice ID within a group. That order is stable, and it is
  not alphabetical. Only the browser knows the reader's language, so one
  shared client helper sorts every list the same way: the report form's
  single-select, multi-select and type-ahead, the editor's options, the
  required-option control, the type-ahead review page's merge targets, and a
  multi-select answer on a report.
- **Separators.** A separator is drawn between non-empty groups where the
  control can draw one. In a multi-select it is a divider between checkbox
  groups. In a single-select `<select>` it is a disabled `──` option, because
  the React version in use does not allow `<hr>` inside `<select>`. A native
  `<datalist>` cannot draw one, so a type-ahead only keeps the group order.
- **The editor re-sorts only when it opens.** It lists options the way the
  form does when a question is opened, and keeps them in place while the
  Administrator edits, so a row never jumps away from the cursor.
- **`display_order` stays, unread.** The column is kept, as invariant 8
  requires, and still written, but no screen reads it any more.
- **Typeform export** writes choices in English alphabetical order within the
  pin groups. The import does not depend on the file's choice order.

## Rejected alternatives

- **Keep the authored order, or let each question choose between authored
  and alphabetical order.** The owner rejected both: the order choices were
  entered in carries no meaning, and a per-question switch is one more thing
  to author and get wrong.
- **Sort on the server.** The server would need the reader's language on
  every read, and a database collation that matches the browser's. The browser
  already has both.
- **Store a separate sort key per language.** It would be derived data that
  drifts whenever a label is fixed or a value is translated.
- **Drag-and-drop or move-up/down for choices.** Out of scope: pinning covers
  the two cases the owner named.

## Consequences

- An Administrator no longer controls where an unpinned choice appears.
- A reporter reading French may see the choices in a different order from one
  reading English.
- `display_order` is written but read by nothing. Dropping it would need its
  own argument (invariant 8).

## Related

- [#514](https://github.com/HPAC-Safety/safety-report/issues/514).
- [ADR-0095](ADR-0095-a-question-owns-its-choices-outside-its-revisions.md) — amended.
- [ADR-0059](ADR-0059-dnd-kit-for-reordering.md) — question order is unchanged.
