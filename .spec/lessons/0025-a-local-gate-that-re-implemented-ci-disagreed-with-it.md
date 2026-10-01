---
title: A local gate that re-implemented CI disagreed with it
description: Two local coverage scripts each re-implemented CI's ratchet on macOS, and their verdict often differed from CI's, so the gate lesson 0010 added could pass locally and fail on the pull request.
type: lesson
date: 2026-09-26
issue: 540
status: accepted
---

# Lesson 0025 — A local gate that re-implemented CI disagreed with it

## Symptom

`tools/coverage-check.sh`, the pre-pull-request gate
[lesson 0010](0010-a-coverage-gate-found-in-ci-not-before-the-pull-request.md)
added, often gave a different verdict from CI's `coverage` job for the same
branch. A second script, `check-coverage.sh`, did the same job a third way.
The other pull request checks (`linked-issue`, `no-session-link`,
`feature-coverage`, `agent-config`, terraform `infra`, e2e) were run by hand or
not at all.

## Root cause

Each script copied CI's commands instead of running CI:

- a different baseline (`origin/main` measured on the Mac, or main's artifact
  compared against a Mac measurement);
- macOS rather than Ubuntu 24.04: Homebrew's ffmpeg, different paths;
- whatever SDK `rollForward: latestFeature` picked, rather than the one
  `global.json` names, which changes the compiled branches.

A copy of a check drifts from the check. The commands matched when written;
the environment never did.

## Spec delta

None upstream: this is delivery tooling, not a product claim.
[ADR-0145](../decisions/ADR-0145-a-pull-requests-checks-run-locally-under-act.md)
replaces both scripts with `tools/ci-local.sh`, which runs the workflow files
themselves under act, in an Ubuntu 24.04 image, against the same baseline
artifact CI downloads.

## Scenario

None. This is a process lesson, and no scenario can prove it.

## Skill

[`deliver-change`](../../skills/deliver-change/SKILL.md) "Verify and publish"
step 1 now says: run the project's local CI runner with the draft pull request
body, which runs the pull request's workflows rather than a copy of them.
[`deliver-hpac-change`](../../skills/deliver-hpac-change/SKILL.md) step 1
names `tools/ci-local.sh --body pr-body.md`, for every pull request.
