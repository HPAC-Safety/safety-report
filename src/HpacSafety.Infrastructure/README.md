# HpacSafety.Infrastructure

EF Core and PostgreSQL persistence for the domain model. It owns the database
schema, the initial bilingual question seed, and application-side encryption of
nullable answer text.

Register it with `AddHpacSafetyPersistence(configuration)`. Required settings:

- `ConnectionStrings:HpacSafety`
- `HpacSafety:FieldEncryption:Key` (base64 256-bit key)

Production keeps both values in Secrets Manager. The app does not contain mail,
CAPTCHA, media-processing, translation, or model-audit adapters.

See [Persistence/README.md](Persistence/README.md) for migrations.
