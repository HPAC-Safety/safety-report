# Persistence

`HpacSafetyDbContext` maps eight application tables: reports, report answers,
summaries, questions, question options, outbox messages, admin users, and audit
log entries.

Question rows are complete immutable revisions. Answers reference `question_id`,
so the exact wording, privacy flag, order, and options remain recoverable without
copying them onto the answer. Nullable text answers are encrypted with AES-GCM;
selected invariant option codes remain queryable.

Create and apply migrations with the pinned SDK:

```bash
dotnet ef migrations add Name \
  --project src/HpacSafety.Infrastructure \
  --startup-project src/HpacSafety.Infrastructure \
  --output-dir Persistence/Migrations
dotnet ef database update \
  --project src/HpacSafety.Infrastructure \
  --startup-project src/HpacSafety.Infrastructure
```

The initial migration calls `QuestionBankSeedWriter.Write` and may seed the
fake `admin@localhost` user only when the PostgreSQL setting
`hpac.seed_development_admin=true` is present.
