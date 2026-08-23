---
name: anonymize-hpac-reports
description: Safely evolve or audit HPAC report anonymization, immutable question privacy, model-input partitioning, summarization and PII-audit prompts, private-data flow, translations, and published-detail boundaries. Use when code, tests, prompts, APIs, logs, exceptions, questions, or documentation could affect what report content reaches a model or public summary.
---

# Protect the anonymization boundary

Read `AGENTS.md`, `docs/anonymization-policy.md`, ADR-0038, and the active prompt
versions. Load [`test-hpac-safety`](../test-hpac-safety/SKILL.md) for tests and
[`localize-hpac-app`](../localize-hpac-app/SKILL.md) for either official
language.

## Classify at question creation

- Every question has an immutable `IsPrivate` value. Default it to `true` so an
  omitted checkbox cannot expose a new answer.
- The administration form presents the privacy checkbox only while creating a
  question. Do not offer it when revising wording, type, options, order, role,
  or activation.
- Never add a reclassification method or migration that silently changes an
  existing question. To change privacy, deactivate the old question and create
  a new identity. Historical answers keep their original classification.
- Copy `Question.IsPrivate` to each `ReportAnswer` when it is recorded.

## Partition the summarizer input

Build the model request through `SummarizationInput.Partition`:

- `report_content` contains only non-private fields. These are the only facts
  the summary may state.
- `private_context` contains private labels and values. The summarizer may use
  them only to recognize the same details inside report content and omit,
  replace, or generalize them. It must not state a fact found only here.
- Keep labels with values so the model knows that “Ada Lovelace” is a pilot
  name, not an aircraft or site. Preserve question and answer boundaries; do
  not flatten the report into an unlabeled blob.
- Only the summarizer provider adapter receives private context. The PII
  auditor receives the candidate summary only. Translation receives the
  anonymized summary only. Public reads, notifications, logs, metrics, and
  exceptions receive neither raw report section.

Do not add deterministic text scrubbers, regex redaction passes, replacement
vocabularies, or staged cleansing classes. The LLM owns textual anonymization.
Deterministic media type validation, malware handling, and metadata removal are
separate controls and remain in the media pipeline.

## Direct the model and review its output

- In report content, replace a person's name with a role phrase such as “the
  pilot” / “le pilote” when the role is known; otherwise omit it. Never emit
  `[redacted]`, private-context values, or an explanation of what was removed.
- Omit names, contact details, member identifiers, URLs, precise sites and
  timing, aircraft make/model, and combinations that identify someone in a
  small community. Generalize only from report content; never infer a new fact
  from private context.
- Generate the source summary in `Report.Language`. Translate only that
  anonymized summary. PII-audit each language and require human approval of the
  pair before publication.
- Keep raw fields and model payloads out of logs, telemetry, exception messages,
  snapshots, issue bodies, and committed fixtures.

## Version and prove the contract

Runtime instructions live under `prompts/`, never in this skill. Add a new
version instead of editing an existing prompt. Record the prompt version with
every generated summary.

Test these boundaries:

- new questions default private and expose no privacy mutation;
- answers snapshot privacy, and partitioning never places a private field in
  `report_content`;
- translator, auditor, and public ports cannot accept `private_context`;
- recorded model fixtures contain synthetic identifiers in the input, omit
  them from output, and preserve important non-private facts;
- English and French output obey the same policy.

Run the full test and coverage gates, then use `agents/anonymization-auditor.md`
against the committed diff. A substantiated privacy leak blocks publication;
the auditor never replaces human approval.
