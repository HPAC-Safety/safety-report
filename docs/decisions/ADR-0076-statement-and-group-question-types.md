---
status: accepted
date: 2026-09-21
decision-makers: Chase Florell
keywords: statement, group, question bank, collects no answer, grouped under, sections
---

# ADR-0076 — Statement and Group are question types again, and a Group has children

## Context

`QuestionType.Statement` and `QuestionType.Group` (surfaced in the admin UI as
a "section heading") existed once and were deleted in #222 (closing #221).
The removal commit's own words: neither had a reporter-facing renderer built
yet, and "no ADR or spec gives a rationale for modeling them as question-bank
rows rather than static page copy." That was correct as stated — no ADR ever
owned these types, before or since. It was not a decision that they should not
exist; it was the absence of one.

Real content forces the question back open. The Typeform export this system
is replacing (`docs/form-spec.md`, and the Typeform JSON import/export in
[ADR-0077](ADR-0077-typeform-json-import-and-export.md)) contains genuine
instructional screens (a `statement` field introducing the form) and genuine
compound fields — Typeform's own `group` type ("Aircraft:", holding type,
manufacturer, model, certification) and `contact_info` ("From:", "Pilot:",
holding first name, last name, phone, email). None of these collect an answer
of their own; all of them need to render as one visual unit. `QuestionType`
has no way to say either thing today.

`QuestionType.Number` was already a similarly quiet member of the enum:
in-scope, correctly modeled, unused by any current question. Statement and
Group return in the same spirit — real capabilities of the question bank, not
speculative ones — because this ADR is written at the moment real content
needs them, not in anticipation of it.

`ADR-0060` already anticipated the shape without owning it: *"a statement or
section heading — which collect no answer — cannot be made conditional."*
This ADR is what makes that sentence true by construction again, and owns the
rest of what these two types need.

## Decision

### `Statement` and `Group` both collect no answer

Both are added back to `QuestionType`. Both are `CollectsNoAnswer` — a new
`QuestionRevision` property, `Type is Statement or Group` — which the
submission path uses to skip them when building the set of answer-producing
revisions a report must record (product invariant #2, "every shown
answer-producing revision"). Neither can be `IsRequired`, `IsPrivate`, or
`IsSystem`; neither can be a conditional parent or child
(`DependsOnQuestionId`/`DependsOnOptionCode` stay `null`, enforced the same
way `ValidatedDependency` already refuses the consent question — ADR-0060's
sentence above is now enforced code, not prose); neither accepts options.
`LabelEn`/`LabelFr` carry the displayed text — the statement's message, or the
group's heading.

### A `Group` may have children; nothing else changes about them

`QuestionRevision` gains `GroupedUnderQuestionId` — a nullable `TinyId`
naming the stable `Question` (not a revision, exactly like
`DependsOnQuestionId`) whose current revision is `Group`-typed. A question
with this set still orders, requires, and validates exactly as it would
without it; the only effect is that the form renders it together with its
group's heading and every sibling naming the same parent.

**This is a new, separate column — not a reuse of `DependsOnQuestionId`.**
"Display together" and "conditional on" are different relationships with
different failure modes: a missing conditional parent hides a question
entirely, a missing group parent only loses a heading. Overloading one column
for both would force `QuestionRevision.IsEnabledGiven` to distinguish two
kinds of dependency it currently treats as one, and would require amending
ADR-0060/ADR-0074's "conditional on" framing for a concern that has nothing
to do with conditions. A second column costs one migration and touches
neither ADR.

Enforcement mirrors the existing dependency split exactly:
`QuestionRevision` checks what one row can see — not self-referential, not
the consent question, not a `Group` naming itself. A new
`QuestionGrouping.EnsureGroupingAllowed` (parallel to
`QuestionDependencies.EnsureDependencyAllowed`) checks what needs the rest of
the bank: the named parent exists, is live, and its current revision is
`Group`-typed. **No nesting**: a `Group` cannot itself carry a
`GroupedUnderQuestionId` — scope stays to what the Typeform evidence actually
needs, one level, matching ADR-0074's precedent of naming what is out of
scope so a widening is a future ADR rather than a silent surprise.

Continuity mirrors ADR-0060/ADR-0074 too: if an administrator retypes a
`Group` parent away from `Group`, or deletes it, a child's
`GroupedUnderQuestionId` is cleared to ungrouped on the next revision that no
longer satisfies it, rather than left naming a heading that no longer exists.

## Consequences

- Two new nullable `QuestionRevision` columns' worth of migration:
  `grouped_under_question_id` (the `Statement`/`Group` type values themselves
  need no column — they are ordinary `question_revisions.type` rows).
- `QuestionEndpoints`/`QuestionContracts` gain a `GroupedUnderQuestionId`
  field alongside the existing `DependsOnQuestionId`; `QuestionEditor` gains a
  "Grouped under" control next to "Depends on," and hides the
  required/private/options controls for `Statement`/`Group` the same way it
  already hides the option editor for `YesNo`.
- The submission path must be re-audited to confirm it skips
  `CollectsNoAnswer` revisions when assembling the answer set — the exact
  logic #222 deleted along with the types has to be re-added, not assumed to
  still be there.
- `QuestionBankSeed` (currently empty — its own comment says removal of
  these types is why, pending "a correct question set") can finally be
  populated, from the Typeform import in ADR-0077.

## Alternatives rejected

**Model instructional/heading content as static page copy in `locales/`
instead of question-bank rows**, which is exactly what #222 left as the open
question. Rejected: the source content (Typeform's `statement` and
`group`/`contact_info` fields) is per-question-bank-revision data an
administrator authors and reorders alongside every other question — giving it
a different home than everything else on the form means two authoring
surfaces for one form, and a heading that cannot move without a code change.

**Reuse `DependsOnQuestionId`/`DependsOnOptionCode` for grouping**, treating
"grouped under" as a condition that is always true. Rejected — see "Decision"
above; it conflates two relationships with different failure semantics and
would require amending ADR-0060/ADR-0074 for no actual gain.

**Allow nested groups.** No current content needs it, and it is a strictly
additive follow-up ADR if it ever does, matching ADR-0074's precedent for
scoping a decision to the concrete need in front of it.

## Related

- [ADR-0060](ADR-0060-conditional-questions-depend-on-a-boolean-question.md) — the carve-out this ADR makes real
- [ADR-0074](ADR-0074-a-single-select-parent-may-enable-a-conditional-question.md) — precedent for a separate, narrowly-scoped dependency-style column
- [ADR-0077](ADR-0077-typeform-json-import-and-export.md) — where `Statement`/`Group` rows actually come from
- [`/features/question-bank-and-form/question-bank-and-form.feature`](../../features/question-bank-and-form/question-bank-and-form.feature)
- Issues #221, #222 (removal), #235 (this work)
