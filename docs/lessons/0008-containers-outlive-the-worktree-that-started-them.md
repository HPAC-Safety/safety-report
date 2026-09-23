---
title: Containers outlive the worktree that started them
description: An agent brought the dev environment up from its worktree and removed the worktree without tearing the containers down, so the next ./dev-up.sh from the primary checkout could not bind its ports and timed out after five minutes.
type: lesson
date: 2026-09-22
issue: 349
status: accepted
---

# Lesson 0008 — Containers outlive the worktree that started them

## Symptom

`./dev-up.sh` from the primary checkout never came up. The postgres container
exited with `Bind for 0.0.0.0:5432 failed: port is already allocated`, the API
container stayed in `Created`, the web container was killed, and the script
waited its full 300 seconds before reporting a generic timeout. It was the
second failure of this shape: an earlier one, after a Docker Desktop restart,
reused containers whose published ports had silently disappeared.

## Root cause

The ports were held by the `hide-question-key` compose project — containers an
agent had started from `.claude/worktrees/issue-342/hide-question-key`, as
`deliver-hpac-change` step 7 told it to, and then abandoned when step 8 told it
to remove the worktree. Nothing in the workflow ever ran `./dev-up.sh --down`.

Compose names a project after the directory it runs in, so every worktree is a
separate project, and every one of them publishes the same fixed host ports.
Removing a worktree removes the files, not the project: its containers keep
running, bind-mounting a directory that no longer exists, holding ports only
one checkout can have at a time. `dev-up.sh` did not look for them, and did not
notice a service had exited while it waited.

## Spec delta

None upstream — the local development environment is not a claim about the
product. The remedy is in `dev-up.sh` and in the skill.

`dev-up.sh` now takes the ports over before it starts: containers from any
other checkout of this repository that publish the dev ports are brought down
(volumes kept), a port held by anything else stops the script naming the
holder, containers are always recreated so their published ports are
re-programmed, and a service that exits fails the wait at once with its logs.

## Scenario

No scenario. This is a property of the developer tooling and of how an agent
executes the delivery workflow, not of the system `features/` describes.

## Skill

[`deliver-hpac-change`](../../skills/deliver-hpac-change/SKILL.md) step 8 now
tears the environment down with `./dev-up.sh --down` before
`git worktree remove`, and says why: containers belong to the checkout that
started them, and removing the checkout does not remove them. Step 7 no longer
asks the agent to judge whether another branch's containers are running —
`dev-up.sh` handles that itself, so an agent that still forgets step 8 costs
the next person nothing.
