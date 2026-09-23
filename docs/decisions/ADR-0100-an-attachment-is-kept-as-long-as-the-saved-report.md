---
title: An attachment is kept as long as the saved report
description: The browser's saved report holds each finished upload's ID and name, so continuing it restores the files; the draft and its uploads share one fixed 15-day window, and abandoning the report erases its uploads.
type: adr
status: accepted
date: 2026-09-23
decision-makers: Chase Florell
keywords: attachments, uploads, quarantine, lifecycle, draft, local storage, privacy, retention
---

# ADR-0100 — An attachment is kept as long as the saved report

## Status

Accepted. This ADR:

- **amends** [ADR-0096](ADR-0096-an-attachment-uploads-on-attach-and-is-claimed-at-submission.md):
  it reverses that record's rejected alternative "Restore uploads after a
  reload", and its rule that the browser draft never holds an upload ID; the
  quarantine lifecycle rule moves from one day to fifteen;
- **amends** AGENTS.md invariant 2 and the guardrail list in
  [`features/README.md`](../../features/README.md) to match.

## Context

A reporter's answers survive leaving the page for fifteen days; their files
did not. Every attachment was uploaded the moment it was attached (ADR-0096),
yet a reload threw its upload ID away and told the reporter to attach it
again. The file sat in quarantine for another day regardless, unreachable by
anyone, including the person who uploaded it.

The owner wants a file kept exactly as long as the partial report it belongs
to: restored with the report, expiring with it, erased when the report is
abandoned, and made durable when it is submitted.

## Decision

**The browser's saved report holds, beside the answers, each finished
upload's opaque ID, the file's name, its size, and when it was uploaded.
Continuing the saved report lists those files again. The saved report and its
uploads share one window: fifteen days from the moment the report was first
saved, however often it is edited afterward. Abandoning the report erases its
uploads.**

- **The draft's window is fixed.** A draft records `startedAtMs` on its first
  save and keeps it on every later save; it expires fifteen days after that.
  A draft that slid on each edit could outlive its files by weeks, because an
  S3 lifecycle rule counts from an object's creation and cannot be renewed. A
  draft written before this change, with no start time, is dated from its last
  save.
- **Quarantine keeps an upload for fifteen days.** The `expire-quarantine`
  lifecycle rule expires a quarantine key fifteen days after it was written.
  S3 rounds expiry up to the next midnight UTC, so a key never stops resolving
  before the draft that names it expires. Noncurrent versions and delete
  markers still go a day later, so a removed file is still gone at once and an
  expired one within a day.
- **The browser does not second-guess the bucket.** Every upload in a draft
  was made after the draft started, so no restored file can be older than the
  window; the browser keeps no upload time and makes no existence check. If a
  restored upload has gone anyway, the submission's refusal naming missing
  uploads marks just that file for attaching again, as it always has
  (REQ-SUB-041, REQ-SUB-051).
- **Abandoning the report erases its uploads.** Each of these asks the API to
  delete every saved upload, best effort, before the draft is cleared:
  choosing "No, start over" in the continue dialog; confirming a new Discard
  report control on the form; and the form finding, on load, a saved report
  past its window. The lifecycle rule is the backstop for a browser that never
  comes back.
- **Submitting is what makes a file durable.** Unchanged: the submission
  copies each claimed upload into the report's own compartments and removes it
  from quarantine
  ([ADR-0098](ADR-0098-submission-copies-the-original-and-the-worker-makes-the-derivative.md)).
- **The server learns nothing new.** No endpoint lists, reads, previews, or
  renews an upload; the draft is the only place its ID and name are kept, and
  it never leaves the browser. An upload still has no database row and names
  no member
  ([ADR-0067](ADR-0067-a-reporter-must-be-a-member-and-is-not-recorded.md)).

## Consequences

- A file nobody submits can now sit in private quarantine for about sixteen
  days instead of about two. It is still unreadable to any reviewer, carries no
  filename on the server, and belongs to nobody the system can name.
- The browser's local storage now holds a file's name and an upload ID
  capable of deleting or claiming that upload. It holds the reporter's own
  answers already, on the same device, under the same fifteen-day rule.
- A member can hold quarantine space fifteen times longer. The per-IP upload
  rate limit and the per-report count limit still bound it.
- A draft now expires fifteen days after it was started even when it was
  edited yesterday. A reporter who takes longer starts again, with their
  answers and files alike.

## Alternatives rejected

- **Keep uploads unrestored (ADR-0096).** It keeps the server window at a day,
  but it asks the reporter to find and attach every file again after any
  reload, which is the experience the owner is replacing.
- **Keep the sliding draft and mark old files expired.** Answers would outlive
  their files, and a returning reporter would meet a half-restored report.
- **Renew an upload's expiry when the draft is saved.** It needs a server call
  that ties an upload to continued activity, and a lifecycle rule cannot be
  renewed per object without rewriting the object.
- **Record uploads in a table, or list them from the server.** It would give an
  unfinished report a database footprint, the thing ADR-0096 refused.
- **A preview endpoint for a restored file.** It would be the first read path
  for unsubmitted content; the restored row shows its name and size only.

## Related

- [ADR-0096](ADR-0096-an-attachment-uploads-on-attach-and-is-claimed-at-submission.md) — uploads on attach, amended here.
- [ADR-0098](ADR-0098-submission-copies-the-original-and-the-worker-makes-the-derivative.md) — what claiming an upload does.
- [ADR-0099](ADR-0099-a-report-page-is-addressed-by-its-question-key.md) — the saved page in the same draft.
- Issue #373.
