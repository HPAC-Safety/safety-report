# ADR-0038 — Question privacy partitions an LLM-only anonymization request

**Status:** Accepted
**Date:** 2026-08-22

## Context

The original pipeline attempted to remove identifiers with deterministic regex
and token stages before calling the summarizer. That duplicated the language
model's job, accumulated replacement vocabulary and edge cases, and still could
not understand identifiers written only in narrative prose.

The form already knows more: an administrator knows that a question such as
“Pilot name” collects private information. The summarizer also benefits from
seeing that labeled value, because it can recognize the same name when the
reporter repeats it in a non-private narrative.

Two risks must be avoided. Removing private answers completely deprives the
model of useful redaction context. Mixing them into ordinary report content
makes them eligible summary facts. Mutable classification could silently change
the handling of old and future answers under the same question identity.

## Decision

Every question carries an immutable boolean `IsPrivate`, chosen when the
question is created and defaulting to `true`. Question administration does not
offer a later privacy edit. To change classification, deactivate the existing
question and create a new question identity. `ReportAnswer` snapshots the value
at submission.

The Worker partitions labeled answers into an owned `SummarizationInput`:

- `ReportContent` / `report_content` contains non-private fields and is the only
  source of facts the summary may state.
- `PrivateContext` / `private_context` contains private labels and values. The
  summarizer may use these only to recognize, omit, replace, or generalize the
  same identifying detail in report content. It may not state or infer a fact
  solely from private context.

Private context crosses one external boundary: the configured summarization
model. It never reaches the PII auditor, translation provider, notification
channels, public API, logs, telemetry, or exceptions. The PII auditor evaluates
candidate summary text only; the translator receives anonymized summary text
only.

Text anonymization is exclusively an LLM responsibility under versioned runtime
prompts. The deterministic scrub, regex stages, scrub vocabulary, markers, and
their tests are removed. Deterministic file validation and metadata stripping
remain separate media controls.

All report answer values remain application-encrypted at rest. `IsPrivate` is a
model-input classification, not an encryption tier.

## Consequences

- A new question fails closed without requiring an administrator to remember a
  privacy choice.
- Privacy cannot be loosened accidentally under a stable question identity, and
  historical answers remain self-describing.
- The model can replace a private name repeated in narrative with a role phrase
  while being forbidden to publish facts found only in private context.
- Provider terms, retention, residency, and security controls must permit the
  summarization model to process both request sections.
- Prompt evaluation, summary-only PII audit, and human review are the safety
  backstops for model variability. Tests assert partitioning and output safety
  properties rather than recreating anonymization with deterministic code.
- Adding or changing a prompt creates a new version so published output remains
  traceable.

## Alternatives rejected

**Drop private answers before the model call.** This prevents direct disclosure
but removes the best signal for recognizing the same identifier in narrative.

**Flatten both categories into one request body.** The model can no longer tell
which fields are eligible facts and which exist only as redaction hints.

**Keep the deterministic scrub as defense in depth.** It preserves the
complexity and competing policies this decision removes, while still failing on
contextual identifiers. Defense in depth remains at the summary-only audit and
human-review boundaries.

**Permit later reclassification.** This changes the contract of an existing
question and creates accidental downgrade paths. A new identity is explicit and
auditable.

## Supersedes

- [ADR-0003](ADR-0003-anonymization-pipeline.md)
- [ADR-0027](ADR-0027-deterministic-scrub-design.md)
- The deterministic implementation details of
  [ADR-0028](ADR-0028-role-words-in-place-of-names.md); role phrases remain the
  desired model output.
