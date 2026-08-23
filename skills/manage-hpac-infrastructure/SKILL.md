---
name: manage-hpac-infrastructure
description: Maintain HPAC Safety's minimal Canadian AWS, Terraform, deployment, secrets, backups, and focused Worker alerts. Use for infrastructure or operations changes.
---

# Manage HPAC Safety infrastructure

Target one small API service, one small Worker service, RDS PostgreSQL, private
S3 attachment storage, and separate public/admin static S3+CloudFront sites in
`ca-central-1`. Use Terraform and GitHub OIDC; never create long-lived AWS keys.

- Use AWS-managed encryption at rest and TLS.
- Keep runtime secret values in Secrets Manager and out of Terraform state and
  GitHub where deployment does not need them.
- Run database migrations explicitly before application rollout and retain
  tested backups.
- Quarantine unreferenced uploads with lifecycle expiry; keep report-linked
  objects private.
- Alert on terminal summary failures and stuck/aged outbox work. Keep logs
  content-free.
- Preserve least privilege and separate public/admin static origins.

Remove SES/email resources, combined-site assumptions, external publication
integrations, speculative scaling, and secrets or alarms that exist only for
retired features. Validate formatting, static security, and a credential-free
plan path in CI where possible.
