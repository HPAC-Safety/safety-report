---
name: clarify-hpac-requirements
description: Resolve genuinely material HPAC Safety requirement ambiguity. Use when two plausible interpretations would change privacy, publication, retention, security, data shape, or user-visible behavior.
---

# Clarify HPAC Safety requirements

First read the relevant `/features` pages, source, tests, and issue. Proceed with a
documented local assumption when the choice is reversible and does not alter
product behavior.

Ask one concise question only when the answer cannot be discovered and choosing
would materially change privacy, publication, retention, authorization, stored
data, compatibility, or externally visible behavior. State:

- the evidence already available;
- the two concrete interpretations;
- the impact of each; and
- the smallest decision needed to continue.

Do not turn routine implementation details into product questions. If the user
changes the design, update the affected canonical specification pages and issue
acceptance criteria in the same change.

An answered question is written back into the specification in the same change,
as a scenario or as an out-of-scope line, so the next run starts from the answer
rather than from the question
([ADR-0083](../../docs/decisions/ADR-0083-specification-driven-development.md)).
