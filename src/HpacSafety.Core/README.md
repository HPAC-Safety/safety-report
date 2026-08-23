# HpacSafety.Core

Non-deployable domain library with no runtime package dependency. The target
model is specified in [`spec/domain-and-lifecycle.md`](../../spec/domain-and-lifecycle.md).

## Target shape

- complete immutable bilingual question revisions;
- revision-bound report answers with consent as the only required/system
  projection;
- report lifecycle and typed outbox work;
- one English/French summary row with shared provenance and approval;
- roles, allowlist, moderation/audit rules, and irreversible soft deletion;
- small ports only for real external boundaries.

Ordinary answers—including aircraft-related answers—remain generic question
data. Core does not infer, normalize, or project specialized aircraft values.
It also has no field cipher, runtime translator, PII auditor, email sender,
publication-channel abstraction, or pre-submit upload-slot abstraction.

## Current status

Current main contains useful consent, IDs, outbox, privacy-partition, media, and
question scaffolding, plus legacy typed projections and one-language summaries
that must be migrated. Do not treat current types as target requirements; see
[`spec/implementation-status.md`](../../spec/implementation-status.md).

Pure unit tests live in `tests/HpacSafety.Core.Tests`; privacy/model boundary
tests use synthetic data in `tests/HpacSafety.Anonymization.Tests`.
