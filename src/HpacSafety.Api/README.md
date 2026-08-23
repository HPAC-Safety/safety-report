# HpacSafety.Api

ASP.NET Core HTTP scaffold. It currently exposes only `/health`.

The intended report endpoint will load current question revisions, validate
only publication consent as mandatory, and save the submission DTO plus one
outbox message in one transaction. Admin endpoints will use normal configured
identity-provider authentication and an allowlist.

The API must not call the model, log report bodies, proxy HPAC passwords, or
perform media processing.

```bash
dotnet run --project src/HpacSafety.Api
```

Configuration uses `ConnectionStrings:HpacSafety` and
`HpacSafety:FieldEncryption:Key`.
