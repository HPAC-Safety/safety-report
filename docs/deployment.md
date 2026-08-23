# Deployment

The target deployment is a small AWS environment in `ca-central-1`:

- one API service and one Worker service;
- RDS PostgreSQL with backups;
- private S3 attachment storage;
- separate public and admin static S3/CloudFront sites;
- Secrets Manager, Turnstile configuration, explicit migrations, and focused
  alerts for failed or stuck Worker work.

GitHub Actions assumes AWS roles through OIDC. Do not create long-lived AWS
access keys. Runtime secret values stay out of source control and Terraform
state. Use AWS-managed encryption at rest and TLS.

Migrations run as a dedicated step before the new API receives traffic.
Rollback redeploys a previously tested artifact; schema changes must support the
previous application during staged rollout. Backup restoration must be tested
before cutover.

The current Terraform and deploy workflows are scaffolding and still include
superseded combined-site and email resources. Issue #30 owns pruning them and
bringing the deployed topology to
[`spec/infrastructure-and-operations.md`](../spec/infrastructure-and-operations.md).
Do not interpret a successful Terraform validation as proof that the target
environment exists or has been applied.

Local development requires no AWS account:

```bash
docker compose up -d db
dotnet run --project src/HpacSafety.Api
dotnet run --project src/HpacSafety.Worker
```

Terraform formatting/validation commands remain documented in
[`infra/README.md`](../infra/README.md).
