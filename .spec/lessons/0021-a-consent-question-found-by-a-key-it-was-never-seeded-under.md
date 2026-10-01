---
title: A consent question found by a key it was never seeded under
description: On every seeded database, the publication-consent answer reached the model as an eligible fact, because the Worker left consent out by the key consent_publish while the seeded question kept its Typeform key, and the test built its own consent question instead of using the seeded one.
type: lesson
date: 2026-09-25
issue: 450
status: accepted
---

# Lesson 0021 — A consent question found by a key it was never seeded under

## Symptom

Binding REQ-QB-027 (#450) listed the booted database's system questions. One
had the key `08f3eedb_e682_431d_be9b_2d83765bf022`, not `consent_publish`, and
was not private. It was the publication-consent question the Typeform seed
writes. `SummarizeReportProcessor` left consent answers out of summary input
by the keys `consent_publish` and `consent_media`. On any seeded database it
therefore sent a reporter's consent answer to the model in `report_content`,
as an eligible fact. REQ-AI-009 said both consent answers are excluded, and it
passed.

## Root cause

There were two consent questions under two keys. `Question.CreateConsentPublish`
uses `consent_publish`. The seed kept the key its Typeform import gave the
field, marked it private `false`, and gave it the `consent_publish` role. The
Worker and every other consumer read consent by role, except the summary
query, which used the key. REQ-AI-009's step built its own consent question
with `CreateConsentPublish` in a migrated database that already held the
seeded one. It proved the rule for the question the test made, not for the one
a real database holds. The seed's privacy was never checked because nothing
required a system question to be private.

## Spec delta

- `SummarizeReportProcessor` leaves an answer out of summary input by its
  question's role, not its key.
- A system question is always private. The domain refuses an edit that
  changes it. The seed writes publication consent as private. Migration
  `KeepSystemQuestionsPrivate` gives any non-private system question a new,
  private revision.
- REQ-QB-027 names the consent questions by role and requires both to stay
  private.

## Scenario

- REQ-AI-009: the step answers the two consent questions the migrations seed,
  not ones it builds, and asserts that neither key reaches either array.
- REQ-QB-027: bound; it reads the system questions' roles from the booted
  database.

## Skill

[`test-hpac-safety`](../../skills/test-hpac-safety/SKILL.md) now says that a
test of a rule over seeded rows uses the rows the migrations seed, not a
factory-built stand-in.

Since #492 the general rule lives in the generic
[`test-from-scenarios`](../../skills/test-from-scenarios/SKILL.md) skill; the project skill named above
keeps this repository's commands, paths, and references.
