---
title: A single-select is a select-only combobox the form draws
description: The report form's single-select question is a hand-built WAI-ARIA select-only combobox drawing the type-ahead's list, replacing the native select, and the multi-select picker's list takes the same rows; the separator is a list row, not a disabled option.
type: adr
status: accepted
date: 2026-09-26
decision-makers: Chase Florell
keywords: single-select, multi-select, combobox, select-only, native select, WAI-ARIA, report form, separators, ADR-0136, ADR-0140
---

# ADR-0150 — A single-select is a select-only combobox the form draws

**Status:** Accepted. This ADR **amends**
[ADR-0136](ADR-0136-choices-are-listed-alphabetically-in-the-readers-language.md)
on one point: a single-select's separator between pin groups is a row the
form's list draws, no longer a disabled `──` option in a `<select>`. It
**extends**
[ADR-0140](ADR-0140-a-type-ahead-is-a-combobox-the-form-draws.md) from the
type-ahead to the report form's other two choice questions. Everything else in
both stands.

## Context

The reporter form drew its three choice questions three ways (#556). The
type-ahead is a combobox the form draws (ADR-0140). A single-select was a
native `<select>`, whose open list the operating system draws: on macOS Chrome
a dark floating menu with its own font and check mark, offset from the field.
The multi-select picker (#343) was drawn by the form, but its open panel was a
column of bare checkboxes with its own spacing, no highlighted row, and an
`<hr>` between pin groups.

A native `<select>`'s open list cannot be styled across browsers. Chromium's
customizable select (`appearance: base-select`) can, but Firefox and Safari do
not support it, so the list would still differ by browser.

## Decision

**A single-select question on the report form is a select-only combobox the
form draws, using the type-ahead's list.**

- **The WAI-ARIA 1.2 select-only combobox pattern.** The focusable field is a
  `<button>` carrying `role="combobox"`, `aria-expanded`, `aria-controls`, and
  `aria-activedescendant`, labelled by the question's `<label>`. Being a
  button, it is disabled natively (ADR-0146) and needs no key handling to open
  from Enter or Space. Nothing can be typed into it: typing a character only
  moves to the next choice starting with it.
- **One list component.** The type-ahead, the single-select, and the
  multi-select picker draw their open list from one set of shared pieces under
  `src/web/src/report-form/`: the same surface, border, shadow, row height,
  separator row, and highlighted row. A change to one changes all three.
- **The placeholder is the first row.** Choosing it clears the answer, as the
  native select's empty option did.
- **`aria-selected` marks the chosen choice**, as the pattern specifies; the
  highlighted one is named by `aria-activedescendant`.
- **The multi-select keeps its trigger and its real checkboxes**; its rows
  become the list's rows, highlighted on hover and on keyboard focus.
- **The answer is unchanged**: a single-select is held and sent by its
  choice's identifier.
- **Scope.** The report form only. The admin pages keep their native selects.

## Considered options

- **Keep the native `<select>`.** Its open list stays the operating system's,
  unlike the other two choice questions.
- **The customizable select (`appearance: base-select`).** Chromium only; the
  list would still be the browser's in Firefox and Safari.
- **The type-ahead component with its text input made read-only.** A
  read-only input still takes a caret and reads as editable text to assistive
  technology; the select-only pattern is a distinct, fully specified role.
- **A combobox library.** Rejected for the type-ahead in ADR-0140 for the same
  reasons: a runtime dependency for one small, fully specified control.
- **Radio buttons for a single-select.** Presentation that changes the form's
  length and does not scale to a long list such as countries.

## Consequences

- All three choice questions look and behave alike in every browser.
- The form owns the single-select's keyboard and screen-reader behaviour,
  which the browser used to supply; its scenarios cover it
  (`REQ-QB-208`–`REQ-QB-211`).
- A browser-level test can no longer use Playwright's `selectOption` on a
  single-select; it opens the list and presses an option.

## Related

- [#556](https://github.com/HPAC-Safety/safety-report/issues/556),
  [#516](https://github.com/HPAC-Safety/safety-report/issues/516),
  [#343](https://github.com/HPAC-Safety/safety-report/issues/343).
- [ADR-0136](ADR-0136-choices-are-listed-alphabetically-in-the-readers-language.md) — amended.
- [ADR-0140](ADR-0140-a-type-ahead-is-a-combobox-the-form-draws.md) — extended.
- [ADR-0146](ADR-0146-a-choice-list-may-depend-on-another-questions-answer.md) — a disabled dependent single-select, unchanged.
