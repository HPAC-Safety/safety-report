---
title: A feature file may never contradict an accepted ADR
description: A feature file scenario and an accepted (non-superseded) ADR must never say two different things about the same behavior or architecture, in either direction, at the moment a pull request merges.
type: adr
status: accepted
date: 2026-09-18
decision-makers: Chase Florell
keywords: ADR, feature files, specification authority, consistency
---

# ADR-0047 — A feature file may never contradict an accepted ADR

**Status:** Accepted. Clarifies, and tightens the forward-looking half of,
`AGENTS.md`'s existing authority rule 3.

## Context

This session found and fixed exactly the failure this ADR exists to prevent:
[ADR-0043](ADR-0043-react-typescript-vite-web-front-end.md) and
[ADR-0044](ADR-0044-containerized-web-hosting.md) changed the web front end's
architecture, but
`features/web-localization-and-design/web-localization-and-design.feature`
still asserted "neither requires a SPA framework, client router, Node
production server, or bundler" and "core content and navigation do not depend
on JavaScript" — both now false. Worse, drafting
[ADR-0044](ADR-0044-containerized-web-hosting.md) initially relied on a
superseded ADR's "one site, admin as a route" shape instead of the feature
file, and produced a wrong hosting design (one container instead of two) that
had to be caught and rewritten before it went further.

`AGENTS.md` rule 3 already says a contradictory ADR is "superseded until it is
aligned with this specification" — but that rule is about *old* ADRs drifting
out of step with a spec that has since moved on. It does not say anything
about the moment a *new* ADR is written: nothing stopped this session from
publishing ADR-0043/0044 while the feature file still contradicted them, and
nothing would have caught it short of the explicit audit this ADR's sibling
request triggered.

## Decision

**A feature file scenario and an accepted (non-superseded) ADR must never
say two different things about the same behavior or architecture, in either
direction, at the moment a pull request merges.**

When a new or updated ADR changes something a feature file currently asserts:

1. the feature file is updated in the **same pull request** as the ADR
   (this restates `AGENTS.md` rule 5, scoped explicitly to this pair); and
2. if the ADR reverses a prior architectural decision, the *prior* ADR's
   status is updated to point at the one that supersedes it, in the same
   pull request — not left silently stale for a future reader (or agent) to
   trust by accident.

When a feature file scenario is found to contradict an ADR that is still
accepted (not superseded), that is a defect to resolve immediately, not a
style question: either the feature file is wrong and gets corrected, or the
ADR is wrong/outdated and a new ADR supersedes it. **The two are never left
standing in contradiction.** `AGENTS.md` rule 3's "spec wins" resolution
applies only to ADRs that predate the current spec and were never reconciled
with it — it is a fallback for inherited drift, not a license to introduce
new drift.

## Why this choice

**ADRs and feature files answer different questions and must still agree.**
A `.feature` file states user-visible acceptance criteria ("what"); an ADR
states a technical decision and its reasoning ("why", and often "how"). They
overlap wherever a technical decision is visible in the product — hosting
model, framework, no-JS guarantees — and an overlap that disagrees is not a
layering question, it is one of the two documents being wrong.

**Catch it at write time, not at read time.** The alternative — relying on
whoever next reads both documents to notice a contradiction — is what
happened this session, and it is exactly the kind of gap this specification's
own rule 4 ("a documented target feature must not be described as already
working merely because its scaffold exists") exists to prevent for
implementation. The same discipline applies to the specification's internal
consistency.

## Alternatives

- **Leave `AGENTS.md` rule 3 as the only rule, relying on it to eventually
  resolve any drift.** Rejected: rule 3 resolves drift after the fact, for a
  reader who happens to notice. It does not stop a new ADR from being written
  without touching the feature file it affects, which is what happened here.
- **Make feature files strictly subordinate to ADRs (ADRs always win).**
  Rejected: this would reverse `AGENTS.md` rule 1 wholesale (feature files are
  the canonical target design) rather than the narrower fix needed —
  contradiction is the defect, not the existence of two documents with
  different jobs.
- **A CI check that greps for known-stale terms.** Tempting, but brittle —
  the actual defect this session found ("no SPA framework") is a phrase, but
  the underlying contradiction is semantic (does the site require JS?), which
  a keyword check cannot verify in general. Left as a possible narrow
  follow-up, not a substitute for the review discipline this ADR states.

## Consequences

- Writing or updating an ADR that touches user-visible or architectural
  behavior now includes, as part of that same change: locating every feature
  file scenario the decision affects (`grep` for the relevant terms across
  `features/`, not just the one file the author assumes is affected) and
  reconciling it.
- A code review (human or agent) that sees an ADR merge without a
  corresponding feature-file update, when one is plausibly needed, treats
  that as an incomplete change — the same weight ADR-0045 gives a UI change
  with no Playwright test.
- `AGENTS.md` rule 3 gains a pointer to this ADR so a reader lands on the
  "why" for both halves of the rule: historical drift resolves in the spec's
  favor, but new drift is not supposed to happen at all.

## Related

- `AGENTS.md` rules 1, 3, and 5
- [ADR-0043](ADR-0043-react-typescript-vite-web-front-end.md),
  [ADR-0044](ADR-0044-containerized-web-hosting.md) — the incident this ADR
  formalizes a response to
- [ADR-0045](ADR-0045-ui-changes-require-playwright-and-server-tests.md) — the
  same "don't let a required artifact silently go missing" pattern applied to
  tests
