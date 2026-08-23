# Deployment

Production runs in AWS `ca-central-1` with one static site, one API service, one
Worker service, PostgreSQL, and one private optional-upload bucket. Terraform is
under `infra/`; GitHub Actions assumes AWS roles through OIDC.

## First deployment

1. Run `infra/bootstrap.sh` once from an administrator's AWS SSO session. It
   creates the encrypted Terraform state bucket and GitHub plan/deploy roles.
2. Run `terraform -chdir=infra init` and review `terraform plan`.
3. Apply Terraform through the protected production workflow.
4. Publish the ACM validation and application CNAME records shown by
   `terraform output -json dns_records_to_publish`.
5. Set these Secrets Manager values out of band:
   - `hpac-safety/connection-string` → `ConnectionStrings__HpacSafety`
   - `hpac-safety/model-api-key` → `Model__ApiKey`
6. Confirm the SNS subscription for the role mailbox that receives only the
   failed-summary and stuck-outbox alarms.
7. Deploy API, Worker, and web assets.

Secret values never belong in Terraform variables, GitHub variables, committed
configuration, or command output captured in a pull request.

## Deploy order

The API workflow builds and pushes an immutable commit-tagged image, runs the
one-off migration task, waits for success, and then updates the API service.
Migrations do not run during application startup. The Worker and web deploy
independently after CI succeeds on `main`.

## Rollback

- API or Worker: update the ECS service to the prior task-definition revision.
- Web: sync the prior commit's static files and invalidate CloudFront.
- Database: application migrations must be backward-compatible with the prior
  service revision. Restore RDS only for destructive data loss, not routine code
  rollback.

## Checks

- `/health` responds through the production API URL.
- site and admin routes load over HTTPS.
- private uploads reject anonymous reads.
- a synthetic report can be submitted and reaches review.
- model input/output content is absent from CloudWatch logs.
- `SummaryFailed` and `OutboxOldestAgeSeconds` alarms have recent data.

The deployment intentionally has no SES application mail, CAPTCHA resource,
media-processing pipeline, multiple publication channels, or speculative
autoscaling.
