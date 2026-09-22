---
title: A choice code nobody could supply, and a reporter choice nobody recorded
description: Administrators were asked for an option code they could not know, and a reporter's new type-ahead value was refused at submission while a hollow step reported it covered.
type: lesson
date: 2026-09-22
issue: 335
status: accepted
---

# Lesson 0006 — A choice code nobody could supply, and a reporter choice nobody recorded

## Symptom

On `/admin/questions` and `/admin/choice-lists`, every choice row asked for a
**Code** beside its English and French wording. An administrator editing a
type-ahead list reported that they did not know what the code was supposed to
be, and nothing on the screen could have told them.

Asking what should happen when a reporter types a choice the type-ahead does
not offer — in French especially — turned up a worse one: the submission
refused it outright with `'<key>' did not offer that answer`. The report was
rejected, and nothing reached the shared list.

## Root cause

An option's code is the invariant stored against an answer: normalized, never
displayed, never changed. The reporter path already derived it from the typed
wording (`OptionSet.AddFromReporter`). The administrator path instead took it
as a required field on `OptionInput`, and both editors rendered that field as
written. No scenario said who chooses a code, so the storage shape became the
authoring shape by default.

The reporter half had the opposite shape. The domain operation existed
(`OptionSet.AddFromReporter`, ADR-0063) and `REQ-QB-035` passed against it, but
nothing on the submission path called it, and answer validation checked a
type-ahead value against the frozen snapshot like any closed list. The
acceptance step for `REQ-SUB-006`'s type-ahead clause was empty, with a comment
saying `HpacSafety.Api.Tests` covered it; it did not. A claim proven only at the
domain layer, plus a step that asserts nothing, reads as covered in the matrix
and is not. The code rule also dropped accented letters rather than folding
them, so "Élévation" would have become `l_vation`, and nothing recorded which
language the reporter had typed.

## Spec delta

- `REQ-QB-090` and `REQ-QB-091`: an administrator writes a choice, on a
  question or a shared choice list, by its wording alone.
- `REQ-QB-092`: a new choice's code is derived from its English wording; an
  existing choice keeps its code when reworded; two choices that would reduce
  to the same code are refused naming the wording.
- `REQ-QB-094`: a reporter answering in French adds a choice recorded in
  French, with that language stored and a code derived from the French wording,
  accents folded.
- `REQ-QB-095`: the same end to end over HTTP — the report is accepted, the
  answer keeps the reporter's words, and the next reporter is offered it.
- `REQ-SUB-006`: narrowed to the type-ahead, whose step now submits a real
  unlisted value. A multi-select with reporter additions remains `REQ-QB-042`,
  not built yet.
- `features/question-bank-and-form/README.md` out of scope: an administrator
  authoring, seeing, or recoding an option code.

## Scenario

`REQ-QB-090`, `REQ-QB-091`, `REQ-QB-092`, `REQ-QB-094`, `REQ-QB-095`,
`REQ-SUB-006`.

## Skill

None — the claim is the remedy.
