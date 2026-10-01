---
title: A report is pending, published, or unpublished
description: Review exists only to check a summary. A reviewed report is Pending, Published, or Unpublished; Approved, Rejected, and Reopen are gone, and a report without publication consent is set Unpublished by the Worker and can only be deleted.
type: adr
status: accepted
date: 2026-09-25
decision-makers: Chase Florell
keywords: lifecycle, review, publish, unpublish, consent, approved, rejected, reopen, ADR-0105, REQ-DOM-006
---

# ADR-0125 — A report is pending, published, or unpublished

**Status:** Accepted. Partially supersedes
[ADR-0105](ADR-0105-approving-a-consented-pair-publishes-it.md): its Approved
status, its Reject and Reopen actions, and its `ApprovedReport` /
`RejectedReport` / `ReopenedReport` audit entries. ADR-0105's `manual`
provenance and `xmin` concurrency stand.

## Context

Since REQ-DOM-006 a report without publication consent is never sent to the
model, so it never has a summary. The Worker still moved it to Pending review,
where the only way out was Reject. It counted in Needs action and on the Admin
menu (ADR-0116) until someone rejected it, and "rejected" said something about
the report that nobody meant. The Approved status that ADR-0105 kept for a
no-consent report became unreachable: approval needs a pair.

For a consented report, ADR-0105 left four verbs — Approve, Reject, Reopen,
Unpublish — over what a reviewer is really deciding: whether this report is
public.

## Decision

**Review exists only to check a summary.** Once the Worker is done with a
report, its status is **Pending**, **Published**, or **Unpublished**. Submitted,
Summarizing, and Summary failed stay as the states before review. Approved and
Rejected are removed, and Pending review is renamed Pending.

**A report without consent is Unpublished for good.** When the Worker picks up
the summary job and consent is not exactly yes, it sets the report Unpublished
without a model call. Nothing can publish it, edit or write a summary for it,
or move it to another status; it never needs action and is never counted.
Soft deletion, which is not a status, is the one thing an officer can do to it.

**Publish and Unpublish replace Approve, Reject, and Reopen.**

- *Publish* approves the current pair and makes the report public in the same
  action, from Pending or from a consented Unpublished report. Every
  publication guard still runs. It writes one `PublishedReport` audit entry.
- *Unpublish* moves a Pending or Published report to Unpublished, clears the
  pair's approval, and takes an optional note only reviewers see (the
  rejection note it replaces). It writes one `UnpublishedReport` entry.
- *Edit summary* still clears the approval and returns the report to Pending.

**The Worker's move is not audited.** An audit entry names an actor, and the
only actor is the reporter, whom the system never records (ADR-0067). The
Worker's other status changes are not audited either.

## Rejected alternatives

- **A Private status set by the API at submission.** Removes a Worker hop, but
  adds a fourth reviewed status alongside Unpublished that means the same
  thing to a reader: not public.
- **A "Mark reviewed" action** for a no-consent report (the #437 audit). An
  acknowledgement step with nothing to review is busywork, and it kept the
  Approved status alive for one case.
- **Keeping Approved and Rejected beside Published.** Two status names for
  "not public" and one for "public" made the admin filters and badges harder
  to read than the decision they record.

## Consequences

- REQ-DOM-001 lists the new transitions, REQ-DOM-006 ends in Unpublished,
  REQ-DOM-015 proves a no-consent report is immutable, and REQ-MOD-090 keeps it
  out of Needs action and the counts.
- The review endpoints are `publish`, `unpublish`, `summary`, and `DELETE`;
  `approve`, `reject`, and `reopen` are gone.
- The admin Rejected filter becomes Unpublished. The Private (no consent)
  badge and filter stay: consent and status are separate facts.
- Existing `ApprovedReport`, `RejectedReport`, and `ReopenedReport` audit rows
  keep their codes; nothing writes them again.
- No production data existed, so the migration only maps old status codes so
  development databases satisfy the new check constraint.
