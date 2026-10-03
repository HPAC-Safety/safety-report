---
title: An accepted ADR is immutable, follows one MADR template, and process rules are conventions
description: An accepted ADR's body never changes; a change is a new ADR and the old one changes only its status. ADRs follow one MADR template with required considered options, statuses are proposed, accepted, rejected, deprecated, or superseded, new process rules go to .spec/conventions/, and every lesson declares a kind that decides what it owes.
type: adr
status: accepted
date: 2026-10-03
decision-makers: Chase Florell
keywords: ADR, MADR, immutability, supersession, status, partially-superseded, considered options, conventions, lessons, lesson kind, incident, template, ADR-0083, ADR-0085, ADR-0087, ADR-0091, ADR-0183
---

# ADR-0192 — An accepted ADR is immutable, follows one MADR template, and process rules are conventions

**Status:** Accepted. Decided by the owner on 2026-10-03 in
[#809](https://github.com/HPAC-Safety/safety-report/issues/809) (decisions 1
and 8) and [#812](https://github.com/HPAC-Safety/safety-report/issues/812).

## Context

An audit of the decision records on 2026-10-03 found that an ADR stopped
being a record of what was decided when:

- 27 accepted ADRs carry appended amendment sections (ADR-0188 has four, added
  over three days); some were rewritten to describe the current target
  (ADR-0002); seven numbers were deleted (0029, 0030, 0036, 0160–0162, 0175).
  A reader cannot tell what was decided on the date at the top.
- The lifecycle vocabulary was ad hoc: `partially-superseded` (34 records)
  and prose such as "amends", "narrows", "extends"; there was no `proposed`,
  `rejected`, or `deprecated`.
- The template drifted: considered alternatives appeared under five headings
  ("Alternatives", "Alternatives rejected", "Rejected alternatives",
  "Alternatives considered", "Why…") and 19 records had none; the status was
  a `## Status` section in 48 records and a `**Status:**` line in the rest.
  `tools/spec/adr-numbers.ts` validated one form.
- About half the records are process, tooling, or interface rules rather than
  architecture. Process rules change often, which is what drove the
  amendments.
- Lessons had no validated structure: 18 of 43 changed nothing upstream, and
  0027–0036 are postmortems of infrastructure incidents, whose remedy, where
  there was one, was a skill change rather than a claim.

## Decision drivers

- What was decided, and when, must stay readable without consulting git.
- A rule must be checked by a tool, not remembered
  ([ADR-0073](ADR-0073-a-ui-scenario-is-skipped-by-reqnroll-itself.md)).
- Normalizing history must not change what any record says.

## Considered options

- **Keep amending in place, with a uniform amendment heading.** Rejected: it
  fixes the formatting and keeps the defect. The record still drifts from the
  decision it was accepted as.
- **Rewrite every ADR to the current target.** Rejected: it destroys the
  record of why the system is the way it is; the specification already
  states the current target.
- **Move existing process ADRs to conventions.** Rejected: moving them
  rewrites every citation and their history for no gain. Only new process
  rules go to conventions.
- **Immutable records, a MADR template, conventions for new process rules,
  and mechanical normalization of the old records** — chosen.

The implementation choices made under that decision:

- **Keep partial supersession, with a status naming what was narrowed.**
  Rejected by the owner on 2026-10-03: a partly superseded record must be read
  together with every record that narrowed it. A narrowing ADR supersedes the
  whole older record and lists what still holds, by link or claim ID, without
  restating it.
- **An incident lesson owes a skill change, like a process lesson.**
  Rejected by the owner on 2026-10-03: a postmortem's cause is often outside
  anything a skill governs. It owes symptom and root cause, and may name a
  skill it changed.

- **Conventions immutable, like ADRs.** Rejected: process rules change often,
  which is what drove the amendments; a convention is edited in place and git
  keeps its history.
- **Immutability in force from a fixed date or commit.** Rejected: keyed on
  this record existing on the pull request's base, the rule starts with the
  merge that writes it, and that pull request can normalize the old records.
- **No link may change.** Rejected: a moved or deleted file would leave a
  dangling link the record could never fix; a link's target may change, its
  text may not.
- **Invent considered options for the records that lack them.** Rejected: it
  would put words in an old decision's mouth. The heading says the options
  sit inline, or that none were recorded.
- **Grow the index generator into the validator.** Rejected: validation is a
  separate tool, so the generator stays a generator.

## Decision

### An accepted ADR is immutable

- Once accepted, an ADR's body never changes. Only its frontmatter `status:`
  and its `**Status:**` line may, to record that a later ADR superseded it or
  that it was deprecated.
- A link's target may be rewritten only when the file it points to moves or
  is deleted (point it at a commit permalink); its text may not. A move must
  not leave a dangling link that the record cannot fix.
- A change to a decision is a new ADR. The record it changes becomes
  `superseded`, its status line linking the new one. A new ADR that changes
  only part of an older one still supersedes the whole record, and **lists**
  what of it still holds — by link or claim ID — **never restating it**, so
  every rule keeps one source.
- No more appended amendment sections. The ones already written stay, as
  history, under one heading form: `## Amendment (YYYY-MM-DD)`, optionally
  followed by ` — <subject>`. Inline amendment notes in a record's opening
  paragraphs (`**Amended …:**`, `**Provider:**`, and the like) stay as
  written.
- An ADR is never deleted. The seven numbers already missing are listed in
  [`README.md`](README.md) with why.

### Statuses

- `proposed`, `accepted`, `rejected`, `deprecated`, `superseded`.
- A `proposed` record may change freely until it is accepted or rejected.
- An `accepted` record may move only to `superseded` or `deprecated`.
  `superseded`, `deprecated`, and `rejected` are terminal.
- A `superseded` or `deprecated` record's status line links the record that
  replaced or retired it: an ADR, or a convention.
- `partially-superseded` is retired. The records that already carry it keep it;
  no record may newly take it. The last four to take it are ADR-0083,
  ADR-0085, ADR-0087, and ADR-0183, which this record narrows.

### One template

[`TEMPLATE.md`](TEMPLATE.md) is a MADR shape:

- frontmatter with `status:`, and one `**Status:**` line directly under the
  heading;
- `## Context`;
- `## Decision drivers` (optional);
- `## Considered options` — required. A decision with no alternative worth
  naming says so under the heading;
- `## Decision`;
- `## Consequences`;
- `## Related` (optional).

A record from this one on uses those sections in that order and no
others. An older record keeps its own sections, normalized mechanically
without changing what it says:

- every status is one `**Status:**` paragraph directly under the heading;
- its alternatives heading is `## Considered options`;
- a record whose rejected options sit inline gets that heading saying so; a
  record that weighed none gets "None were recorded when this decision was
  accepted." Nothing is invented;
- its amendment headings take the one form above.

### What goes where

- **A new process, tooling, or agent-workflow rule** goes to
  [`.spec/conventions/`](../conventions/README.md), not to an ADR. A
  convention is `CONV-NNN-kebab-slug.md`, `type: convention`, with a stable
  number never reused. It is a living rule, edited in place; git keeps its
  history. Existing process ADRs stay where they are.
- **Interface detail** — wording, layout, a field's behavior — belongs in a
  scenario, not an ADR.
- An ADR records architecture: a technology, a boundary, a data shape, a
  durable trade-off with rejected options.

### Lessons declare a kind

Every lesson's frontmatter carries `kind`:

- `product` — a claim is the remedy: its `## Spec delta` names a `REQ-` or
  `CON-` ID.
- `process` — a skill or convention is the remedy: its `## Skill` names one.
- `incident` — an operational postmortem: what failed in running the system,
  and why. It owes `## Symptom` and `## Root cause`, and may also name a
  skill it changed.

A product or process lesson keeps the five sections of
[`../lessons/README.md`](../lessons/README.md); no lesson carries any other
`##` section.

### Checks

- `node tools/spec/check-records.ts` validates every ADR's template, status,
  and considered options, every lesson's kind and sections, and every
  convention's shape, in the pre-commit hook and the `docs` job.
- `node tools/spec/check-adr-immutability.ts` fails a pull request whose diff
  to an ADR that existed on its base changes anything but the status or a
  moved link's target, deletes the ADR, or makes a status move the lifecycle
  above forbids. It applies from the first base that carries this record.
- `tools/spec/adr-numbers.ts` accepts the five statuses, plus
  `partially-superseded` on the records that carry it.

### What this narrows

- [ADR-0083](ADR-0083-specification-driven-development.md)'s 2026-09-29
  amendment on what an ADR records: a process rule is now a convention, and
  interface detail a scenario.
- [ADR-0085](ADR-0085-a-lesson-flows-upstream-into-the-specification.md):
  a lesson declares one of three kinds, and an incident, a postmortem, owes
  only its symptom and root cause.
- [ADR-0087](ADR-0087-every-markdown-file-declares-itself.md): `convention`
  joins the types, and a lesson adds `kind`.
- [ADR-0183](ADR-0183-the-specification-lives-in-a-spec-directory.md): the
  closed set of ADR statuses is the five above, and the index lists
  conventions.

## Consequences

- An ADR reads as what was decided on its date. The current rule is the
  newest record in its chain, and the specification.
- Changing a decision costs a new ADR rather than a paragraph. That cost is
  the point: process churn moves to conventions, which are edited in place.
- The normalization touches every ADR once, mechanically; their decisions are
  unchanged.
- The index gains a conventions table and a lesson kind column.
- A pull request that only fixes a typo in an accepted ADR now fails; the
  typo stays.

## Related

- [ADR-0047](ADR-0047-feature-files-must-not-contradict-adrs.md)
- [ADR-0091](ADR-0091-an-adr-number-is-verified-not-assumed.md)
