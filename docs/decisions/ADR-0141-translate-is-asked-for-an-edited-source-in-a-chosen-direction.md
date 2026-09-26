---
title: Translate is asked for an edited source, in a direction the administrator chooses
description: While authoring a question, Translate is offered for a source the administrator has edited, goes in the direction one shared switch shows, and replaces the other language with an editable draft saved only on Save; choices are translated one at a time.
type: adr
status: accepted
date: 2026-09-26
decision-makers: Chase Florell
keywords: translation, question bank, authoring, choices, direction switch, draft, dirty, ADR-0062, ADR-0095, ADR-0108
---

# ADR-0141 — Translate is asked for an edited source, in a direction the administrator chooses

## Status

Accepted. This ADR **partially supersedes**
[ADR-0062](ADR-0062-administrators-may-machine-translate-question-text.md) in
one respect: its framing of Translate as "a draft in the empty box". The rest of
ADR-0062 stands. Translation happens on the server behind `ITranslator`, the
database holds only what an administrator saved, and Save stays disabled until
both languages are present.

It builds on
[ADR-0095](ADR-0095-a-question-owns-its-choices-outside-its-revisions.md):
translating a choice edits the question's choices, never its revision.

## Context

ADR-0062 let an administrator press Translate while authoring. The editor built
it as "fill the empty side". It was enabled only while one language was blank,
and it inferred the direction from which side that was.

That held for a new question and failed for every existing one (#518, #522).
Once the wording exists in both languages, no side is empty, so Translate stays
disabled. An administrator who adds choices in English to a bilingual question,
or who rewrites the English of one, has nothing to press. The Manufacturer
type-ahead had more than twenty English-only choices and no way to draft their
French.

The first fix for #518 was a bulk "Translate choices" action that filled every
choice's empty side. The owner rejected it on 2026-09-26. A choice is often a
brand name or a place, and a bulk press drafts wording nobody asked for. It also
kept the "empty side" rule that caused the problem.

## Decision

1. **Translate is enabled by an edited source, not an empty target.** A source
   is dirty when its text differs from what it was when the editor opened, or
   from what it was when it was last translated. A new, blank source becomes
   dirty as soon as it has text. After a translate, the source is clean again
   until it is edited. For choices, the unit is one choice. For the question
   wording (#522), it is the wording block.
2. **The administrator chooses the direction explicitly.** One switch, English
   → French or French → English, sets it for every Translate it governs. The
   default is English → French. Nothing infers the direction from which side is
   empty.
3. **Translate replaces the target's current text with an editable draft.** It
   is saved only on Save. Nothing is written to the question bank when the
   button is pressed.
4. **Choices are translated one at a time.** Each choice has its own Translate,
   and there is no bulk or automatic translation of the choices.
5. **One direction-switch component serves every authoring Translate.**
   `TranslationDirectionSwitch` sits in the Choices panel now. #522 wires the
   same component to the question wording's Translate, so the two look and
   behave alike.

### Why a question draft may be replaced without a diff

[ADR-0108](ADR-0108-a-reviewer-may-machine-translate-a-summary-language.md)
has a reviewer confirm a summary translation against a diff before it replaces
the other language. That rule protects text that is already saved and reviewed:
a summary language may be human-written, and replacing it silently would lose a
deliberate edit behind a later save.

A question draft is different. It is unsaved form state on the screen where
the administrator is working. The administrator pressed Translate on one field
and sees the result in place. Every draft is reviewed again before Save, and
Cancel discards all of it. A diff dialog for each choice would add a step to
every press and protect nothing that is not already on screen.

### What this changes in the specification

- REQ-QB-164 to REQ-QB-169 specify the per-choice Translate and the direction
  switch.
- REQ-QB-069 and REQ-QB-070 still describe the wording's Translate, which
  infers its direction from the empty side. #522 amends them to use the switch
  and the dirty rule in the pull request that changes that behaviour. Until
  then, they describe the current code.

## Consequences

- An administrator can translate a choice on a question that is already
  bilingual, and can retranslate a choice after rewording its source.
- A translate replaces what the target held. An administrator who had corrected
  the French by hand and then presses Translate again loses that correction to
  the new draft. The button is enabled only after the source was edited, which
  is exactly when the old target no longer matches it.
- Each press sends one request through the existing `POST /api/admin/translate`.
  The server does not change.
- The editor keeps a baseline of each choice's wording beside the draft, so it
  can tell whether a choice has been edited. The draft sent to the API is
  unchanged.

## Alternatives rejected

**Bulk "Translate choices" that fills every empty side.** This was built first
and rejected by the owner. It drafts every brand name and place at once, and it
keeps the empty-side rule that fails for bilingual questions.

**Infer the direction per choice from its empty side.** This is the rule that
failed. It cannot retranslate a choice whose two sides are both written.

**A select or a pair of buttons for the direction.** A select adds another
picker to a busy panel. A pair of buttons ("to French", "to English") on each
choice doubles its controls. One switch in the panel header does the job with
one control.

**Confirm each replacement against a diff, as ADR-0108 does.** This is covered
above: the text being replaced is an unsaved draft that the administrator is
looking at.

## Related

- [ADR-0062](ADR-0062-administrators-may-machine-translate-question-text.md): partially superseded (the "empty box" framing)
- [ADR-0095](ADR-0095-a-question-owns-its-choices-outside-its-revisions.md): choices sit outside revisions
- [ADR-0108](ADR-0108-a-reviewer-may-machine-translate-a-summary-language.md): the accept-before-replace rule for summaries
- [ADR-0129](ADR-0129-a-type-ahead-value-is-edited-in-place-merged-and-reviewed.md): reporter-added values are translated by the Worker, not here
- [`/features/question-bank-and-form/question-bank-and-form.feature`](../../features/question-bank-and-form/question-bank-and-form.feature)
