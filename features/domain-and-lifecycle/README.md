---
title: Domain and lifecycle
description: Supporting detail for the report states, invariants, deletion, and retention scenarios.
type: spec
area: domain-and-lifecycle
---

# Domain and lifecycle

Supporting detail for
[`domain-and-lifecycle.feature`](domain-and-lifecycle.feature) that doesn't
fit Gherkin.

## Lifecycle diagram

```mermaid
stateDiagram-v2
    [*] --> Submitted
    Submitted --> Summarizing: Worker claims summary job, consent yes
    Submitted --> Unpublished: Worker claims summary job, consent not yes
    Summarizing --> Pending: valid bilingual pair saved
    Summarizing --> SummaryFailed: bounded retries exhausted
    SummaryFailed --> Pending: officer writes both texts
    Pending --> Pending: either text edited; approval cleared
    Pending --> Published: officer publishes the pair
    Pending --> Unpublished: officer unpublishes (optional note)
    Published --> Pending: either text edited
    Published --> Unpublished: officer unpublishes
    Unpublished --> Published: officer publishes, consent yes
    Unpublished --> Pending: either text edited, consent yes
```

A report is Pending, Published, or Unpublished once the Worker is done with
it; there is no Approved or Rejected status
([ADR-0125](../../docs/decisions/ADR-0125-a-report-is-pending-published-or-unpublished.md)).
Review exists only to check a summary. Publishing approves the current pair
and makes the report public at once, and unpublishing takes it off the public
feed without deleting it; either can be done again later.

A report whose reporter did not consent is never sent to the model: the Worker
sets it Unpublished with no summary, and it stays that way for good. Nobody
can publish it, edit a summary for it, or move it to any other state; the one
thing an officer can do is delete it (REQ-DOM-006, REQ-DOM-015). An action from
a state the diagram does not allow is refused and changes nothing
(REQ-DOM-014).

Soft deletion may occur from any state and is a terminal application state
even though retained rows still contain their prior status.

## Aggregate boundaries

The report aggregate owns its answers, files, bilingual summary pair, and
report-related outbox work for invariants and deletion. Question revisions are
a separate aggregate. There is no user aggregate — identity lives in the
token, never in the database
([ADR-0065](../../docs/decisions/ADR-0065-no-user-records-identity-is-the-token-subject.md)).
Audit-log entries are append-only records. Storage objects are referenced by opaque keys but are not database
entities.

## Soft deletion mechanics

Every persisted entity/table except `audit_log` has a nullable PostgreSQL
`deleted timestamptz` column mapped from the C# property `Deleted`.
Application queries use global filters by default. Administrative
investigations that intentionally include deleted rows use an explicit
unfiltered query and remain authorized and audited. `audit_log` has no
`Deleted` column and no delete operation.

## Identity and time

External IDs use the repository's opaque TinyId value rather than sequential
database identifiers. Persisted instants use `DateTimeOffset`/`timestamptz`.
Question answers that represent a date use `DateOnly`; local wall-clock
answers use `TimeOnly`; unspecified `DateTime` is prohibited. Enum values
persist as stable lowercase codes and are localized only at UI/API-message
edges.

## Out of scope

What not to build here. The global list in
[system overview](../../docs/system-overview.md) still holds; this narrows it
to this area ([ADR-0083](../../docs/decisions/ADR-0083-specification-driven-development.md)).

- Undelete, restore, or any path back from a soft deletion.
- Physical deletion of an application record, or a cascade that removes rows
  rather than stamping them.
- An automated purge or retention job over raw reports. Retention ends at an
  explicit deletion.
- Lifecycle states beyond the ones the scenarios name, or a workflow engine to
  move between them.
- Ownership of a report by a member. A report belongs to no one
  ([ADR-0067](../../docs/decisions/ADR-0067-a-reporter-must-be-a-member-and-is-not-recorded.md)).
- A way to publish, summarize, or acknowledge a report whose reporter did not
  consent, or to change that consent after submission.
- An Approved, Rejected, or Reopened status; Publish and Unpublish cover them
  ([ADR-0125](../../docs/decisions/ADR-0125-a-report-is-pending-published-or-unpublished.md)).
