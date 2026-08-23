---
name: clarify-hpac-requirements
description: Resolve ambiguous or incomplete HPAC safety-report requirements without inventing behaviour. Use when two readings change the implementation, a required value is missing, repository rules conflict with a request, or work touches anonymization, prompts, publication, privacy, retention, or localization and any detail is unclear.
---

# Clarify material gaps

Stop before implementing work that depends on a material gap. A plausible guess
is still invented behaviour, and in this repository it can identify a real
injured pilot.

## Decide whether to ask

Ask when:

- two readings produce materially different work;
- a term could name different summaries, users, data, or environments;
- a required name, address, threshold, retention period, or French string was
  not supplied;
- the request conflicts with `AGENTS.md`, a recorded decision, or a safety
  invariant;
- anonymization, runtime prompts, publication, privacy, consent, or credentials
  are involved and anything is unclear.

Use routine judgment for variable names, established file placement, and
equivalent idioms. The dividing line is whether another reasonable answer would
change observable behaviour, safety, or scope.

## Ask once and usefully

1. Inspect the code, issue, ADRs, docs, and applicable skills first.
2. Complete all work that does not depend on the answer.
3. Ask all remaining questions in one message before implementation.
4. State the missing fact, why it changes the work, and the default you would
   choose if explicitly authorized.
5. Treat the answer as a requirement and capture it durably in the same pull
   request: repository-writing rules in `AGENTS.md` or a skill, runtime-system
   behaviour in `docs/` and possibly an ADR, and model input in a new prompt
   version.

If work genuinely must proceed without an answer, mark the assumption in both
the code and pull-request body. Never use this escape hatch for a safety or
privacy invariant.
