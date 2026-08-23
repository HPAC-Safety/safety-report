# ADR-0010 — Terraform, with a scripted one-time bootstrap

**Status:** Accepted. The state-locking clause is **superseded** by
[ADR-0031](ADR-0031-terraform-shape-and-topology.md): locking is S3-native
(`use_lockfile`), and there is no DynamoDB table.

## Context

Hosting is AWS (ADR-0009). The environment should be reproducible and set up by
running a build rather than by following a click-path, so that a second
environment — or a rebuild after a mistake — is not an archaeology exercise.

There is one irreducible constraint: **a workflow cannot create the thing that
lets it authenticate.** The GitHub OIDC provider, the deploy role, and the
Terraform state backend must exist before any CI run can reach AWS at all.

## Decision

**Terraform**, in `infra/`, run from GitHub Actions.

- `terraform plan` on pull requests, posted as a PR comment
- `terraform apply` on merge to `main`, behind a `production` environment with a
  required reviewer
- State in S3 with ~~a DynamoDB lock table~~ — superseded: an S3-native lock
  object, `use_lockfile` (ADR-0031)

**A committed, idempotent `infra/bootstrap.sh`** creates the OIDC provider, the
deploy role, and the state backend. (It also creates a second, read-only role for
`terraform plan` on pull requests — see
[ADR-0032](ADR-0032-terraform-ci-without-an-aws-account.md) for why one role
cannot serve both.) An administrator runs it once, against their
own SSO session.

## Why Terraform

`terraform plan` posted on a pull request is a readable diff of exactly what will
change in AWS, before it changes. On a system holding personal information about
real accidents, that review artifact is worth more than any other property of
the tooling.

CDK in C# was the close alternative and would have kept one language across the
repository, but `cdk diff` is noticeably noisier to review, and CDK's own
bootstrap stack adds a second bootstrap concept on top of the one this design
already requires.

CloudFormation was rejected for verbosity and weak drift handling.
Console-and-a-runbook was rejected because nothing is idempotent or reviewable,
and the documentation starts drifting from reality on day one.

## Why bootstrap locally rather than from a workflow

The alternative was putting a temporary admin access key in GitHub secrets,
running a bootstrap workflow, then revoking it. That would automate the last 10
minutes of manual work at the cost of creating a long-lived admin credential and
placing it in a secret store.

Running a committed script against an administrator's existing session achieves
the same result with **no long-lived AWS credential ever existing** — not
during bootstrap, not after. The script is idempotent and re-runnable, so it is
as reproducible as the Terraform it enables.

## Consequences

- One manual step, roughly ten minutes, once per environment. Everything after
  it is a build.
- `terraform apply` on an unchanged repository must be a no-op. If it is not,
  something drifted.
- **Nothing is created by hand after bootstrap.** A console click Terraform does
  not know about becomes drift, and drift makes the plan untrustworthy — at
  which point people stop reading it, and the review artifact this decision was
  made for is lost.
- Terraform creates Secrets Manager **entries**, never their **values**. A value
  in a `.tfvars` file ends up in state, and state is a file in S3 readable by
  more people than should see an API key.
- **One deliberate exception:** `cloudflare_turnstile_widget` exposes the
  widget's secret as an attribute, so managing the widget in Terraform puts that
  secret in state. It is accepted because a leaked Turnstile secret is contained
  — it verifies tokens for one widget, and grants no spend and no data access —
  and because the alternative is creating the widget by hand and losing
  idempotency. It is also the concrete reason the state bucket's encryption and
  access restrictions must be verified rather than assumed.
- Terraform also manages Cloudflare, sharing the same state and the same
  plan-on-PR workflow. Cloudflare has no OIDC equivalent, so it needs a
  long-lived API token — scoped to Turnstile Edit on one account, with no zone
  permissions, so it cannot touch DNS if it leaks.
- Some AWS-side steps stay manual because they need a human or another
  organisation: account creation, SES production access, and DNS records on
  `hpac.ca`. These are listed in the bootstrap issue and should be started early,
  since they take days rather than minutes.

## Related

- `docs/decisions/ADR-0009-hosting-on-aws.md`
- `docs/data-handling.md`
