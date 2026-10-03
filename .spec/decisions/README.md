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
  [ADR-0192](ADR-0192-an-accepted-adr-is-immutable-and-process-rules-are-conventions.md)
  stay here.

## An accepted ADR is immutable

[ADR-0192](ADR-0192-an-accepted-adr-is-immutable-and-process-rules-are-conventions.md):

- Once accepted, only its frontmatter `status:` and its `**Status:**` line ever
  change, and a link's target when the file it points to moves or is deleted
  (point it at a commit permalink).
- A change to a decision is a new ADR. The record it changes becomes
  `superseded`, its status line linking the new one; the new one lists what of
  the old still holds, by link or claim ID, never restating it.
- No new amendment sections. The ones written before ADR-0192 stay as history,
  headed `## Amendment (YYYY-MM-DD)`. Inline amendment notes in a record's
  opening paragraphs (`**Amended …:**`, `**Provider:**`, and the like) stay as
  written.
- An ADR is never deleted.

`node tools/spec/check-adr-immutability.ts` fails a pull request that breaks any
of these, in the `docs` job and the pre-commit hook.

## Lifecycle

| Status | Meaning |
|---|---|
| `proposed` | Written, not yet decided. May change freely. |
| `accepted` | Decided. Immutable from here. |
| `rejected` | Decided against; kept for the reasoning. |
| `deprecated` | No longer applies; its status line links the ADR or convention that retired it. |
| `superseded` | Replaced; its status line links the successor. |

An `accepted` record moves only to `superseded` or `deprecated`; `superseded`,
`deprecated`, and `rejected` are terminal.

`partially-superseded` is retired. The records that already carry it keep it;
no record takes it again.

## Template

Copy [`TEMPLATE.md`](TEMPLATE.md): one `**Status:**` line under the heading,
then `## Context`, `## Decision drivers` (optional), `## Considered options`
(required), `## Decision`, `## Consequences`, and `## Related` (optional), in
that order. Take the number with `node tools/spec/adr-numbers.ts --next` after
rebasing.

The records before ADR-0192 keep their own sections, normalized once without
changing what any says: one status line, and a `## Considered options`
heading — over the options they weighed, saying they sit inline, or saying
none were recorded.

## Checks

All three run in the pre-commit hook and the `docs` job.

- `node tools/spec/adr-numbers.ts`
  ([ADR-0091](ADR-0091-an-adr-number-is-verified-not-assumed.md),
  [ADR-0183](ADR-0183-the-specification-lives-in-a-spec-directory.md)) fails:
  - a duplicate number, or a filename and heading that disagree;
  - a `status:` outside the five and the retired `partially-superseded`;
  - a status line saying "superseded by ADR-NNNN" on a record not
    `superseded`, or naming an ADR that does not exist;
  - a `superseded` or `deprecated` record whose status line links no ADR or
    convention.
- `node tools/spec/check-records.ts` fails:
  - a status that is not one `**Status:**` paragraph directly under the
    heading, or a `## Status` section;
  - a record without exactly one `## Considered options`;
  - an amendment heading other than `## Amendment (YYYY-MM-DD)`, optionally
    ` — subject`;
  - from ADR-0192 on: a section the template lacks, a missing required one,
    sections out of order, or a status line not opening with its status
    (`Accepted`, `Superseded`, …);
  - a missing `TEMPLATE.md`.
- `node tools/spec/check-adr-immutability.ts` fails a pull request whose diff
  to an ADR on its base changes anything but its status, a status sentence
  ("Superseded by …", "Deprecated …"), or the target of a link whose file is
  gone; deletes one; or moves a status in a way the lifecycle forbids. A
  `proposed` record is exempt. CI passes the pull request's or merge group's
  base as `BASE_SHA`, and a base that is not a commit fails. Pre-commit runs it
  with `--staged` against the merge base with `origin/main`.

## Missing numbers

Seven numbers have no record. Each went missing before ADR-0192 made deletion
impossible; none will be reused.

| Number | Why |
|---|---|
| 0029, 0030, 0036 | Written for aircraft classification (#54, 2026-08-22) and deleted with every other aircraft-classifier artifact when that concept was retired (#83, 2026-08-23). Git history preserves them. |
| 0160, 0161, 0162 | Claimed by branches whose records merged under other numbers. No record with these numbers exists in any fetched history; 0161 was cited only in workflow and Terraform comments by #586 and #588. |
| 0175 | Drafted for #667 in pull request #683, which closed unmerged when the issue was closed as not planned. |

## Index

Every decision, newest first, with its status and date, is listed in the
generated [specification index](../README.md#decisions).
