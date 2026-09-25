---
title: A copy change that ran no browser test
description: A pull request that changed only French catalogue strings merged with the browser suite skipped, and main's French review-page test broke, because CI's web and e2e path filters did not list the locales/ directory the web bundle loads.
type: lesson
date: 2026-09-25
issue: 437
status: accepted
---

# Lesson 0020 — A copy change that ran no browser test

## Symptom

PR #442 changed the French word for "report" from « rapport » to
« signalement » (#436), including the review page's heading. Every required
check passed, and the pull request merged. On the next local run of the
browser suite, the three French examples of REQ-MOD-075 failed. The step still
looked for a heading named "Rapport", and that heading no longer exists.

## Root cause

CI decides which jobs run from path filters
([ADR-0039](../decisions/ADR-0039-path-gated-required-checks.md)). The `web` and `e2e`
filters listed `src/web/**`, `tests/e2e/**`, and the workflow file. They did
not list `locales/**`, even though the web bundle loads its catalogues from
there. A pull request that changed only catalogue strings therefore ran
neither job, and GitHub counts a skipped job as passing. The filter listed the
directory the code lives in, not every input the build reads.

## Spec delta

- `.github/workflows/ci.yml`: the `web` and `e2e` filters include
  `locales/**`.
- `tests/e2e/steps/manage-reports.steps.ts`: the review-page heading is
  "Report" or « Signalement ».

## Scenario

- REQ-MOD-075: its French examples run again on any catalogue change, and
  pass.

## Skill

[`deliver-hpac-change`](../../skills/deliver-hpac-change/SKILL.md) now says
that a job's path filter lists every input the job reads, including one
outside its own directory. It also says a change to `locales/` runs the
browser suite locally before the pull request.
