---
title: A rule the template never asks for
description: Two web UI pull requests reached review without the screenshots the delivery skills require, because the pull request template an author fills in had no Screenshots section and no check refused the omission.
type: lesson
date: 2026-09-26
issue: 530
status: accepted
---

# Lesson 0023 — A rule the template never asks for

## Symptom

PR #525 changed the type-ahead into a combobox and showed only after shots:
the before state was the browser's native datalist popup, which a page
screenshot cannot capture, so the before shot was skipped. PR #524's first
version showed light-mode after shots only.

## Root cause

The rule was written down, in `deliver-change` step 6 and
`deliver-hpac-change`, but only there. Every pull request body is written from
`.github/pull_request_template.md`, which had no Screenshots section and no
checkbox, and no check failed a `src/web` change with no screenshot. An author
filling in the template was never asked, so the rule held only while they
remembered it. Nothing said what to do when the page cannot show the before
state, so "it can't be captured" became a reason to skip it.

## Spec delta

- [ADR-0142](../decisions/ADR-0142-a-web-ui-pull-request-shows-its-screenshots.md)
  records the rule and its exemption line.
- The template has a `## Screenshots` section, the
  `No screenshot needed: <reason>` line, and a checkbox.
- `tools/pr-screenshots.mjs`, run by the `screenshots` job in
  `linked-issue.yml`, fails a rendered web change whose body has neither a
  pinned screenshot link nor that line. `tests/js/pr-screenshots.test.mjs`
  also keeps the template's section and exemption line in place.

## Scenario

None. This is a process lesson, and no scenario can prove it. The check and
its tests are its guard.

## Skill

[`deliver-change`](../../skills/deliver-change/SKILL.md) step 6 now says: the
before shot comes from a build of `origin/main`; state a page cannot capture is
captured at OS level; shots wait for entry animations; light and dark when the
issue asks; a change with nothing visible says why.
[`deliver-hpac-change`](../../skills/deliver-hpac-change/SKILL.md) step 6
names the macOS capture command, the template section, the check, and how to
run it locally.
