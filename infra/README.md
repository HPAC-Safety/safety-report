# Infrastructure

Terraform for the minimal AWS deployment in `ca-central-1`:

- one static site bucket and CloudFront distribution;
- one private optional-upload bucket;
- one small API service and one small Worker service on ECS;
- PostgreSQL on RDS;
- ECR, networking, log groups, and Secrets Manager entries;
- two Worker alerts: summarization failure and stuck outbox.

There is no SES application mail, CAPTCHA resource, media quarantine pipeline,
or speculative scaling. Alert delivery through SNS is operational monitoring,
not application email.

```bash
./infra/bootstrap.sh
terraform -chdir=infra init
terraform -chdir=infra plan
```

Bootstrap creates the state bucket and GitHub OIDC roles once. Terraform creates
secret entries but never secret values. Before deployment, set the database
connection string and model API key with `aws secretsmanager put-secret-value`.
Deployments run migrations explicitly before the new API revision receives
traffic.
