# ADR-0012 — Every pull request closes an issue, enforced in CI

**Status:** Accepted

## Context

The backlog is how this project sequences work: issues carry milestones, labels,
and native GitHub dependency edges so that nothing gets implemented out of
order. That only holds if the issues actually close when the work lands. An
issue left open after its pull request merges makes the dependency graph lie,
and the graph is the thing an agent reads to decide what to pick up next.

The requirement was: every completed pull request closes its issue,
automatically, forced if necessary.

**There is no GitHub setting for this.** Auto-close is driven by one mechanism
only — a closing keyword (`Closes`, `Fixes`, `Resolves`) followed by an issue
reference, in the merged commit message or in the pull request body. It cannot
be configured on, defaulted, or required through repository settings, rulesets,
or branch protection.

## Decision

**Enforce the keyword in the pull request body, with a required CI check.**

- `linked-issue` in `.github/workflows/ci.yml` fails when the body contains no
  `(Closes|Fixes|Resolves) #N`, and is one of the required status checks.
- The pull request template carries `Closes #` with a comment explaining why.
- `AGENTS.md` and `CONTRIBUTING.md` both state the rule.

The body is the right place rather than the commit message because the
repository merges with `squash_merge_commit_message=PR_BODY` — the body *becomes*
the commit message, so enforcing it once covers both paths.

`renovate[bot]`, `dependabot[bot]`, and `github-actions[bot]` are exempt. Their
pull requests legitimately have no issue behind them, and failing a required
check on them would deadlock the automerge configured in ADR-0011.

## Alternatives considered

**A merge-time action that closes the issue by API.** Would work without the
keyword, but it needs a token with `issues: write` on a `pull_request` trigger,
which is exactly the permission a public repository should not hand to
fork-authored workflows. Rejected on that alone.

**A branch-naming convention (`issue-123/...`) parsed by a workflow.** Adds a
second source of truth for the same link, and says nothing in the pull request
itself where a reviewer reads it.

**Trust the template.** A template is a suggestion. The `Closes #` line already
existed in the template before this change, and was routinely left as a bare
`Closes #`.

## Consequences

- A change with no issue behind it cannot merge. That is intended: opening the
  issue first is how the dependency graph stays complete. It does add a step for
  a one-line typo fix.
- The check reads `github.event.pull_request.body` from the event payload, so it
  needs no permission beyond `contents: read` and is safe on fork pull requests.
- It lives in its own workflow, `.github/workflows/linked-issue.yml`, rather than
  in `ci.yml`. It has to subscribe to `pull_request: types: [edited]` so that
  fixing the body re-runs the check — and putting that trigger on `ci.yml` would
  rebuild and re-test the whole solution every time someone touched a word of a
  description.
- Bot exemption is by login. A new bot needs adding to that list.
