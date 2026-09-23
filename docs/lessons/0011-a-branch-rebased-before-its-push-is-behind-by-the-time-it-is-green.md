---
title: A branch rebased before its push is behind by the time it is green
description: An agent rebased onto origin/main right before pushing, as the workflow required, then reported the pull request ready once its checks went green — but main had moved while the checks ran, and the branch was already out of date.
type: lesson
date: 2026-09-23
issue: 358
status: accepted
---

# Lesson 0011 — A branch rebased before its push is behind by the time it is green

## Symptom

Pull request #359 was reported green and ready, and GitHub showed it as
out of date with `main`. The owner noticed it before the agent did.

## Root cause

The agent followed the rule. It ran `git fetch origin main && git rebase
origin/main` immediately before pushing. While the checks ran, Renovate merged
#357 to `main`, so the branch was one commit behind before the checks finished.

The workflow guarded the moment of the push and nothing after it. Step 9 said
to finish "only when checks are green", and a green run on a stale base meets
that wording. With several agents and Renovate merging continuously, the
window between a push and a green run is several minutes, and `main` often
moves during it.

## Spec delta

None upstream. This concerns how a change is delivered, not a claim about the
product.

## Scenario

No scenario. This is a property of the delivery workflow, not of the system
`features/` describes.

## Skill

[`deliver-hpac-change`](../../skills/deliver-hpac-change/SKILL.md) step 9 now
ends with a second freshness check. Once the checks are green, fetch again and
confirm that `gh pr view <pr> --json mergeStateStatus` does not say `BEHIND`.
If it does, rebase, push, and watch the checks again. A run is finished only
when its checks are green on a current branch.
