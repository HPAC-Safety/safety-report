---
title: A storage format shown as a display format
description: The admin report view and the continue dialog showed dates and times in their ISO 8601 storage form, because the specification said how an answer is stored and never said how a person reads it.
type: lesson
date: 2026-09-24
issue: 403
status: accepted
---

# Lesson 0015 — A storage format shown as a display format

## Symptom

A reviewer opening a report saw a date answer as `2026-09-13`. Under it was
"Translation: 2026-09-13", the same string again. The reporter's continue
dialog showed a saved date and time the same way, in either language.

## Root cause

[ADR-0072](../decisions/ADR-0072-every-answer-is-stored-as-a-string.md) and
the product invariants say an answer is stored as ISO 8601 "as the storage
form only". No scenario said what a person reads instead, so each screen
printed the stored string. The admin detail view could not have done better
anyway: it did not say which question type an answer belonged to, so the page
could not tell a date from free text.

## Spec delta

- `features/moderation-authentication-and-publication/README.md`, "Reading a
  date, time, or yes/no answer": the view shows these answers in the
  reviewer's interface language, shows no translation beside them, and falls
  back to the stored string when it cannot read it. The detail view names
  each question's type (REQ-MOD-031).
- `features/report-submission/README.md`, "Returning to a saved report": the
  continue dialog does the same.
- Out-of-scope lines: storage, the API, and the model input keep the ISO form.
  There is no time-zone conversion and no per-reviewer format preference.

## Scenario

- `REQ-MOD-075`: date, time, and yes/no answers in English and French.
- `REQ-MOD-076`: an unreadable date is shown as stored.
- `REQ-SUB-068`: the continue dialog in English and French.
