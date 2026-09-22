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

## Summarization (implemented — #17, #20)

[`Outbox/SummarizeReportProcessor.cs`](Outbox/SummarizeReportProcessor.cs)
queries exact revision-bound answers into a `ReportForSummaryDto`, partitions
them into eligible `report_content` and recognition-only `private_context`
(excluding consent, skipped answers, and file-upload answers), and
deterministically marks any exact or token-level occurrence of a private
value found in `report_content` (see
[ADR-0082](../../docs/decisions/ADR-0082-a-deterministic-marking-pass-precedes-the-one-model-call.md))
before `PromptDrivenSummarizer` loads the current prompt from
[`Prompts/`](Prompts/), makes exactly one model call, and validates strict
English/French JSON. A successful attempt persists one summary row with
shared provenance and moves the report to `PendingReview`; a failure lets
`OutboxClaimer` record it on the outbox message and, once retries are
exhausted, moves the report to `SummaryFailed` with a content-free error for
manual bilingual entry. `IAiChatClient`'s only registered concretion today is
the fail-closed `UnconfiguredAiChatClient` — no provider has been
reviewed/approved yet.

## Target work

- Process each attachment independently: safe image/video derivative or
  validated private document original (#81).
- Review and wire a real `IAiChatClient` concretion (a follow-on issue; first
  candidate is Google Gemini).
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
