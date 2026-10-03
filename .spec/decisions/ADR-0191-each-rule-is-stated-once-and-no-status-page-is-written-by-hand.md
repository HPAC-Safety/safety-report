---
title: Each rule is stated once, and no status page is written by hand
description: AGENTS.md states each product invariant in one line with links to the claims and ADRs that hold it; docs/implementation-status.md is deleted; docs/issue-traceability.md is generated from GitHub, and its drift issue asks for a regeneration instead of hand-edited rows.
type: adr
status: accepted
date: 2026-10-03
decision-makers: Chase Florell
keywords: single source, AGENTS.md, product invariants, implementation status, issue traceability, generated documentation, drift, ADR-0037, ADR-0084, ADR-0143
---

# ADR-0191 — Each rule is stated once, and no status page is written by hand

**Status:** Accepted. Decided by the owner on 2026-10-03 in
[#809](https://github.com/HPAC-Safety/safety-report/issues/809) (decisions 4
and 7) and [#811](https://github.com/HPAC-Safety/safety-report/issues/811).
Supersedes
[ADR-0143](ADR-0143-issue-traceability-drift-opens-an-issue-and-gates-nothing.md)
for the issue page; its source-inventory half stands. Supersedes the part of
[ADR-0084](ADR-0084-stable-claim-ids-and-a-generated-traceability-matrix.md)
that kept the two narrative pages.

## Context

A product rule was stated three or four times: in an `AGENTS.md` invariant, in
the ADR that decided it, in the area README, and in the scenario. The copies
drifted. `.spec/features/README.md` still said every answer is stored as one
string, after ADR-0128 made a choice answer name its choice and ADR-0130 made
a yes/no answer a boolean.

Two hand-written pages restated what generated ones show:

- `docs/implementation-status.md`, a prose matrix of main against the target.
  Its audit baseline was months old, and every gap it named is now an
  `@ignore` claim, an ADR, or an open issue.
- `docs/issue-traceability.md`, a disposition per open issue. ADR-0143 made
  its drift visible as an issue, but the fix was still a person writing rows.

## Decision

- **`AGENTS.md` states each product invariant in one line**, keeping its
  number, and links the claims (`REQ-*`, `CON-*`) and ADRs that hold the
  detail. The "Not built" and "Machine translation" sections do the same. A
  rule that lives nowhere else stays in full until a scenario, constraint, or
  ADR holds it.
- **`docs/implementation-status.md` is deleted.** A gap is an `@ignore`
  scenario in the generated matrix, or an open issue; nothing else records
  one.
- **`docs/issue-traceability.md` is generated** by
  `node tools/spec/generate-issue-traceability.ts` from GitHub's REST API:
  one row per open issue with its title, milestone, labels, and parent. The
  `in progress` label is left out; it changes too often. The page has no
  date and no prose per issue; what an issue asks for is in the issue.
- **Drift is checked the way ADR-0143 checked it**, by
  `tools/spec/check-issue-traceability.ts --sync` from
  `.github/workflows/issue-traceability.yml`, daily, on push to `main`, and on
  dispatch, never on a pull request. Drift now means the committed page is not
  what the generator would write: a missing, stale, or extra row, or edited
  text. The one drift issue asks whoever takes it to run the generator and
  open a pull request that closes it. The drift issue's identity, labels,
  milestone, and reopening rules are ADR-0143's, unchanged.
- **A pull request that closes an issue no longer edits the page.** The next
  regeneration drops the row.

## Consequences

- `AGENTS.md` shrinks, and a product rule changes in one place plus a link.
- A page nobody writes cannot fall behind its source. Between regenerations it
  lags GitHub, and the drift issue says so.
- The per-issue disposition prose is gone. Where it pointed at a constraint,
  the constraint itself says so.
- ADR-0183's note that the status page stays in `docs/` no longer applies;
  its placement rule stands.

## Alternatives rejected

- **Have the workflow open the regeneration pull request itself.** Every
  issue filed or closed would open a bot pull request, each needing review
  and a linked issue. A daily drift issue is enough.
- **Regenerate the page on every pull request.** It would make every pull
  request touch it, and conflict with each other.
- **Keep the disposition column, generated from a field in each issue.** It
  restates the issue and needs a convention nobody follows today.
