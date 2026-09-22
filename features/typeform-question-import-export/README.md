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
