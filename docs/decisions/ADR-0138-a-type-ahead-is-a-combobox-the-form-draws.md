---
title: A type-ahead is a combobox the form draws
description: The report form's type-ahead question is a hand-built WAI-ARIA combobox whose list the application draws, replacing the browser's datalist; it draws the pin-group separators ADR-0136 could not.
type: adr
status: accepted
date: 2026-09-26
decision-makers: Chase Florell
keywords: type-ahead, autocomplete, combobox, datalist, WAI-ARIA, report form, separators, ADR-0136, ADR-0129
---

# ADR-0138 — A type-ahead is a combobox the form draws

## Status

Accepted. This ADR **amends**
[ADR-0136](ADR-0136-choices-are-listed-alphabetically-in-the-readers-language.md)
on one point: a type-ahead now draws the separators between pin groups.
Everything else in ADR-0136 stands.

## Context

The report form showed a type-ahead question as an `<input list>` with a
`<datalist>`. The browser draws a datalist's suggestions itself. In Chrome
they appear in a dark floating box, offset from the field, with its own font
and a pointer arrow, partly covering the help text. Each browser draws them
differently, and none matches the form's pickers (#516).

A datalist also cannot draw a separator, so ADR-0136 let the type-ahead keep
only the order of its pin groups, not the separators between them.

The type-ahead must still take a value its list does not offer: a reporter
may add a missing value, which is then reviewed
([ADR-0129](ADR-0129-a-type-ahead-value-is-edited-in-place-merged-and-reviewed.md)).

## Decision

**A type-ahead question is a combobox whose list the form draws.**

- **One field, one list.** A text input styled like the form's other fields,
  with a caret, and a list that opens directly beneath it, as wide as the
  field, in the design-system tokens. No `<datalist>`.
- **The WAI-ARIA 1.2 combobox pattern with list autocomplete.** The input
  carries `role="combobox"`, `aria-expanded`, `aria-controls`,
  `aria-autocomplete="list"`, and `aria-activedescendant`. The list is a
  `role="listbox"` of `role="option"` entries. The input keeps focus
  throughout; its label, help text, and error stay wired as before.
- **Filtering in the browser.** Typing narrows the list to choices whose
  wording in the reader's language contains the text, ignoring case and
  accents. The form already holds every live choice.
- **Free text stays.** Typed text is the field's value whether or not it
  matches a choice; the answer is mapped to a choice exactly as before.
- **Separators.** The list draws a separator between non-empty pin groups,
  as a single-select and a multi-select do. This replaces ADR-0136's sentence
  that a type-ahead "only keeps the group order".
- **Hand-built**, like the multi-select picker, in one component under
  `src/web/src/report-form/`.

## Rejected alternatives

- **Keep the datalist and restyle it.** A datalist's popup cannot be styled;
  the browser owns it.
- **A combobox library** (Downshift, Headless UI, React Aria). Each adds a
  runtime dependency to the public form for one control, while the
  multi-select picker is already hand-built on the same tokens. The pattern is
  small and fully specified by WAI-ARIA.
- **A single-select with an "Other" text field.** It would change how a
  reporter adds a value, which ADR-0129 settles, and split one answer across
  two controls.
- **Fetching matches from the server as the reporter types.** Every live
  choice is already in the form's questions response, and a request per
  keystroke would be a pre-submission call the form does not otherwise make.

## Consequences

- The type-ahead looks the same in every browser, and matches the form's
  other pickers.
- The form owns the combobox's keyboard and screen-reader behaviour, which the
  browser used to supply; its scenarios cover it.
- A type-ahead's list shows the pin-group separators.

## Related

- [#516](https://github.com/HPAC-Safety/safety-report/issues/516).
- [ADR-0136](ADR-0136-choices-are-listed-alphabetically-in-the-readers-language.md) — amended.
- [ADR-0129](ADR-0129-a-type-ahead-value-is-edited-in-place-merged-and-reviewed.md) — a reporter-added value, unchanged.
