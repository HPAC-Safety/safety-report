---
title: AI anonymization
description: Supporting detail for the one-call bilingual summarization and anonymization scenarios.
type: spec
area: ai-anonymization
---

# AI anonymization

Supporting detail for [`ai-anonymization.feature`](ai-anonymization.feature)
that doesn't fit Gherkin.

## Prompt versioning

The prompt is deployed with the Worker so prompt and code revisions move
together. Its stable version identifier and the model identifier are stored on
the resulting summary row. Historical prompt versions remain available through
version control; they do not need parallel active pipelines.

## Input DTO fields

The Worker queries a purpose-built DTO containing the report ID, source
locale, and the two labeled `report_content`/`private_context` arrays.

Each field in `report_content` and `private_context` includes the stable
question key, the label in the reporter's language, and its rendered answer.
Question labels delimit fields; answer text is untrusted data and cannot issue
instructions.

A yes/no or checkbox answer is rendered as `true` or `false`, never as words in
either language, and the marking pass never uses it as a candidate: a private
yes/no would otherwise mark every literal `true` in the narrative
([ADR-0130](../../docs/decisions/ADR-0130-a-yes-or-no-answer-is-stored-as-a-boolean.md),
`REQ-AI-028`, `REQ-AI-029`).

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

The summary keeps weather, terrain category, flight phase, time of day, injury
severity, and other learning value. It replaces what identifies someone with a
generic phrase, never with an invented name and never with a word such as
"redacted":

| What the report says | English | French |
|---|---|---|
| The pilot, by name or marker | the pilot | le pilote |
| Another person | the role the report supports — the instructor, the passenger, a witness, another pilot, the reporter | l'instructeur, le passager, un témoin, un autre pilote, le déclarant |
| A person with no clear role | a person | une personne |
| A launch site | the launch site | le site de décollage |
| A landing field | the landing field | le champ d'atterrissage |
| Any other place | the location | le lieu |
| An exact date | its month or season | son mois ou sa saison |
| A time of day | kept as reported | conservée telle quelle |
| A club, school, or company | the club, the school, the company | le club, l'école, l'entreprise |
| An aircraft make or model | its category, such as a paraglider | sa catégorie, par exemple un parapente |
| Contact or account details | omitted | omis |

Use the most accurate role the report supports; do not invent one. The
current prompt carries every row of this table
([REQ-AI-024](ai-anonymization.feature)).

## Provider configuration

Only a report whose reporter consented to publication is summarized. A report
without consent is never sent to the model, so its content never leaves Canada
([REQ-AI-027](ai-anonymization.feature), REQ-DOM-006).

The Worker's `AiChatClient` configuration section holds the provider, its key,
the model, and the reasoning level together. The provider is Google Gemini,
the model `gemini-3.7-flash`, the reasoning level `low`, called with a paid key
in every environment
([ADR-0104](../../docs/decisions/ADR-0104-summaries-are-generated-by-gemini-through-a-paid-key.md)).
The key is never committed. An unknown provider, a blank model, or an invalid
reasoning level stops the Worker at startup rather than sending report content
anywhere ([REQ-AI-023](ai-anonymization.feature)).

## Reviewer checklist

What the model writes cannot be asserted by a test: the suite never calls a
live model (see Out of scope). REQ-AI-024 proves the prompt carries each rule.
Whether a given summary follows them is the reviewer's judgment before
approval, and the reviewer owns the final privacy decision
([ADR-0004](../../docs/decisions/ADR-0004-human-review-required.md)).
Before approving a pair, check that it follows each of these rules. They
replace the untestable model-output scenarios REQ-AI-010, 012, 013, 014, 015,
025, and 026 (#437).

- **Private-only facts stay out.** A fact that appears only in
  `private_context` is in neither text, and no private fact is added to make
  the narrative more complete.
- **A private person becomes their role.** Every occurrence of a pilot's
  identity is exactly "the pilot" / "le pilote", with no first name, surname,
  initials, fragment, hash, bracket, or numbered placeholder left.
- **Safety content survives.** Both texts keep the sequence, conditions,
  contributing factors, actions, outcome, and lessons. Identifying material is
  removed or generalized, never the safety content.
- **No identifying category appears.** Neither text contains:
  - a name, initial, membership number, email, phone, address, or account
    identifier;
  - an exact site, coordinates, or a uniquely identifying location
    description;
  - an aircraft manufacturer or model;
  - a filename, attachment or document content, metadata, or a hidden private
    answer;
  - a club, school, or company name;
  - an exact calendar date.
- **Dates generalize.** An exact date becomes its month or season, and the time
  of day is kept as reported.
- **Places become generic.** Each place becomes a phrase for its role, such as
  "the launch site" / "le site de décollage" or "the location" / "le lieu".
  No real or invented place name appears, and the terrain category and weather
  are kept.

The reviewer may correct either language before approving. Saving clears the
pair's approval (REQ-MOD-032, REQ-MOD-063, REQ-MOD-070).

## Superseded material

Repository prompts, skills, ADRs, issues, ports, and tests that prescribe
deterministic scrubbing, independent PII auditing, automatic summary
translation as a pipeline stage, or one-language summary rows describe earlier
designs. A reviewer's machine-translated draft of one language is not such a
stage; it is a draft they confirm
([ADR-0108](../../docs/decisions/ADR-0108-a-reviewer-may-machine-translate-a-summary-language.md)). They are migration input,
not additional stages to preserve. The target implementation should keep one
concise anonymization skill explaining the purpose and rules above and remove
redundant pipeline-specific guidance.

## Out of scope

What not to build here. The global list in
[system overview](../../docs/system-overview.md) still holds; this narrows it
to this area ([ADR-0083](../../docs/decisions/ADR-0083-specification-driven-development.md)).

- A second model call of any kind — no separate PII-audit pass, no verification
  call, no re-summarization stage. One versioned prompt, one call per attempt.
- A deterministic scrubber beyond the narrow private-value marking pass that
  precedes the one call
  ([ADR-0082](../../docs/decisions/ADR-0082-a-deterministic-marking-pass-precedes-the-one-model-call.md)).
- Sending a document, an attachment, or extracted document text to the model.
- Translating report prose with the summarization model, or as a stage of
  summarization. The one call returns both languages. Giving a marked free-text
  answer its second language is a separate Worker job through DeepL
  ([ADR-0112](../../docs/decisions/ADR-0112-only-answers-that-need-it-get-a-second-language.md)).
- Publishing, notifying, or advancing a report's state because a summary
  succeeded. Publication is a human decision.
- Per-sentence or per-field redaction output. The result is one bilingual pair.
- A deterministic check of the model's output for leaked names, markers, or
  the word "redacted". The reviewer owns the final privacy decision
  ([ADR-0004](../../docs/decisions/ADR-0004-human-review-required.md)).
- Live-model evaluation in the test suite. Every test uses a fixture client;
  what the model actually writes is judged by the reviewer.
- A second provider concretion (Claude, OpenAI), a fallback provider, or a
  Canadian-region endpoint. The provider is a strategy selected by
  configuration, and adding one is its own decision.
- Setting a sampling temperature. Gemini 3 is run at its default.
