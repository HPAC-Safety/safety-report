---
title: A stubbed form never saw what the migrations seed
description: A database created from scratch lost every seeded group, and staging asked each grouped question on its own page. The browser suite stubbed the questions by hand and no scenario said what a freshly migrated database sends, so nothing failed.
type: lesson
date: 2026-10-02
issue: 754
status: accepted
---

# Lesson 0041 — A stubbed form never saw what the migrations seed

## Symptom

On staging, **From**, **Pilot**, and **Aircraft** were empty headings, and each
of their questions was asked on a page of its own. Local dev databases looked
right.

## Root cause

- `InitialSchema` writes the seed as it is when the migration runs, through the
  legacy shape, which has no grouping column. The seed was empty when it
  shipped, so older databases got the full seed later, groups included. A
  database created from scratch got the questions early without their groups,
  and the full seed skipped them as already present.
- The browser suite stubs `/api/v1/questions` with hand-written groups, so it
  tested the group page against a shape the migrations no longer produced.
- No scenario said what a freshly migrated database sends for the seeded
  groups.

## Spec delta

- REQ-QB-259: a freshly migrated database sends each seeded group with its
  questions.
- REQ-QB-260 to REQ-QB-264: `RestoreSeededGroups` re-links them like any edit,
  and leaves an Administrator's choices alone.
- REQ-QB-265: the browser suite's seeded form equals what a freshly migrated
  database sends.
- REQ-QB-266: each seeded group is one page with its heading and exactly its
  questions, walked against that fixture.
- ADR-0187 records the decision.

## Scenario

REQ-QB-259 to REQ-QB-266.

## Skill

None; the drift check is the remedy.
