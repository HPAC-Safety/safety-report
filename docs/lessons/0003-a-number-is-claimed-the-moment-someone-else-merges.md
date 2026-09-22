---
title: A number is claimed the moment someone else merges
description: Three ADR numbers were taken out from under a branch in one afternoon, because the number was chosen when work started and verified never.
type: lesson
date: 2026-09-22
issue: 319
status: accepted
---

# Lesson 0003 — A number is claimed the moment someone else merges

## Symptom

Three times in one afternoon, an ADR number was gone by the time the branch
that picked it reached `main`:

- **ADR-0081 and ADR-0082** — chosen while writing the specification-driven
  development records, taken by another session's pull requests (#279, #280)
  before the branch rebased. Everything shifted to 0083–0086.
- **ADR-0089** — written, committed and pushed, then found to collide with
  "no malware scanning for attachments" (#312), which had merged minutes
  earlier. It became ADR-0090.

Each time the rebase either conflicted or, worse, merged cleanly and left two
files claiming the same number, with nothing in the repository objecting.

## Root cause

The number is the record's identity, and it was claimed by naming a file — an
act nothing verified. `docs/decisions/` could hold two `ADR-0089-*.md` files and
every check passed.

Underneath that: the number is chosen when writing *starts* and only becomes
contested when the branch *merges*, and several agents work in this repository
at once. The delivery contract already said to rebase before every push, which
is true and was not enough — by the time a push happens the filename has existed
for an hour.

The cost was also out of proportion. Renaming the file is trivial. Finding
every reference is not: the number appears in the filename, the heading, links
from other records, skills, `AGENTS.md`, workflow comments and tool source, and
a missed one leaves a link resolving to somebody else's decision — which reads
as correct, so nobody catches it.

## Spec delta

[ADR-0091](../decisions/ADR-0091-an-adr-number-is-verified-not-assumed.md) makes
the number verified rather than assumed. `tools/adr-numbers.mjs --check` fails a
duplicate and a filename disagreeing with its heading, in the pre-commit hook
and in CI's `docs` job. `--next` counts every fetched remote branch, so a number
an open pull request has taken is skipped rather than collided with. And
`--renumber <old> <new>` moves the file and rewrites every reference in one
pass, so losing the race costs a command.

The delivery contract now says to rebase onto fresh `origin/main` **before
committing**, not only before pushing, and to take the number after that rebase.

## Scenario

No scenario. This is a property of the repository's own records and tooling,
not of the system the repository builds, so nothing in `features/` can assert
it. `tests/js/adr-numbers.test.mjs` covers it directly, including a two-repository
test that proves a number claimed only on an unmerged remote branch still counts
as taken.

## Skill

[`deliver-hpac-change`](../../skills/deliver-hpac-change/SKILL.md) carries the
general rule: **rebase before you commit, not only before you push**, and claim
a shared identifier — a number, a name, a slug — from the tree as it is after
that rebase, never from the tree as it was when you started. It also names
`--next` and `--renumber` so the recovery is a command rather than an
investigation.
