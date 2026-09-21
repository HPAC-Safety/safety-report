---
status: accepted
date: 2026-09-21
decision-makers: Chase Florell
keywords: conditional questions, dependency, question bank, single-select, validation
---

# ADR-0074 — A conditional question's parent may be yes/no or single-select, naming a required option

**Status:** Accepted, partially supersedes
[ADR-0060](ADR-0060-conditional-questions-depend-on-a-boolean-question.md)'s
restriction to a `yes_no` parent. Everything else ADR-0060 decided — where the
dependency lives, how it is enforced, what a cycle means — still holds.

## Context

ADR-0060 named this exact case and rejected it, only as unearned at the time:
*"a `single_select` parent would need to name which option counts. Both are
real features and neither is this one."* The concrete need has since arrived:
a question asking whether a reporter flies hang gliders or paragliders should
enable one follow-up rating question (H1–H4) for one answer and a different
one (P1–P4) for the other. A `yes_no`-only parent cannot express that; two
questions each depending on a separate `yes_no` proxy would ask the reporter
the same fact twice.

## Decision

### A `single_select` parent additionally names a required option

`question_revisions` gains a nullable `depends_on_option_code`, alongside the
existing `depends_on_question_id`. It is:

- always `null` when the parent is `yes_no` — that condition stays the
  implicit, invariant `"yes"` ADR-0060 and
  [ADR-0072](ADR-0072-every-answer-is-stored-as-a-string.md) established;
- required, and validated against the parent's **live** option set, when the
  parent is `single_select`.

The code is the parent's invariant option code, never a localized label —
consistent with how an answer to a `single_select` question is itself
compared (`QuestionRevision.Offers`), and with
[ADR-0058](ADR-0058-shared-option-sets-with-a-revision-snapshot.md)'s
invariant option codes.

### Scope: exactly these two parent types, one condition each

In scope: `yes_no` (unchanged) and `single_select` (new), each naming exactly
one required value, compared with equality only.

Out of scope, named so the next step is an incremental ADR rather than a
surprise:

- **`multi_select` or `autocomplete` parents.** A multi-select's condition
  would mean "contains," not "equals" — a different comparison, not a
  narrower version of this one. Autocomplete is domain-identical to
  single-select (ADR-0058) and would be a small follow-up, but is left out to
  keep this decision's blast radius to what the concrete need actually asks
  for.
- **Any operator beyond equality, or more than one condition.** Still "the
  rules-engine alternative in a smaller disguise" ADR-0060 rejected. This ADR
  narrows that rejection to "not yet," not "never" — the schema still grows
  an operator later without rewriting what exists.
- **Evaluating whether a question is currently visible on the public form.**
  Nothing evaluates this for the existing `yes_no` case either — ADR-0060
  explicitly deferred it to the reporter-facing form story, and it still has
  not arrived. This decision adds a pure domain function
  (`QuestionRevision.IsEnabledGiven`) that a future form can call, and tests
  it directly, but wires nothing into a public endpoint.

### Enforcement split is unchanged, generalized in place

`QuestionRevision` still checks only what one row can see: not
self-referential, not the system question. `QuestionDependencies` still
checks what needs the rest of the bank — parent exists, is live, and now
either is `yes_no` or is `single_select` with a code its current revision
currently offers — plus the existing cycle check.

### Continuity when the parent stops fitting the condition

Mirrors ADR-0060's accepted `yes_no` behaviour rather than inventing a new
policy: if an administrator retypes a `single_select` parent away from
`single_select` (or to `yes_no`), or edits its options to drop the code a
child names, the child's dependency is cleared to unconditional on the next
revision that no longer satisfies it, rather than left pointing at a
condition that can never be true. A question silently disappearing from a
safety form is worse than one asked unnecessarily — the same reasoning
ADR-0060 gave for the `yes_no` case applies unchanged.

## Consequences

- One new nullable column, no join table, no operator table.
- The authoring screen's parent picker widens to `yes_no` and `single_select`
  questions, and single-select gains a second control naming the required
  option, populated from that question's live option set.
- `EnsureDependencyAllowed`'s rejection message changes from "only a yes/no
  question can enable another one" to name both accepted types.
- Existing `yes_no` dependencies are unaffected: the new column is `null` for
  every one of them, and reads through the same code path as before.

## Alternatives rejected

**A separate `question_conditions` table with operators**, and **any type as
a parent with an expected value.** Both already rejected by ADR-0060 for the
same reason: neither `AND`/`OR` nor a comparison beyond equality has been
asked for, and an empty generalization is still a schema to migrate later
either way.

**Widening to `multi_select`/`autocomplete` in the same change.** Considered,
since both are mechanically close to `single_select`. Rejected for this ADR
specifically to keep the reviewable surface to the concrete need — a
follow-up ADR can widen scope without touching what this one decided.

## Related

- [ADR-0060](ADR-0060-conditional-questions-depend-on-a-boolean-question.md) — superseded in part by this decision
- [ADR-0058](ADR-0058-shared-option-sets-with-a-revision-snapshot.md) — invariant option codes
- [ADR-0072](ADR-0072-every-answer-is-stored-as-a-string.md) — why `"yes"` is invariant, and how a select answer is compared
- [`/features/question-bank-and-form/question-bank-and-form.feature`](../../features/question-bank-and-form/question-bank-and-form.feature)
