---
title: Architecture decision records
description: What an ADR is for in this repository, its lifecycle and template, how it relates to the specification, and which numbers are missing and why.
type: guide
---

# Architecture decision records

ADRs preserve the reasoning and implementation context that existed when a
decision was made. They are historical records, not the current product-design
authority. [`.spec/features`](../features/README.md) wins whenever an ADR conflicts with
the target.

## What gets an ADR

Every architectural decision — a technology choice, a boundary, a data shape, a
durable trade-off between designs with a rejected alternative — gets an ADR.
This is not discretionary. If in doubt, write the ADR. These do not:

- a routine implementation detail with no rejected alternative;
- a restatement of `.spec/features`;
- interface detail — wording, layout, a field's behavior — which is a scenario;
- a new process, tooling, or agent-workflow rule, which is a
  [convention](../conventions/README.md). The process ADRs written before
  [ADR-0191](ADR-0191-an-accepted-adr-is-immutable-and-process-rules-are-conventions.md)
  stay here.

## An accepted ADR is immutable

[ADR-0191](ADR-0191-an-accepted-adr-is-immutable-and-process-rules-are-conventions.md):

- Once accepted, only its frontmatter `status:` and its `**Status:**` line ever
  change, and a link's target when the file it points to moves.
- A change to a decision is a new ADR. The record it changes becomes
  `superseded`, its status line linking the new one; the new one states what of
  the old still holds.
- No new amendment sections. The ones written before ADR-0191 stay as history,
  headed `## Amendment (YYYY-MM-DD)`.
- An ADR is never deleted.

`node tools/spec/check-adr-immutability.ts` fails a pull request that breaks any
of these, in the `docs` job and the pre-commit hook.

## Lifecycle

| Status | Meaning |
|---|---|
| `proposed` | Written, not yet decided. May change freely. |
| `accepted` | Decided. Immutable from here. |
| `rejected` | Decided against; kept for the reasoning. |
| `deprecated` | No longer applies, and nothing replaced it. |
| `superseded` | Replaced; its status line links the successor. |

`partially-superseded` is retired. The records that already carry it keep it;
no record takes it again.

## Template

Copy [`TEMPLATE.md`](TEMPLATE.md): one `**Status:**` line under the heading,
then `## Context`, `## Decision drivers` (optional), `## Considered options`
(required), `## Decision`, `## Consequences`, and `## Related` (optional), in
that order. Take the number with `node tools/spec/adr-numbers.ts --next` after
rebasing.

The records before ADR-0191 keep their own sections, normalized once without
changing what any says: one status line, and a `## Considered options`
heading — over the options they weighed, or saying none were recorded.

## Checks

- `node tools/spec/adr-numbers.ts` — numbering, and a `status:` that agrees
  with its status line and names a successor that exists
  ([ADR-0091](ADR-0091-an-adr-number-is-verified-not-assumed.md),
  [ADR-0183](ADR-0183-the-specification-lives-in-a-spec-directory.md)).
- `node tools/spec/check-records.ts` — the template, the status line, and the
  considered options.
- `node tools/spec/check-adr-immutability.ts` — a pull request changes only a
  status.

All three run in the pre-commit hook and the `docs` job.

## Missing numbers

Seven numbers have no record. Each went missing before ADR-0191 made deletion
impossible; none will be reused.

| Number | Why |
|---|---|
| 0029, 0030, 0036 | Written for aircraft classification (#54, 2026-08-22) and deleted with every other aircraft-classifier artifact when that concept was retired (#83, 2026-08-23). Git history preserves them. |
| 0160, 0161, 0162 | Claimed by branches whose records merged under other numbers. No record with these numbers exists in any fetched history; 0161 was cited only in workflow and Terraform comments by #586 and #588. |
| 0175 | Drafted for #667 in pull request #683, which closed unmerged when the issue was closed as not planned. |

## Index

Every decision, newest first, with its status and date, is listed in the
generated [specification index](../README.md#decisions).
