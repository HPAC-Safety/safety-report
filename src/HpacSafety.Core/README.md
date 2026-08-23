# HpacSafety.Core

The dependency-free domain model.

- `Question` is one complete immutable bilingual revision.
- `ReportAnswer` references the exact revision shown and can record a skip.
- `Report` projects only publication consent; ordinary answers stay as answers.
- `ReportForSummaryDto` carries questions, privacy, and answers to the Worker.
- `SummarizationInput` partitions answered values into `report_content` and
  `private_context` for one `ISummarizer` call.
- `Summary` is one human-reviewable candidate per report.
- `OutboxMessage` makes submission and Worker handoff atomic.

Do not add infrastructure SDKs or duplicate ordinary answers as typed report
properties. Product invariants live in [AGENTS.md](../../AGENTS.md).
