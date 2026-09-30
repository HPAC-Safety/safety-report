---
title: A choice answer keeps its submitted wording
description: A single-select, multi-select, or type-ahead answer keeps the exact label the reporter saw and chose as an immutable column, separate from the choice it names by ID; the reporter's account is read from it everywhere, and the choice's current official value is secondary context.
type: adr
status: accepted
date: 2026-09-29
decision-makers: Chase Florell
keywords: choices, answers, submitted wording, single-select, multi-select, type-ahead, choice ID, admin detail, report content, marking pass, ADR-0128, ADR-0129, ADR-0082
---

# ADR-0175 — A choice answer keeps its submitted wording

## Status

Accepted. Part of [#667](https://github.com/HPAC-Safety/safety-report/issues/667),
which is part of [#665](https://github.com/HPAC-Safety/safety-report/issues/665).
This ADR:

- **amends**
  [ADR-0128](ADR-0128-an-answer-names-its-choice-and-a-picker-option-is-fixed-or-replaced.md):
  "An answer names its choice by ID" said an answer "stores no label" and
  "copies none of its wording." That is superseded below: an answer still
  names its choice by ID, but it also keeps its own immutable copy of the
  wording it was given under. Every other line of ADR-0128 — fixing versus
  replacing a picker option, a condition following a replacement, a fork
  copying choices as new rows — stands.
- **amends**
  [ADR-0129](ADR-0129-a-type-ahead-value-is-edited-in-place-merged-and-reviewed.md):
  "Answers are not rewritten. An answer naming B resolves to A wherever it is
  read... and shows A's labels" is superseded for the reporter's own account,
  below. The choice an answer names still resolves through a merge chain
  exactly as before — that resolution is what `ChoiceId` is for now (grouping,
  search, filters, counts, dependent choices) — but the reporter's account is
  no longer read from the resolved choice at all. Every other line of
  ADR-0129 stands.
- **amends** AGENTS.md invariant 1 ("An answer names its choice by ID",
  "Picker options", "Type-ahead values", "Answers") and invariant 3's
  `report_content` line.

## Context

ADR-0128 replaced a choice answer's copied label with a reference: the
answer names a `QuestionChoice` by ID, and both languages are read from the
choice as it stands today. That let an Administrator fix a typo for every
answer, or replace an option whose meaning changed while leaving old answers
naming the old one — but the old one's *wording* still reads however the
choice reads today, because nothing about the choice's own words is pinned
to the moment any one reporter chose it. A safety officer reading an old
report cannot tell, from the report alone, whether "Cooper's Hill" is exactly
what the reporter typed or a later correction; the Worker's model input and
the ADR-0082 marking pass silently pick up a later edit too, for a report the
model may have already summarized differently before that edit landed.

The owner ruled (issue #667, and its follow-up comment): the reporter's
submitted wording is the reporter's account, always, and it must be
recoverable independent of anything that happens to the choice afterward.

## Decision

### An answer keeps its submitted wording, immutably

- `report_answers` gains `submitted_wording text`, set once when the answer
  is recorded and never written again — like `value`, `choice_id`, and
  `locale`, nothing on the submission path or anywhere else ever overwrites
  it. `ReportAnswer.SubmittedWording` is `private init`; there is no
  mutator, in anticipation of [#669](https://github.com/HPAC-Safety/safety-report/issues/669)
  (database triggers), which can enforce the same rule at the database level
  without this ADR changing.
- It holds the exact label the reporter saw and chose, in `Locale`:
  - naming a live choice by ID (the submission's normal path): the choice's
    label in the reporter's language, as it read at that moment;
  - naming a choice by matched text (a single-select or type-ahead answered
    by exact label): that text, as sent;
  - a type-ahead value the reporter added: exactly what they typed.
- It is set exactly when `ChoiceId` is: `ck_report_answers_submitted_wording_needs_choice`
  enforces `(choice_id IS NULL) = (submitted_wording IS NULL)`.
- `ChoiceId` is unchanged by this ADR: the answer still names one row of
  `question_choices` by ID, and a fix, replace, or merge still re-points what
  that choice **resolves to** exactly as ADR-0128 and ADR-0129 describe.
  Nothing about resolving a choice changes.

### The reporter's submitted wording is always the source of truth for their account

**Every read of a choice answer's own wording — its account, in its own
words — reads `SubmittedWording`, never the choice it resolves to. The
choice's current official value is secondary context, offered beside it only
where a person is comparing the two.** Concretely:

- **The Worker's `report_content` and `private_context`, and the ADR-0082
  deterministic marking pass that runs on them,** read a choice answer's
  submitted wording only. A fix, a replace, or a merge made after a report
  was submitted must never change what the model sees for that report, or
  what the marking pass matches against private context for it. This also
  simplifies `SummarizeReportProcessor`: it no longer joins `question_choices`
  or follows `MergedInto` to build a field's value.
- **Admin report detail** shows the reporter's account as `SubmittedWording`
  (`ReportAnswerValueView.Value`, `ReportAnswer.Text` in the domain). When the
  choice's current official label (`ReportAnswer.OfficialText`) reads
  differently — a fix, a replace, or a merge landed since — the view also
  carries it as `OfficialValue`, shown beside the account, never in place of
  it. They read the same, `OfficialValue` is null and nothing extra is shown.
- **Any future export of a report's answers** reads `SubmittedWording` the
  same way; there is no path that legitimately needs the resolved choice's
  wording as an answer's own account.
- **`ChoiceId` stays for grouping only**: search, filters, counts, and
  dependent-choice checks keep resolving through it to the choice's current
  official value, exactly as ADR-0128 and ADR-0129 already do. Grouping two
  reports as "answered the same thing" is a different question from "what did
  each reporter's account say", and this ADR only answers the second one.
  `admin_report_search_document` already reads `question_choices.label_en`/
  `label_fr` directly, not an answer's stored text, so admin search is
  unaffected.
- **The second language of a choice answer is still a lookup on the choice**
  (`TranslationSource.Choice`, ADR-0112) — untouched by this ADR. Submitted
  wording is recorded only in the language it was given in; translating it
  is not this decision's problem, and the choice's other-language label
  remains the best available second language exactly as before.

### Existing rows are backfilled, not rewritten

Migration `20260929231509_StoreChoiceAnswersSubmittedWording`:

1. A select answer stored **before** ADR-0128 already copied its label into
   `value`; that copied label is exactly what the reporter was shown, so it
   becomes `submitted_wording` verbatim.
2. Every other existing choice answer copied no wording at all, and answers
   are immutable (ADR-0080) — what the reporter actually saw is not
   recoverable. It is backfilled from the choice's current official label in
   the answer's own language, following one merge hop (ADR-0129 flattens
   merge chains when they are made, so this is never more than one hop). This
   is the best record available. **Wording changed before this migration
   ran cannot be recovered**; a row backfilled this way may already differ
   from what was actually shown, and there is no way to tell which rows do.

Neither step touches `value` or `choice_id`.

## Rejected alternatives

- **Reconstruct submitted wording from an audit log.** None exists for
  `question_choices` edits before this ADR; there is nothing to reconstruct
  from.
- **Leave old rows' `submitted_wording` null and treat null as "unknown, fall
  back to the choice."** Rejected: it would mean two different meanings for
  the same null (a boolean/text answer with no choice at all, versus a choice
  answer with a genuinely unrecoverable account), and every reader would need
  to know the difference. Backfilling with the best available guess, even an
  imperfect one, keeps the column's contract simple: non-null exactly when
  `choice_id` is.
- **Keep reading the resolved choice for `report_content`/the marking pass and
  only add the column for admin display.** Rejected by the owner (issue
  comment): the model input and the marking pass are exactly the two readers
  where a silent, unannounced change to an already-summarized report's
  content matters most.
- **Rewrite `ChoiceId` resolution to stop following merges/replacements for
  new answers.** Out of scope: grouping (search, filters, counts, dependent
  choices) still needs the current answer, and this ADR does not touch it.

## Consequences

- `report_answers` gains `submitted_wording` and
  `ck_report_answers_submitted_wording_needs_choice`.
- `ReportAnswer` gains `SubmittedWording` (immutable) and `OfficialText`
  (computed, from the loaded `Choice`); `Text` and `ValueIn` now read
  `SubmittedWording` for a choice answer's own language instead of the
  choice's current label.
- `SummarizeReportProcessor`'s query drops its join through `answer.Choice`
  and `MergedInto` entirely; it reads `answer.SubmittedWording` directly. No
  Worker prompt version change: the prompt's instructions are unchanged, only
  which stored value fills `report_content`/`private_context` for a choice
  answer.
- `ReportAnswerValueView` gains `OfficialValue`; the web admin report detail
  page shows it beside the reporter's account when they differ.
- Every writer of a choice answer (the submission endpoint, through
  `Report.Answer`/`Report.AnswerChoices`; Typeform import, which writes
  questions and choices, never answers; test seeds and fixtures) is
  unaffected in shape — `ReportAnswer.ForChoice` and the choice-matching path
  through `ReportAnswer.For` now also set `SubmittedWording`, with no new
  caller-supplied parameter.

## Related

- [ADR-0128](ADR-0128-an-answer-names-its-choice-and-a-picker-option-is-fixed-or-replaced.md) — amended
- [ADR-0129](ADR-0129-a-type-ahead-value-is-edited-in-place-merged-and-reviewed.md) — amended
- [ADR-0082](ADR-0082-a-deterministic-marking-pass-precedes-the-one-model-call.md) — its `report_content`/`private_context` inputs, now sourced from submitted wording for a choice answer
- [ADR-0112](ADR-0112-only-answers-that-need-it-get-a-second-language.md) — a choice answer's second language, unaffected
- [issue #667](https://github.com/HPAC-Safety/safety-report/issues/667), [issue #665](https://github.com/HPAC-Safety/safety-report/issues/665), [issue #654](https://github.com/HPAC-Safety/safety-report/issues/654)/[PR #664](https://github.com/HPAC-Safety/safety-report/pull/664) — a merged value's aliases are its merged-away wording, a property of the choice; this ADR's `SubmittedWording` is a property of one answer, and the two are independent
