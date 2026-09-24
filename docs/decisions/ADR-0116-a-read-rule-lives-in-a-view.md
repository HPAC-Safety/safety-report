---
title: A read rule lives in a view
description: When a read query carries a rule — a filter, a derived flag, a count — the rule is written once as a PostgreSQL view, and C# reads the view instead of restating the rule or filtering in memory.
type: adr
status: accepted
date: 2026-09-24
decision-makers: Chase Florell
keywords: views, read models, admin report queue, needs action, stuck, translation queue, pending counts, ADR-0055
---

# ADR-0116 — A read rule lives in a view

**Status:** Accepted. Builds on
[ADR-0055](ADR-0055-ef-core-migrations-sql-files-stored-procedures.md), which
says where view SQL lives. This record says when a query should be a view.

## Context

The Admin menu needed to count the reports that need action and the answers
awaiting translation (#418). Both rules already existed, and each was written
in C#.

"Needs action" lived in the report list endpoint. It was a dictionary of
in-memory predicates. The endpoint loaded every live report, projected each
row, computed "stuck" against `TimeProvider`, and then filtered in memory.
The report detail view computed "stuck" a second time.

"Awaiting translation" was an inline LINQ predicate in the queue endpoint. It
repeated the filter of the partial index that serves it.

Counting either one would have meant a third copy of the rule, or loading the
whole list just to take its length.

`public_reports` (#28) had already put the publication invariant in a view.
The owner then asked for SQL views wherever they improve performance and
simplify the C#.

## Decision

When a read query applies a rule, the rule is written once, as a view. The
C# reads the view. It does not restate the rule and does not filter rows in
memory.

- The view SQL lives in `Persistence/Sql/` and a migration creates it, per
  ADR-0055.
- EF maps each view to a read-only type under `Persistence/Views/` using
  `ToView`.
- A list's filters become `Where` clauses over the view's columns, so they
  run in SQL.
- A count is a view too, or a `COUNT` over one.
- A query that is just a projection, with no rule in it, stays LINQ.

This record adds three views:

| View | What it carries |
|---|---|
| `admin_report_queue` | Every live report, with `is_stuck` and `needs_action` |
| `answers_awaiting_translation` | Every live answer waiting for its machine-translated second language |
| `admin_pending_counts` | One row counting both |

## Consequences

- The list, its filters, the report detail's stuck flag, the translation
  queue, and the Admin menu's counts cannot disagree, because each of them
  reads the same view.
- The Needs action filter now runs in SQL, where it used to load every
  report. A count is a single row.
- "Stuck" is measured against the database's `now()`, not the API's
  `TimeProvider`. No test fakes the clock for this, and a report 24 hours old
  is stuck on either clock. A test that needs a fixed clock for this rule
  seeds `submitted_at` relative to real time, as the existing ones already do.
- Changing a rule means a migration that replaces the view. That is heavier
  than editing a lambda, and it is deliberate: a rule the list, the detail,
  and a badge all depend on should change in one reviewed place.

## Rejected alternatives

- **One shared C# expression per rule.** Rejected because the "stuck" rule
  compares against the current time, which EF translates unevenly, and the
  list still had to materialize rows before it could filter them. A view
  holds the rule where the rows are.
- **A count query that reuses the list endpoint's result.** Rejected because
  it loads every report on every navigation just to draw a badge.
