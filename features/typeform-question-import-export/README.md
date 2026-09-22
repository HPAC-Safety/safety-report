# Typeform question import and export

Supporting detail for
[`typeform-question-import-export.feature`](typeform-question-import-export.feature)
that does not fit Gherkin. Rationale for *why* each decision was made lives in
[ADR-0077](../../docs/decisions/ADR-0077-typeform-json-import-and-export.md),
not here.

## Type mapping

See ADR-0077's table. It is not repeated here to avoid two copies drifting
apart — read the ADR.

## Correlation key

Typeform's field- and choice-level `ref` GUID, not `id` (which differs
between the English and French exports of the same form) and not field
order (which breaks the moment the two language forms diverge).

## What import never does

- Never persists a `Question`/`QuestionRevision` row by itself. It produces
  drafts an Administrator reviews and saves through the ordinary authoring
  screen (`QuestionEditor`), one at a time.
- Never fabricates a missing translation. A field present in only one file
  is a hard import error, not a partial import.
- Never silently drops content it cannot fully represent. An unsupported
  field type is listed as not imported; unsupported branching logic still
  imports the question, unconditionally, with a pending logic note recorded
  for manual follow-up.

## Current implementation status

Not yet built — every scenario above carries `@ignore`. See
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
