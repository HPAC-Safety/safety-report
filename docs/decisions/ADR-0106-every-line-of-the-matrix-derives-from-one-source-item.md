---
title: Every line of the traceability matrix derives from one source item
description: docs/traceability.md drops its whole-tree totals line, sorts by ID, and gives each claim and constraint its own block, so git merges two correct matrices into the correct one; and traceability.yml treats a push rejected by a newer head as superseded.
type: adr
status: accepted
date: 2026-09-23
decision-makers: Chase Florell
keywords: traceability, generated documentation, merge conflicts, git merge, pull_request_target, ADR-0084, ADR-0101
---

# ADR-0106 — Every line of the traceability matrix derives from one source item

**Status:** Accepted. Amends
[ADR-0084](ADR-0084-stable-claim-ids-and-a-generated-traceability-matrix.md)
on the matrix's format, and
[ADR-0101](ADR-0101-ci-regenerates-the-traceability-matrix.md) on how
`traceability.yml` handles a rejected push. The matrix is still generated,
still tracked, still drift-checked, and still regenerated onto same-repo pull
requests.

## Context

Even after ADR-0101, `docs/traceability.md` kept causing merge conflicts and
red builds. The failures had four causes:

1. **A totals line counted across the whole tree.** `314 claims across 8
   areas: 242 covered…` changed in every pull request that touched any
   scenario, so any two such pull requests conflicted on it. Two squash merges
   could also each carry a correct-for-its-base count and leave `main` stale:
   this is the #369 incident ADR-0101 records, and it recurred (run
   35929243393).
2. **Adjacent table rows.** Git's three-way merge conflicts when changed lines
   are next to each other. A status change on one claim and on its neighbour
   conflicted, and so did two new claims appended to the same area.
3. **Feature-file order.** Rows followed the order of scenarios in their
   `.feature` file (`REQ-WLD-026` came before `REQ-WLD-014`), so moving a
   scenario within a file moved rows in the matrix.
4. **A push race in `traceability.yml`.** The job commits the regenerated
   matrix onto the pull request's branch. When the author pushed while it
   ran, the bot's push was rejected (`! [rejected] … (fetch first)`, run
   35960597491). The job went red, and the `docs` check then failed on the
   stale matrix.

The local `merge=ours` driver that `init-dev.sh` installs does not help with
any of this on GitHub: GitHub's merge and its "Update branch" button never
read a clone-local `.gitattributes`.

## Decision

**Every line of the matrix derives from exactly one scenario or one
constraint.** Nothing in the file is counted or computed across the tree.
Where that holds and every item's data line is separated from its neighbours
by unchanged lines, git's merge of two correct matrices *is* the matrix of the
merged tree. Regenerating after the merge changes nothing.

1. **No totals in the file.** `node tools/traceability.mjs` prints them, and
   when `GITHUB_STEP_SUMMARY` is set, writes them to the CI job summary.
2. **Sorted by area, then ID.** New claims append at the end of their area,
   and reordering a feature file changes nothing.
3. **One block per item.** Each claim is a `### REQ-…` heading, a blank line,
   and one data line (`<scenario> — *<engine>, <status>*`), under a
   `## Claims: <area>` heading. Each constraint has the same shape. The
   headings also give every claim a linkable anchor and a clean node for the
   graph ([ADR-0088](ADR-0088-the-matrix-carries-the-specification-into-the-graph.md)).
4. **A rejected push that lost to a newer head is a superseded run.** When
   `git push` fails and the branch no longer points at the event's head SHA,
   the author has pushed. That push starts its own run over the newer head.
   The job emits a notice and exits 0, and fails only when the branch has not
   moved.

`tests/js/traceability.test.mjs` proves the property with `git merge-file`:
status changes on neighbouring claims and new claims in different areas merge
cleanly into exactly `render()` of the combined input.

The one conflict left is two branches claiming the same new ID. That
collision is real, and it should stop the merge.

## Alternatives considered

- **Stop committing the matrix.** Make it a gitignored build output, published
  to the job summary. This removes conflicts entirely. It was rejected
  because it loses the browsable file on GitHub and changes what ADR-0084,
  ADR-0088, and ADR-0090 rely on.
- **One file per claim.** This has nearly the same merge property, but the
  cost is about 360 files, a noisier graph, and a push race that still
  remains.
- **One file per area.** Removes cross-area conflicts only. Two pull requests
  in the same area still conflict on adjacent rows.

## Consequences

- The matrix is longer: about 1,800 lines instead of about 400. It is still
  one file to read or grep.
- The totals are no longer in the repository. Read them from the generator's
  output or the CI job summary.
- `merge=ours` stays as a local convenience for the same-ID case, with the
  post-merge and post-rewrite hooks regenerating after it.
- A future change to the generator must keep the property. A whole-tree
  aggregate added back to the file breaks the merge tests.
