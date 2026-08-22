# HpacSafety.Infrastructure

Everything that talks to the outside world. Implements the interfaces declared
in `HpacSafety.Core`.

**Not deployable.** A class library, referenced by Api and Worker.

## Contents

| Concern | Implements |
|---|---|
| Persistence | EF Core `DbContext`, entity configuration, migrations |
| Outbox | Claiming with `FOR UPDATE SKIP LOCKED`, backoff, poison handling |
| AI | `AnthropicSummarizer`, `AnthropicPiiAuditor`, `AnthropicTranslator` |
| Auth | `HpacMembersProxyAuthenticator` (and `OidcAuthenticator` later) |
| Storage | `S3BlobStore`, `FileSystemBlobStore` |
| Email | `SesEmailSender`, `SmtpEmailSender`, `LoggingEmailSender` |
| Spam | `TurnstileVerifier`, `NoOpTurnstileVerifier` |

## Why the seams exist

Hosting is AWS, so production registers `S3BlobStore` and an SES
`IEmailSender`; local development uses the filesystem and a logging mailer. The
seams still earn their keep — swapping a provider stays a registration change
rather than a rewrite, and the test suite runs against MinIO and the filesystem
without touching AWS.

`IMemberAuthenticator` exists because `members.hpac.ca` has no OAuth today. When
HPAC ships it, `OidcAuthenticator` replaces the proxy and nothing outside this
project changes. If something outside *does* need to change, the abstraction
leaked and that is a bug worth fixing before the migration.

## Migrations

```bash
dotnet ef migrations add <Name> -p src/HpacSafety.Infrastructure -s src/HpacSafety.Api
dotnet ef database update      -p src/HpacSafety.Infrastructure -s src/HpacSafety.Api
```

The API is the startup project; this library holds the model and the migrations.

## Handling credentials

`HpacMembersProxyAuthenticator` handles real HPAC member passwords. They live in
a local variable for one call — never persisted, cached, logged at any level, or
allowed into an exception message. Read
[`docs/authentication.md`](../../docs/authentication.md) before touching it.

## Related

- [`docs/architecture.md`](../../docs/architecture.md)
- [`docs/data-handling.md`](../../docs/data-handling.md)
