# Anonymization policy

The Worker summarizes and anonymizes in one LLM call. The authority for how an
answer is used is the immutable question revision's `IsPrivate` value.

## Model input

- `report_content`: answered non-private questions. These are the only eligible
  sources of public facts.
- `private_context`: answered private questions. These values are recognition
  aids only; they may not introduce or support a public fact.
- skipped answers: omitted from both arrays.

Private context lets the model recognize an identifier repeated inside a
non-private narrative. If private pilot fields contain `Chase` and `FLorell` and
the description says `Chase Florell landed hard`, the candidate must say
`the pilot landed hard`. It must never say `Chase`, `Florell`, initials, or
`the pilot Florell`.

The same rule applies case-insensitively to partial names, minor spelling
variants, contact details, precise sites and times, identifying equipment, and
combinations that would identify someone in a small flying community. Prefer
omission when a safe role replacement is not useful. Never use private context
to infer causes, classifications, conditions, or outcomes.

## Controls

- One prompt and one model call; no regex scrub or second AI audit.
- Raw answers and model payloads never enter logs, telemetry, test fixtures,
  issues, or public DTOs.
- The stored candidate records its model and prompt version.
- A human can edit, approve, reject, or manually replace a failed candidate.
- Publication requires explicit positive consent and human approval.

Runtime instructions live with the Worker in
[summarize.v4.md](../src/HpacSafety.Worker/Prompts/summarize.v4.md).
Repository-change guidance lives in
[anonymize-hpac-reports](../skills/anonymize-hpac-reports/SKILL.md).
