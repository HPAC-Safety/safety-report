---
title: A type-ahead's list opens with a hint below 3 characters
description: A type-ahead's open list shows no choices, only a hint to type, until the reporter has typed 3 or more characters, and it draws no caret, so the field reads as a place to type rather than a dropdown to pick from.
type: adr
status: accepted
date: 2026-09-26
decision-makers: Chase Florell
keywords: type-ahead, autocomplete, combobox, threshold, hint, caret, WAI-ARIA, report form, ADR-0140, ADR-0150
---

# ADR-0152 — A type-ahead's list opens with a hint below 3 characters

## Status

Accepted. This ADR **amends**
[ADR-0140](ADR-0140-a-type-ahead-is-a-combobox-the-form-draws.md) on two
points: a type-ahead no longer draws a caret, and its list no longer opens
with every choice. Everything else in ADR-0140 stands, including that the
form draws the list itself, the WAI-ARIA 1.2 combobox pattern, and that free
text is always the field's value.

[ADR-0150](ADR-0150-a-single-select-is-a-select-only-combobox-the-form-draws.md)
is untouched: a single-select and a multi-select keep their caret and still
show their full list as soon as they open. Nothing here reaches them (#556).

## Context

ADR-0140 made the type-ahead a combobox the form draws, opening its full list
on a click, the caret, Alt and the down arrow, or the first character typed.
Reporters read that as a dropdown to choose from, so they missed that they
could type a value the list does not offer — the type-ahead's whole point
(ADR-0129, #516). A parent choice does not change this: a dependent
type-ahead (ADR-0146) opened its already-narrowed list the same way.

## Decision

**A type-ahead shows no choices until the reporter has typed 3 characters,
trimmed of spaces, and draws no caret.**

- **No caret.** The field looks like a plain text field. Clicking it, or
  pressing Alt and the down arrow, still opens the list; so does typing.
- **Below 3 characters**, the open list shows no choices, only a hint row —
  "Type 3 or more letters to see matching choices, or enter your own." — as a
  presentational row of the list, not an option. No option is active there,
  so the up and down arrows and Enter do nothing. The hint's appearance is
  also announced through a polite live status, for a reporter who cannot see
  the row.
- **At 3 or more characters**, the hint is replaced by the matching choices,
  filtered exactly as ADR-0140 already describes — case- and
  accent-insensitive, pin-group separators kept. Deleting back below 3
  characters brings the hint back.
- **Reopening filters by what the field already holds.** Closing the list
  (Escape, Tab, a press outside) and opening it again — by a click, Alt and
  the down arrow, or typing — filters by the field's current text exactly as
  typing it would: below 3 characters, the hint; at 3 or more, only the
  matching choices. It never falls back to every choice unfiltered, whatever
  the field holds and however it is reopened — a reporter who types a query,
  presses Escape, then clicks or presses Alt and the down arrow does not see
  the full list either (`REQ-QB-232`). Confirmed by the owner, 2026-09-27:
  "It should reopen as filtered from the text in the input."
- **Every type-ahead**, dependent ones included: the threshold applies on top
  of, not instead of, a dependent type-ahead's narrowing by its parent's
  answer (ADR-0146).
- **The threshold is fixed at 3.** No administrator setting.
- **Free text is unchanged.** A value shorter than 3 characters, or one the
  list does not offer, is still the field's value and is submitted exactly as
  before (ADR-0129). Only what the open list shows changes.

## Rejected alternatives

- **No popup at all below the threshold**, closing the list rather than
  showing a hint. A reporter pressing Alt and the down arrow, or clicking the
  field, would see nothing happen and might conclude the control is broken.
  The hint row confirms the field is listening and says what to do next.
- **Keep the caret.** A caret reads as "there is a fixed list here, opened by
  this button," which is exactly the impression this decision removes. The
  single-select and the multi-select keep theirs: they only ever offer a
  fixed list (ADR-0150), so the caret still describes them correctly.
- **An administrator-configurable threshold.** One fixed number is simpler to
  build, test, and explain, and no report backing a different number was
  raised.
- **Filtering on the server as the reporter types**, to shrink what the
  client holds before three characters. Every live choice is already in the
  form's questions response; ADR-0140 already rejected a server round trip
  per keystroke, and the threshold is a display rule over data the client
  already has.

## Consequences

- A type-ahead reads as a place to type first, a picker second.
- A reporter typing 1 or 2 characters sees a hint instead of a truncated,
  often-irrelevant match list.
- Verifying "what a dependent type-ahead currently offers" in a test can no
  longer be done by opening an empty field; it now means typing enough of the
  choice in question, same as a reporter would.

## Related

- [#561](https://github.com/HPAC-Safety/safety-report/issues/561).
- [ADR-0140](ADR-0140-a-type-ahead-is-a-combobox-the-form-draws.md) — amended.
- [ADR-0150](ADR-0150-a-single-select-is-a-select-only-combobox-the-form-draws.md) — untouched; single-select and multi-select are out of scope.
- [ADR-0146](ADR-0146-a-choice-list-may-depend-on-another-questions-answer.md) — a dependent type-ahead follows this threshold too.
- [ADR-0129](ADR-0129-a-type-ahead-value-is-edited-in-place-merged-and-reviewed.md) — free text, unchanged.
