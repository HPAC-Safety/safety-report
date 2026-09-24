---
title: A persisted checkout token outranks the PAT on the remote
description: The translation bot's push authenticated as the built-in GITHUB_TOKEN rather than the PAT on its remote URL, so the CI it started waited for a maintainer to approve it.
type: lesson
date: 2026-09-24
issue: 416
status: accepted
---

# Lesson 0018 — A persisted checkout token outranks the PAT on the remote

## Symptom

A pull request's CI sometimes stopped at **Approve and run**. The four runs on
f95c19d7 (the translation bot's commit on the `issue-344` branch) were
`action_required`, and each had `triggering_actor = github-actions[bot]`.
Other translation commits ran as the PAT owner, without being asked.

## Root cause

`i18n-translate.yml` and `terraform-relock.yml` checked out with the default
`persist-credentials: true`. `actions/checkout` then writes an
`http.https://github.com/.extraheader` carrying the built-in `GITHUB_TOKEN`
into an `includeIf` credentials file, as the run log shows.

Both workflows later set `https://x-access-token:${TRANSLATION_PR_TOKEN}@…` on
the remote so their push would run CI as the PAT. Git sends the extra header
first, though, so the push authenticated as `github-actions[bot]`. GitHub either
starts no run for that push or holds it for approval. The PAT on the URL was
never used. `traceability.yml` already checked out with
`persist-credentials: false`, which is why only the translation bot's pushes
were affected.

## Spec delta

Both workflows now check out with `persist-credentials: false`.
`tests/js/workflow-push-credentials.test.mjs` fails if any workflow that puts a
token on its remote URL has a checkout step that persists credentials.

## Scenario

No scenario is needed. This is a property of the delivery tooling, not of the
system that `features/` describes. The test above proves it.

## Skill

[`deliver-hpac-change`](../../skills/deliver-hpac-change/SKILL.md) now says
that a workflow pushing with a token on the remote URL checks out with
`persist-credentials: false`.
