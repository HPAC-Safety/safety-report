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
| Storage | `S3BlobStore`, `FileSystemBlobStore` — see [`Storage/`](Storage/README.md) |
| Media | `MagickNetMediaSniffer`, `MagickNetExifStripper` — see [`Media/`](Media/README.md) |
| Email | `SesEmailSender`, `SmtpEmailSender`, `LoggingEmailSender` |
| Spam | `TurnstileVerifier`, `NoOpTurnstileVerifier` |

## Why the seams exist

Hosting is AWS, so production registers `S3BlobStore` and an SES
`IEmailSender`; local development uses the filesystem and a logging mailer. The
seams still earn their keep — swapping a provider stays a registration change
rather than a rewrite, and the test suite runs against MinIO and the filesystem
without touching AWS.

Uploaded media is the sharpest case. A browser PUTs straight to a private bucket
through a pre-signed URL scoped to one key, ingest strips the EXIF, and a
reviewer is shown the derivative through a pre-signed GET — **no code path here
hands blob bytes to the API**. The filesystem store signs its URLs exactly as S3
does, because a development stand-in that skips the guarantee is how the
guarantee stops being tested. See
[ADR-0025](../../docs/decisions/ADR-0025-magick-net-for-exif-stripping.md) and
[ADR-0026](../../docs/decisions/ADR-0026-presigned-urls-and-private-blob-storage.md).

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

## Tests

`tests/HpacSafety.Infrastructure.Tests`. Anything needing a container carries
`[Trait("Category", "Integration")]`:

```bash
dotnet test tests/HpacSafety.Infrastructure.Tests --filter "Category!=Integration"
```

## Handling credentials

`HpacMembersProxyAuthenticator` handles real HPAC member passwords. They live in
a local variable for one call — never persisted, cached, logged at any level, or
allowed into an exception message. Read
[`docs/authentication.md`](../../docs/authentication.md) before touching it.

## Related

- [`docs/architecture.md`](../../docs/architecture.md)
- [`docs/data-handling.md`](../../docs/data-handling.md)
