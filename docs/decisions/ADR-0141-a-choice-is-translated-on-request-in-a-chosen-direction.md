---
title: A choice is translated on request, one at a time, in a direction the administrator chooses
description: In the question editor's Choices panel, each choice has its own Translate, offered when its other language is empty or its source was edited, going in the direction one shared switch shows, and replacing the other language with an editable draft saved only on Save.
type: adr
status: accepted
date: 2026-09-26
decision-makers: Chase Florell
keywords: translation, question bank, authoring, choices, direction switch, draft, dirty, ADR-0062, ADR-0095, ADR-0108
---

# ADR-0141 — A choice is translated on request, one at a time, in a direction the administrator chooses

## Status

Accepted. For **choices**, this ADR **partially supersedes**
[ADR-0062](ADR-0062-administrators-may-machine-translate-question-text.md),
replacing its framing of Translate as "a draft in the empty box" with the rules
below. The question wording's Translate, including the choices it fills when
the wording has one empty side, stays under ADR-0062 until a later decision
extends this one to it (#522). The rest of ADR-0062 stands: translation happens
on the server behind `ITranslator`, the database holds only what an
administrator saved, and Save stays disabled until both languages are present.

[ADR-0144](ADR-0144-the-wording-is-translated-on-request-in-a-chosen-direction.md)
extends these rules to the question wording's Translate, which no longer
translates choices.

It builds on
[ADR-0095](ADR-0095-a-question-owns-its-choices-outside-its-revisions.md):
translating a choice edits the question's choices, never its revision.

## Context

ADR-0062 let an administrator press Translate while authoring. The editor built
it as "fill the empty side". It was enabled only while one language of the
wording was blank, and it inferred the direction from which side that was.

That works for a new question and fails for every existing one (#518). Once
the wording exists in both languages, no side is empty, so Translate is
disabled. An administrator who adds choices in English to a bilingual question,
or who rewrites the English of one, has nothing to press. The Manufacturer
type-ahead had more than twenty English-only choices and no way to draft their
French.

The first fix for #518 was a bulk "Translate choices" action that filled every
choice's empty side. The owner rejected it on 2026-09-26. A choice is often a
brand name or a place, and a bulk press drafts wording nobody asked for.

## Decision

In the question editor's **Choices panel**:

1. **Each choice's Translate is offered when that choice needs it.** It is
   enabled when the choice's source has text and either:
   - its target is empty, as with an English-only choice on an existing
     question, or
   - its source differs from what it was when the editor opened, or from what
     it was when that choice was last translated.

   A choice written in both languages that nobody has edited offers nothing.
   After a translate, the target is filled and the source is back at its
   baseline, so the button is disabled until the source is edited again.
2. **The administrator chooses the direction explicitly.** One switch, English
   → French or French → English, sets it for every choice. The default is
   English → French. The direction is never inferred from which side is empty.
3. **Translate replaces the target's current text with an editable draft.** The
   draft is saved only on Save. Pressing the button writes nothing to the
   question bank.
4. **Choices are translated one at a time.** Each choice has its own Translate.
   The Choices panel has no bulk or automatic translation.
5. **The direction switch is one shared component,
   `TranslationDirectionSwitch`.** It lives in the Choices panel now, and any
   later authoring Translate that takes an explicit direction uses the same
   component, so the controls look and behave alike.

### Why a question draft may be replaced without a diff

[ADR-0108](ADR-0108-a-reviewer-may-machine-translate-a-summary-language.md)
has a reviewer confirm a summary translation against a diff before it replaces
the other language. That rule protects text that is already saved and
reviewed. A summary language may be human-written, and replacing it silently
would lose a deliberate edit behind a later save.

A choice's wording in the editor is different. It is unsaved form state on the
screen where the administrator is working. The administrator presses Translate
on one row and sees the result in place. Every draft is looked at again before
Save, and Cancel discards all of it. A diff dialog for each choice would add a
step to every press and protect nothing that is not already on screen.

## Consequences

- An administrator can translate a choice on a question that is already
  bilingual. That includes a choice missing its other language, and a choice
  whose source they have just reworded.
- A translate replaces what the target held. A hand correction to the French
  is lost if the administrator edits the English and translates again. That is
  the moment the old French no longer matches the English anyway.
- Each press sends one request through the existing `POST /api/admin/translate`.
  The server is unchanged.
- The editor keeps a baseline of each choice's wording beside the draft, and
  the draft sent to the API is unchanged.
- REQ-QB-164 to REQ-QB-170 specify the per-choice Translate and the direction
  switch. The wording's Translate is specified by REQ-QB-069 to REQ-QB-072 and
  REQ-QB-172 to REQ-QB-175, under ADR-0144.

## Alternatives rejected

- **Bulk "Translate choices" that fills every empty side.** It was built first
  and rejected by the owner, because it drafts every brand name and place at
  once.
- **Infer the direction per choice from its empty side.** It cannot retranslate
  a choice whose two sides are both written.
- **Enable Translate only after an edit.** A strict dirty rule leaves an
  English-only choice on an existing question untranslatable until the
  administrator makes a meaningless edit. The owner chose on 2026-09-26 that an
  empty target also enables it.
- **A select, or a pair of buttons per choice, for the direction.** A select
  adds another picker to a busy panel, and a pair of buttons on every choice
  doubles its controls. One switch in the panel header does the job.
- **Confirm each replacement against a diff, as ADR-0108 does.** It protects
  nothing here, as explained above.

## Related

- [ADR-0062](ADR-0062-administrators-may-machine-translate-question-text.md): partially superseded for choices
- [ADR-0095](ADR-0095-a-question-owns-its-choices-outside-its-revisions.md): choices sit outside revisions
- [ADR-0108](ADR-0108-a-reviewer-may-machine-translate-a-summary-language.md): the accept-before-replace rule for summaries
- [ADR-0129](ADR-0129-a-type-ahead-value-is-edited-in-place-merged-and-reviewed.md): reporter-added values are translated by the Worker, not here
- [`/features/question-bank-and-form/question-bank-and-form.feature`](../../features/question-bank-and-form/question-bank-and-form.feature)
