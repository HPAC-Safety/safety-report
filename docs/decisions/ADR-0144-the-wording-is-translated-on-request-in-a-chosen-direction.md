---
title: The question wording is translated on request, in a direction the administrator chooses
description: The question editor's wording Translate follows ADR-0141. It is offered when a source field is empty on the other side or was edited, goes in the direction its switch shows, replaces the question and help text that need it with drafts, and no longer touches choices or the placeholder.
type: adr
status: accepted
date: 2026-09-26
decision-makers: Chase Florell
keywords: translation, question bank, authoring, wording, direction switch, draft, dirty, ADR-0062, ADR-0141
---

# ADR-0144 — The question wording is translated on request, in a direction the administrator chooses

## Status

Accepted. This ADR **extends**
[ADR-0141](ADR-0141-a-choice-is-translated-on-request-in-a-chosen-direction.md)
from choices to the question wording. It also **supersedes** what remained of
[ADR-0062](ADR-0062-administrators-may-machine-translate-question-text.md)'s
"draft in the empty box" framing. The rest of ADR-0062 stands: translation
happens on the server behind `ITranslator`, the database holds only what an
administrator saved, and Save stays disabled until both languages are present.

## Context

ADR-0141 moved choices to explicit, per-choice translation and left the
wording's Translate under ADR-0062 (#518). That Translate was enabled only
while one side of the question label was empty, and it inferred the direction
from which side that was. On any existing question both sides are written, so
Translate stayed disabled even after the administrator reworded the English.
The other language had to be retyped by hand (#522).

It also translated every choice along with the wording. Now that choices are
translated one at a time from the Choices panel, that duplicated a control and
could overwrite a choice drafted there.

## Decision

The wording's Translate works as ADR-0141 does for a choice. It works on the
question and the help text, each field on its own:

1. **A wording field needs translating** when its source has text and either:
   - its target is empty, or
   - its source differs from what it was when the editor opened, or from what
     it was when that field was last translated.

   Translate is enabled while any field needs translating. This is the same
   rule the owner chose for a choice on #518, so a new question is
   translatable as soon as a source field is written, and an untouched
   bilingual question offers nothing.
2. **The administrator chooses the direction** with a
   `TranslationDirectionSwitch` beside the button, English to French by
   default. The direction is never inferred from the empty side. The wording
   and the Choices panel each have their own switch, built from the same
   component.
3. **Translate replaces the target of each field that needs translating, and
   only those**, as an editable draft saved only on Save. A field written in
   both languages whose source was not edited keeps its target, so a
   hand-curated French question survives a translate of the help text. An
   empty source field is not sent and clears nothing.

   A result is applied to the draft as it is when it arrives. A field is left
   alone when its source or its target changed while the request was out, or
   when its translation came back empty. The whole result is dropped when the
   direction flipped. Only the fields replaced are rebaselined, in both
   languages, so each stays clean until its source is edited again, and a
   field that was left alone still offers Translate.
4. **The wording's Translate never translates a choice or the placeholder.**
   Choices are translated one at a time from the Choices panel (ADR-0141). The
   editor has no placeholder field, so a drafted placeholder would be saved
   without anyone seeing it, and ADR-0062 holds that the database stores only
   what an administrator saved.
5. **The wording's switch and Translate are one named group** ("Question
   wording"), and the Choices panel is a group named for its heading, so the
   two switches, which share an accessible name, are told apart by context.
6. **The hint** says "Edit the wording, then translate it." until a translate
   has replaced something, and then the draft warning, until a field needs
   translating again.

The reason ADR-0141 gives for replacing a draft without a diff applies here
unchanged: the wording in the editor is unsaved form state the administrator is
looking at.

## Consequences

- An administrator can retranslate the wording of an existing question after
  editing either language.
- A new question behaves as before for English first. French first now needs
  the switch flipped to French to English. REQ-QB-070 says so.
- The rule, exactly: a press sends and replaces the fields whose source has
  text and whose target is empty or whose source changed since its baseline.
  Nothing else in the wording changes.
- A hand correction to a target is lost only if the administrator edits that
  field's source and translates again. That is the moment the old target no
  longer matches the source.
- REQ-QB-069 to REQ-QB-072 and REQ-QB-172 to REQ-QB-178 specify the wording's
  Translate. REQ-QB-176 proves the per-field rule, REQ-QB-177 the dropped
  result after a flip, and REQ-QB-178 a target typed while the request was out.

## Alternatives rejected

- **Keep inferring the direction from the empty side.** This is what caused
  #522: it cannot retranslate wording that is written in both languages.
- **One switch shared by the wording and the choices.** A single switch for the
  whole editor would be further from both buttons, and the administrator may
  want the wording and a choice to go different ways in one sitting. Two
  instances of one component look and behave alike, which is what ADR-0141
  asked for.
- **Keep translating choices with the wording.** It duplicated the per-choice
  control and could overwrite a choice drafted there.

## Related

- [ADR-0141](ADR-0141-a-choice-is-translated-on-request-in-a-chosen-direction.md): extended by this record
- [ADR-0062](ADR-0062-administrators-may-machine-translate-question-text.md): its empty-box framing is superseded
- [Lesson 0024](../lessons/0024-a-translate-that-only-filled-the-empty-side.md)
- [`/features/question-bank-and-form/question-bank-and-form.feature`](../../features/question-bank-and-form/question-bank-and-form.feature)
