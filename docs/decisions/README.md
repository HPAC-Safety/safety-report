# Architecture decision records

ADRs preserve the reasoning and implementation context that existed when a
decision was made. They are historical records, not the current product-design
authority. [`/features`](../../features/README.md) wins whenever an ADR conflicts with
the target.

Contradictory ADRs carry an explicit superseded or narrowed status. Dedicated
records for the retired aircraft-processing concept were removed from the
active tree; Git history preserves them if their history is ever needed.

Add an ADR only when a durable trade-off would otherwise be difficult to recover
from the specification and code. A routine implementation choice or restatement
of `/spec` does not need one.
