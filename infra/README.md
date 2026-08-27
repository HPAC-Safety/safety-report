# Infrastructure

Terraform for the target AWS environment in `ca-central-1`. The intended
topology is one small API, one small Worker, RDS PostgreSQL, private attachment
S3, and separate public/admin S3+CloudFront sites. GitHub Actions assumes roles
through OIDC; migrations run explicitly; backups and focused Worker alerts are
required.

The current Terraform has never been applied and still contains superseded
combined-site, SES/email, and legacy upload assumptions. Issue #30 owns pruning
it to [`features/infrastructure-and-operations/infrastructure-and-operations.md`](../features/infrastructure-and-operations/infrastructure-and-operations.md).

The one-time bootstrap creates only the resources needed before Terraform can
authenticate: GitHub OIDC roles and the remote state bucket. Secret values stay
out of Terraform state; application data uses AWS-managed encryption and TLS.

Credential-free local checks:

```bash
terraform -chdir=infra fmt -check -recursive -diff
terraform -chdir=infra init -backend=false -lockfile=readonly
terraform -chdir=infra validate
tflint --chdir=infra --init
tflint --chdir=infra
shellcheck -s sh infra/bootstrap.sh
```

Do not add SES, outbound notifications, public attachment delivery, speculative
scaling, or long-lived AWS keys. Before cutover, apply through the protected
production environment and test database backup restoration.
