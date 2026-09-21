---
status: accepted
date: 2026-09-20
decision-makers: Chase Florell
keywords: required questions, product invariant, question bank, consent, submission validation
---

# ADR-0061 — An administrator may make any question mandatory; consent is merely the one that cannot be optional

## Context

Product invariant #1 has said, since the question bank was designed:

> Only explicit publication consent is a required system question; every
> ordinary question may be skipped.

That was implemented literally. `QuestionRevision` derived
`IsRequired = isSystem`, so the flag was not a field an administrator could
author at all, and the scenario *"consent_publish is the only required
question"* pinned it.

The reasoning behind it is good and worth keeping in view. This system receives
reports about crashes, sometimes fatal ones, sometimes written by the person
they happened to. A form that refuses to submit until every box is filled is a
form that collects fewer reports, and a report that was never filed teaches
nobody anything. Every mandatory field is a chance for a reporter to give up.

The counter-argument is that some questions are not optional in practice. A
report with no occurrence date and no location is close to useless for trend
analysis, and HPAC — not this repository — is the body that decides what it
needs. The invariant took that decision away from them permanently, in code,
with no way to revisit it short of a deploy. That is precisely the thing
ADR-0016 set out to stop: the question set is data, and "is this question
mandatory" is a property of a question.

The owner ruled that the invariant changes.

## Decision

**`question_revisions.is_required` becomes an authored field.** An
administrator sets it per question, per revision, on the authoring screen, and
changing it creates a new revision like every other edit.

**Publication consent is still forced.** `QuestionRevision` takes `isRequired`
from the caller but computes `IsSystem || isRequired`, so consent is required
whatever is passed and there is no route by which it becomes optional. A form
that lets a reporter skip consent cannot publish anything.

**The invariant is rewritten, not quietly reinterpreted.** AGENTS.md's product
invariant #1 and the `consent_publish is the only required question` scenario
both change in the pull request that lands this, per
[ADR-0047](ADR-0047-feature-files-must-not-contradict-adrs.md). What survives
of the old rule is the part that was actually load-bearing: **consent is the
only *system* question**, and it is the only answer any other logic reads by
name.

## Consequences

- Submission validation gains a second reason to reject: a missing answer to a
  required ordinary question, not only a missing consent. **That enforcement
  lands with the reporter-facing form story, not with this one** — this change
  makes the field authorable and stores it; the public form does not yet exist
  to enforce it against. Until then `is_required` is honest about what it
  records and does not yet change what the API accepts.
- An administrator can now make the form harder to complete than it is today,
  and nothing in the software stops them. That is the trade the owner accepted:
  the decision moves from a developer who wrote an invariant to the association
  that runs the form. The authoring screen's wording ("Reporters must answer")
  is deliberately blunt about what the toggle does.
- Existing rows are unaffected. The migration adds no default and rewrites
  nothing, so consent stays required and every ordinary question stays optional
  until somebody changes one on purpose.

## Alternatives rejected

**Keep the invariant.** Fewest moving parts, and the strongest version of the
"never discourage a report" argument. Rejected by the owner: it is a product
decision encoded as a permanent technical constraint, and the body that owns
the form should own it.

**Allow required questions, but make the public form show the requirement
without enforcing it.** Rejected as a field that lies — a control labelled
"must answer" that does not must is worse than not having one.

**Allow required only on a per-section or per-question-type basis.** Rejected
as arbitrary: no rule of that shape was asked for, and it would need its own
explanation to every administrator who hit it.

## Related

- [ADR-0016](ADR-0016-data-driven-question-bank.md) — the question set is data, which is the argument this decision follows through on
- [ADR-0047](ADR-0047-feature-files-must-not-contradict-adrs.md) — why the feature file changes in the same pull request
- [`/features/question-bank-and-form/question-bank-and-form.feature`](../../features/question-bank-and-form/question-bank-and-form.feature)
