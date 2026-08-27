# HpacSafety.Infrastructure

Non-deployable adapters and persistence for the ports/use cases owned by Core,
API, and Worker.

## Target responsibilities

- EF Core schema, migrations, transactions, outbox claims, and query DTOs;
- one model adapter for strict bilingual summarization;
- current hardcoded-TLS HPAC authentication adapter and future OIDC adapter;
- Turnstile verification;
- private bounded stream storage and attachment detection/processing;
- image/video safe derivatives and validated private document originals.

Use managed encryption at rest and TLS. The target removes application AES,
runtime translation, a separate PII auditor, outbound email, external
publication adapters, and pre-submit upload slots.

Current-main mappings and adapters still include several of those superseded
pieces. Their required disposition is listed path-by-path in
[`../../docs/source-inventory.md`](../../docs/source-inventory.md).

```bash
dotnet ef migrations add <Name> \
  -p src/HpacSafety.Infrastructure -s src/HpacSafety.Infrastructure \
  -o Persistence/Migrations
dotnet ef database update \
  -p src/HpacSafety.Infrastructure -s src/HpacSafety.Infrastructure
```

Never let credentials, report values, client filenames, prompt/model payloads,
or private object URLs enter logs or exceptions.
