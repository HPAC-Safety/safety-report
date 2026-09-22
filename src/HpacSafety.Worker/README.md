# HpacSafety.Worker

Deployable long-running outbox consumer. It keeps slow/failure-prone processing
outside the report request.

## Target work

- Claim typed outbox messages safely and idempotently — built:
  [`Outbox/`](Outbox/), one `IOutboxMessageProcessor` per message type, claimed
  via `OutboxClaimer`'s `FOR UPDATE SKIP LOCKED` query.
- Mechanically supply the second language of every answer with a value —
  narrative included — via the same `ITranslator` port question authoring
  uses, never the summarization model. Built:
  [`Outbox/TranslateAnswersProcessor.cs`](Outbox/TranslateAnswersProcessor.cs).
  See ADR-0080.
- Query exact revision-bound answers, partition answered values into eligible
  `report_content` and recognition-only `private_context`, and exclude consent
  and all attachments.
- Before building the prompt, deterministically mark any exact or token-level
  occurrence of a private value found in `report_content` (see
  [ADR-0082](../../docs/decisions/ADR-0082-a-deterministic-marking-pass-precedes-the-one-model-call.md)).
  Load one current prompt from [`Prompts/`](Prompts/), make exactly one model
  call, validate strict English/French JSON, and persist one pair row with
  shared provenance.
- Process each attachment independently: safe image/video derivative or
  validated private document original.
- Retry within a bounded budget; expose terminal summary failure for manual
  bilingual entry and alert on failed/stuck work.

There is no separate PII audit, general-purpose deterministic scrub beyond the
narrow private-value marking pass above, specialized aircraft processing,
notification email, or extra model repair stage. Documents never enter model
input. Answer translation (above) is mechanical and literal, not the
anonymized summarization model — it never runs on the submission path and
never produces the bilingual summary.

Current main claims and translates answers; the summarization pipeline is
still a Worker host scaffold and the legacy ports/prompts do not describe the
target pipeline. See
[`../../docs/implementation-status.md`](../../docs/implementation-status.md).

```bash
docker compose up -d db
dotnet run --project src/HpacSafety.Worker
```
