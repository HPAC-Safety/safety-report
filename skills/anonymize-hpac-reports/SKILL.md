---
name: anonymize-hpac-reports
description: Audit or change HPAC question privacy, Worker summary input, runtime prompts, review, or public output. Use whenever report answers or identifying details could reach an LLM, log, reviewer, or published summary.
---

# Preserve the one anonymization boundary

Read `AGENTS.md`, `docs/anonymization-policy.md`, and the current prompt before
changing the flow.

- Treat each immutable question revision as the authority for `IsPrivate`.
- Query the exact question revisions shown with their answers and privacy flags.
- Put answered non-private fields in `report_content`. They are the only source
  of summary facts.
- Put answered private fields in labeled `private_context`. Use them only to
  recognize details that must be omitted or replaced by a role.
- Replace a matching person with the appropriate role. For example, a private
  pilot name repeated in narrative becomes “the pilot”; never retain a first
  name, surname, initial, or casing variant.
- Omit contact details, identifiers, precise locations and times, distinctive
  equipment, and combinations that identify someone in a small community.
- Prefer omission over inference. Never invent a fact from private context.
- Keep raw answers and model payloads out of logs, telemetry, errors, fixtures,
  and public DTOs.
- Require explicit publication consent and human approval.

The Worker performs anonymization and summarization in one LLM call using one
versioned prompt, in the report's own language, then translates the resulting
candidate to produce the second official language — translation receives the
anonymized summary text only, never report content or private context. Do not
add deterministic text scrubbers, replacement chains, a second LLM audit, or a
separate classification subsystem; aircraft class is an ordinary public
question, never inferred.

Test with synthetic identifiers. Assert that every private token is present in
the model request, absent from the summary, and that useful non-private incident
details remain.
