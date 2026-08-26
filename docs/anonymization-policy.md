# Anonymization policy

The normative contract is
[`features/ai-anonymization/ai-anonymization.feature`](../features/ai-anonymization/ai-anonymization.feature). HPAC publishes safety
lessons, not identities.

The Worker makes one model call per attempt with one versioned prompt. Answered
non-private questions form `report_content`, the only eligible facts. Answered
private questions form labeled `private_context`, which may only help recognize
identifying material repeated in eligible content. Consent, skipped answers,
attachments, and document text are excluded.

The call returns exactly one English/French summary pair. It must remove names,
contact/account details, precise identifying locations, aircraft make/model,
and private-only facts while preserving supported safety lessons. A private
pilot identity repeated in narrative becomes exactly “the pilot” / “le pilote,”
with no identity fragment remaining.

There is no deterministic text scrubber, independent PII-audit call, runtime
translation call, specialized aircraft processing, or repair call. Invalid
output retries the same one-call operation within a bounded budget and then
moves to manual bilingual authoring.

Documents are validated private evidence. They are not transformed,
anonymized, parsed, sent to AI, inline-rendered, or published.

A safety officer reviews and approves the current pair. Editing either language
clears approval. Positive publication consent and a live report remain required
for public visibility. Model inputs and outputs are never logged.
