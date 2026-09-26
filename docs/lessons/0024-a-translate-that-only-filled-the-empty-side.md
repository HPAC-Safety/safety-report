---
title: A Translate that only filled the empty side
description: The question editor's Translate stayed disabled on every existing question, because the scenarios specified translating a new question from one written language and never an edit to a question already written in both.
type: lesson
date: 2026-09-26
issue: 522
status: accepted
---

# Lesson 0024 — A Translate that only filled the empty side

## Symptom

In the question editor, an administrator edited the English help text of an
existing question such as the Manufacturer type-ahead. **Translate** stayed
disabled, and its hint still said "Write one language, then translate it into
the other." The French had to be retyped by hand. Choices added in English to
the same question could not be translated either (#518).

## Root cause

REQ-QB-069 and REQ-QB-070 specified Translate only for a **new** question:
write one language, press Translate, and the other language is filled. Every
scenario started with one side empty. None described an existing question,
where both sides are written and the administrator changes one of them.
ADR-0062 described the feature as "a draft in the empty box".

The code did what the scenarios said. It derived the direction from which side
was empty and disabled the button when neither was. Because nothing specified
the edit case, nothing failed when it was missing.

## Spec delta

- [ADR-0141](../decisions/ADR-0141-a-choice-is-translated-on-request-in-a-chosen-direction.md)
  (#518) and
  [ADR-0144](../decisions/ADR-0144-the-wording-is-translated-on-request-in-a-chosen-direction.md)
  (#522) establish the new rule. Translate is offered when a source was edited
  or its target is empty. It goes in a direction the administrator chooses, and
  it replaces the target with a draft. ADR-0062's empty-box framing is
  superseded.
- REQ-QB-069 now asserts the default direction. REQ-QB-070 flips the switch
  before translating French into English.
- New scenarios cover editing a question already written in both languages:
  REQ-QB-164 to REQ-QB-170 for choices, and REQ-QB-172 to REQ-QB-175 for the
  wording.

## Scenario

REQ-QB-172 (editing a bilingual question's help text offers Translate) and
REQ-QB-173 (Translate replaces the French with drafts) now prove the case this
lesson is about. Their question has help text in both languages, so the edit
changes a written field rather than filling an empty one: the edited-source
branch the old code never had. REQ-QB-164 and REQ-QB-170 prove it for choices.

## Skill

None. This is a product lesson, and its remedy is the claims above.
