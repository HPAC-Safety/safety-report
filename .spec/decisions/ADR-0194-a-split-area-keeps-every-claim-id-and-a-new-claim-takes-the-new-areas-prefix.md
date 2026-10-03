---
title: A split area keeps every claim ID, and a new claim takes the new area's prefix
description: A scenario keeps its claim ID when it moves to another area. Each area declares in its README the prefix every new claim in it takes; a split area's prefix retires at its last number; the generator fails a claim under a prefix no area declares and none retired, and .spec/claims.json lists every area with its prefixes.
type: adr
status: accepted
date: 2026-10-03
decision-makers: Chase Florell
keywords: feature areas, claim IDs, claim prefix, retired prefix, Rule blocks, split, traceability, claims.json, ADR-0084, ADR-0184, ADR-0193, CONV-002
---

# ADR-0194 — A split area keeps every claim ID, and a new claim takes the new area's prefix

**Status:** Accepted. Decided by the owner on 2026-10-03 in
[#809](https://github.com/HPAC-Safety/safety-report/issues/809) (decision 3)
and [#814](https://github.com/HPAC-Safety/safety-report/issues/814). Supersedes
[ADR-0193](ADR-0193-the-claims-are-generated-as-json-a-graph-fragment-and-one-slim-matrix.md).

## Context

Two areas held 57% of the specification's feature lines:
`question-bank-and-form` (2,207 lines, 227 scenarios) and
`moderation-authentication-and-publication` (1,990 lines, 200 scenarios).
ADR-0184's amendment, which ADR-0193 kept, grouped each with Gherkin `Rule:`
blocks instead of splitting it. The blocks turned out to be section headings,
not business rules, and the areas still mixed unrelated concerns.

A claim ID is cited from commits, ADRs, lessons, code comments, and pull
request bodies already in history, and is never renumbered (ADR-0084). Until
now an ID's prefix also named its area, one prefix per folder, so a scenario
could not move without breaking one rule or the other.

## Decision drivers

- No claim ID changes, so every citation already written stays true.
- A new claim's prefix says which area it was written in.
- A tool checks the prefixes; nobody has to remember them.

## Considered options

- **Keep the `Rule:` blocks in one file per area.** Rejected: the files stay
  2,000 lines, and the blocks group by section, not by rule.
- **Renumber every moved scenario under its new area's prefix.** Rejected: it
  breaks every citation in history, which a stable ID exists to prevent.
- **Let the new areas go on issuing `REQ-QB` and `REQ-MOD`.** Rejected by the
  owner: a new claim takes its own area's prefix.
- **Record each area's prefix in a table in the tools.** Rejected: an area
  declares its own prefix in its README's frontmatter, so adding an area
  touches only the area.
- **Put all of this in a convention.** Rejected for the data rules: what an ID
  may look like, and what `.spec/claims.json` holds, is the shape of the
  specification's data, which tools and CI read and which is not edited in
  place. How to split an area, the size at which to do it, and how to take
  the next ID are working practice, and they are
  [CONV-002](../conventions/CONV-002-an-area-past-800-lines-is-split-and-its-scenarios-keep-their-ids.md).
- **IDs survive a move, each area declares its prefix, and split prefixes
  retire** — chosen.

## Decision

1. **A scenario keeps its claim ID when it moves to another area.** An ID's
   prefix records where the claim was first written, not where it lives.
2. **Each area declares its prefix.** Its README's frontmatter carries
   `prefix: REQ-<AREA>`, the prefix every new claim in the area takes. No two
   areas declare the same prefix.
3. **A split area's prefix retires.** `tools/spec/claim-prefixes.ts` records
   each retired prefix with its last number: `REQ-QB` at `REQ-QB-268` and
   `REQ-MOD` at `REQ-MOD-211`, from #814's split.
4. **What the generator accepts.** `node tools/spec/generate-traceability.ts`
   accepts a claim under:
   - any prefix an area declares, wherever the claim now lives; or
   - a retired prefix, at or below its last number.

   It fails any other claim, and an area with no prefix, or a malformed,
   shared, or retired one.
5. **`.spec/claims.json` lists every area** with its own prefix and every
   prefix its claims carry. The graph fragment puts them on the area's node,
   and the index shows them.

### What still holds of ADR-0193

All of
[ADR-0193](ADR-0193-the-claims-are-generated-as-json-a-graph-fragment-and-one-slim-matrix.md)'s
Decision section, and the records it lists as still holding, except one
item: a large area is no longer grouped with `Rule:` blocks.

## Consequences

- #814 split the two areas into nine, and the largest of those is now 697
  lines (`question-authoring`), then 607 (`report-form`).
  `report-submission` (1,158 lines) was left unsplit by the owner.
- An area's folder no longer names every claim in it: `.spec/claims.json` and
  the matrix give each claim's area.
- A future split adds its area's prefix to the retired list in the same pull
  request.
- Scenarios that shared one test class may run in parallel after a split.
  Two steps in #814 had to re-read until their reads of the shared database
  agreed ([CONV-002](../conventions/CONV-002-an-area-past-800-lines-is-split-and-its-scenarios-keep-their-ids.md)).

## Related

- [ADR-0084](ADR-0084-stable-claim-ids-and-a-generated-traceability-matrix.md):
  stable claim IDs, through ADR-0193.
- [ADR-0192](ADR-0192-an-accepted-adr-is-immutable-and-process-rules-are-conventions.md):
  a narrowing record supersedes the whole older one and lists what still
  holds.
- [CONV-002](../conventions/CONV-002-an-area-past-800-lines-is-split-and-its-scenarios-keep-their-ids.md):
  how to split an area, and how to take the next ID.
