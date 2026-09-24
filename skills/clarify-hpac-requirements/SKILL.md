---
name: clarify-hpac-requirements
description: Resolve genuinely material HPAC Safety requirement ambiguity. Use when two plausible interpretations would change privacy, publication, retention, security, data shape, or user-visible behavior.
---

# Clarify HPAC Safety requirements

## First, look

Read the relevant `/features` pages, source, tests, and issue.

## Decide or ask

- **Reversible and no product-behavior change?** Proceed on a documented local
  assumption. Routine implementation details are not product questions.
- **Ask one concise question** only when the answer cannot be found and the
  choice would materially change privacy, publication, retention,
  authorization, stored data, compatibility, or externally visible behavior.
  State:
  - the evidence already available;
  - the two concrete interpretations;
  - the impact of each;
  - the smallest decision needed to continue.

## Write the answer back

In the same change:

- the answer becomes a scenario or an out-of-scope line, so the next run starts
  from the answer
  ([ADR-0083](../../docs/decisions/ADR-0083-specification-driven-development.md));
- a design change updates the affected canonical specification pages and the
  issue's acceptance criteria (see
  [`deliver-hpac-change`](../deliver-hpac-change/SKILL.md) "Keep the issue true
  while you work").
