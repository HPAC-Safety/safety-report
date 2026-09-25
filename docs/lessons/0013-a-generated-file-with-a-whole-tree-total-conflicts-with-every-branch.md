---
title: A generated file with a whole-tree total conflicts with every branch
description: docs/traceability.md carried a count across every scenario and a table of adjacent rows, so parallel pull requests conflicted on it and squash merges left main stale, however well CI regenerated it.
type: lesson
date: 2026-09-23
issue: 395
status: accepted
---

# Lesson 0013 — A generated file with a whole-tree total conflicts with every branch

## Symptom

Pull requests kept conflicting on `docs/traceability.md` and failing the
`docs` check. `main` itself went stale after two squash merges (run
35929243393). The regeneration job went red when the author pushed while it
ran (run 35960597491).

## Root cause

The file was committed, but it was not mergeable.

- One line counted every claim in the tree, so every branch that touched any
  scenario rewrote the same line.
- The claims were adjacent table rows, so edits to neighbouring claims
  touched adjacent lines.

ADR-0101 fixed who regenerates the file, but not the file's shape, so the
conflicts continued. The regeneration job also treated losing a push race as
a failure.

## Spec delta

[ADR-0106](../decisions/ADR-0106-every-line-of-the-matrix-derives-from-one-source-item.md):

- Every line of the matrix derives from one scenario or constraint.
- There are no totals in the file.
- Items are sorted by ID, one block per item.
- A push rejected by a newer head is a superseded run.

## Scenario

No scenario. This is a property of the delivery tooling, not of the system
`features/` describes. `tests/js/traceability.test.mjs` proves it with
`git merge-file`.

## Skill

[`hpac-safety-conventions`](../../skills/hpac-safety-conventions/SKILL.md)
now says that a committed generated file must merge the way its sources do:

- No line may derive from the whole tree.
- Independent items must be separated by unchanged lines.

Since #492 the general rule lives in the generic
[`coding-conventions`](../../skills/coding-conventions/SKILL.md) skill; the project skill named above
keeps this repository's commands, paths, and references.
