---
title: Report form
description: Supporting detail for the report form: stored answer forms, the type-ahead and picker fields, the seeded attachment and Country questions, and the seeded groups.
type: spec
area: report-form
prefix: REQ-RFM
---

# Report form

Supporting detail for [`report-form.feature`](report-form.feature)
that doesn't fit Gherkin.

## The type-ahead question

A type-ahead question looks like the form's other pickers: one question, whose
list opens directly beneath it, as wide as the question and drawn in the form's
own surface, font, border, and focus ring. The form draws that list itself;
the browser's own suggestion list (`<datalist>`) is not used, so it looks the
same in every browser (`REQ-QB-159`,
[ADR-0140](../../decisions/ADR-0140-a-type-ahead-is-a-combobox-the-form-draws.md)).
Unlike the single-select and the multi-select, it has **no caret**: it reads
as a place to type, not a picker to choose from
([ADR-0152](../../decisions/ADR-0152-a-type-aheads-list-opens-with-a-hint-below-3-characters.md)).

- **Opening.** Clicking the question, pressing Alt and the down arrow, or typing
  opens the list.
- **A hint below 3 characters.** Trimmed of spaces, fewer than 3 typed
  characters shows the open list with no choices, only a hint row: "Type 3 or
  more letters to see matching choices, or enter your own." No choice is
  active there, so the up and down arrows and Enter do nothing. Reaching 3
  characters replaces the hint with the matching choices; deleting back below
  3 brings the hint back. This is announced to assistive technology through a
  polite live status (`REQ-QB-159`, `REQ-QB-229`, `REQ-QB-230`,
  [ADR-0152](../../decisions/ADR-0152-a-type-aheads-list-opens-with-a-hint-below-3-characters.md)).
  A dependent type-ahead follows the same rule, on top of its own narrowing by
  the parent's answer (`REQ-QB-231`, ADR-0146).
- **Reopening filters by what the question holds** (`REQ-QB-232`, ADR-0152).
- **Filtering.** At 3 or more characters, what the reporter typed narrows the
  list to the choices whose wording contains it anywhere, ignoring case and
  accents, in the reader's language (`REQ-QB-160`). The list keeps the order
  and separators above.
- **A merged-away wording still finds its survivor.** A value the reporter
  typed narrows the list to a choice that once had it, before it was merged
  into another: the survivor is offered, in either official language,
  whatever the form's own language, with a hint naming the alias that
  matched — never the merged-away value itself, and never widening what a
  dependent type-ahead offers under its parent's answer (`REQ-QB-233`,
  `REQ-QB-234`, `REQ-QB-236`, ADR-0129 amendment). A chained merge is
  flattened before the client ever sees it, so typing the first value in a
  chain still offers only the final survivor, with no chain to follow
  (`REQ-QB-235`).
- **Keyboard and pointer.** At 3 or more characters, the down and up arrows
  move the highlighted choice through the list, and pointing at a choice
  highlights it. Enter takes the highlighted choice, Alt and the down arrow
  open the list without moving, and Escape or a press outside the question
  closes it, keeping what the question holds (`REQ-QB-161`). Tab moves on and
  closes it too (`REQ-QB-162`). The question follows the WAI-ARIA 1.2 combobox
  pattern, and keeps its label, help text, and error.
- **A picked choice.** A choice taken from the list is sent as that choice,
  by its identifier, even where another choice carries the same wording
  (`REQ-QB-171`). Text typed without picking is matched to a choice by its
  wording, ignoring case, or else sent as typed.
- **A value it does not offer.** The reporter may still type one, of any
  length. The list says nothing matches once 3 or more characters match no
  choice, and the words typed stay in the question and are sent as a
  reporter-added value (`REQ-QB-162`, `REQ-SUB-083`,
  [ADR-0129](../../decisions/ADR-0129-a-type-ahead-value-is-edited-in-place-merged-and-reviewed.md)).
- **One-language choices.** A choice a reporter added in one language is
  offered in that language, marked with it for assistive technology
  (`REQ-QB-103`).
- **Small screens.** The list never makes the page scroll sideways, and a long
  list scrolls within itself (`REQ-QB-163`).

## The single-select and multi-select questions

A single-select and a multi-select look and feel like the type-ahead: the
same question style and list, drawn by the form, with the same rows, separators,
and highlighted row. Unlike the type-ahead, each keeps a caret and shows its
full list as soon as it opens — the threshold and hint above are the
type-ahead's alone. Each keeps its own input type
([ADR-0150](../../decisions/ADR-0150-a-single-select-is-a-select-only-combobox-the-form-draws.md)).

- **Single-select.** A question with a caret shows the chosen choice, or "Choose
  one". It is not the browser's `<select>` (`REQ-QB-208`). It follows the
  WAI-ARIA 1.2 select-only combobox pattern:
  - **Opening.** Clicking it, Enter, Space, the down arrow, or Alt and the
    down arrow opens the list directly beneath it, with the chosen choice
    highlighted.
  - **Moving.** The up and down arrows move the highlighted choice, Home and
    End jump to the ends, typing a character jumps to the next choice
    starting with it, and pointing at a choice highlights it.
  - **Choosing.** Enter or Space takes the highlighted choice and closes the
    list. Escape, Tab, or a press outside closes it without changing the
    answer (`REQ-QB-209`).
  - **Clearing.** "Choose one" is the list's first row; choosing it leaves
    the question unanswered (`REQ-QB-210`).
  - **The pointer** picks a choice by a click on its row (`REQ-QB-268`). The
    rows are `role="option"` and never take focus: the keyboard drives them
    from the question through `aria-activedescendant`, as in the type-ahead
    (`REQ-QB-267`), so a row has no key handler of its own and the click is
    taken once, on the question's container, for whichever row it landed on.
  - **The answer** is held and sent by its choice's identifier, as before.
    Nothing can be typed into a single-select; that stays the type-ahead's.
  - **Disabled.** A dependent single-select waiting on its parent looks and
    behaves disabled, as a type-ahead does (`REQ-QB-197`).
  - **Small screens.** As the type-ahead's (`REQ-QB-163`).
- **Multi-select.** The closed trigger is a combobox button
  (`REQ-SUB-034`, `REQ-SUB-132`). Its open
  list takes the type-ahead's rows and separators, a real checkbox on each
  row, and the type-ahead's highlight on the row pointed at or focused. It
  stays open while several are checked (`REQ-QB-211`).

## Correcting seeded wording

The seeded attachment question departs from Typeform on purpose. Typeform took
one file and asked for the rest by email. This form takes several, so the
question reads "Photos or videos" and asks for images, videos, or documents
(`REQ-QB-104`).

A database seeded before that change is corrected by a migration that follows
the same rule as an Administrator's edit. An unanswered question gets a new
revision (`REQ-QB-105`), and an answered one forks (`REQ-QB-106`). The
migration acts only while the question still carries the exact seeded wording,
so it never overwrites an Administrator's own edit (`REQ-QB-107`).

## The Country pick list

The seeded Country question is an optional single-select of every ISO 3166-1
country (249), not the yes/no "Did the occurrence happen in Canada?" it was
([ADR-0186](../../decisions/ADR-0186-the-country-question-is-a-pinned-country-pick-list-and-province-follows-it.md)).

- Each choice's code is its lowercase alpha-2 code, and its wording is CLDR's
  region name in en-CA and fr-CA. Canada and the United States are pinned first
  and the rest are not pinned, so the open list reads Canada, United States, a
  separator, then the other countries alphabetically in the reader's language
  (`REQ-QB-249`, `REQ-QB-256`). It is optional (`REQ-QB-257`).
- Province is shown only when Country is Canada, and a reporter who leaves
  Country blank is never asked Province (`REQ-QB-258`).
- A migration converts a database seeded before the change by the same rule as
  an Administrator's edit: an unanswered question gets a new revision
  (`REQ-QB-250`), and an answered one forks with its key kept (`REQ-QB-251`,
  `REQ-QB-252` for Province). Old yes/no answers stay on the retired question as
  given. The migration acts only while Country still reads as seeded, so a re-run
  or a changed question is left alone (`REQ-QB-253`, `REQ-QB-254`). A question
  that waited for "answered yes" waits for Canada instead (`REQ-QB-255`).

## The seeded groups on a database created from scratch

The seed groups the reporter's name, phone, and email under **From**, the
pilot's name under **Pilot**, and the aircraft's type, manufacturer, model, and
certification under **Aircraft**. A database created from scratch must send each
of those groups with its questions (`REQ-QB-259`).

`RestoreSeededGroups` re-links a seeded question that lost its group on such a
database (`REQ-QB-260` to `REQ-QB-264`,
[ADR-0187](../../decisions/ADR-0187-a-migration-restores-the-seeded-groups-a-fresh-database-lost.md)).
It acts only while no revision under that key ever had a group and the group is
still live, so an Administrator's own grouping, ungrouping, or deleted group
stands.

The browser suite's seeded form, `tests/e2e/fixtures/seeded-questions.json`, is
exactly what a freshly migrated database sends (`REQ-QB-265`). The scenario that
checks it rewrites it when run with `HPAC_WRITE_SEEDED_FORM_FIXTURE=1`. The group
pages are walked against it (`REQ-QB-266`).

**Not built:** no change to `InitialSchema` or the seed writer, and no
full-stack browser run against a real API.

## Out of scope

What not to build here. The global list in
[system overview](../../system-overview.md) still holds; this narrows it
to this area ([ADR-0083](../../decisions/ADR-0083-specification-driven-development.md)).

- Accepting a date, time, yes/no, or checkbox answer in any shape but its
  stored form, then converting it. The API refuses it instead (`REQ-QB-118`):
  no seconds on a time, no locale date format, no prose, and no string —
  `yes`, `oui`, or `true` — for a yes/no or checkbox, which is a JSON boolean.
  The form's own inputs already send the stored form (ADR-0072, ADR-0130).
- Storing a yes/no or checkbox answer as words, or giving it a second
  language. It is `true` or `false` in `value_boolean`; only the interface
  turns it into Yes / Oui or No / Non
  ([ADR-0130](../../decisions/ADR-0130-a-yes-or-no-answer-is-stored-as-a-boolean.md)).
- Rewriting any other answer. Converting the stored yes/no words to booleans
  (`REQ-QB-137`) was a one-time migration, not a precedent.
- Fetching a type-ahead's choices from the server as the reporter types. The
  form already holds every live choice, and filters them in the browser.
- A combobox library, or the browser's `<datalist>`, for the type-ahead. The
  form draws its own list, as it does the multi-select picker
  ([ADR-0140](../../decisions/ADR-0140-a-type-ahead-is-a-combobox-the-form-draws.md)).
- The browser's `<select>` for a single-select on the report form, or
  type-to-filter and reporter-added values in one. The admin pages keep their
  native selects
  ([ADR-0150](../../decisions/ADR-0150-a-single-select-is-a-select-only-combobox-the-form-draws.md),
  [ADR-0129](../../decisions/ADR-0129-a-type-ahead-value-is-edited-in-place-merged-and-reviewed.md)).
- Correcting any other seeded question's wording by migration. Once a database
  is seeded, an Administrator owns its wording, and the attachment question's
  correction (`REQ-QB-105`) is not a pattern for re-seeding. The Country
  conversion (`REQ-QB-250`) is the same one-off, not a pattern.
- Back-filling or rewriting an old yes/no Country answer, and a flag beside a
  country. The historical import (#566) maps `true` to Canada and `false` to no
  answer; it is not built here.
