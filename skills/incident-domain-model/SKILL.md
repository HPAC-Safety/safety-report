---
name: incident-domain-model
description: Model HPAC Safety questions, reports, summaries, moderation, deletion, and publication. Use for Core domain or lifecycle changes.
---

# HPAC Safety domain model

- A question revision is a complete immutable record: stable key/revision,
  English/French labels and help, type/options, order/section, privacy, active,
  system/required flags, timestamps, and predecessor link. Every edit inserts a
  new revision.
- The current form examines only the latest live revision per key. An inactive
  latest revision does not reveal an older one.
- `consent_publish` is the only system/required question, is yes/no, and has no
  default. Ordinary answers remain revision-bound data; do not create typed
  projections or specialized aircraft domain behavior.
- Submission records every shown answer-producing revision, including skips,
  and enqueues summary and attachment work atomically.
- One summary row holds `AiSummaryEn` and `AiSummaryFr`, shared provenance, and
  one approval. Editing either text clears approval.
- Publication requires positive consent, current approval, and live report,
  summary, and question data. Public DTOs contain only ID, both texts, and
  publication time.
- Every application record except `audit_log` supports irreversible soft
  deletion. Deleting a report cascade-stamps dependents with one timestamp;
  answered question revisions can never be deleted, even through deleted
  reports.

Keep behavior in the smallest aggregate/use case that enforces it. Do not add
restore, physical deletion, outbound publication channels, or hypothetical
domain abstractions.
