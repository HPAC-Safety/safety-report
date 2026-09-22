---
title: A conditional question names a parent question, which must be a yes/no question
description: A conditional question names a parent question by its stable key rather than a revision, and that parent must be a yes/no question.
type: adr
status: partially-superseded
date: 2026-09-20
decision-makers: Chase Florell
keywords: conditional questions, dependency, question bank, yes/no, validation
---

# ADR-0060 — A conditional question names a parent question, which must be a yes/no question

**Status:** Partially superseded by
[ADR-0074](ADR-0074-a-single-select-parent-may-enable-a-conditional-question.md):
a `single_select` question may also be a parent, naming a required option.
"Only a `yes_no` question may be a parent" below no longer holds; everything
else — the dependency naming the parent question rather than a revision, the
domain/API enforcement split, the cycle check, and the database enforcing
only the foreign key — stands unchanged.

## Context

Most of the occurrence form is only relevant some of the time. "Were you
injured?" is worth asking everyone; the six questions that follow it are worth
asking almost nobody. Without conditional questions, either every reporter
answers all of them or the form asks nothing specific enough to be useful.

Three things had to be decided: what a dependency points at, what may be a
parent, and where the rule is enforced.

## Decision

### A dependency names the parent **question**, not a parent revision

`question_revisions.depends_on_question_id` is a nullable foreign key to
`questions.id` — the stable row, not the revision. Rewording the parent creates
a new parent revision, and the child must survive that untouched. Pointing at a
revision would break every child each time its parent's wording changed.

The column lives on the **revision**, not on the question, for the same reason
every other display fact does: a report has to render exactly the form it was
shown, and "this question was only asked because you answered yes above" is
part of that.

### Only a `yes_no` question may be a parent

The condition is "the parent was answered yes." A free-text or date parent
would need an operator and a comparison value, which is a rules engine; a
`single_select` parent would need to name which option counts. Both are real
features and neither is this one. One boolean parent covers what the form
actually needs and leaves the schema able to grow an operator later without
rewriting what exists.

### The rule is enforced in the domain and at the API, **not by the database**

`QuestionRevision` checks what one row can see: a question is not conditional
on itself, publication consent is never conditional, and a statement or section
heading — which collect no answer — cannot be made conditional.

`QuestionDependencies.EnsureDependencyAllowed` checks what needs the rest of
the bank: the parent exists, is live, is currently a `yes_no` question, and
does not lead back to the child through a chain of dependencies.

The database enforces the foreign key and nothing more. It **cannot** enforce
"the parent is a yes/no question," because the parent's type lives on the
parent's current revision — a different row, chosen by highest revision number.
Expressing that in a `CHECK` is impossible; expressing it in a trigger is
possible and is rejected: a trigger is a business rule invisible to everyone
reading the C#, and this repository keeps its rules in the domain where they
can be read and tested.

The consequence is stated plainly: **an administrator can retype a parent from
yes/no to something else and leave a child pointing at a non-boolean parent.**
The form treats such a child as unconditional rather than hiding it, because a
question that silently disappears from a safety form is worse than one asked
unnecessarily.

## Consequences

- One nullable column and one index, no join table, and no rules engine.
- The authoring screen offers only `yes_no` questions as parents, which is
  where the constraint is visible to the person authoring.
- A cycle is refused at save time rather than discovered as a form where two
  questions permanently disable each other.
- Rendering the condition on the public form is **not** part of this decision —
  it lands with the reporter-facing form story, which is where "enabled" versus
  "hidden" gets settled.

## Alternatives rejected

**A separate `question_conditions` table supporting several conditions and
operators.** More capable, and the shape this would grow into if the need
arrives. Rejected as unearned now: nothing has asked for `AND`/`OR` or for
comparison against an option code, and an empty generalization is a schema to
migrate later either way.

*Since [ADR-0072](ADR-0072-every-answer-is-stored-as-a-string.md) there are no
option codes to compare against. The condition is a comparison of the parent's
stored answer against the literal `"yes"`, which is invariant across both
official languages precisely so this check does not depend on the reporter's
locale. The argument against generalizing is unchanged.*

**Enforce the parent's type with a trigger.** The only way to make the database
authoritative. Rejected: it hides a domain rule in a place no test in this
repository reads, and it has to duplicate the "current revision is the highest
revision number" logic that already exists in C#.

**Let any type be a parent, with an expected value.** Rejected together with
the operators above — it is the rules-engine alternative in a smaller disguise.

## Related

- [ADR-0016](ADR-0016-data-driven-question-bank.md) — the question set is data
- [ADR-0058](ADR-0058-shared-option-sets-with-a-revision-snapshot.md) — the other revision field added alongside this
- [ADR-0072](ADR-0072-every-answer-is-stored-as-a-string.md) — why `"yes"` is invariant
- [ADR-0074](ADR-0074-a-single-select-parent-may-enable-a-conditional-question.md) — partially supersedes this decision: a single-select question may also be a parent
- [`/features/question-bank-and-form/question-bank-and-form.feature`](../../features/question-bank-and-form/question-bank-and-form.feature)
