# HpacSafety.Worker

Deployable long-running outbox consumer. It keeps slow/failure-prone processing
outside the report request.

## Target work

- Claim typed outbox messages safely and idempotently.
- Query exact revision-bound answers, partition answered values into eligible
  `report_content` and recognition-only `private_context`, and exclude consent
  and all attachments.
- Load one current prompt from [`Prompts/`](Prompts/), make exactly one model
  call, validate strict English/French JSON, and persist one pair row with
  shared provenance.
- Process each attachment independently: safe image/video derivative or
  validated private document original.
- Retry within a bounded budget; expose terminal summary failure for manual
  bilingual entry and alert on failed/stuck work.

There is no separate PII audit, runtime translation, deterministic scrub,
specialized aircraft processing, notification email, or extra model repair
stage. Documents never enter model input.

Current main is mostly a Worker host scaffold; the legacy ports/prompts do not
describe the target pipeline. See
[`features/implementation-status/implementation-status.md`](../../features/implementation-status/implementation-status.md).

```bash
docker compose up -d db
dotnet run --project src/HpacSafety.Worker
```
