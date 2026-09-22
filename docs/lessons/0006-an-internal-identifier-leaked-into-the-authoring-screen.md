---
title: An internal identifier leaked into the authoring screen
description: Every choice an administrator wrote asked for a "Code" they had no way to know, because the storage identifier was part of the authoring contract.
type: lesson
date: 2026-09-22
issue: 335
status: accepted
---

# Lesson 0006 — An internal identifier leaked into the authoring screen

## Symptom

On `/admin/questions` and `/admin/choice-lists`, every choice row asked for a
**Code** beside its English and French wording. An administrator editing a
type-ahead list reported that they did not know what the code was supposed to
be, and nothing on the screen could have told them.

## Root cause

An option's code is the invariant stored against an answer: normalized, never
displayed, never changed. The reporter path already derived it from the typed
wording (`OptionSet.AddFromReporter`). The administrator path instead took it
as a required field on `OptionInput`, and both editors rendered that field as
written. No scenario said who chooses a code, so the storage shape became the
authoring shape by default.

## Spec delta

- `REQ-QB-090` and `REQ-QB-091`: an administrator writes a choice, on a
  question or a shared choice list, by its wording alone.
- `REQ-QB-092`: a new choice's code is derived from its English wording; an
  existing choice keeps its code when reworded; two choices that would reduce
  to the same code are refused naming the wording.
- `features/question-bank-and-form/README.md` out of scope: an administrator
  authoring, seeing, or recoding an option code.

## Scenario

`REQ-QB-090`, `REQ-QB-091`, `REQ-QB-092`.

## Skill

None — the claim is the remedy.
