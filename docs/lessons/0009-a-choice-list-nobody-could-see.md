---
title: A choice list nobody could see
description: The "Where" question offered five choices while the page for curating choices showed none, because a choice could live in two places and the page only looked in one.
type: lesson
date: 2026-09-22
issue: 352
status: accepted
---

# Lesson 0009 — A choice list nobody could see

## Symptom

The seeded "Where" question offered five sites on the report form.
`/admin/choice-lists`, the page an Administrator was sent to for curating
choices, was empty. It said nothing about "Where" or any other question.

## Root cause

A choice had two homes:

- rows on a revision, `question_revision_options`, typed into the question
  editor;
- a shared list, `option_sets`, snapshotted into each revision that used it
  ([ADR-0058](../decisions/ADR-0058-shared-option-sets-with-a-revision-snapshot.md)).

The seed and the Typeform import only ever wrote the first. The curation page
only read the second. Nothing in the specification said which home a question's
choices were in, so both were built and neither was wrong on its own terms.

The per-revision home also made choices part of an immutable revision. Adding
one site to an answered type-ahead was therefore a fork (ADR-0071), so the
cheapest edit an Administrator can make retired the question.

## Spec delta

[ADR-0095](../decisions/ADR-0095-a-question-owns-its-choices-outside-its-revisions.md)
gives choices one home, `question_choices`, owned by the question and outside
its revisions. Shared lists and the page are removed.

- Editing choices never revises or forks (`REQ-QB-099`).
- A removed choice is hidden and kept (`REQ-QB-100`).
- A fork copies every choice (`REQ-QB-098`).

## Scenario

`REQ-QB-099` and `REQ-QB-100` now prove the choices an Administrator edits are
the ones the form offers, whatever the question's history.

## Skill

None. This is a product requirement, and its remedy is the claims above.
