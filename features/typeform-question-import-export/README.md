---
title: Typeform question import and export
description: Supporting detail for importing and exporting the question bank as Typeform JSON.
type: spec
area: typeform-question-import-export
---

# Typeform question import and export

Supporting detail for
[`typeform-question-import-export.feature`](typeform-question-import-export.feature)
that does not fit Gherkin. This page states the current mapping. Rationale
and history live in
[ADR-0077](../../docs/decisions/ADR-0077-typeform-json-import-and-export.md),
[ADR-0078](../../docs/decisions/ADR-0078-typeform-import-is-english-led-and-defers-all-branching-logic.md),
and
[ADR-0095](../../docs/decisions/ADR-0095-a-question-owns-its-choices-outside-its-revisions.md).
ADR-0077's own mapping table predates ADR-0095. Where they differ, this page
and ADR-0095 hold.

## Type mapping

Import produces review drafts, never persisted rows (see "What import never
does"). Each Typeform field maps to one draft, by its type:

| Typeform `type` | Draft question type |
|---|---|
| `statement` | `statement`, except the generated answer-recap screen, which is skipped (a description that interpolates other fields with `{{field:…}}`) |
| `group`, `contact_info` | `group`, a heading from the field's title, and one draft per nested field or subfield, each grouped under it and mapped by its own type |
| `short_text` | `short_text` |
| `long_text` | `long_text` |
| `email` | `email` |
| `phone_number` | `phone` |
| `date` | `date` |
| `number` | `number` |
| `file_upload` | `file_upload` |
| `yes_no` | `yes_no` |
| `dropdown` | `single_select`. Dropdown versus radio buttons is presentation, so export always writes `multiple_choice`. |
| `multiple_choice`, without multiple selection | `single_select` |
| `multiple_choice`, with multiple selection | `multi_select` |
| any other type (matrix, slider, ranking, …) | not imported, and listed in the import report as unsupported |

A plain Typeform file never produces an `autocomplete` (type-ahead),
`checkbox`, or `time` question. Only the `hpac` extension below can name one.

### Choices

- A `multiple_choice` or `dropdown` field's choices become the draft's own
  choices. Each choice's code is its `ref`, and its French label is the
  French file's choice with the same `ref`.
- Saved, the question owns that list alone. An import never merges one
  question's choices into another's
  ([ADR-0095](../../docs/decisions/ADR-0095-a-question-owns-its-choices-outside-its-revisions.md)).

### Reporter additions

- `allow_other_choice` is ignored. Only a type-ahead takes a value a reporter
  adds
  ([ADR-0063](../../docs/decisions/ADR-0063-a-reporter-may-add-a-type-ahead-choice.md),
  [ADR-0095](../../docs/decisions/ADR-0095-a-question-owns-its-choices-outside-its-revisions.md),
  [ADR-0129](../../docs/decisions/ADR-0129-a-type-ahead-value-is-edited-in-place-merged-and-reviewed.md)),
  so a multi-select imported with it takes no additions.
- A type-ahead comes only from a file whose field carries the `hpac`
  extension naming that type.

### Other fields of a draft

- **Help text**: the field's description, paired with the French description
  by `ref`. A field with no English description has no help text.
- **Private and required**, without the extension: a heading (`statement`
  or `group`) is not private. Every other draft is private and not required,
  for the Administrator to change before saving.
- **Condition**: none. A real branching rule becomes a pending logic note
  instead (see "What import never does").

### The `hpac` extension

A file this system exported carries an `hpac` object on each field. When
present, it wins over the Typeform type and the defaults above. It sets:

- the exact question type, which tells a `group` from a `statement` and a
  type-ahead (`autocomplete`) from a `single_select`;
- private and required;
- the condition: the question it depends on, by key, and the parent's choice
  code;
- the group it sits under, by key.

## Correlation key

Typeform's field- and choice-level `ref` GUID, not `id` (which differs
between the English and French exports of the same form) and not field
order (which breaks the moment the two language forms diverge).

## What import never does

- Never persists a `Question`/`QuestionRevision` row by itself. It produces
  drafts an Administrator reviews and saves through the ordinary authoring
  screen (`QuestionEditor`), one at a time.
- Never fabricates a translation. The English file leads; a field or choice
  with no French counterpart by `ref` defaults its French side to the English
  text and is flagged for an administrator to author, and a French-only field
  is not imported
  ([ADR-0078](../../docs/decisions/ADR-0078-typeform-import-is-english-led-and-defers-all-branching-logic.md)).
- Never silently drops content it cannot fully represent. An unsupported
  field type is listed as not imported; unsupported branching logic still
  imports the question, unconditionally, with a pending logic note recorded
  for manual follow-up.

## Current implementation status

Built: every scenario above is bound and runs. See
[implementation status](../../docs/implementation-status.md).

## Out of scope

What not to build here. The global list in
[system overview](../../docs/system-overview.md) still holds; this narrows it
to this area ([ADR-0083](../../docs/decisions/ADR-0083-specification-driven-development.md)).

- A live connection to Typeform: no API sync, no webhook, no scheduled poll.
  Import and export move files a human supplies or downloads.
- Auto-mapping a real branching rule. Every one is recorded as pending for an
  administrator to wire by hand
  ([ADR-0078](../../docs/decisions/ADR-0078-typeform-import-is-english-led-and-defers-all-branching-logic.md)).
- Importing a question straight into the bank. An import produces drafts that
  an administrator reviews through the ordinary editor.
- QSF or any other vendor format
  ([ADR-0077](../../docs/decisions/ADR-0077-typeform-json-import-and-export.md)).
- Machine-translating an imported question. The import carries what the two
  files say; an administrator authors the rest.
