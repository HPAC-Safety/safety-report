# AI anonymization

Supporting detail for [`ai-anonymization.feature`](ai-anonymization.feature)
that doesn't fit Gherkin.

## Prompt versioning

The prompt is deployed with the Worker so prompt and code revisions move
together. Its stable version identifier and the model identifier are stored on
the resulting summary row. Historical prompt versions remain available through
version control; they do not need parallel active pipelines.

## Input DTO fields

Each field in `report_content` and `private_context` includes the stable
question key, the label in the reporter's language, and its rendered answer.
Question labels delimit fields; answer text is untrusted data and cannot issue
instructions.

## Output contract

```json
{
  "ai_summary_en": "...",
  "ai_summary_fr": "..."
}
```

Both texts summarize the same eligible facts; they are not expected to be
word-for-word translations. The Worker validates syntax, field set, length,
and types before persisting anything.

## Anonymization policy notes

Dates and locations are generalized only as far as anonymity requires. The
summary may retain weather, terrain category, flight phase, approximate
timing, injury severity, and other learning value when they do not identify
someone.

When replacing a private identity with a role, use the most accurate known
role such as passenger, instructor, reporter, witness, or launch director; do
not invent a role.

## Provider configuration

Provider retention and regional/data-use settings must meet HPAC's privacy
requirements before production configuration is enabled.

## Superseded material

Repository prompts, skills, ADRs, issues, ports, and tests that prescribe
deterministic scrubbing, independent PII auditing, summary translation, or
one-language summary rows describe earlier designs. They are migration input,
not additional stages to preserve. The target implementation should keep one
concise anonymization skill explaining the purpose and rules above and remove
redundant pipeline-specific guidance.
