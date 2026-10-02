---
title: Architecture decision records
description: What an ADR is for in this repository, and how it relates to the specification.
type: guide
---

# Architecture decision records

ADRs preserve the reasoning and implementation context that existed when a
decision was made. They are historical records, not the current product-design
authority. [`.spec/features`](../features/README.md) wins whenever an ADR conflicts with
the target.

Contradictory ADRs carry an explicit superseded or narrowed status. Dedicated
records for the retired aircraft-processing concept were removed from the
active tree; Git history preserves them if their history is ever needed.

Every architectural decision — a technology choice, a rejected alternative, a
durable trade-off between designs — gets an ADR. This is not discretionary. A
routine implementation detail with no rejected alternative, or a restatement of
`.spec/features`, does not need one; if in doubt, write the ADR.

Every decision, newest first, with its status and date, is listed in the
generated [specification index](../README.md#decisions). A record's `status:`
must agree with its own `**Status:**` line, and a successor it names must
exist; `node tools/spec/adr-numbers.mjs` checks both, with the numbering, in the
pre-commit hook and in CI
([ADR-0183](ADR-0183-the-specification-lives-in-a-spec-directory.md)).
