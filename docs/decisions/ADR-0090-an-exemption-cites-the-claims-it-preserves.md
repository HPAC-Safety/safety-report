---
title: An exemption from scenario coverage cites the claims it preserves
description: A change that needs no new scenario names the existing claims it leaves standing, from a closed category vocabulary, checked against the generated matrix.
type: adr
status: accepted
date: 2026-09-22
decision-makers: Chase Florell
keywords: feature-coverage, exemption, escape hatch, gates, claim IDs, agents
---

# ADR-0090 — An exemption cites the claims it preserves

## Status

Accepted. Hardens the gate widened by
[ADR-0083](ADR-0083-specification-driven-development.md)'s delivery rules.

## Context

`feature-coverage` guards every behavior change under `src/`. CI cannot judge
whether a change is behavioral — only a person or an agent can — so the gate
flags the shape and lets a human answer. The answer was one line in the
pull-request body:

    No .feature scenario needed: <reason>

That line is an assertion. Nothing checked it, nothing constrained what it
said, and it exempted the whole pull request. Three ways it fails open:

- **Free text.** "refactor" is accepted for a change that alters behavior.
- **Whole-pull-request scope.** Eleven mechanical files and one behavioral one
  are exempted by the same sentence.
- **It costs less than compliance.** Writing the line is faster than writing
  the scenario.

The third is the one that matters, and it is not a hypothetical about bad
faith. Most changes here are made by a coding agent working toward a green
build. When a rule offers an escape that is cheaper than obeying it, that is a
gradient, and an agent follows a gradient by default — not by deciding to cheat.
A person under time pressure follows the same one. The honest reading is that
the old hatch was a design defect, not a trust problem.

This repository already knows the shape of the failure: lesson 0001 records a
rule that held only where somebody typed a flag. An escape hatch that nothing
inspects is the same failure, one level up — a rule that holds only where
somebody chooses to honour it.

## Decision

**An exemption is a citation, not an assertion.** A change that genuinely needs
no new scenario is, by definition, a change that preserves behavior some
existing claim already states. It names those claims:

    No .feature scenario needed: <category> — <what changed, and why no behavior did>
    Claims preserved: REQ-SUB-012, REQ-SUB-013

Four constraints, all enforced by `tools/feature-coverage.mjs`:

1. **A closed category vocabulary** — `refactor`, `styling`, `dependency`,
   `test-only`, `build`, `revert`, `docs`. A new category is a decision
   somebody argues for, not a word somebody types.
2. **`Claims preserved:` is mandatory, and every id must exist** in
   [`docs/traceability.md`](../traceability.md). A dangling id fails here
   exactly as a dangling `Verified by:` fails the matrix.
3. **A category the diff can contradict is checked against the diff.**
   `test-only` fails when a production file changed; `docs` fails when a
   non-markdown file changed; `dependency` fails when anything outside the
   dependency manifests changed.
4. **A reason must carry information.** A single word repeating the category is
   the category typed twice, and fails.

The judgement lives in a tested tool rather than in inline workflow shell, so
it runs locally as well as in CI — the rule from lesson 0001, applied to the
gate that enforces the rules.

**The exemption is not removed.** A dependency bump and a revert are real, and
a gate with no valve gets disabled rather than obeyed. What changes is its
price: you may skip the scenario only by pointing at the scenarios that already
cover what you preserved. That is the same work the specification asks for, and
it is why a well-formed exemption is evidence rather than an excuse.

## Consequences

- An exemption is auditable. The pull-request body becomes the squash commit
  message, so `git log --grep 'No .feature scenario needed'` is the history of
  every exemption ever claimed, with the claims each one cited.
- A reviewer verifies an exemption instead of accepting it — the claims are
  named, so "does this change really leave those standing?" is a question with
  an answer. `agents/spec-reviewer.md` treats an unverified exemption as a
  finding.
- A genuine refactor now has to know what it preserves, which is a reasonable
  thing to ask of a refactor.
- False positives still exist and still cost a sentence plus two claim ids. If
  that becomes a burden, the gate's pathspec is the thing to revisit, not the
  citation.

## Alternatives

- **Leave the free-text line.** Rejected: it is the failure this record exists
  to close, and the cost asymmetry means it degrades with use rather than
  holding.
- **Remove the exemption entirely.** Rejected: a dependency bump genuinely
  needs no scenario, and a gate that cannot be satisfied honestly gets marked
  non-required — trading a leaky rule for no rule.
- **Require the exemption to list exempted file paths.** Rejected: a thirty-file
  mechanical refactor would spend its honesty on transcription, and a path list
  says nothing about behavior. The claims are the meaningful unit.
- **Have the gate judge whether a change is behavioral.** Rejected as
  impossible, and pretending otherwise would produce a gate people learn to
  work around — which is exactly this record's subject.
- **Require a second reviewer's approval on any exemption.** Rejected for now:
  the repository has one maintainer, so it would mean self-approval, which is
  ceremony rather than scrutiny.

## Related

- [ADR-0073](ADR-0073-a-ui-scenario-is-skipped-by-reqnroll-itself.md)
- [ADR-0083](ADR-0083-specification-driven-development.md)
- [ADR-0084](ADR-0084-stable-claim-ids-and-a-generated-traceability-matrix.md)
- [lesson 0001](../lessons/0001-a-guard-that-lives-only-in-ci-is-not-a-guard.md)
