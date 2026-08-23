# HpacSafety.Worker

Background-service scaffold for durable report summarization.

The implemented Worker will claim a submitted-report outbox row, query one
`ReportForSummaryDto`, partition it by immutable question privacy, and make one
model call with [summarize.v4.md](Prompts/summarize.v4.md) in the report's own
language. It then translates that candidate through `ITranslator` to produce
the second official language and stores both for human review. Once both are
stored, it sends one `IEmailSender` notification to `safety@hpac.ca` that the
report is ready for review, riding the same outbox row so a failed send can
never roll back the report. Failures use bounded retry and remain visible for
manual handling.

There is no second PII audit, classifier, or generic publication-channel
pipeline.

```bash
dotnet run --project src/HpacSafety.Worker
```

Runtime settings are `ConnectionStrings:HpacSafety`, the field-encryption key,
the configured model API key, and `Notifications:To`.
