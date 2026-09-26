---
title: A question that has been answered forks instead of revising
description: An edit forks the question once any answer exists.
type: adr
status: accepted
date: 2026-09-21
decision-makers: Chase Florell
keywords: question bank, revisions, soft delete, fork, immutability, question key
---

# ADR-0071 — A question that has been answered forks instead of revising

**Status:** Amended by [ADR-0095](ADR-0095-a-question-owns-its-choices-outside-its-revisions.md). A question's choices live outside its
revisions, so editing only the choices never revises or forks the question. A
fork copies every choice, removed ones and reporter-added marks included, onto
the replacement. Amended again by [ADR-0128](ADR-0128-an-answer-names-its-choice-and-a-picker-option-is-fixed-or-replaced.md): the copies are new
choice rows, and answers to the retired question keep naming its rows.
Amended by [ADR-0132](ADR-0132-a-condition-follows-its-parent-through-a-fork.md): a fork leaves the questions that depend on the
forked one as they are; each resolves its condition to the replacement on
read.

## Context

The question bank has one job beyond asking questions: a report has to record
what its reporter was actually asked. [ADR-0016](ADR-0016-data-driven-question-bank.md)
delivers that with a revision chain. A question row is mutable identity, every
edit appends a complete immutable `question_revisions` row, and
`report_answers.question_revision_id` pins an answer to the wording in force
when it was given.

That works, and it has been load-bearing for a month. What it costs is that
"the question" is not a thing you can point at. It is a row that means whatever
its newest revision says, and the wording a reporter saw lives one join away in
a table of historical rows that the current form deliberately ignores. Reading a
two-year-old report means resolving an answer through a revision id to find out
which of eleven versions of "Where did this happen?" was on screen that day.

The owner's position is that a question whose wording changed after somebody
answered it is not the same question any more, and modelling it as one version
of one row understates that. Changing "Were you injured?" to "Did you require
medical attention?" is not an edit; it is a different question that happens to
occupy the same slot on the form.

## Decision

**An edit forks the question once any answer exists.**

`Question.Revise` keeps its current behaviour while the question has no
answers — an administrator fixing a typo an hour after authoring should not
strand a dead row. Once any `report_answers` row references the question,
including an answer on a soft-deleted report, an edit instead:

1. soft-deletes the existing question, and
2. creates a new question with a new `TinyId`, carrying the edited content.

Old answers keep pointing at the question they were given under, and that
question is frozen because a deleted question refuses further revision. The
revision chain is not removed — it still carries the unanswered-edit case and
the consent question — but for an answered question the fork is what records
history.

### The key carries across a fork

`questions.key` is the stable non-localized identity that exports and
integrations resolve by, so the fork has to keep it. The unique index narrows to

```sql
CREATE UNIQUE INDEX ix_questions_key ON questions (key) WHERE deleted IS NULL;
```

Every question in a fork chain shares one key and at most one of them is live.
A consumer asking for `occurrence_site` gets the question being asked today;
a consumer holding an old answer reads that answer's own question id and gets
the wording that answer was given under. Neither has to know the chain exists.

### Soft deletion is irreversible

There is no undelete. A question is retired by an administrator or by being
forked, and in both cases something downstream — a report, an export, a
reviewer's memory — may already treat the retirement as fact. Reviving a row
that answers were frozen against would reopen exactly the question this ADR
closes. An administrator who wants a retired question back authors it again.

### Publication consent never forks

Consent is the one question that cannot be deleted
([ADR-0016](ADR-0016-data-driven-question-bank.md), product invariant #1): it
gates every publication path, the `ConsentPublish` role must resolve to exactly
one live question, and there is no defined behaviour without it. Forking it
would mean deleting it, so it keeps revising in place even when answered. This
is a named exception rather than a general escape hatch — it applies to
`IsSystem` and to nothing else.

## Consequences

- **A question is a thing you can point at.** An answer's question id resolves
  to one immutable wording, with no revision-number arithmetic in between.
- **The form's "one live question per key" rule is now enforced by the
  database** rather than by choosing the highest revision number at read time.
- **Fork chains accumulate rows.** A question reworded five times over a decade
  leaves five dead question rows sharing a key. At HPAC's volume
  ([ADR-0034](ADR-0034-tiny-ids.md)) this is nothing, and it is the same trade
  ADR-0058 already made for option rows.
- **Two mechanisms have to be explained**, because an unanswered edit and an
  answered edit do different things. The boundary is mechanical — does any
  answer reference this question — so it is testable rather than a judgement
  call, but it is real surface and a reader meeting it for the first time will
  need this page.
- **"Has this been answered" becomes a domain question the question bank must
  be able to ask.** That is a read across an aggregate boundary, resolved where
  the edit is orchestrated rather than inside `Question`.
- An administrator editing an answered question sees the form's version number
  reset, because the new question starts a fresh revision chain. The chain is no
  longer the history; the key is.

## Alternatives rejected

**Keep the revision chain for everything.** No migration, no second mechanism,
and the immutability guarantee already holds. Rejected by the owner: it records
history correctly while modelling a reworded question as the same question,
which is the thing being disputed.

**Fork every edit, including unanswered ones.** One rule, no "has it been
answered" read, and `question_revisions` could be dropped outright. Rejected
because it makes authoring hostile — every typo fix during initial authoring
mints a dead row and a new id — and because consent needs the revision path
regardless, so the table cannot actually go away.

**Mint a new key on the fork.** Leaves the unique index alone. Rejected because
it breaks the one promise the key makes: every export keyed by name would see
`occurrence_site`, `occurrence_site_2`, `occurrence_site_3` and have to
reassemble the series itself.

**Revive a deleted question instead of authoring a new one.** Convenient for an
administrator who retired something by mistake. Rejected: answers were frozen
against that row's retirement, and a row that can come back is not frozen.

## Related

- [ADR-0016](ADR-0016-data-driven-question-bank.md) — the revision chain this narrows
- [ADR-0072](ADR-0072-every-answer-is-stored-as-a-string.md) — answers stop resolving through a revision
- [ADR-0058](ADR-0058-shared-option-sets-with-a-revision-snapshot.md) — the same volume trade for option rows
- [ADR-0034](ADR-0034-tiny-ids.md) — why the volume argument holds
- [`/features/question-bank-and-form/question-bank-and-form.feature`](../../features/question-bank-and-form/question-bank-and-form.feature)
