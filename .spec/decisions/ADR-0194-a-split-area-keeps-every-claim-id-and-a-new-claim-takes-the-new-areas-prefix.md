---
title: A split area keeps every claim ID, and a new claim takes the new area's prefix
description: question-bank-and-form and moderation-authentication-and-publication are split into nine cohesive areas instead of being grouped with Rule blocks. Every moved scenario keeps its claim ID; each area declares in its README the prefix a new claim takes; REQ-QB and REQ-MOD are retired at their last numbers, and the generator fails a claim under any other prefix.
type: adr
status: accepted
date: 2026-10-03
decision-makers: Chase Florell
keywords: feature areas, claim IDs, claim prefix, retired prefix, Rule blocks, split, traceability, claims.json, ADR-0084, ADR-0184, ADR-0193
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
blocks rather than split it, so that no area, link, or step-definition scope
had to change. The blocks turned out to be section headings, not business
rules, and the areas still mixed unrelated concerns: authentication, review,
the admin list, and the public feed in one; authoring, choices, translation,
and the reporter's form in the other. Cross-cutting scenarios were split
between areas: the unsaved-changes dialog sat in four, private attachments in
two.

A claim ID is cited from commits, ADRs, lessons, code comments, and pull
request bodies already in history, and is never renumbered (ADR-0084). Until
now an ID's prefix also named its area, one per folder.

## Decision drivers

- No area large enough that a reader cannot hold it; each about one concern.
- No claim ID changes, so every citation already written stays true.
- A new claim's prefix says which area it was written in.
- A rule a tool can check, not one a person remembers.

## Considered options

- **Keep the `Rule:` blocks in one file per area.** Rejected: the files stay
  2,000 lines, and the blocks group by section, not by rule.
- **Renumber every moved scenario under its new area's prefix.** Rejected: it
  breaks every citation in history, which is what a stable ID exists to
  prevent.
- **Let the new areas go on issuing `REQ-QB` and `REQ-MOD`.** Rejected by the
  owner: a new claim takes its own area's prefix, so the prefix keeps saying
  where a claim was written.
- **Record each area's prefix in a table in the tools.** Rejected: an area
  declares its own prefix in its README frontmatter, beside its other
  frontmatter, so adding an area touches only the area.
- **Split each area into cohesive areas; moved scenarios keep their IDs; each
  area declares the prefix its new claims take; the split prefixes retire** —
  chosen.

## Decision

1. **The two areas are split.**
   - `question-bank-and-form` becomes `question-authoring`,
     `question-translation`, `choices-and-type-ahead`, `dependent-choices`, and
     `report-form`.
   - `moderation-authentication-and-publication` becomes
     `authentication-and-roles`, `review-and-publication`,
     `admin-report-search`, and `public-feed`.
   - Its own README's "Out of scope" goes with each area.
   - A cross-cutting scenario moves to the one area that owns it: the
     unsaved-changes dialogs of the question editor, the summary editor, the
     type-ahead review queue, and the private-note composer to
     `web-localization-and-design`, and private attachments to `media`.
     None stated the same claim as another, so no claim was deleted.
2. **Every moved scenario keeps its ID**, `REQ-QB-001` in `question-authoring`
   as anywhere else. No scenario's wording changed in the move.
3. **Each area declares its prefix.** Its README's frontmatter carries
   `prefix: REQ-<AREA>`, the prefix every new claim in the area takes. Each
   area's is its own.
4. **A split area's prefix retires.** `REQ-QB` retired at `REQ-QB-268` and
   `REQ-MOD` at `REQ-MOD-211`; `tools/spec/claim-prefixes.ts` records each with
   its last number. A claim may carry its area's prefix, or a retired prefix
   at or below its last number, and nothing else.
   `node tools/spec/generate-traceability.ts` fails any other claim, and an
   area with no prefix, a malformed or retired one, or one another area
   declares.
5. **The next ID** under an area's prefix is
   `node tools/spec/claim-prefixes.ts --next <area>`.
6. **The data names the prefixes.** `.spec/claims.json` lists every area with
   its prefix and every prefix its claims carry; the graph fragment puts them
   on the area's node; the index shows each area's prefix, then the retired
   ones it holds.
7. **No `Rule:` block is required.** The split areas carry none. A `Rule:`
   block, where one is used, still carries no tags.

### What still holds of ADR-0193

All of
[ADR-0193](ADR-0193-the-claims-are-generated-as-json-a-graph-fragment-and-one-slim-matrix.md)'s
decisions 1 to 6, and the records it lists as still holding, except one: a
large area is no longer grouped with `Rule:` blocks.

## Consequences

- Sixteen areas instead of nine; the largest two split areas are now about
  700 lines each, and `media` about 760 with private attachments.
  `report-submission`, at 1,158 lines, was not part of this split.
- An area's folder no longer names every claim in it: a reader finds a claim
  by its ID in `.spec/claims.json` or the matrix, which give its area.
- Reqnroll `[Scope(Feature = …)]` bindings name the new feature titles, one
  attribute per feature whose scenarios use them. The bindings recorded for
  every claim are the same files as before the split.
- Every QB-derived feature keeps the QB `Background` and
  `@xunit:collection(QuestionBankRunsAlone)`, so those scenarios still run
  serially and with the same steps as before.
- A link from an accepted ADR to a deleted area file now points at a
  permalink to the file as it was, and
  `tools/spec/check-adr-immutability.ts` allows that inside the status
  paragraph too.
- A future split adds its area's prefix to the retired list in the same pull
  request.

## Related

- [ADR-0084](ADR-0084-stable-claim-ids-and-a-generated-traceability-matrix.md),
  [ADR-0184](ADR-0184-a-generated-map-binds-every-claim-to-its-step-definitions.md):
  stable claim IDs and the binding rules, through ADR-0193.
- [ADR-0192](ADR-0192-an-accepted-adr-is-immutable-and-process-rules-are-conventions.md):
  a narrowing record supersedes the whole older one and lists what still
  holds.
