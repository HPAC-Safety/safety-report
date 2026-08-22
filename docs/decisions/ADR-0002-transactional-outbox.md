# ADR-0002 — Transactional outbox for AI processing

**Status:** Accepted

## Context

Submitting a report must trigger summarization. Summarization takes seconds,
calls a third-party model, and fails in ways an HTTP request should not care
about.

A lost safety report is not recoverable from anywhere — the pilot has already
closed the tab.

## Decision

The API writes the report and an `outbox_messages` row in **one** transaction. A
separate worker process claims rows with `SELECT ... FOR UPDATE SKIP LOCKED`,
applies exponential backoff, and sets aside poison messages after a threshold.

Everything asynchronous rides the same mechanism: summarization, translation,
and notification email.

## Alternatives

- **In-process `BackgroundService`.** Simplest deploy, but the work dies with
  the API process and cannot scale separately.
- **Postgres `LISTEN/NOTIFY` alone.** Low latency, but a notification delivered
  while the worker is restarting is lost, so a catch-up sweep is needed anyway.
- **Cloud queue.** Best at scale; disproportionate infrastructure for a national
  association receiving dozens of reports a year.

## Consequences

- No report is lost to a process restart or a model outage.
- Summarization can be scaled, restarted, or switched off without affecting the
  ability to *receive* reports — the part that must never be down.
- `LISTEN/NOTIFY` may be layered on later purely for latency. Polling remains the
  source of truth.
- A `SummaryFailed` state is required so a failed report still reaches a human.
