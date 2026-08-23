# ADR-0008 — Rulesets, and no CODEOWNERS

**Status:** Accepted

## Context

Development happens in public — anyone may fork and open a pull request — but
only the repository administrators should be able to
approve. Managing who can approve should not itself require a pull request.

## Decision

**Rulesets**, not classic branch protection: one required approval, squash-only,
linear history, stale approvals dismissed on push, conversation resolution
required, and repository **admin as a bypass actor**.

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

## Renovate auto-approval, and why `require_last_push_approval` is off

Renovate is configured to approve and automerge its own patch and minor updates
(`autoApprove` + `automerge` in `renovate.json`). For that to work at all,
**`require_last_push_approval` had to be turned off.**

That setting means an approval from the person who made the most recent push
does not count. Renovate is both the pusher and the approver on its own PRs, so
with the setting on, every dependency PR would sit blocked forever — the bot
cannot satisfy a rule that exists to stop people approving their own work.

What we keep instead: **`dismiss_stale_reviews_on_push` stays on**, which covers
the risk that actually matters — an approval granted, then further commits
pushed underneath it. New commits dismiss the approval and it has to be granted
again. The two settings overlap heavily; the one retained is the one doing the
real work.

What we lose: a human approver can now also be the last pusher on a PR they
approved. With two administrators, both of whom review each other's work, that
is an acceptable narrowing.

The alternative was adding Renovate as a ruleset **bypass actor**, which was
rejected: bypassing the pull-request rule also bypasses required status checks,
so a dependency bump could merge with a red build. Auto-approval keeps the
checks binding.

`platformAutomerge` is on, so GitHub's own auto-merge holds the PR until
required checks pass rather than Renovate merging directly, and
`automergeStrategy` is `squash` because that is the only merge method the
ruleset permits.

**Until required status checks exist (see the CI issue), auto-merge has nothing
to wait for and will merge as soon as the approval lands.** Install the Renovate
app after CI is in place, not before.

## Consequences

- **Never grant `write` to a drive-by contributor** — write access *is* approval
  power. Use `triage` for issue and label management.
- The Renovate app is the one deliberate exception: it holds write so its
  approvals count. It approves only its own dependency PRs, only patch and
  minor, and only behind green CI.
- Majors are labelled `breaking-change` and always reviewed by a human.
- Admin bypass exists because with two members, a solo author would otherwise be
  unable to land anything when the other is unavailable. The default path for
  every PR remains a real review.
- Fork PRs run untrusted code: `pull_request` (never `pull_request_target`), no
  secrets in PR-triggered jobs, model-dependent tests skipped on forks in favour
  of recorded fixtures.
- Required status checks are added by the CI issue, once `ci.yml` exists to
  produce them. A required context that never reports blocks every PR forever.
