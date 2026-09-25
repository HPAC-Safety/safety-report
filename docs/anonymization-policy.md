---
title: Anonymization policy
description: The identity-replacement rules a published summary must satisfy, pointing at the normative contract.
type: guide
---

# Anonymization policy

The normative contract is
[`features/ai-anonymization/ai-anonymization.feature`](../features/ai-anonymization/ai-anonymization.feature). HPAC publishes safety
lessons, not identities.

The Worker makes one model call per attempt with one versioned prompt. Answered
non-private questions form `report_content`, the only eligible facts. Answered
private questions form labeled `private_context`, which may only help recognize
identifying material repeated in eligible content. Consent, skipped answers,
attachments, and document text are excluded.

Before the call, the Worker deterministically marks any exact or token-level
occurrence of a private value found in `report_content` with a
`[PRIVATE:<question-key>]` marker
([ADR-0082](decisions/ADR-0082-a-deterministic-marking-pass-precedes-the-one-model-call.md)).
`private_context` is still sent in full — the marking pass narrows what the
model has to infer, it does not replace it.

The call returns exactly one English/French summary pair. It must resolve
every marker and remove names, contact/account details, precise identifying
locations, aircraft make/model, and private-only facts while preserving
supported safety lessons. A private pilot identity repeated in narrative
becomes exactly “the pilot” / “le pilote,” with no identity fragment or
literal marker remaining.

Every statement in the pair is supported by `report_content`; nothing is
inferred or invented. What identifies someone becomes a generic phrase, never
an invented name and never a word such as “redacted” or “caviardé”: another
person becomes the role the report supports or “a person” / “une personne”; a
place becomes “the launch site”, “the landing field”, or “the location” /
“le lieu”; an exact date becomes its month or season while the time of day is
kept; a club, school, or company becomes “the club”, “the school”, or “the
company”; an aircraft becomes its category. The full table is in the
[AI anonymization supporting detail](../features/ai-anonymization/README.md).

Summarization makes no independent PII-audit call, translation call,
specialized aircraft processing, or repair call, and no general-purpose deterministic
scrubber beyond the narrow private-value marking pass above. Invalid output
retries the same one-call operation within a bounded budget and then moves to
manual bilingual authoring.

Documents are validated evidence. They are not transformed, anonymized,
parsed, sent to AI, or rendered inline. On a published report whose reporter
consented to media under wording that names documents, the unchanged original
is offered as a forced download
([ADR-0119](decisions/ADR-0119-a-published-report-offers-its-documents-for-download.md)).

A safety officer reviews and approves the current pair. Editing either language
clears approval. Positive publication consent and a live report remain required
for public visibility. Model inputs and outputs are never logged.
