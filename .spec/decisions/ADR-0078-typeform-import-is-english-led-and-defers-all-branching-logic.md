---
title: Typeform import is English-led, and every real branching rule is pending, not auto-mapped
description: Typeform import is led by the English file, and every real branching rule is recorded as pending for an administrator rather than auto-mapped.
type: adr
status: accepted
date: 2026-09-22
decision-makers: Chase Florell
keywords: typeform, import, ref, bilingual, branching logic, pending
---

# ADR-0078 — Typeform import is English-led, and every real branching rule is pending, not auto-mapped

**Status:** Amends two decisions in
[ADR-0077](ADR-0077-typeform-json-import-and-export.md), made before the
mapper was actually built against real data. Everything else ADR-0077
decided — the type-mapping table, `Key` from `ref`, the `hpac` extension
block, the pending-logic table's shape and hard-delete exception, the seed
source — still stands.

## Context

ADR-0077 said a `ref` missing from either the English or French file is a
hard import error, and that a `logic[]` shape reducible to one yes/no-parent
"answered yes" gate maps to a dependency. Both were written before the
mapper existed. Building it against the organization's real
`formENG.json`/`formFR.json` pair — now checked in as
`tests/HpacSafety.Core.Tests/Fixtures/Typeform/form-en.json`/`form-fr.json`
— surfaced two problems with each.

### The organization's real files fail the hard-reject rule

The publication-consent field carries a **different `ref` in each
language** — `08f3eedb-e682-431d-be9b-2d83765bf022` in English,
`98b1dbfc-d3e2-47a4-80c8-ef7889a5aae9` in French. Every other field matches.
Under ADR-0077's rule as written, importing the organization's own actual
form fails immediately, on the one field that matters most for
publication. That is not a defensible outcome for a feature whose entire
point is importing this organization's form.

### Typeform's branching logic is not a per-field "depends on" gate

ADR-0077 imagined a `logic[]` entry as expressing "this question is shown
only when that one is answered yes." Typeform's actual model is "jump to a
different field next" — a `logic` entry is keyed by the field just answered,
and its actions say which field comes next, under what condition. Whether
that implies "the skipped field depends on this condition" is a fact about
the surrounding flow graph, not about one field's rule read in isolation.
Getting that translation subtly wrong would silently wire an incorrect
dependency — a worse outcome than flagging it for a human to wire by hand.

## Decision

### The English file drives the import; French fills in by `ref`, defaulting when absent

Import walks the English file's fields, in order. For each one, French text
(field or choice) is looked up by matching `ref` in the French file. When
found, both languages carry over. When not found — for a field, a choice,
or a nested subfield — the French side defaults to the English text, and
the draft carries `FrenchDefaultedToEnglish: true`. A French-only field or
choice, with no English counterpart to anchor it, is not imported.

Nothing is rejected and nothing blocks the rest of the pair from importing.
An Administrator sees exactly which drafts still need real French wording
— the same "review before saving" gate ADR-0077 already put on every
import, so a defaulted draft can never reach the database still holding
English text as its French answer without an Administrator having looked
at it.

### No branching logic is auto-mapped; every real condition is a pending note

`TypeformLogicRule.HasRealCondition()` distinguishes a rule that only ever
takes its unconditional `"always"` action (ordinary linear flow, not a
condition at all) from one with a real `is`/`and`/`or` comparison. Every
field with a real condition — regardless of shape, including the case
ADR-0077 thought was simple enough to auto-map — is captured as a
`PendingTypeformLogic` note. None are translated into a `DependsOnQuestionId`
by the importer itself.

This narrows ADR-0077's mapping-table row for `logic[]` to: *nothing is
auto-mapped; every real condition is pending.* A future ADR can revisit
auto-mapping the genuinely simple case once there is a tested, correct way
to read it off the flow graph rather than off one field's rule in
isolation — this decision does not rule that out, it declines to guess at
it now.

## Consequences

- `ImportedQuestionDraft`/`ImportedOption` carry `FrenchDefaultedToEnglish`
  so the admin review UI (a later issue) can surface it plainly — e.g. "12
  questions used English text as a French placeholder."
- The organization's real export pair now imports successfully end to end,
  proven by a test against the checked-in real fixture files, not only
  synthetic ones.
- Every field with real Typeform logic becomes a pending note on import —
  in the real form, that is three fields (passenger injury, aircraft type,
  and the where/province/country branch), all genuinely multi-condition or
  multi-select-parented, none of which ADR-0077's "simple" case actually
  covered anyway once checked against real data.

## Alternatives rejected

**Positional fallback matching**: when a `ref` has no match, pair it with
whatever field sits in the same position in the other file. Rejected —
raised and reconsidered in the same discussion that produced this ADR. It
can silently pair the wrong two fields the moment the two language forms
ever reorder relative to each other, which is a worse failure mode than
either rejecting or defaulting: a wrong pairing looks correct until someone
reads the French text closely.

**Keep the hard-reject rule; require the organization to fix the ref
mismatch in Typeform first.** Correct in principle — the mismatch is a real
data quality issue worth fixing — but makes the importer's first real test
a hard failure on the one file it exists to import, for a problem the
importer can safely work around by defaulting and flagging instead.

**Attempt the single-condition auto-mapping only for the exact "yes/no
constant-true" shape seen in the sample, reject everything else.** Considered,
since one real field (`Where:`, gated on `Country:`) does reduce to exactly
that shape. Rejected for now: correctly confirming a jump target actually
*skips* a field (rather than, say, jumping past several) still requires
reading the field order and the rest of the rules, which is exactly the
flow-graph reasoning this decision defers. A narrow special case tested
against one example is not confidence enough to wire a dependency
automatically.

## Related

- [ADR-0077](ADR-0077-typeform-json-import-and-export.md) — amended here
- [`TypeformQuestionMapper`](../../src/HpacSafety.Core/Features/QuestionBank/Typeform/TypeformQuestionMapper.cs)
- Issue #242
