# HpacSafety.Worker

Background-service scaffold for durable report summarization.

The implemented Worker will claim a submitted-report outbox row, query one
`ReportForSummaryDto`, partition it by immutable question privacy, and make one
model call with [summarize.v4.md](Prompts/summarize.v4.md). It stores one
candidate summary for human review. Failures use bounded retry and remain
visible for manual handling.

There is no second PII audit, translation call, classifier, email notification,
or publication-channel pipeline.

```bash
dotnet run --project src/HpacSafety.Worker
```

Runtime settings are `ConnectionStrings:HpacSafety`, the field-encryption key,
and the configured model API key.
