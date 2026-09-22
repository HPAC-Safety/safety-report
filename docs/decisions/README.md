---
title: Architecture decision records
description: What an ADR is for in this repository, and how it relates to the specification.
type: guide
---

# Architecture decision records

ADRs preserve the reasoning and implementation context that existed when a
decision was made. They are historical records, not the current product-design
authority. [`/features`](../../features/README.md) wins whenever an ADR conflicts with
the target.

Contradictory ADRs carry an explicit superseded or narrowed status. Dedicated
records for the retired aircraft-processing concept were removed from the
active tree; Git history preserves them if their history is ever needed.

Every architectural decision — a technology choice, a rejected alternative, a
durable trade-off between designs — gets an ADR. This is not discretionary. A
routine implementation detail with no rejected alternative, or a restatement of
`/features`, does not need one; if in doubt, write the ADR.
