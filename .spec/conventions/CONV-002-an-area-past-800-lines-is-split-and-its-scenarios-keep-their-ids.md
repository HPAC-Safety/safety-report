---
title: An area past about 800 lines is split, and its scenarios keep their IDs
description: How to split a feature area that has grown past about 800 lines, which steps a split owes (scopes, Backgrounds, collections, links, the retired prefix), and how to take the next claim ID with claim-prefixes.ts --next.
type: convention
status: accepted
date: 2026-10-03
---

# CONV-002 — An area past about 800 lines is split, and its scenarios keep their IDs

## Rule

### When to split

- An area is one concern. Once its `.feature` file grows past about 800 lines,
  split it into new areas rather than grouping it with `Rule:` blocks.
- A `Rule:` block, where one is used, states a business rule. It carries no
  tags; `node tools/spec/generate-traceability.ts` fails a tagged Rule.

### How to split

1. Propose the cut on the issue before moving anything.
2. Give each new area its own `.feature`, a `README.md` with "Out of scope",
   and a new `prefix:` in that README's frontmatter.
3. Move every scenario verbatim. It keeps its ID; never renumber one.
4. Retire the split area's prefix: add it to `RETIRED_PREFIXES` in
   `tools/spec/claim-prefixes.ts`, at its last number.
5. Check that each moved scenario still runs as it did:
   - its `Background`: copy the old one into each new feature that needs it,
     or confirm that the steps it gains or loses are contextual no-ops;
   - its `@xunit:collection(...)` tag: keep it wherever the reason for it
     still holds, and its partial class in
     `tests/HpacSafety.Acceptance.Tests/XunitCollectionBindings.cs`;
   - its parallelism: scenarios that used to share one test class may now run
     in parallel. A step that reads a count or a list from the shared booted
     database reads it until it agrees with itself (as `PendingCountSteps`
     does), or compares only against data its own scenario seeded.
6. Rescope every `[Scope(Feature = …)]` that named the old feature title, one
   attribute per new feature whose scenarios use it. Then compare the bound
   step files per claim in `.spec/claims.json` before and after the move.
7. Fix every link into the old files. In an accepted ADR, point the link at a
   permalink to the file as it was, using the full 40-character commit SHA,
   and leave the link text unchanged
   ([ADR-0192](../decisions/ADR-0192-an-accepted-adr-is-immutable-and-process-rules-are-conventions.md)).
8. Run the moved areas' Reqnroll scenarios several times, not once, and the
   browser suite.

### Taking a new claim ID

- `node tools/spec/claim-prefixes.ts --next <area>` prints the next ID under
  the area's own prefix. It counts every ID a scenario carries, wherever the
  scenario now lives, and every ID a decision or lesson cites.
- The limit: a deleted claim that no decision or lesson cites, and that was
  the highest under its prefix, can be issued again. Before using an ID, also
  search the history (`git log -S'<ID>'`) when the previous highest was
  deleted recently.

## Why

`question-bank-and-form` and `moderation-authentication-and-publication` held
57% of the feature lines and mixed unrelated concerns; their `Rule:` blocks
were section headings, not rules (#814). The ID and prefix rules are
[ADR-0194](../decisions/ADR-0194-a-split-area-keeps-every-claim-id-and-a-new-claim-takes-the-new-areas-prefix.md).
Splitting moved two Reqnroll features into parallel test classes, and two
steps that compared reads of the shared database turned flaky until they
re-read until their reads agreed.

## Enforced by

- `node tools/spec/generate-traceability.ts` (the `docs` job) fails:
  - a claim under a prefix that no area declares and that isn't retired;
  - a number above a retired prefix's last;
  - an area with no `prefix:`, or a malformed, shared, or retired one;
  - a tagged Rule.
- `node tools/docs/check-links.ts` fails a link left pointing at a moved file.
- `node tools/spec/check-adr-immutability.ts` lets an ADR's link target change
  only when the old file is gone.
- The size threshold and the parallel-safety check are written, not checked.
