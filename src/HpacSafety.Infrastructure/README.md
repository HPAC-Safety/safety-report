# HpacSafety.Infrastructure

EF Core and PostgreSQL persistence for the domain model. It owns the database
schema, the initial bilingual question seed, and application-side encryption of
nullable answer text.

Register it with `AddHpacSafetyPersistence(configuration)`. Required settings:

- `ConnectionStrings:HpacSafety`
- `HpacSafety:FieldEncryption:Key` (base64 256-bit key)

Production keeps both values in Secrets Manager. The app has no model-audit
adapter, but does own the other infrastructure boundaries: [Media](Media/README.md)
(EXIF stripping, content sniffing) and [Storage](Storage/README.md) (private
blob storage) implement the media ports; mail, translation, and Turnstile
adapters are added when the Api and Worker are implemented.

See [Persistence/README.md](Persistence/README.md) for migrations.
