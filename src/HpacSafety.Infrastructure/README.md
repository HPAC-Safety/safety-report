# HpacSafety.Infrastructure

Everything that talks to the outside world. Implements the interfaces declared
in `HpacSafety.Core`.

**Not deployable.** A class library, referenced by Api and Worker.

## Contents

| Concern | Implements |
|---|---|
| Persistence | EF Core `DbContext`, entity configuration, migrations, seeding, field encryption — [`Persistence/README.md`](Persistence/README.md) |
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

## Persistence

This project owns **every table in the system** and every migration, including
tables whose behaviour lives elsewhere — `report_files` is defined here and
filled by the blob storage in #16.

```bash
dotnet ef migrations add <Name> \
  -p src/HpacSafety.Infrastructure -s src/HpacSafety.Infrastructure \
  -o Persistence/Migrations

dotnet ef database update \
  -p src/HpacSafety.Infrastructure -s src/HpacSafety.Infrastructure
```

This library is both the migrations project and the startup project:
`HpacSafetyDbContextFactory` builds the context for design-time tooling, so
scaffolding needs no running application and no deployment configuration.
`HPAC_SAFETY_CONNECTION` overrides the local default.

Detail — the tables, the encryption, and the seeding — is in
[`Persistence/README.md`](Persistence/README.md).

## Configuration

| Setting | What it is |
|---|---|
| `ConnectionStrings:HpacSafety` | The database. Empty in `appsettings.json` so nothing falls back to a database somebody did not mean to write to. |
| `HpacSafety:FieldEncryption:Key` | Base64 256-bit key for encrypted report values. A throwaway literal in `appsettings.Development.json`, a Secrets Manager reference in production. |

`AddHpacSafetyPersistence(configuration)` registers both, and refuses to start
without either. A missing key is never a quiet fallback to storing report
text in the clear.

The development connection string also carries
`Options=-c hpac.seed_development_admin=true`, which is what opts a local
database in to the seeded `admin@localhost` administrator. See
[ADR-0020](../../docs/decisions/ADR-0020-seeding-by-migration.md).

## Handling personal data

Every report answer value is encrypted with AES-256-GCM by this project before
PostgreSQL sees it, through `IFieldCipher` — a port declared in `Core` so that
`Core` still depends on nothing. `IsPrivate` controls whether an answer becomes
model-only redaction context, not encryption. A database dump is inert without
the key.

Read [`docs/data-handling.md`](../../docs/data-handling.md) and
[ADR-0019](../../docs/decisions/ADR-0019-application-side-field-encryption.md)
before touching anything under `Persistence/Encryption`.

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
