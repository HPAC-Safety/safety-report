---
title: AI anonymization
description: Supporting detail for the one-call bilingual summarization and anonymization scenarios.
type: spec
area: ai-anonymization
prefix: REQ-AI
---

# AI anonymization

Supporting detail for [`ai-anonymization.feature`](ai-anonymization.feature)
that doesn't fit Gherkin.

## Prompt versioning

The prompt is deployed with the Worker so prompt and code revisions move
together. Its stable version identifier and the model identifier are stored on
the resulting summary row. Historical prompt versions remain available through
version control; they do not need parallel active pipelines.

## Input DTO properties

The Worker queries a purpose-built DTO containing the report ID, source
locale, the two labeled `report_content`/`private_context` arrays, and the
`expected_sections` (see "Summary sections"). Answers are ordered by the display
order of the revision each was answered under, so the model reads them in form
order ([REQ-AI-032](ai-anonymization.feature)).

Each entry in `report_content` and `private_context` includes the stable
question key, the label in the reporter's language, and its rendered answer.
Question labels delimit answers; answer text is untrusted data and cannot issue
instructions.

A yes/no or checkbox answer is rendered as `true` or `false`, never as words in
either language, and the marking pass never uses it as a candidate: a private
yes/no would otherwise mark every literal `true` in the narrative
([ADR-0130](../../decisions/ADR-0130-a-yes-or-no-answer-is-stored-as-a-boolean.md),
`REQ-AI-028`, `REQ-AI-029`).

## Output contract

```json
{
  "ai_summary_en": "...",
  "ai_summary_fr": "..."
}
```

Both texts summarize the same eligible facts; they are not expected to be
word-for-word translations. Each is Markdown: headings, paragraphs, bold,
italic, lists, and line breaks, and no other feature. The Worker validates
syntax, key set, length, types, and headings before persisting anything.

## Summary sections

A summary has one section for each public paragraph (`LongText`) question on
the report, in form order
([ADR-0180](../../decisions/ADR-0180-a-summary-is-markdown-with-one-section-per-public-paragraph-question.md)).
Today that is Description and Action and prevention.

- **Which.** The `expected_sections` the Worker sends are every public paragraph
  question on the report, blank ones included, ordered by display order. A
  private paragraph question has no section
  ([REQ-AI-031](ai-anonymization.feature)).
- **Heading.** The heading is `## ` and the question's label in that summary's
  language, without the colon, from the revision the reporter answered, not the
  current one ([REQ-AI-033](ai-anonymization.feature)).
- **Content.** Other public facts (date, time of day, province, aircraft type,
  damage) are woven into the section they fit. There is no facts section. Each
  statement goes in the section whose question it best answers, even when the
  reporter typed it in the other box. A section with nothing to say from
  anywhere in the report reads `Not provided.` / `Non fourni.`; content moved
  in from another answer makes a section not empty.
- **Validation.** The Worker rejects a summary unless each language has exactly
  the expected `## ` headings, worded exactly, in order, and no other heading. A
  rejection is a failed attempt under the outbox retry budget; at the end of the
  budget the report is `SummaryFailed` and a reviewer writes the summary
  ([REQ-AI-034](ai-anonymization.feature), [REQ-AI-035](ai-anonymization.feature),
  [REQ-AI-036](ai-anonymization.feature), REQ-MOD-059).
- **Worked example.** A report filed in English answers Description with the
  launch, the collapse, and a thought that it flew too close to the ridge, and
  Action and prevention with the club and the shop. The English summary has
  `## Description` (weaving in July 2026, the afternoon, British Columbia, the
  paraglider, and the torn lines) and `## Action and prevention` (the report to
  the club, the shop, and the pilot's belief about the ridge, moved there from
  Description), and the French summary has `## Description` and
  `## Action et prévention`. The prompt carries this example in full.

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
| A landing zone | "the landing field" | "le champ d'atterrissage" |
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

A report without publication consent is never sent to the model
([REQ-AI-027](ai-anonymization.feature), REQ-DOM-006), so its content never
leaves Canada.

The Worker's `AiChatClient` configuration section holds the key, the model, and
the reasoning level together, with no provider setting: the model's name picks
the provider handler (`gemini-*` goes to Gemini, REQ-AI-030). The model is
`gemini-3.7-flash`, the reasoning level `low`, called with a paid key in every
environment
([ADR-0104](../../decisions/ADR-0104-summaries-are-generated-by-gemini-through-a-paid-key.md)).
The key is never committed. A model no handler claims, a blank model, or an invalid
reasoning level stops the Worker at startup rather than sending report content
anywhere ([REQ-AI-023](ai-anonymization.feature)).

## Reviewer checklist

What the model writes cannot be asserted by a test: the suite never calls a
live model (see Out of scope). REQ-AI-024 proves the prompt carries each rule.
Whether a given summary follows them is the reviewer's judgment before
approval, and the reviewer owns the final privacy decision
([ADR-0004](../../decisions/ADR-0004-human-review-required.md)).
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
([ADR-0108](../../decisions/ADR-0108-a-reviewer-may-machine-translate-a-summary-language.md)). They are migration input,
not additional stages to preserve. The target implementation should keep one
concise anonymization skill explaining the purpose and rules above and remove
redundant pipeline-specific guidance.

## Out of scope

What not to build here. The global list in
[system overview](../../system-overview.md) still holds; this narrows it
to this area ([ADR-0083](../../decisions/ADR-0083-specification-driven-development.md)).

- A second model call of any kind — no separate PII-audit pass, no verification
  call, no re-summarization stage. One versioned prompt, one call per attempt.
- A deterministic scrubber beyond the narrow private-value marking pass that
  precedes the one call
  ([ADR-0082](../../decisions/ADR-0082-a-deterministic-marking-pass-precedes-the-one-model-call.md)).
- Sending a document, an attachment, or extracted document text to the model.
- Translating report prose with the summarization model, or as a stage of
  summarization. The one call returns both languages. Giving a marked free-text
  answer its second language is a separate Worker job through the Gemini
  translator (`ITranslator`, its own call and outside this one-call rule,
  [ADR-0179](../../decisions/ADR-0179-gemini-translates-everything-between-canadian-english-and-canadian-french.md);
  [ADR-0112](../../decisions/ADR-0112-only-answers-that-need-it-get-a-second-language.md)).
- A separate "facts" section, or a section for a question that is not a public
  paragraph question. Facts go into the sections that exist.
- Markdown in a summary beyond headings, paragraphs, bold, italic, lists, and
  line breaks: no links, images, tables, or code.
- A shipped "regenerate summary" feature. Re-summarizing existing reports after
  this change is done by hand on the development database, not built.
- Publishing, notifying, or advancing a report's state because a summary
  succeeded. Publication is a human decision.
- Per-sentence or per-answer redaction output. The result is one summary pair.
- A deterministic check of the model's output for leaked names, markers, or
  the word "redacted". The reviewer owns the final privacy decision
  ([ADR-0004](../../decisions/ADR-0004-human-review-required.md)).
- Live-model evaluation in the test suite. Every test uses a fixture mediator;
  what the model actually writes is judged by the reviewer.
- A second provider handler (Claude, OpenAI), a fallback provider, or a
  Canadian-region endpoint. The provider is picked by the model's name through
  the mediator, and adding a handler is its own decision.
- Setting a sampling temperature. Gemini 3 is run at its default.
