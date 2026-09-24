---
title: Approving a consented pair publishes it
description: A safety officer's approval of the bilingual pair publishes the report in the same action when the reporter consented; review adds reopen, unpublish, an optional rejection note, manual pairs, and optimistic concurrency on PostgreSQL xmin.
type: adr
status: accepted
date: 2026-09-23
decision-makers: Chase Florell
keywords: review, approval, publication, consent, reopen, unpublish, rejection note, concurrency, xmin, ADR-0004
---

# ADR-0105 — Approving a consented pair publishes it

**Status:** Accepted. Keeps [ADR-0004](ADR-0004-human-review-required.md):
nothing reaches the public without a human approving the current pair.

## Context

The lifecycle specification had approval and publication as two separate
officer steps: PendingReview → Approved, then Approved → Published when consent
is yes. With one reviewer role and dozens of reports a year, the second click
adds no judgement the first one did not already make: the officer approving the
pair has just read it, and the consent answer is shown beside it. The review
screen also needed answers to questions the specification left open — whether a
rejection can be undone, how a published report comes down without an edit,
whether a rejection says why, what provenance a hand-written pair carries, and
how two officers are kept from overwriting each other (CON-IF-006).

## Decision

**Approve publishes when consent is yes.** Approving a PendingReview report
records the approver subject and time on the pair and, in the same transaction,
publishes the report when the reporter answered yes. With a no or an unanswered
consent the report becomes Approved and is never public. Every publication
guard still runs: not deleted, consent exactly yes, two nonblank texts, a
current approval.

**Rejected reopens.** A Reopen action returns a Rejected report to
PendingReview, audited.

**Unpublish is its own action.** A published report can be taken down without
inventing an edit: approval is cleared and the report returns to PendingReview.

**A rejection may carry a note.** Optional free text stored on the report,
shown only in the admin report view; never in the public DTO, the audit log, or
application logs. Reopening clears it.

**A hand-written pair is provenance `manual`.** When summarization failed, the
officer writes both texts; the summary row records `manual` as its model and
prompt version, so a reader of the row can tell it was never generated.

**Optimistic concurrency on PostgreSQL `xmin`.** The report and its summary use
the system column `xmin` as their concurrency token, so no migration adds a
version column. The detail view returns a version built from both; every command
sends it back, and a mismatch answers `409` with a problem asking the reviewer
to reload. Nothing from a stale command is saved.

**New audit actions.** `ReopenedReport` and `UnpublishedReport` join the
existing `EditedSummary`, `ApprovedReport`, `RejectedReport`, and
`DeletedReport`. An approval that publishes writes one `ApprovedReport` entry;
the publication is part of that action.

## Rejected alternatives

- **Separate Approve and Publish buttons.** Matches the original table, but a
  second confirmation adds no review and a report left Approved-but-unpublished
  by mistake is invisible to members.
- **Rejection is final.** Simpler, but a mistaken rejection would need a
  database edit.
- **Unpublish only by editing.** Forces a reviewer to change the text to take a
  report down for a reason that has nothing to do with the text.
- **A version column.** `xmin` already changes on every update; an extra column
  and migration buys nothing.

## Consequences

- REQ-DOM-001's transition table has PendingReview → Published and
  PendingReview → Approved by consent, Published → PendingReview on unpublish,
  and Rejected → PendingReview on reopen; Approved → Published is gone.
- REQ-MOD-035's publication guards run inside the approval.
- The public feed (#28) reads Published reports; nothing else publishes.
