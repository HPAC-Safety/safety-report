---
status: partially-superseded
date: 2026-08-22
decision-makers: Chase Florell
keywords: question bank, data-driven, form
---

# ADR-0016: The question set is data, not code

**Status:** Superseded in shape by the
[complete-revision specification](../../features/question-bank-and-form/question-bank-and-form.feature). The
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
  **Narrowed by [ADR-0071](ADR-0071-an-answered-question-forks-instead-of-revising.md):**
  this holds while a question has no answers, and for publication consent
  always. Once any answer references a question, an edit soft-deletes it and
  creates a new question carrying the same stable key.
- The current form examines the latest live, non-deleted revision per stable key
  and never falls back to an older revision. Since ADR-0071 there is at most one
  live question per key, enforced by a partial unique index.
- Report answers reference the exact revision shown, including a stored skip.
  **Narrowed by [ADR-0072](ADR-0072-every-answer-is-stored-as-a-string.md):**
  an answer still records the revision it was given under, but it no longer
  resolves its own content through that revision — it stores its value as a
  string, in the reporter's language.
- Publication consent is the only system/required question and the only answer
  projected onto the report. Every ordinary answer remains generic data.
- Administrators author both language versions. Since
  [ADR-0062](ADR-0062-administrators-may-machine-translate-question-text.md) they
  may use machine translation as a drafting aid while authoring, but nothing
  writes question content on its own: a revision holds exactly what an
  administrator saved, and a question cannot be saved in one language.

The earlier normalized child-row model, typed ordinary-answer projections,
automatic question translation, and creation-time privacy identity are retired.
Their implementation remains visible in repository history and is migration
input only.

## Consequences

The public form and Worker query through revision DTOs rather than hardcoded
fields. A form edit cannot reinterpret an existing answer, and the system gains
no special processing path for one category of ordinary question.

## Related

- [`/features/question-bank-and-form/question-bank-and-form.feature`](../../features/question-bank-and-form/question-bank-and-form.feature)
- [`/features/report-submission/report-submission.feature`](../../features/report-submission/report-submission.feature)
- [ADR-0038](ADR-0038-question-privacy-and-llm-anonymization.md)
- [ADR-0062](ADR-0062-administrators-may-machine-translate-question-text.md)
- [ADR-0071](ADR-0071-an-answered-question-forks-instead-of-revising.md)
- [ADR-0072](ADR-0072-every-answer-is-stored-as-a-string.md)
