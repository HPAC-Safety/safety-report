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
  ([ADR-0138](../../decisions/ADR-0138-a-date-question-allows-future-dates-only-when-it-says-so.md));
- creation timestamp and the revision it supersedes, when any;
- a nullable `deleted` timestamp.

A question's choices are not part of any revision. A single-select,
multi-select, or type-ahead question owns one list of choices, edited in
place: adding, changing, pinning, or removing one never creates a revision and
never retires the question, even once it has been answered. A fork carries a
copy of the whole list, removed choices, pins, and reporter-added marks
included, to the replacement
([ADR-0095](../../decisions/ADR-0095-a-question-owns-its-choices-outside-its-revisions.md)).

The list has no order of its own. Wherever choices are shown — the report
form's single-select, multi-select, and type-ahead, the question editor's
options, the required-option control, the type-ahead review page, and a
multi-select answer on a report — they are listed alphabetically in the
reader's language, ignoring accents and case, so the English and French lists
may differ in order. An Administrator may pin a choice **first** or **last**;
by default it is not pinned. The list shows three groups in turn — pinned
first, not pinned, pinned last — each alphabetical, with a separator between
groups. A value a reporter adds is not pinned and takes its alphabetical place
at once. The editor re-sorts its options when it opens, never while the
Administrator is typing
([ADR-0136](../../decisions/ADR-0136-choices-are-listed-alphabetically-in-the-readers-language.md)).

An answer names its choice by identifier and copies none of its wording; both
languages are read from the choice. A removed choice is hidden from the form,
never erased, and every answer that named it still names it and reads its
wording ([ADR-0128](../../decisions/ADR-0128-an-answer-names-its-choice-and-a-picker-option-is-fixed-or-replaced.md)).

- **Picker options** (single-select, multi-select) are authored by an
  Administrator in both languages. Changing an option's wording, they choose
  to **fix it in place** (same choice; every answer reads the fix) or
  **replace it** (the old choice is retired, still named by every earlier
  answer, and a new choice takes its place). A condition naming a replaced
  choice follows it to its replacement ([ADR-0128](../../decisions/ADR-0128-an-answer-names-its-choice-and-a-picker-option-is-fixed-or-replaced.md)).
  A condition also follows its parent when the parent forks: the dependent
  question is not revised, and the form and the editor name the live question
  that replaced the parent and its copy of the choice
  ([ADR-0132](../../decisions/ADR-0132-a-condition-follows-its-parent-through-a-fork.md)).
- **Type-ahead values** are corrected in place for every answer that names
  them, removed by soft delete, and merged: merging B into A retires B, and
  answers naming B read A without being rewritten. A value a reporter adds is
  flagged for review, offered at once in the language it was typed, and given
  its other language by the Worker. A Safety Officer or an Administrator
  reviews it ([ADR-0129](../../decisions/ADR-0129-a-type-ahead-value-is-edited-in-place-merged-and-reviewed.md)).
  A merged value's wording is not only resolved at submission: the survivor
  carries it as an **alias**, so the form offers the survivor while a
  reporter is still typing it, with a hint naming the alias, in either
  language and following a chained merge with nothing for the client to
  chase (`REQ-QB-233`–`REQ-QB-236`). The type-ahead review page lists each
  value's aliases too, read-only — there is still no un-merge
  (`REQ-QB-237`, ADR-0129 amendment).

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

## Choices that depend on another question

A single-select or type-ahead question's choices may **depend on** another
single-select or type-ahead question, its *parent*: a paraglider's model
depends on its make. Every live choice of the *child* then names **one or
more** parent choices, and the form offers it whenever the parent's answer is
any one of them (`REQ-QB-179`–`REQ-QB-228`,
[ADR-0151](../../decisions/ADR-0151-one-dependent-choice-may-be-offered-under-several-parent-choices.md),
which supersedes
[ADR-0146](../../decisions/ADR-0146-a-choice-list-may-depend-on-another-questions-answer.md)).
It is not a condition: a condition decides whether a question is shown, a
dependency decides which of its choices are offered, and a question may be
both.

- **Shape.** One level only: a child is nobody's parent, and a parent depends
  on nothing. The parent is asked before the child, on every save and every
  reorder, judged by where each is asked: a grouped question on its group's
  page (`REQ-QB-180`, `REQ-QB-181`, `REQ-QB-206`). The manage page shows a
  refused reorder (`REQ-QB-205`).
- **Links sit outside revisions.** The dependency is on the question and each
  link beside its choice, so setting, changing, or clearing either never
  revises or forks a question (`REQ-QB-184`). A model sold under two makes, or
  a catch-all such as "Other", is one choice offered under each make it
  applies to. Wording is unique on the child, in either language, ignoring
  case and whitespace (`REQ-QB-212`, `REQ-QB-213`). An unticked link is
  stamped removed, never erased, and ticking it again restores it.
- **The editor.** With a parent set, every choice row, a new one included, has
  an "Offered under" multi-select of the parent's live choices, listed as the
  form lists them. A save with any choice offered under nothing is refused,
  naming the choices (`REQ-QB-212`, `REQ-QB-222`). Clearing the parent keeps
  every link; they stop filtering (`REQ-QB-185`).
- **Following the parent.** A replaced picker parent choice, a merged
  type-ahead parent value, and a forked parent question each pass their links
  on at once, without revising the child (`REQ-QB-187`–`REQ-QB-190`). A link
  passed onto a parent choice the child choice already names collapses into
  one (`REQ-QB-215`). A parent choice is removed only while every child choice
  under it keeps another live parent; its links then stay and filter nothing,
  and the child still saves and its values still take new parents, since a
  save checks only the parent choices it newly ticks. Otherwise the removal is refused, naming the child choices, and the parent
  choice is replaced or merged instead (`REQ-QB-214`).
- **The form.** The child is disabled until the parent is answered, then offers
  only the choices under that answer. Changing the parent keeps a picked choice
  that is also under the new answer, and clears one that is not; typed words
  stay and are sent as typed, even when they read as a choice under another
  answer (`REQ-QB-197`, `REQ-QB-198`, `REQ-QB-223`, `REQ-QB-227`). A
  disabled child never holds the reporter back, even when required; nor does
  a single-select child with nothing under the parent's answer, which says so.
  The form leaves such a child out of the submission, and the API records
  nothing for it (`REQ-QB-201`, `REQ-QB-204`, `REQ-SUB-114`). A polite live
  region tells a screen reader when the parent's answer opens the child. A
  parent answered with a new typed value leaves a type-ahead child nothing to
  pick and a value to type (`REQ-QB-199`). A saved report restores both
  answers, dropping a child answer no longer under the parent's
  (`REQ-QB-200`, `REQ-QB-223`).
- **A parent the form does not ask** — deactivated, or deleted rather than
  forked — filters nothing, as a condition whose parent is missing hides
  nothing, and the API does not check the link (`REQ-QB-203`).
- **Reporter-added values.** A value typed into a dependent type-ahead is
  matched against the whole question, ignoring case and whitespace. A match
  already under the parent's answer is named as it is (`REQ-QB-216`). A live
  match under another answer gains a link to the parent's answer and is
  flagged for review (`REQ-QB-217`); a merged match does the same through its
  target (`REQ-QB-218`); a removed match comes back flagged and is not revived
  (`REQ-QB-219`). A new value is offered under the parent's answer, even when
  that answer is itself a new value (`REQ-QB-192`).
- **Review.** A Safety Officer or an Administrator adds or removes a value's
  parents on the type-ahead review page, never down to none. A merged value's
  parents are not changed: it reads as its target (`REQ-QB-220`,
  `REQ-QB-224`). Merging two values offers the survivor under every parent
  either was under (`REQ-QB-221`).
- **The API.** A submission naming a child choice not offered under the
  parent's answer, or answering the child while the parent is unanswered, is
  refused by question key before anything is written (`REQ-SUB-113`,
  `REQ-SUB-115`). An answer still names only its own choice; nothing about the
  parent is copied into it.
- **The migration** folds each old single link into the join table, then
  merges a dependent question's live choices whose English and French wording
  both match: the oldest survives under every parent the copies had, and each
  other copy is retired into it without rewriting any answer. A pair matching
  in one language only is left for an Administrator (`REQ-QB-225`,
  `REQ-QB-226`, `REQ-QB-228`).

## The type-ahead field

A type-ahead question looks like the form's other pickers: one field, whose
list opens directly beneath it, as wide as the field and drawn in the form's
own surface, font, border, and focus ring. The form draws that list itself;
the browser's own suggestion list (`<datalist>`) is not used, so it looks the
same in every browser (`REQ-QB-159`,
[ADR-0140](../../decisions/ADR-0140-a-type-ahead-is-a-combobox-the-form-draws.md)).
Unlike the single-select and the multi-select, it has **no caret**: it reads
as a place to type, not a dropdown to pick from
([ADR-0152](../../decisions/ADR-0152-a-type-aheads-list-opens-with-a-hint-below-3-characters.md)).

- **Opening.** Clicking the field, pressing Alt and the down arrow, or typing
  opens the list.
- **A hint below 3 characters.** Trimmed of spaces, fewer than 3 typed
  characters shows the open list with no choices, only a hint row: "Type 3 or
  more letters to see matching choices, or enter your own." No option is
  active there, so the up and down arrows and Enter do nothing. Reaching 3
  characters replaces the hint with the matching choices; deleting back below
  3 brings the hint back. This is announced to assistive technology through a
  polite live status (`REQ-QB-159`, `REQ-QB-229`, `REQ-QB-230`,
  [ADR-0152](../../decisions/ADR-0152-a-type-aheads-list-opens-with-a-hint-below-3-characters.md)).
  A dependent type-ahead follows the same rule, on top of its own narrowing by
  the parent's answer (`REQ-QB-231`, ADR-0146).
- **Reopening filters by what the field holds.** Closing the list and opening
  it again — by any of the ways above — filters by the field's current text
  exactly as typing it would: the hint below 3 characters, only the matching
  choices at 3 or more. It never shows every choice unfiltered on reopen,
  whatever the field holds (`REQ-QB-232`, ADR-0152).
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
  open the list without moving, and Escape or a press outside the field
  closes it, keeping what the field holds (`REQ-QB-161`). Tab moves on and
  closes it too (`REQ-QB-162`). The field follows the WAI-ARIA 1.2 combobox
  pattern, and keeps its label, help text, and error.
- **A picked choice.** A choice taken from the list is sent as that choice,
  by its identifier, even where another choice carries the same wording
  (`REQ-QB-171`). Text typed without picking is matched to a choice by its
  wording, ignoring case, or else sent as typed.
- **A value it does not offer.** The reporter may still type one, of any
  length. The list says nothing matches once 3 or more characters match no
  choice, and the words typed stay in the field and are sent as a
  reporter-added value (`REQ-QB-162`, `REQ-SUB-083`,
  [ADR-0129](../../decisions/ADR-0129-a-type-ahead-value-is-edited-in-place-merged-and-reviewed.md)).
- **One-language choices.** A choice a reporter added in one language is
  offered in that language, marked with it for assistive technology
  (`REQ-QB-103`).
- **Small screens.** The list never makes the page scroll sideways, and a long
  list scrolls within itself (`REQ-QB-163`).

## The single-select and multi-select fields

A single-select and a multi-select look and feel like the type-ahead: the
same field style and list, drawn by the form, with the same rows, separators,
and highlighted row. Unlike the type-ahead, each keeps a caret and shows its
full list as soon as it opens — the threshold and hint above are the
type-ahead's alone. Each keeps its own input type
([ADR-0150](../../decisions/ADR-0150-a-single-select-is-a-select-only-combobox-the-form-draws.md)).

- **Single-select.** A field with a caret shows the chosen choice, or "Choose
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
    from the field through `aria-activedescendant`, as in the type-ahead
    (`REQ-QB-267`), so a row has no key handler of its own and the click is
    taken once, on the field's container, for whichever row it landed on.
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
question reads "Photos or videos" and asks for photos, videos, or documents
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

### The seeded groups on a database created from scratch

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
- For choices that depend on another question
  ([ADR-0151](../../decisions/ADR-0151-one-dependent-choice-may-be-offered-under-several-parent-choices.md)):
  - a multi-select parent or child;
  - chains deeper than one level (make → model → size);
  - a child choice under no parent choice, shown whatever the parent's answer.
    A choice for every parent ticks every parent. The one unlinked choice is a
    value a reporter typed while the parent was off the form; it waits for a
    reviewer to link it;
  - bulk linking: pasting a list, or ticking a parent across many rows at
    once. An Administrator ticks each choice's parents on its own row;
  - merging duplicates whose wording differs, in either language;
  - copying the parent's answer, or anything about it, into a child answer.
- Machine translation on the submission path. Translation is administrator-
  initiated while authoring, or Worker-run off the submission path
  ([ADR-0080](../../decisions/ADR-0080-every-answer-gets-a-worker-translated-second-language.md)).
- Translating every choice at once, or automatically. An administrator
  translates one choice at a time from the Choices panel, in the direction its
  switch shows: a choice missing its other language, or one whose source was
  edited. The wording's Translate translates the question and help text that
  need it, never a choice or a placeholder. Each result is a draft saved only
  on Save
  (`REQ-QB-164`–`REQ-QB-170`, `REQ-QB-172`–`REQ-QB-178`,
  [ADR-0141](../../decisions/ADR-0141-a-choice-is-translated-on-request-in-a-chosen-direction.md),
  [ADR-0144](../../decisions/ADR-0144-the-wording-is-translated-on-request-in-a-chosen-direction.md)).
- Translating a choice on the submission path. A reporter-added type-ahead
  value gets its other language from the Worker
  ([ADR-0129](../../decisions/ADR-0129-a-type-ahead-value-is-edited-in-place-merged-and-reviewed.md)).
- Keeping a brand name untranslated. The administrator corrects the draft.
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
  ([ADR-0130]../../.spec/decisions/ADR-0130-a-yes-or-no-answer-is-stored-as-a-boolean.md)).
- Rewriting any other answer. Converting the stored yes/no words to booleans
  (`REQ-QB-137`) was a one-time migration, not a precedent.
- Shared choice lists, or reusing one question's choices on another in any
  form ([ADR-0095](../../decisions/ADR-0095-a-question-owns-its-choices-outside-its-revisions.md)).
- A reporter editing, curating, or removing a choice. A reporter may add a
  missing value to a type-ahead; a Safety Officer or an Administrator reviews
  it ([ADR-0129](../../decisions/ADR-0129-a-type-ahead-value-is-edited-in-place-merged-and-reviewed.md)).
- A reporter adding a choice to a single-select or multi-select question.
- A record of exactly which choices a reporter was shown. The answer names the
  choice it was given under ([ADR-0128](../../decisions/ADR-0128-an-answer-names-its-choice-and-a-picker-option-is-fixed-or-replaced.md)).
- Holding a reporter's new type-ahead value back until it is approved. It is
  offered at once and reviewed afterwards ([ADR-0129](../../decisions/ADR-0129-a-type-ahead-value-is-edited-in-place-merged-and-reviewed.md)).
- Merging picker options, or a Safety Officer editing one. A picker option is
  fixed or replaced by an Administrator ([ADR-0128](../../decisions/ADR-0128-an-answer-names-its-choice-and-a-picker-option-is-fixed-or-replaced.md)).
- Replacing a type-ahead value, or un-merging one. A type-ahead value is only
  ever corrected in place ([ADR-0129](../../decisions/ADR-0129-a-type-ahead-value-is-edited-in-place-merged-and-reviewed.md)).
- Rewriting an answer to name a different choice, on merge, replacement, or
  migration. Readers follow the link instead.
- Ordering choices by hand: dragging them, moving them up or down, or letting
  a question choose between the order they were written in and alphabetical
  order. Choices are alphabetical, apart from pinning
  ([ADR-0136](../../decisions/ADR-0136-choices-are-listed-alphabetically-in-the-readers-language.md)).
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
  correction (`REQ-QB-105`) is not a pattern for re-seeding. The Country
  conversion (`REQ-QB-250`) is the same one-off, not a pattern.
- Back-filling or rewriting an old yes/no Country answer, and a flag beside a
  country. The historical import (#566) maps `true` to Canada and `false` to no
  answer; it is not built here.
