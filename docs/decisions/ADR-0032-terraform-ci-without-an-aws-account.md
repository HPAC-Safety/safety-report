# ADR-0032 — Two roles, and a check that works without an AWS account

**Status:** Accepted for OIDC roles and credential-free validation. Any SES or
combined-site examples below are superseded by the
[infrastructure specification](../infrastructure-and-operations.md).

## Context

Three requirements from [#32](https://github.com/HPAC-Safety/safety-report/issues/32)
and [ADR-0010](ADR-0010-infrastructure-as-code.md) do not fit together on first
reading:

1. `terraform plan` runs **on pull requests** and is posted as a comment. It is
   named as the single most useful review artifact in the whole deployment story.
2. The deploy role's trust policy names this repository **and
   `ref:refs/heads/main` specifically**. A role trusting the organisation, or
   trusting every ref, would let any repository or any branch — including one
   pushed to a fork — deploy a system holding personal information about real
   accidents.
3. The Terraform lands **before the AWS account exists**. There is no account, no
   credentials, and the human-only steps in #32's Phase 1 take days.

(1) and (2) are in direct conflict. A `pull_request`-triggered workflow presents
the OIDC subject `repo:HPAC-Safety/safety-report:pull_request` — not a branch
ref — so it cannot assume a role scoped to `refs/heads/main`. And (3) means
whatever is wired up has to produce a *meaningful* result on a pull request
opened today.

## Decision

### Two roles, not one widened one

| Role | Trusts | May |
|---|---|---|
| `hpac-safety-deploy` | `repo:HPAC-Safety/safety-report:ref:refs/heads/main` | Manage the environment. Push images. Update services. |
| `hpac-safety-plan` | `repo:HPAC-Safety/safety-report:pull_request` | Read. Nothing else. |

Both are created by `infra/bootstrap.sh`. The deploy role's trust policy is
unchanged from what #32 requires.

`hpac-safety-plan` carries the AWS managed `ReadOnlyAccess`, plus read on the
state bucket, plus **three explicit denials**:

- `s3:GetObject` on `hpac-safety-uploads-*` — those objects are photographs of
  crash sites. `ReadOnlyAccess` alone would hand every one of them to any plan
  running on any pull request.
- `secretsmanager:GetSecretValue`.
- `rds-data:*` and the RDS log-download actions.

`Deny` beats `Allow` unconditionally, so those hold no matter what AWS adds to
`ReadOnlyAccess` later. That last clause is the reason they are written out even
where the managed policy does not currently grant the action.

`terraform plan` on a pull request runs with `-lock=false`: the S3 backend takes
its lock by writing a `.tflock` object into the state bucket
([ADR-0031](ADR-0031-terraform-shape-and-topology.md)), the plan role can read
that bucket but not write to it, and a plan writes no state anyway. The lock
exists to stop two *applies* overlapping.

**Rejected: widen the deploy role to `repo:HPAC-Safety/safety-report:*`.** One
condition, no second role, and it is exactly what #32 forbids — it would let any
branch anyone can push assume a role that can change production.

**Rejected: no plan on pull requests; plan and apply together on main.** Keeps
one role, and throws away the artifact ADR-0010 chose Terraform *for*. A plan
read after the merge is a changelog, not a review.

**Rejected: `pull_request_target`.** It would give the run `main`'s context and
its secrets. On a public repository that takes fork pull requests, running
fork-authored Terraform with a role attached is the worst option available.

### Fork pull requests get validation, not a plan

GitHub withholds secrets and issues a read-only token to fork-triggered runs, so
a fork cannot assume either role. The `plan` job checks
`head.repo.full_name == github.repository` explicitly anyway and skips with a
notice — "GitHub happens not to hand the secret over" is not a boundary a
reviewer should have to look up.

### `infra` is the required check; `plan` and `apply` skip when unconfigured

The `infra` job runs `terraform fmt -check`, `terraform init -backend=false`,
`terraform validate`, `tflint`, `shellcheck` over `bootstrap.sh`, and
`node --check` over the CloudFront Function. **None of it touches AWS**, so it
works today, on a fresh clone, with no account. It is a required status check.

`plan` and `apply` detect that `AWS_PLAN_ROLE_ARN` / `AWS_DEPLOY_ROLE_ARN` and
`TF_STATE_BUCKET` are absent, emit a `::notice::` naming #32, and exit 0.

This is [ADR-0011](ADR-0011-ci-contexts-precede-their-checks.md)'s reasoning
applied to the same problem: a permanently-red check trains people to merge past
red, and once that habit exists the next red check — a real one — is merged past
too. The `::notice::` is what keeps the gap visible rather than silent.

**Rejected: let `plan` fail until the account exists.** Every pull request would
carry a red check that everyone learns to ignore, for however many weeks the SES
sandbox exit and HPAC's DNS take.

**Rejected: omit the workflow until the account exists.** Then the workflow
itself is never exercised before the day it matters, which is the deploy blind
spot `.github/workflows/README.md` already describes and #36 already
demonstrated.

### `apply` asserts its own idempotency

After applying, the job runs `terraform plan -detailed-exitcode` again and fails
if the result is non-empty. ADR-0010 requires apply on an unchanged repository to
be a no-op; this makes that an assertion rather than an aspiration. It is what
caught `final_snapshot_identifier` — a `timestamp()` in a name would have made
every plan dirty forever.

### The `terraform.yml` workflow has no `paths:` filter

`infra` is a required context. A required context that never reports blocks a
pull request permanently, and a `paths: ['infra/**']` filter would do exactly
that to every pull request that does not touch `infra/`. This is the trap
ADR-0011 exists to describe. The job runs in well under a minute.

## Consequences

- **There are two GitHub secrets now**, not one: `AWS_DEPLOY_ROLE_ARN` and
  `AWS_PLAN_ROLE_ARN`. Neither is a credential — both are inert without their
  trust policies. `docs/deployment.md` carries the contract.
- `infra` reports green on a pull request while `plan` says nothing about AWS.
  That is a real cost, the same one ADR-0011 accepted: a reviewer sees a green
  list and may read more assurance into it than is there. It ends when the
  account exists.
- The plan role is read-only by construction, so a plan can never be the thing
  that changes production — which also means a plan of a *creation* is all a
  reviewer gets to see before an apply that is behind a required reviewer anyway.
- Running the bootstrap again is how the trust policies change. Both are
  converged on every run, not skipped when the role already exists.

## Related

- [ADR-0010](ADR-0010-infrastructure-as-code.md), [ADR-0011](ADR-0011-ci-contexts-precede-their-checks.md)
- [ADR-0031](ADR-0031-terraform-shape-and-topology.md)
- `infra/README.md`, `docs/deployment.md`, `.github/workflows/README.md`
