---
name: manage-hpac-infrastructure
description: Apply HPAC safety-report's Terraform, AWS identity, Canadian residency, secrets, observability, email, topology, and drift constraints. Use when changing infra/, deployment workflows, OIDC roles, S3, CloudFront, Secrets Manager, regions, alarms, custom metrics, hostnames, or bootstrap behaviour.
---

# Manage the declared AWS environment

Read `infra/README.md`, `docs/deployment.md`, ADR-0031, and ADR-0032 before
editing infrastructure.

## Preserve identity and residency

- Never create a long-lived AWS credential, even temporarily. GitHub Actions
  assumes roles through OIDC; bootstrap uses an administrator's SSO session.
- Scope the deploy-role trust policy to this repository and one ref. Use a
  separate read-only pull-request role.
- Keep all report data in `ca-central-1`. Only a CloudFront ACM certificate may
  live in `us-east-1`, where it carries no report data.
- Create Secrets Manager entries in Terraform, never secret values or
  `aws_secretsmanager_secret_version` resources.

## Preserve operational contracts

- Send worker notifications and operational alarms to the one configured
  production address, `safety@hpac.ca`. Do not hardcode it or add a second
  address.
- Keep the `HpacSafety/SummaryFailed` count and
  `HpacSafety/OutboxOldestAgeSeconds` gauge synchronized between Worker and
  Terraform. The namespace arrives as `Metrics__Namespace`.
- Serve one website from one bucket and CloudFront distribution:
  `safety.hpac.ca` hosts `/` and `/admin/`; `api.hpac.ca` hosts the HTTPS API.
  Static admin assets contain no report data; API authorization protects the
  review queue.
- Require an unchanged `terraform apply` to be a no-op. Treat console changes
  after bootstrap as drift, not setup.

Pin Terraform in `infra/.terraform-version` and tflint in
`infra/.tflint-version`; scripts and workflows must read those pins rather than
copy their version numbers.
