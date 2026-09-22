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
([ADR-0081](decisions/ADR-0081-a-deterministic-marking-pass-precedes-the-one-model-call.md)).
`private_context` is still sent in full — the marking pass narrows what the
model has to infer, it does not replace it.

The call returns exactly one English/French summary pair. It must resolve
every marker and remove names, contact/account details, precise identifying
locations, aircraft make/model, and private-only facts while preserving
supported safety lessons. A private pilot identity repeated in narrative
becomes exactly “the pilot” / “le pilote,” with no identity fragment or
literal marker remaining.

There is no independent PII-audit call, runtime translation call, specialized
aircraft processing, or repair call, and no general-purpose deterministic
scrubber beyond the narrow private-value marking pass above. Invalid output
retries the same one-call operation within a bounded budget and then moves to
manual bilingual authoring.

Documents are validated private evidence. They are not transformed,
anonymized, parsed, sent to AI, inline-rendered, or published.

A safety officer reviews and approves the current pair. Editing either language
clears approval. Positive publication consent and a live report remain required
for public visibility. Model inputs and outputs are never logged.
