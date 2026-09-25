---
title: A closed list kept where the author never looks
description: A pull request claimed a feature-coverage exemption as "copy" and failed, because the closed category list lived only in ADR-0090 and the tool, never in the template, instructions, or skill an author reads while writing the body.
type: lesson
date: 2026-09-25
issue: 473
status: accepted
---

# Lesson 0021 — A closed list kept where the author never looks

## Symptom

PR #470 changed the wording of the Typeform import notice. It claimed the
`feature-coverage` exemption as
`No .feature scenario needed: copy — …`, and the check failed:
`"copy" is not a reason a scenario can be skipped`.

## Root cause

The category vocabulary is closed on purpose
([ADR-0090](../decisions/ADR-0090-an-exemption-cites-the-claims-it-preserves.md)),
but the list was written down in only two places: that ADR and
`tools/feature-coverage.mjs`. The pull request template, `AGENTS.md`, and the
`deliver-hpac-change` skill all said "a closed category" without naming one.
An author writing the body had nothing to choose from, so they guessed.

A second trap sat beside it. The skill said to run
`node tools/feature-coverage.mjs` locally. The tool reads the changed files
and the body from the environment, so a bare run checks nothing and passes.

## Spec delta

- `.github/pull_request_template.md` shows the exemption shape and all seven
  categories with their meanings.
- `tests/js/feature-coverage.test.mjs` fails when the template's list differs
  from the tool's `CATEGORIES`, and when the template itself would parse as an
  exemption.
- `AGENTS.md` names the seven categories.

## Scenario

None. This is a process lesson, and no scenario can prove it. The test above
is its guard.

## Skill

[`deliver-hpac-change`](../../skills/deliver-hpac-change/SKILL.md) now says to
take the category from the template's list, and gives the full local command,
with its environment variables, that reproduces the CI verdict.
