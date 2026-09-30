---
title: HpacSafety.Worker
description: The deployable long-running outbox consumer that keeps slow work out of the report request.
type: readme
---

# HpacSafety.Worker

Deployable long-running outbox consumer. It keeps slow/failure-prone processing
outside the report request.

`Worker` polls for due outbox work: each registered
[`IOutboxMessageProcessor`](Outbox/IOutboxMessageProcessor.cs) drains its own
message type in turn, claimed via `OutboxClaimer`'s `FOR UPDATE SKIP LOCKED`
query, and a processor's writes commit atomically with the claim.

## Answer translation (implemented — #271, ADR-0080)

[`Outbox/TranslateAnswersProcessor.cs`](Outbox/TranslateAnswersProcessor.cs)
mechanically supplies the second language of every answer with a value —
narrative included — via the same `ITranslator` port question authoring uses,
never the summarization model. Mechanical and literal, not anonymized
summarization — it never runs on the submission path and never produces the
bilingual summary.

## Attachment processing (implemented — #361, ADR-0098)

[`Outbox/ProcessAttachmentProcessor.cs`](Outbox/ProcessAttachmentProcessor.cs)
handles one `ProcessAttachment` message per file. The submission has already
copied the original into `<report id>/original/<file id>`; this reads it,
sniffs it against the recorded type, and writes the stripped derivative to
`<report id>/stripped/<file id>` — re-encoding an image, remuxing a video
(ADR-0094), and leaving a document as it is. A file whose bytes no longer
match, or that the image library cannot clean, is marked failed with a safe
code. A deleted report, an already-processed file, or an already-failed one is
left alone, so a redelivered message changes nothing. Storage and database
errors are left to the outbox's retry. The original is streamed into a
temporary file and hashed on the way; nothing holds a whole attachment in
memory except an image's decoded pixels (#362, REQ-MED-024).

## Summarization (implemented — #17, #20)

[`Outbox/SummarizeReportProcessor.cs`](Outbox/SummarizeReportProcessor.cs)
queries exact revision-bound answers into a `ReportForSummaryDto`, partitions
them into eligible `report_content` and recognition-only `private_context`
(excluding consent, skipped answers, and file-upload answers), and
deterministically marks any exact or token-level occurrence of a private
value found in `report_content` (see
[ADR-0082](../../docs/decisions/ADR-0082-a-deterministic-marking-pass-precedes-the-one-model-call.md))
before `OpenAiSummarizer` loads the current prompt from
[`Prompts/`](Prompts/), makes exactly one model call, and validates strict
English/French JSON. Answers go in form order, and the request names the
`expected_sections`: one per public paragraph question on the report, blank ones
included, worded as the revision the reporter answered. Each language must come
back as Markdown with exactly those `## ` headings, in order, and no other; a
mismatch is a failed attempt like any invalid response
([ADR-0180](../../docs/decisions/ADR-0180-a-summary-is-markdown-with-one-section-per-public-paragraph-question.md)). A successful attempt persists one summary row with
shared provenance and moves the report to `PendingReview`; a failure lets
`OutboxClaimer` record it on the outbox message and, once retries are
exhausted, moves the report to `SummaryFailed` with a content-free error for
manual bilingual entry.

The model call goes through `IAiMediator`, which hands it to the provider
handler whose model-name prefix matches: `GeminiHandler` claims `gemini-*`.
The `AiChatClient` section of `appsettings.json` holds the key, model, and
reasoning level together, with no provider setting: today `gemini-3.7-flash`,
reasoning `low`
([ADR-0104](../../docs/decisions/ADR-0104-summaries-are-generated-by-gemini-through-a-paid-key.md)).
The key is never committed; set `AiChatClient__ApiKey` (docker-compose maps an
exported `GEMINI_API_KEY` to it). With no key the mediator
reports itself unconfigured, and every attempt retries and then fails; with a
key, a model no handler claims, a blank model, or an invalid reasoning level
stops the Worker at startup.

## Target work

- Ship ffmpeg in the deployed image (#30).
- Pass `AiChatClient__ApiKey` into the deployed task (#30).
- Evaluate AWS Bedrock as a second provider (#387).
- Alert on failed/stuck work.

There is no separate PII audit, general-purpose deterministic scrub beyond the
narrow private-value marking pass above, specialized aircraft processing,
notification email, or extra model repair stage. Documents never enter model
input.

See [`../../docs/implementation-status.md`](../../docs/implementation-status.md)
for the full capability matrix.

```bash
docker compose up -d db
dotnet run --project src/HpacSafety.Worker
```
