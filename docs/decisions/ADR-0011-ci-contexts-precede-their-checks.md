# ADR-0011 — CI contexts exist before the things they check

**Status:** Accepted

## Context

The `main` ruleset deliberately shipped without `required_status_checks`,
because a required context that never reports blocks every pull request
permanently. Adding the checks was the closing step of the CI issue.

But CI has seven jobs and only three of them have anything to verify today:

| Job | Verifies | Written by |
|---|---|---|
| `build` | The solution compiles warning-free | exists |
| `test` | xUnit and `node:test` pass | exists |
| `agent-config` | Symlinks resolve, `skillfile` is clean | exists |
| `coverage` | Threshold gate and ratchet | #5 |
| `web` | `tools/build-css.sh` produces `site.css` | #10 |
| `e2e` | Playwright suite | #26 |
| `i18n` | Locale parity, hardcoded-string lint | #8 |

Renovate is configured with `autoApprove` and `platformAutomerge`, which holds a
pull request until required checks pass. With no required checks, auto-merge has
nothing to wait for and would merge a dependency bump the moment approval lands.
So required checks are the gate on turning Renovate back on, and Renovate is the
thing keeping the dependency set current.

## Decision

**Define all seven jobs now. The four with nothing to verify detect the missing
artifact, emit a `::notice::` naming the issue that fills them in, and exit 0.**

All seven become required status checks in a single ruleset change.

Two of them are not purely inert. `i18n` fails — rather than skipping — if
`locales/en-CA.json` exists without `tools/check-locales.mjs`, so the skip
cannot outlive the reason for it. `coverage` already collects and uploads
Cobertura; #5 adds the comparison, not the plumbing.

## Alternatives considered

**Add only `build`, `test`, and `agent-config`; let each later issue add its own
context.** Honest — every green check would mean something. Rejected because it
puts four more edits to the live ruleset on the critical path, each one a chance
to typo a context name into a permanently-blocking check, and because it leaves
Renovate blocked on the last of them rather than the first.

**Define all seven and let the unbuilt ones fail.** Rejected outright: they
could not be required, so nothing changes, and every pull request carries four
red checks that everyone learns to ignore. A permanently-red check trains people
to merge past red.

## Consequences

- Four checks report green while verifying nothing. This is a real cost: a
  reviewer glancing at the check list gets more confidence than is warranted.
  The `::notice::` in each run is what makes it visible rather than silent.
- Each of #5, #8, #10, and #26 must **replace** its job's skip branch, not add a
  second job. Adding a job renames the context and silently drops it from the
  required set — the ruleset would go on waiting for a name nothing reports.
- Job ids are the context names. Renaming a job is a ruleset change.
- **Renovate's `automerge` does not go back on in the same pull request that
  lands CI.** It first looked like it should, but the ruleset is applied by hand
  after that pull request merges — it cannot be applied before, or the pull
  request blocks itself on contexts that have never reported. That leaves a
  window with no required checks, and a Renovate pull request opening inside it
  would have nothing to wait for and would merge on its own approval. The flag
  is flipped separately, after the ruleset is confirmed live: #35.

## Superseded when

All four skip branches are gone. At that point this ADR is history rather than
policy, and nothing needs to replace it.
