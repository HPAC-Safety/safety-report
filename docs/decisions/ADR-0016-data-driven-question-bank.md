# ADR-0016: The question set is data, not code

**Status:** Accepted
**Date:** 2026-08-22

## Context

`docs/form-spec.md` is the question set HPAC has been collecting through
Typeform, and the obvious way to build it is a fixed set of typed columns: a
`Report` with `PilotInjury`, `Province`, `OccurredOn`, and so on.

That works exactly until someone at HPAC wants to ask a new question. Every
wording change, every new choice in a dropdown, every reordering becomes a
migration, a deploy, and a developer. For a volunteer association running a
safety programme, that is the difference between a form that stays current and
one that quietly stops matching what the safety committee needs to know.

At the same time, this system has invariants that read specific answers:
publication consent gates everything, a serious injury escalates notification,
and an aircraft is published as a certification class and never as a brand.
Those cannot become string lookups nobody can typecheck.

## Decision

**Questions are rows.** `questions`, `question_versions`, `question_options`,
`question_translations`, and `question_option_translations` hold the form.
Answers land in `report_answers`, one row per question answered.

Five decisions inside that one:

### 1. A small number of answers additionally project onto typed properties

`Report.ConsentPublish`, `OccurredOn`, `Province`, `PilotInjury`, and
`PassengerInjury` remain typed. They are populated from answers at submit time
rather than being separate inputs.

### 2. Projection is driven by an optional role, not by a fixed key

A question carries a `QuestionRole` — `PilotInjury`, `OccurrenceDate`,
`Province`, and so on — which an administrator may move to a different question
or clear entirely. Logic asks "which question carries the injury role" instead of
hardcoding a key.

**Every role is optional and its absence is a defined state.** With no injury
question on the form, severity is `NotAnswered` and a report takes the ordinary
review path instead of the escalated one. Nothing crashes and nothing silently
mis-handles a fatality as a non-injury.

### 3. Publication consent is the one system question

`consent_publish` cannot be deleted, deactivated, or retyped. Its wording is
editable and it reorders like any other question.

It is the exception because it is the gate: there is no defined behaviour for
"publish a report whose consent question does not exist". Everything else —
injury, date, province, aircraft — is ordinary data, on the reasoning that the
handful of people who can edit the question bank are not going to delete the
injury question by accident, and if they do, the degradation above is safe.

**Consent has no default.** It is `YesNo`, required, and stored as `bool?` so
that unanswered is distinguishable from no. A pre-selected radio button is not a
consent, and an unreadable consent answer is an error rather than a quiet no.

`YesNo` is therefore a fixed two-answer type: its codes are `yes` and `no`, it
cannot be given a third option, and its labels are ordinary UI chrome from
`locales/` rather than option rows.

### 4. Versions are immutable

Rewording, retyping, or changing the option set creates a new `QuestionVersion`,
and `report_answers` references the version it was answered under. A report from
last season still renders the question it was actually answering.

Reordering, activating, and deactivating are **not** versioned — none of them
changes what an answer means. An option's `Code` never changes either, so a
rename is a translation change and historical answers keep pointing at the same
thing.

### 5. Question text lives in the database, not in `locales/`

`question_translations` holds one row per locale, with `is_source` marking the
language a human wrote in and `is_machine_translated` marking text nobody has
reviewed — the same shape `summaries` uses.

A question is authored in one language and **auto-translated into the other,
in both directions**, through the `ITranslator` already declared for summaries.
The authoring locale comes from the browser, using the detection in
`docs/localization.md`. A question cannot be activated with a missing
counterpart; a machine-translated counterpart is acceptable, an absent one is
not.

## Consequences

- HPAC edits its own form. Adding a question is an afternoon, not a release.
- The invariants stay enforceable in `Core`, in plain unit tests, with no
  database.
- `Report` is smaller than the form: only what logic reads is a property.
- Answers are versioned, so historical reports stay honest about what they were
  asked. Reporting across a reworded question needs care — that is real
  complexity this decision accepts.
- There are now two translation paths, which is the cost most likely to confuse:
  UI chrome is translated in CI and reviewed by a human, question content is
  translated at authoring time through `ITranslator`. `docs/localization.md`
  states the split.
- The admin UI has to exist for any of this to be usable — #49 builds it, #50
  adds drag-and-drop ordering.

## Alternatives rejected

**Fixed typed columns.** Simplest, fastest to build, and the reason the current
Typeform is where it is: changing it requires whoever owns the tool. Rejected
because the form is the product and HPAC has to own it.

**Pure EAV, nothing typed.** Maximum flexibility. Rejected because consent,
severity, and aircraft class are read by invariant-bearing logic, and turning
compiler-checked types into string lookups in the one part of the system that
must not fail is a bad trade.

**Every role a locked system question.** The first draft of this ADR made
injury, date, province, and aircraft type undeletable too. Rejected: the people
with access are few and deliberate, the failure mode is already safe, and
locking questions "just in case" is how a data-driven form becomes a fixed form
with extra tables.

**Question text as keys in `locales/`.** Would keep one translation pipeline.
Rejected because a question created in the admin UI would then need a code change
and a deploy before it rendered, which defeats the point.

**Snapshotting the label onto each answer instead of versioning.** Cheaper.
Rejected because it captures the wording and not the option set, so a
reinterpreted choice still changes the meaning of old answers.

## Related

- `docs/localization.md`, `docs/data-handling.md`, `docs/form-spec.md`
- `skills/incident-domain-model/SKILL.md`
- [ADR-0007](ADR-0007-localization.md), [ADR-0004](ADR-0004-human-review-required.md)
- Issues #6, #7, #49, #50
