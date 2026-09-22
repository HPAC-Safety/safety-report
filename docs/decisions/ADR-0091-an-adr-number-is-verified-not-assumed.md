---
title: An ADR number is verified, not assumed
description: A decision record's number is claimed by naming a file, so a duplicate fails the build and renumbering is one command rather than a hand-audit.
type: adr
status: accepted
date: 2026-09-22
decision-makers: Chase Florell
keywords: ADR, numbering, collisions, rebase, concurrency, tooling
---

# ADR-0091 — An ADR number is verified, not assumed

## Status

Accepted.

## Context

An ADR's number is its identity. It appears in the filename, in the document's
own heading, in links from other records, in skills, in `AGENTS.md`, in workflow
comments, in commit messages and in issue history. People say it aloud.

It is claimed by naming a file, and nothing verified the claim. Two branches
could each create `docs/decisions/ADR-0089-*.md` and every check in the
repository would pass; the collision surfaced only when one of them rebased.

That is not hypothetical. It happened three times in a single afternoon —
ADR-0081, ADR-0082 and ADR-0089 — because several agents work in this
repository concurrently and each picks a number when it starts writing, not
when it merges. The delivery contract already said to rebase before every push,
but the number is chosen earlier than that, so the guidance was one step too
late to help.

Losing the race was also expensive out of proportion to the mistake. Renaming
the file is trivial; finding every reference is not, and a missed one leaves a
link that resolves to somebody else's decision — worse than a broken link,
because it reads as correct.

## Decision

**The number is verified where it is claimed, and losing the race costs a
command.** `tools/adr-numbers.mjs` does three things:

- `--check`, the default, fails on two records sharing a number, on a filename
  whose number disagrees with the document's own heading, and on a file in
  `docs/decisions/` not named `ADR-NNNN-kebab-slug.md`. It runs in the
  pre-commit hook when a record is staged, and in CI's `docs` job — local
  first, because a rule enforced only in CI holds only in CI
  ([ADR-0073](ADR-0073-a-ui-scenario-is-skipped-by-reqnroll-itself.md)).
- `--next` prints the next free number, counting **every fetched remote
  branch** and not only `origin/main`. A number another open pull request has
  already taken is taken, even though it has not merged — which is exactly the
  race that keeps costing a rebase.
- `--renumber <old> <new>` moves the file and rewrites every reference across
  tracked markdown, tooling and workflows in one pass, so losing the race is a
  command rather than an audit.

**Sequential numbers stay.** The obvious way to make collisions impossible is
to stop counting — dates, or a hash, or the issue number. Rejected: these
identifiers are cited in prose and in history, and "ADR-0083" is something a
person can hold in their head and say out loud. The numbering is not the
problem; the unverified claim was.

**Rebase before committing, not only before pushing.** The number is chosen
when the file is named, so that is when the working tree needs to be current.
The delivery contract says so.

## Consequences

- A duplicate fails locally at commit time, and in CI for anyone who skipped
  the hook.
- A record whose filename and heading disagree — the classic half-finished
  rename — fails the same way.
- Both heading styles in the tree are accepted: older records write
  `# ADR-0016: title` and newer ones `# ADR-0083 — title`. The separator is not
  the point, and churning thirteen historical files to satisfy a cosmetic rule
  would be the kind of make-work this repository avoids.
- `--next` is only as current as the last `git fetch`. It says what it
  consulted, and the delivery contract pairs it with the rebase.
- `--renumber` rewrites every *textual* occurrence, which includes a test
  fixture naming the number on purpose. That is the honest trade for catching
  prose references like "(ADR-0089)", so it prints every file it touched and
  the diff is read before committing, like any other mechanical edit. Symlinked
  instruction files are skipped, because they resolve to `AGENTS.md` and would
  otherwise be rewritten three times.

## Alternatives

- **Leave it to review.** Rejected: it escaped review three times in one
  afternoon, and a reviewer reading one pull request cannot see the number
  another branch took.
- **Date-stamped or hash-based identifiers.** Rejected: see above. It trades a
  solved problem for an unreadable identifier in every citation.
- **Reserve numbers centrally before work starts.** Rejected: it requires
  concurrent sessions to coordinate before doing anything, which is the
  coordination this repository's worktree-per-issue arrangement exists to
  avoid.
- **A pre-commit hook alone.** Rejected as the only mechanism: a hook can be
  skipped, and the collision is created by two branches that each passed their
  own hook. CI is where the tree is whole.

## Related

- [ADR-0073](ADR-0073-a-ui-scenario-is-skipped-by-reqnroll-itself.md)
- [ADR-0087](ADR-0087-every-markdown-file-declares-itself.md)
- [lesson 0003](../lessons/0003-a-number-is-claimed-the-moment-someone-else-merges.md)
