# ADR-0016: The question set is data, not code

**Status:** Superseded in shape by the
[complete-revision specification](../../spec/question-bank-and-form.md). The
core decision that questions are database data remains.

**Date:** 2026-08-22

## Context

HPAC must be able to change question wording, options, order, privacy, and live
state without an application deployment. Reports must also retain exactly what
the reporter was asked.

## Current decision

- Each question version is one complete immutable bilingual revision containing
  every value needed to render and validate it.
- Every edit inserts a new revision. Historical rows are never updated.
- The current form examines the latest live, non-deleted revision per stable key
  and never falls back to an older revision.
- Report answers reference the exact revision shown, including a stored skip.
- Publication consent is the only system/required question and the only answer
  projected onto the report. Every ordinary answer remains generic data.
- Administrators provide both language versions; no translation service writes
  question content.

The earlier normalized child-row model, typed ordinary-answer projections,
automatic question translation, and creation-time privacy identity are retired.
Their implementation remains visible in repository history and is migration
input only.

## Consequences

The public form and Worker query through revision DTOs rather than hardcoded
fields. A form edit cannot reinterpret an existing answer, and the system gains
no special processing path for one category of ordinary question.

## Related

- [`/spec/question-bank-and-form`](../../spec/question-bank-and-form.md)
- [`/spec/report-submission`](../../spec/report-submission.md)
- [ADR-0038](ADR-0038-question-privacy-and-llm-anonymization.md)
