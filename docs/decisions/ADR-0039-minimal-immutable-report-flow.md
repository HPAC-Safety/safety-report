# ADR-0039: One immutable report flow and one model call

Status: Accepted — 2026-08-22

## Context

The scaffold accumulated separate version/translation tables, typed projections
of ordinary answers, deterministic media and anonymization stages, two model
audits, translation services, classification, application email, CAPTCHA, and
generic publication channels. Those pieces exceeded the required incident flow
and made the privacy boundary harder to inspect.

## Decision

- One `questions` row is one complete immutable bilingual revision. Any change,
  including order, privacy, options, wording, or active state, inserts a row.
- Reports store answers against the exact question revision shown. Only
  publication consent is projected and required.
- Submission and Worker query DTOs preserve questions, answers, and privacy.
- The Worker partitions those values and uses one versioned prompt in one model
  call to anonymize and summarize. Private answers are recognition context only;
  a matching pilot identity becomes “the pilot.”
- Store one candidate summary for mandatory human review.
- Do not implement a deterministic scrubber, second model audit, summary
  translation, classifier, custom credential proxy, application email,
  CAPTCHA, media-processing pipeline, or publication-channel framework without
  a new approved requirement.

## Consequences

The schema has eight application tables and one initial migration. English and
French question text are reviewed together. Public summaries exist in the
report language. Optional uploaded files remain private objects but receive no
derivative-processing pipeline. The smaller design has fewer places where raw
answers or private context can leak.

This decision supersedes ADR-0003, ADR-0005, ADR-0007, the question structure
in ADR-0016, ADR-0021, ADR-0022, ADR-0025 through ADR-0030, ADR-0036, and the
multi-stage portions of ADR-0038. It retains the transactional outbox,
application-side answer encryption, Canadian hosting, and mandatory human
review.
