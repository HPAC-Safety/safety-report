# ADR-0004 — Mandatory human review before publication

**Status:** Accepted

## Context

An anonymization failure on a real accident is not a bug report — it is harm to
a person who filed in good faith under a non-punitive policy, and it is not
retractable once published.

## Decision

No code path leads from submission to publication without a safety officer's
approval. The officer approves the **English and French pair**; approving one
does not implicitly approve the other.

Publication is additionally gated on the reporter's consent answer. A report
without consent is stored, summarized, and counted internally — never published.

## Alternatives

- **Auto-publish with flagging.** Faster; makes the PII audit the last line of
  defence, which it is not good enough to be.
- **Fully automated.** Not defensible for accident reporting.

## Consequences

- Publication latency depends on a volunteer's availability. Acceptable: these
  are incident reports, not news.
- The admin UI must show both language versions side by side and make clear
  which is the source and which the translation.
- `SummaryFailed` must still reach the queue, so a model outage delays a report
  rather than hiding it.
