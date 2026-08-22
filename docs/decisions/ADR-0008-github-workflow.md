# ADR-0008 — Rulesets, and no CODEOWNERS

**Status:** Accepted

## Context

Development happens in public — anyone may fork and open a pull request — but
only the two administrators (`ChaseFlorell`, `jopekar`) should be able to
approve. Managing who can approve should not itself require a pull request.

## Decision

**Rulesets**, not classic branch protection: one required approval, squash-only,
linear history, stale approvals dismissed on push, `require_last_push_approval`,
conversation resolution required, and repository **admin as a bypass actor**.

**No `CODEOWNERS` file.**

## Why no CODEOWNERS

GitHub counts an approving review toward the required count only from a reviewer
with **write or admin** permission. The repository access page is therefore
already the approver roster: grant write and someone can approve, revoke and
they cannot.

A `CODEOWNERS` file would move that control into a committed file, so every
reviewer change would need a PR — precisely the opposite of what is wanted.

An outside contributor may leave an approving review; it does not count, and the
PR stays blocked until an administrator approves. No extra configuration is
needed to achieve "owner or administrator approval".

## Consequences

- **Never grant `write` to a drive-by contributor** — write access *is* approval
  power. Use `triage` for issue and label management.
- Admin bypass exists because with two members, a solo author would otherwise be
  unable to land anything when the other is unavailable. The default path for
  every PR remains a real review.
- Fork PRs run untrusted code: `pull_request` (never `pull_request_target`), no
  secrets in PR-triggered jobs, model-dependent tests skipped on forks in favour
  of recorded fixtures.
- Required status checks are added by the CI issue, once `ci.yml` exists to
  produce them. A required context that never reports blocks every PR forever.
