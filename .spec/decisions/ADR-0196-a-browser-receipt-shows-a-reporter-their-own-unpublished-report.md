---
title: A browser receipt shows a reporter their own unpublished report
description: A submission returns a random receipt token the browser keeps and the report stores only as a SHA-256 hash, so the reporter's browser, and no other visitor, sees its own report on the public list before it is published. The receipt proves "this browser filed it", never "this member filed it"; no member identity is stored.
type: adr
status: accepted
date: 2026-10-03
decision-makers: Chase Florell
keywords: receipt, browser receipt, anonymity, reporter, privacy, hash, public feed, own report, REQ-PUB-001, REQ-SUB-133, ADR-0067, ADR-0117, ADR-0119, #820
---

# ADR-0196 — A browser receipt shows a reporter their own unpublished report

**Status:** Accepted. Decided by the owner on 2026-10-03 in
[#820](https://github.com/HPAC-Safety/safety-report/issues/820). Supersedes
[ADR-0067](ADR-0067-a-reporter-must-be-a-member-and-is-not-recorded.md).

## Context

A reporter has no way to confirm that their report reached the system. After
submitting, the form clears and nothing on the public list shows it until a
reviewer publishes it, which may be days later or, for a report without
publication consent, never.

[ADR-0067](ADR-0067-a-reporter-must-be-a-member-and-is-not-recorded.md) forbids
the obvious fix. Storing who filed a report, or a hash of who filed it, would
let the report be shown to its author, and it would also let anyone with
database access attribute every report. ADR-0067 rejected the hash of the
subject specifically: a hash over a small, known population, the HPAC
membership, is reversible by trying every member.

That objection is about the input to the hash, not about hashing. A hash of a
random value that no member's identity can be derived from has no population to
enumerate.

## Decision drivers

- No member identity is ever recorded, and nothing stored can be used to work
  out who filed a report (ADR-0067's guarantee).
- The reporter's own browser can see its report, and no other visitor, signed
  in or not, can.
- What the reporter sees must not widen what is public: attachments, consent,
  and hiding follow the public rules.

## Considered options

- **Store the member subject, or a hash of it, on the report.** Rejected: the
  subject hash is reversible by enumeration, as ADR-0067 argued, and the subject
  is exactly the record that decision exists to avoid.
- **A random receipt returned at submission, kept by the browser, stored on the
  report only as its hash** — chosen.
- **A random receipt stored in the clear.** Rejected: a database reader could
  then open any report as its author. A hash lets the server recognize a
  receipt it is shown without being able to produce one.
- **A signed cookie or session.** Rejected: it would be sent with every
  request, to every endpoint, by design, and a cookie is not something the
  browser can be asked to drop report by report.
- **Put the receipt in the report's address.** Rejected: an address lands in
  history, logs, referrer headers, and shared links. The receipt travels only in
  a request body.
- **Show a report again once a reviewer unpublishes it.** Rejected by the owner:
  a report that was ever published never returns to its holder.
- **Read the durable fact from `published_at` or the audit log.** Rejected:
  `published_at` is cleared on unpublish, and the audit log is not written by
  every path that publishes, nor by seeded rows. A single column the domain sets
  is reliable and cheap to read from a view.
- **Do nothing.** Rejected: the reporter's only confirmation is a transient
  message.

## Decision

- **A submission's `202` body carries a receipt**: at least 256 random bits from
  a cryptographically secure generator, base64url. A new one per submission;
  nothing about the member or the report goes into it.
- **The report stores only the receipt's SHA-256 hash**, in a nullable
  `receipt_hash` column with a unique index. A report filed before this
  decision has none. The column is locked after submission with the reporter's
  other columns
  ([ADR-0178](ADR-0178-the-database-refuses-changes-to-the-reporters-account-and-to-summary-revisions.md)).
  The plain receipt is never stored, logged, or put in an audit entry, and the
  member's token subject is never stored or logged, as before.
- **A hash of a random token is not reversible by enumeration.** Unlike the
  subject hash ADR-0067 rejected, its input is 256 random bits, not a member out
  of a small list; guessing it is not feasible, and no member's identity is an
  input.
- **It proves "this browser filed it", not "this member filed it".** Whoever
  holds the receipt can see the report; nothing links it to an account, and the
  same member filing twice gets two unrelated receipts.
- **The browser keeps `{ reportId, receipt }` for each report it filed**, in
  its own local storage, and sends them to the API in a request body, never in a
  URL or query string.
- **Only a browser that holds the receipt sees the report, and sees what the
  public will see**: the same list entry and page, plus a pill saying it is not
  yet published or not for publication, and the latest summary revision, approved
  or not, labelled a draft. Attachments follow the public rules exactly, through
  the same view. A report a reviewer deleted disappears; a published one is
  public to everyone and the browser drops its receipt. A report that was ever
  published, even one a reviewer has since unpublished, never returns to its
  holder's own reports.
- **`reports.first_published_at`** is that durable fact: set once, on the first
  publication, never cleared, and locked by the ADR-0178 trigger (null to a value,
  then fixed). The migration backfills it for existing reports from the earliest
  of `published_at`, a `published_report` audit entry, and an approved summary
  revision, and for a Published report with none of them from its submission
  time. `own_reports` excludes every report where it is set.
- **Limitation, accepted:** the report is visible only in the browser that
  filed it. Clearing site data loses the receipt, and another device or browser
  never sees the report.
- **What still holds from ADR-0067**, unchanged and not restated here: a
  signed-in member of any role may submit; the stored report records nothing
  about the member (REQ-SUB-020, REQ-SUB-021); the form tells the reporter so
  (REQ-SUB-023, REQ-SUB-024). "My reports" as an account page, amending, and
  withdrawing remain impossible by construction (see the report-submission and public-feed area READMEs).

## Consequences

- A reporter sees their report on **View safety reports** right after
  submitting, until it is published.
- The `reports` table gains a column that identifies a browser, not a member.
  Anyone who steals a browser's local storage can read that browser's unpublished
  summaries; that is the same reach as reading the form's saved draft.
- A database reader can no more attribute a report to a member than before, and
  cannot open a report by receipt either.
- Reports filed before this decision are never shown to their reporter.
- While a search is active the holder's own reports are hidden, and a failed
  receipt lookup shows none and says nothing; the owner accepted both.
- Adding an account-linked "my reports" page stays out of scope and would need
  its own decision that supersedes this one.

## Related

- [ADR-0067](ADR-0067-a-reporter-must-be-a-member-and-is-not-recorded.md)
- [ADR-0117](ADR-0117-a-published-report-shows-the-reporters-photos-and-video.md)
- [ADR-0119](ADR-0119-a-published-report-offers-its-documents-for-download.md)
- [ADR-0153](ADR-0153-the-public-feed-sorts-by-submission-time.md)
- [ADR-0177](ADR-0177-summaries-are-append-only-revisions-and-a-live-edit-publishes-itself.md)
- [ADR-0178](ADR-0178-the-database-refuses-changes-to-the-reporters-account-and-to-summary-revisions.md)
