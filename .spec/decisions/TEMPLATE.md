---
title: ADR template
description: The MADR shape every new architecture decision record copies — status line, context, decision drivers, considered options, decision, consequences.
type: template
---

<!--
Copy this file to ADR-NNNN-kebab-slug.md, taking NNNN from
`node tools/spec/adr-numbers.ts --next` after rebasing onto origin/main.
Replace the frontmatter block above with the ADR frontmatter below, and
delete this comment and every <placeholder>.

---
title: <the decision, as a sentence>
description: <one or two sentences: what was decided>
type: adr
status: accepted
date: <YYYY-MM-DD>
decision-makers: <names>
keywords: <comma-separated terms, and the ADR numbers it touches>
---

Rules (ADR-0192):
- status is proposed, accepted, rejected, deprecated, or superseded.
- Once accepted, only `status:` and the **Status:** line ever change.
  A change to the decision is a new ADR that supersedes this one.
- Sections: exactly these, in this order. Decision drivers and Related are
  optional; Considered options is required.
- A process, tooling, or agent-workflow rule is a convention under
  .spec/conventions/, not an ADR. Interface detail is a scenario.
-->

# ADR-NNNN — <the decision, as a sentence>

**Status:** Accepted. Decided by <who> on <YYYY-MM-DD> in <issue link>.

## Context

<The forces at play: what is true, what hurts, what constrains the choice.>

## Decision drivers

- <What a good answer must achieve.>

## Considered options

- **<Option>.** Rejected: <why>.
- **<Option>** — chosen.

<When no alternative was worth naming, say so here in one sentence.>

## Decision

<What is decided, stated as rules.>

## Consequences

- <What becomes easier, harder, or newly required.>

## Related

- <Links to the ADRs, lessons, or issues this builds on.>
