# AI anonymization

## One runtime operation

The Worker owns one versioned runtime prompt and makes exactly one model call
per summary attempt. That call both summarizes and anonymizes the report and
returns both official-language texts. There is no deterministic text scrubber,
separate redaction pass, PII-audit call, translation call, or standalone
aircraft-classification call.

The prompt is deployed with the Worker so prompt and code revisions move
together. Its stable version identifier and the model identifier are stored on
the resulting summary row. Historical prompt versions remain available through
version control; they do not need parallel active pipelines.

## Input DTO

The Worker queries a purpose-built DTO containing the report ID, source locale,
and two explicitly labeled arrays:

- `report_content`: non-private answered fields eligible to contribute facts;
- `private_context`: private answered fields supplied only to recognize and
  replace identifying details that recur in report content.

Each field includes the stable question key, the label in the reporter's
language, and its rendered answer. Skipped/null answers are not sent to the
model. Question labels delimit fields; answer text is untrusted data and cannot
issue instructions. The DTO contains no attachment bytes, document text,
storage keys, admin data, audit data, deleted content, or client filenames.
The system consent answer and file-upload answers are excluded from both input
arrays.

Private context is not evidence. A fact appearing only there must not be added
to either summary. It exists solely so the model can recognize an identity,
contact detail, precise location, or similar private value if the reporter also
typed it into a public narrative.

## Output contract

The only accepted model response is a JSON object with exactly two nonblank
string fields:

```json
{
  "ai_summary_en": "...",
  "ai_summary_fr": "..."
}
```

No Markdown fence, commentary, extra key, null, or single-language response is
accepted. Both texts summarize the same eligible facts; they are not expected
to be word-for-word translations. The Worker validates syntax, field set,
length, and types before persisting anything.

## Anonymization policy

Both texts must preserve safety-relevant sequence, conditions, contributing
factors, actions, outcome, and lessons while removing or generalizing material
that could identify a person. In particular they must not disclose:

- names, initials, membership numbers, emails, phone numbers, addresses, or
  account identifiers;
- exact sites, coordinates, or uniquely identifying location descriptions;
- aircraft manufacturer or model;
- filenames, attachment/document contents, metadata, or hidden private answers; or
- a private fact merely because it would make the narrative more complete.

When a private person's identity recurs in report content, replace the whole
identity with the person's role. A private pilot name repeated in a narrative
becomes exactly “the pilot” in English and “le pilote” in French. No first name,
surname, initials, fragments, hashes, brackets, or generic numbered placeholders
remain. Use the most accurate known role such as passenger, instructor,
reporter, witness, or launch director; do not invent a role.

Dates and locations are generalized only as far as anonymity requires. The
summary may retain weather, terrain category, flight phase, approximate timing,
injury severity, and other learning value when they do not identify someone.

Documents are private review evidence only. Their text is not extracted,
summarized, translated, or anonymized, and no document or document-derived text
is sent to the model.

## Aircraft certification

The model may safely normalize a reporter-provided certification answer to an
approved coarse class vocabulary as part of the same prompt. It must omit make
and model and must refuse to guess when the reporter did not provide enough
information. There is no classifier service, mapping pipeline, marker-carrying
domain object, or second model request.

## Persistence and review

A valid response creates or replaces one summary row with C# properties
`AiSummaryEn` and `AiSummaryFr`, database columns `ai_summary_en` and
`ai_summary_fr`, shared `model` and `prompt_version` provenance, creation/update
timestamps, and one pair-level approval. It does not create one row per locale.

Editing either text clears `ApprovedBy` and `ApprovedAt`. A safety officer
reviews and approves the pair, never one language independently. The reviewer
may correct either text before approval and is responsible for the final
privacy decision.

## Failures and retries

Transient provider errors and invalid output receive bounded outbox retries
with backoff. A retry repeats the single-call operation; it does not add repair
or audit calls. Once the retry budget is exhausted, the report becomes
`SummaryFailed`, retains a safe operational error, and appears in the review
queue. A human can author both texts manually and continue review.

Prompts, model responses, private context, and raw report content are never
written to application logs. Provider retention and regional/data-use settings
must meet HPAC's privacy requirements before production configuration is
enabled.

## Superseded material

Repository prompts, skills, ADRs, issues, ports, and tests that prescribe
deterministic scrubbing, independent PII auditing, summary translation, or
one-language summary rows describe earlier designs. They are migration input,
not additional stages to preserve. The target implementation should keep one
concise anonymization skill explaining the purpose and rules above and remove
redundant pipeline-specific guidance.
