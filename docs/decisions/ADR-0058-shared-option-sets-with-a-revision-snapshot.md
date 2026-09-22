---
title: A shared choice list is authored once and snapshotted into every revision that uses it
description: Two tables, two different mutability rules, and a copy between them.
type: adr
status: accepted
date: 2026-09-20
decision-makers: Chase Florell
keywords: option sets, question bank, immutable revisions, snapshot, autocomplete
---

# ADR-0058 — A shared choice list is authored once and snapshotted into every revision that uses it

## Context

Several questions need the same list of choices. A province list, an aerodrome
list, and a glider-make list are each long, each maintained by hand, and each
wanted by more than one question. Today
`question_revision_options` is the only place a choice can live, and it belongs
to exactly one revision, so offering the same forty aerodromes on two questions
means typing them twice and maintaining them twice — in both official
languages.

The obvious fix is a shared table the questions join to. That fix collides
head-on with the rule the question bank is built around: a revision is complete
and immutable, and a report has to render exactly what its reporter was shown
(ADR-0016, product invariant #1). If a revision reads its choices from a shared
table at render time, then adding an aerodrome silently rewrites what a report
from last year appears to have been offered, and removing one makes a stored
answer point at nothing.

Both of those are real needs, and neither is negotiable: an administrator has
to be able to add an aerodrome without touching every question, and a report
from last year has to keep showing what it actually asked.

## Decision

**Two tables, two different mutability rules, and a copy between them.**

`option_sets` and `option_set_items` are the working lists. They are
**mutable**: an administrator adds, relabels, reorders, and removes items, and
a removal is a soft delete like everything else here. This is the list as a
thing an administrator maintains.

`question_revision_options` stays exactly what it is: the **frozen** set of
choices one revision offers, created with that revision and never touched
again.

When a revision is created from a shared list, the list's live items are
**copied** into that revision's own option rows. From that moment the revision
answers from its own copy and never consults the set again. Editing the set
changes what the *next* revision will offer, and changes nothing about any
revision that already exists.

Two nullable columns record where a copy came from:
`question_revisions.option_set_id` and
`question_revision_options.source_item_id`. They are **provenance only** — they
let the authoring screen say "these choices came from the Aerodromes list" and
offer to refresh them. Nothing reads them to render a question or to validate
an answer. `source_item_id` is `ON DELETE RESTRICT` and `option_set_id` is
`ON DELETE SET NULL`, so retiring a list can never reach back into a revision
and change what it offered.

`QuestionType.Autocomplete` is added alongside this, because a shared list is
what makes an autocomplete worth having: it is domain-identical to
`SingleSelect` — one stored option code — and differs only in how many choices
are practical to show at once.

### Amended by ADR-0063 for one type

[ADR-0063](ADR-0063-a-reporter-may-add-a-type-ahead-choice.md) narrows the
"always render the snapshot" half of this decision for
`QuestionType.Autocomplete` backed by a live set, which renders the **live**
list instead. A reporter can add to a type-ahead, and a choice nobody can see
until an administrator republishes the question is no use to the next reporter.

Everything else here stands, including for autocompletes: the snapshot is still
written, still immutable, and still the record of what a given reporter was
offered. What changed is which of the two a form renders.

### Amended by ADR-0072: the snapshot is a record, not a lookup

[ADR-0072](ADR-0072-every-answer-is-stored-as-a-string.md) made an answer store
its own text instead of an option code. That removes the join this ADR was
built to protect.

Two of the arguments above were load-bearing and are now spent. "Removing one
makes a stored answer point at nothing" cannot happen, because an answer points
at nothing by design. "A revision would render choices it was never offered" is
still true and still the reason the snapshot is written, but it is no longer
also a data-integrity argument.

What survives is the fact the snapshot records: **the complete set of choices
this reporter was offered.** A reviewer reading a two-year-old report needs to
know that "Springbank" was picked out of forty aerodromes rather than four, and
nothing else in the schema says so. The copy-on-create behaviour, the two
mutability rules, the provenance columns, and the `ON DELETE` rules are all
unchanged.

What is retired is using the snapshot to *resolve* an answer. Rendering a
historical answer reads one column. Validating a submitted answer still consults
the snapshot — the submitted text must be one of the labels the revision
offered — but reading a stored one does not.

## Consequences

- An administrator maintains the aerodrome list in one place, and every
  question authored afterwards picks it up.
- A revision stays self-contained. Rendering a historical report still reads
  one revision and its own option rows, exactly as before, with no join to a
  table that has since changed.
- **Choices are duplicated on purpose.** A list of four hundred aerodromes
  used by three questions is twelve hundred option rows, and a new revision of
  one of those questions is four hundred more. At HPAC's volume — a few hundred
  question rows, dozens of reports a year (ADR-0034) — this is nothing, and
  paying it buys the immutability guarantee outright rather than by convention.
- A revision can drift from the list it was built from, and that is the correct
  behaviour rather than a bug. The authoring screen shows the provenance so an
  administrator can create a fresh revision when they want the current list.

## Alternatives rejected

**Point a revision at the shared set and read it at render time.** The
smallest schema and the least duplication. Rejected because it breaks the one
rule the question bank exists to enforce: a report would render choices it was
never offered, and an answer could point at an item that has since been
removed. Product invariant #1 and ADR-0016 both fall over.

**Keep only per-revision options and accept the retyping.** No new tables and
nothing to explain. Rejected because it makes an aerodrome list unmaintainable
in practice — every correction is repeated per question, per language, and a
transcription slip between two questions is invisible until a reporter meets
it.

**Version the shared set instead, and point a revision at a set version.**
Immutability preserved without copying rows. Rejected as a second, parallel
revision mechanism: the bank would then have two different things that version,
with their own numbering and their own edge cases, to save duplicate rows that
this system's volume makes free.

## Related

- [ADR-0072](ADR-0072-every-answer-is-stored-as-a-string.md) — amends what the snapshot is for
- [ADR-0016](ADR-0016-data-driven-question-bank.md) — the question set is data
- [ADR-0034](ADR-0034-tiny-ids.md) — why the volume argument above holds
- [ADR-0040](ADR-0040-migrate-canonical-domain-and-persistence.md) — the schema baseline
- [`/features/question-bank-and-form/question-bank-and-form.feature`](../../features/question-bank-and-form/question-bank-and-form.feature)
