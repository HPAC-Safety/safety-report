---
title: A summary is Markdown with one section per public paragraph question
description: The Worker's one model call writes each summary language as Markdown with one "## " section per public paragraph question on the report, headed by that question's label from the revision the reporter answered, in form order. The Worker rejects a summary whose headings differ, and the web renders summaries and paragraph answers through one safe Markdown subset.
type: adr
status: accepted
date: 2026-09-30
decision-makers: Chase Florell
keywords: summary, markdown, sections, headings, prompt v4, display order, validation, retry budget, safe renderer, ADR-0082, ADR-0104, ADR-0177
---

# ADR-0180 — A summary is Markdown with one section per public paragraph question

**Status:** Accepted. Decided by the owner on 2026-09-30 in
[#697](https://github.com/HPAC-Safety/safety-report/issues/697). Changes the
output of the one model call that
[ADR-0082](ADR-0082-a-deterministic-marking-pass-precedes-the-one-model-call.md)
and [ADR-0104](ADR-0104-summaries-are-generated-by-gemini-through-a-paid-key.md)
describe (still one call, still one strict English/French pair) and the text
that [ADR-0177](ADR-0177-summaries-are-append-only-revisions-and-a-live-edit-publishes-itself.md)
stores as revisions. No earlier ADR is superseded. The label-colon change that
shipped in the same pull request is its own decision,
[ADR-0181](ADR-0181-a-one-time-migration-trims-label-colons-in-place.md).

## Context

The Worker's prompt, `summarize-anonymize.v3.md`, forbade headings and returned
two plain-prose strings. The answers reached the model in no set order
(`SummarizeReportProcessor.LoadForSummary` had no `OrderBy`). The questionnaire
has two paragraph (`LongText`) questions, both public, Description and Action
and prevention. A reader of the public summary cannot tell what happened from
what was done about it, and a reporter's thought about the cause, typed in
Description, reads as narrative.

## Decision

**A summary is one Markdown text per language, with one section per public
paragraph question on the report.**

- **Which sections.** Every public `LongText` question on the report, blank ones
  included, ordered by the display order of the revision the reporter answered.
  A private paragraph question has no section. A report with none has a summary
  with no headings.
- **The heading is the label.** It is written `## ` and the question's label in
  that summary's language, from the revision the reporter answered (the wording
  they saw), with no trailing colon.
- **Content.** The other public facts (date, time of day, province, aircraft
  type, damage) are woven into the section they fit; there is no facts section.
  Each statement goes in the section whose question it best answers, even when
  the reporter typed it in the other box. A section with nothing from anywhere in
  the report reads `Not provided.` (`Non fourni.` in French). A section that
  received moved content is not empty.
- **Still one call, one pair.** `ai_summary_en` and `ai_summary_fr` are still the
  only two fields, now Markdown, and the anonymization and accuracy rules are
  unchanged. The Worker adds `expected_sections` to the request (each entry a
  question key with its English and French label) and orders the answers by
  display order. The marking pass is unchanged.
- **Prompt v4.** `summarize-anonymize.v4.md` keeps every v3 rule except the style
  line that forbade headings and Markdown, and adds the section rules and a
  worked example. v3 is never edited, and a summary records the version that
  wrote it.
- **The Worker checks the headings.** Each language must have exactly the
  expected `## ` headings, with the exact label text, in form order, and no other
  heading. A mismatch is a `SummarizationFailedException` like any invalid
  response, so the existing outbox retry budget applies; when it is spent the
  report becomes `SummaryFailed` and a reviewer writes the summary by hand. The
  check reads headings only: it does not judge a section's body.

### Rendering

The web renders Markdown, as a **safe subset**, in the places a summary or a
reporter's paragraph answer is read: the public report page, the admin review
page and its revision history, and a long-text answer and its Worker translation
on the admin report detail page. One shared component renders it.

- **Allowed:** headings, paragraphs, bold, italic, lists, and line breaks.
- **Raw HTML is escaped**, never rendered. **A link is its text only.** **An
  image never renders**, so a summary can never make the reader's browser fetch
  anything.
- **The public feed preview** shows the first section's body as plain text,
  without its heading.
- **Markdown stays hidden from users.** Textareas stay plain: no editor, toolbar,
  preview, or hint. A reviewer and a reporter who know Markdown can use it.

## Considered options

- **One structured object per section** (`{ "description": ..., "action": ... }`)
  instead of Markdown in the two fields. It would change the stored shape
  (`ai_summary_en`, `ai_summary_fr`, `summary_revisions`), the review editor, and
  the public view, for a presentation rule. Markdown keeps the pair and lets a
  reviewer edit one text.
- **A separate "facts" section.** It repeats what the narrative says and pushes
  the lesson further down. Weaving the facts in keeps each section a story.
- **A sanitizing HTML pipeline** (render Markdown to HTML, then clean it). A
  subset that never produces the dangerous elements is simpler to reason about
  than one that produces them and removes them.
- **Validating bodies.** A check that a section has "enough" text, or that
  content moved correctly, is a judgment the reviewer owns
  ([ADR-0004](ADR-0004-human-review-required.md)).

## Consequences

- Reports summarized before this change keep their plain-prose summary, which
  renders as paragraphs. Re-summarizing them is a one-off by hand on the
  development database; nothing in the product regenerates a summary
  (owner, 2026-09-30).
- The public search view still reads the raw summary text, Markdown characters
  included.
- A paragraph question added later gets a section with no change to the code. A
  report whose only paragraph question is private gets a summary with no
  headings.
- Every test that builds a summary response for a report with paragraph
  questions includes the headings.

## Related

- [ADR-0082](ADR-0082-a-deterministic-marking-pass-precedes-the-one-model-call.md)
  — the marking pass, unchanged.
- [ADR-0104](ADR-0104-summaries-are-generated-by-gemini-through-a-paid-key.md)
  — the model call, unchanged.
- [ADR-0177](ADR-0177-summaries-are-append-only-revisions-and-a-live-edit-publishes-itself.md)
  — the revisions that hold the Markdown.
- [`.spec/features/ai-anonymization/README.md`](../features/ai-anonymization/README.md)
  "Summary sections"; claims `REQ-AI-031` to `REQ-AI-036`, `REQ-WLD-045`,
  `REQ-MOD-208` to `REQ-MOD-211`.
