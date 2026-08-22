# ADR-0031 — One Terraform root module, and who owns which field

**Status:** Accepted

## Context

[ADR-0010](ADR-0010-infrastructure-as-code.md) chose Terraform and a scripted
bootstrap. It did not say how the Terraform is laid out, and several of those
choices are ones somebody will otherwise re-litigate: how many environments
there are, who owns an ECS image tag, where a tool version is pinned, and what
happens to a resource whose value has to come from outside AWS.

This ADR records those. The plan-on-pull-request mechanics and what can be
verified without an AWS account are [ADR-0032](ADR-0032-terraform-ci-without-an-aws-account.md).

## Decision

### One root module in `infra/`, one environment

Flat `.tf` files at `infra/`, split by area — `network.tf`, `database.tf`,
`ecs.tf` — with no `modules/` directory and no workspaces.

There is one environment: production. HPAC receives dozens of reports a year and
has two administrators. A staging environment would double the RDS and Fargate
bill and, more to the point, would be the place someone pastes real report data
to reproduce a bug.

**Rejected: Terraform workspaces.** They share one configuration across
environments, so the difference between environments is invisible in the code
and lives in whoever remembers which workspace they are in. With one environment
they buy nothing and add a way to apply to the wrong one.

**Rejected: `environments/production/` plus `modules/`.** The standard shape for
a repository with three environments. With one, every module is called exactly
once, so the module boundary carries no reuse and every value makes two hops —
variable in, output out — for a reader to follow.

**Rejected: Terragrunt.** A second tool, a second DSL, and a second thing to keep
current, to solve a duplication problem that does not exist at one environment.

If a second environment is ever wanted, the extraction is mechanical and this ADR
is superseded rather than worked around.

### Terraform owns the shape of a task definition; the deploy workflow owns the revision

```mermaid
flowchart LR
    tf["terraform apply"] -->|"family, roles, ports,<br/>secret ARNs, log config"| td["task definition"]
    dep["deploy-api.yml"] -->|"new revision:<br/>image tag = CI's head_sha"| td
    dep -->|"update-service"| svc["ECS service"]
    tf -.->|"ignore_changes"| svc
```

The services declare `ignore_changes = [task_definition, desired_count]` and the
task definitions `ignore_changes = [container_definitions]`.

Without that, the two fight: a deploy sets the image to a commit SHA, the next
`terraform plan` reads it as drift, and `apply` rolls production back to
whatever tag is written in `ecs.tf`. Both tools would be correct and the result
would be a silent rollback on an unrelated infrastructure change.

**Rejected: Terraform sets the image tag from a variable.** Every deploy becomes
a Terraform apply behind a required reviewer, which turns a rollback — the thing
that has to be fast — into an approval queue.

**Rejected: the deploy workflow renders the whole task definition.** Then the
secret ARNs, the log group names, and the task role live in a workflow's YAML
rather than beside the resources they name, and Terraform no longer knows what
is deployed at all.

### No secret value, and no generated credential, reaches state

ADR-0010 already forbids Secrets Manager values in `.tfvars`. Two things follow
that are worth writing down because they look like omissions:

- There is no `aws_secretsmanager_secret_version` resource anywhere in `infra/`.
  A task whose secret has no value **fails to start**, loudly, in the ECS event
  log. That is correct — an API booting without its Turnstile key would accept
  unverified submissions — and it makes `put-secret-value` part of first deploy
  rather than a later tidy-up.
- The RDS master password uses `manage_master_user_password`, so RDS generates
  and rotates it into its own secret. Terraform never receives it. The
  alternative, `random_password`, writes the password into state in plaintext.

### A tool version is pinned in exactly one file

`infra/.terraform-version` pins Terraform; `infra/.tflint-version` pins tflint;
`.tflint.hcl` pins the AWS ruleset. The `infra` CI job reads each from its file.

`versions.tf` therefore has **no `required_version`**, and tflint's
`terraform_required_version` rule is disabled with a comment saying why. That
looks like a missing best practice, and it is a deliberate application of the
rule in `AGENTS.md`: a version written in two files is a version that will
drift, and the drift shows up as a contributor whose local run disagrees with CI
for no visible reason. The same rule is why `init-dev.sh` reads the .NET version
out of `global.json`. See [ADR-0015](ADR-0015-one-shell-script-for-development-setup.md).

The AWS **provider** version is a constraint (`~> 6.61`) plus a committed
`.terraform.lock.hcl` holding checksums for five platforms. That is one pin — the
lock file — with a range above it, which is how Terraform is designed to work.

### DNS on `hpac.ca` is an output, not a resource

`hpac.ca` is administered by HPAC, outside this AWS account, so Terraform cannot
create a Route 53 record for it. Every record a human must publish — SES
verification, three DKIM CNAMEs, the MAIL FROM MX and SPF TXT, DMARC, ACM
validation, and the CloudFront and ALB aliases — is collected into the single
`dns_records_to_publish` output, because an external dependency on another
organisation takes days and a value nobody can find takes longer.

The `aws_acm_certificate_validation` resources deliberately **block**, for up to
two hours, until those records appear. The alternative is a listener serving a
certificate that was never validated.

### One NAT gateway, not one per availability zone

A NAT gateway is the largest fixed cost here after RDS. One is a single point of
failure for **outbound** traffic only: report submission (viewer → CloudFront, or
viewer → ALB → API → RDS) never traverses it. Losing that AZ delays
summarization, which is already asynchronous behind the outbox, until the NAT is
recreated.

### The state lock is still DynamoDB, and that is now deprecated

`bootstrap.sh` creates the DynamoDB lock table and `backend.tf` uses
`dynamodb_table`, as ADR-0010 and #32 specify. Terraform 1.11 deprecated that
parameter in favour of `use_lockfile` — S3-native conditional-write locking, no
second service — and 1.13 onwards prints a deprecation warning on every `init`.

Migrating is a one-line change plus deleting a table, but it is a change to an
accepted decision, so it is recorded here rather than made in passing. It should
be done before the parameter is removed.

## Consequences

- Adding a second environment means extracting modules, and supersedes this ADR.
- A reviewer reading `ecs.tf` sees `:latest` as the image and must know it is
  never what is running. The comment at the top of that file says so; this is the
  cost of the split above.
- The first `terraform apply` on a fresh account does **not** produce a working
  system on its own. Nothing has been pushed to ECR and no secret has a value, so
  the services do not stabilise until the first deploy and the first
  `put-secret-value`. The order is in `docs/deployment.md`; it is a property of
  bootstrapping, not a defect.
- `terraform init` prints a deprecation warning until the lock mechanism moves.

## Related

- [ADR-0009](ADR-0009-hosting-on-aws.md), [ADR-0010](ADR-0010-infrastructure-as-code.md)
- [ADR-0032](ADR-0032-terraform-ci-without-an-aws-account.md)
- `infra/README.md`, `docs/deployment.md`
