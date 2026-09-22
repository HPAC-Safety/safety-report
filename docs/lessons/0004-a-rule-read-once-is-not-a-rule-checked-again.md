---
title: A rule read once is not a rule checked again
description: An agent that had already followed the worktree rule twice on issue #24 started issue #82 by editing eleven files directly on main — the rule existed in prose, but nothing forced a check at the moment it mattered.
type: lesson
date: 2026-09-22
issue: 82
status: accepted
---

# Lesson 0004 — A rule read once is not a rule checked again

## Symptom

An agent working issue #82 (soft deletion) edited eleven tracked files and
added five new ones — domain code, an endpoint, Worker code, and tests —
entirely inside the primary checkout, on `main`. The same agent, in the same
session, had correctly followed `deliver-hpac-change`'s worktree rule twice
already for issue #24. The user caught it by noticing unstaged changes on
`main` and asking whether they were the agent's.

## Root cause

`deliver-hpac-change`'s "Start" section already said, plainly: "Never create
work directly on a branch in the primary checkout... Fetch fresh `origin/main`,
then create a git worktree." That sentence was followed for issue #24.

It was not re-checked for issue #82. Between the two, the session did a
sequencing detour — clarifying that #25 was blocked by #82, asking which
blocker to tackle first — and by the time coding began, nothing in that
detour prompted loading the skill again or checking the working directory.
The rule lived in prose the agent had read once, hours of conversation
earlier; nothing re-surfaced it at the moment the first `Edit` call landed.

This is the same shape as [Lesson 0001](0001-a-guard-that-lives-only-in-ci-is-not-a-guard.md):
a rule that holds only where it is actively checked is not yet a rule, it is a
description. A prose instruction read at skill-load time and never
mechanically re-verified is exactly that — it survived the first invocation
on trust, not on a check, and the second invocation had no trust to spend.

## Spec delta

None upstream — this is not a claim about the product, it is a claim about
how this agent works. The remedy is entirely in the skill.

## Scenario

No scenario. This is a property of how an agent executes the delivery
workflow, not of the system `features/` describes, the same as Lesson 0001
and Lesson 0003.

## Skill

[`deliver-hpac-change`](../../skills/deliver-hpac-change/SKILL.md) now states
the check as an action taken at the moment of the first edit, not only as
background context read once at skill-load time: **before the first `Edit` or
`Write` call for an issue, run `git branch --show-current` (or confirm the
active working directory is already a `.claude/worktrees/issue-<n>/...`
path); if it reports `main`, stop and create the worktree first.** It also
names the specific trap this lesson hit — a mid-conversation sequencing
detour, a resumed session, or a plain "continue" — as exactly the moment this
check is most likely to be skipped, because nothing about the moment looks
like "starting an issue."
