---
title: A same-repo pull request gets its French translated onto its own branch; a fork PR still waits until after merge
description: Add a pull_request_target trigger to i18n-translate.yml, gated to github.event.pull_request.head.repo.full_name == github.repository.
type: adr
status: accepted
date: 2026-09-20
decision-makers: Chase Florell
keywords: i18n, DeepL, pull_request_target, security boundary, ADR-0021
---

# ADR-0057 — A same-repo pull request gets its French translated onto its own branch; a fork PR still waits until after merge

**Status:** Accepted. Extends
[ADR-0021](ADR-0021-ci-translation-opens-a-pull-request.md); the security
boundary that ADR describes (`pull_request` never translates, only a push to
`main` does) is unchanged for fork-authored pull requests. This ADR adds a
second, narrower path for pull requests opened from a branch of this
repository itself.

## Context

ADR-0021 made "generate French" exclusively a post-merge action: a pull
request adds English keys, CI only verifies (`translate-locale.mjs
--check`), and the real translation happens in a separate
`i18n-translate.yml` run after the PR merges to `main`, which opens its own
`chore/fr-CA-translations` pull request.

That gap between "English key merges" and "French key exists" means every
ordinary feature PR that adds a new string goes red on multiple checks for
a reason that isn't a defect: `check-locales.mjs` (the `i18n` job and the
pre-commit hook), the `web` job's build-drift check (the local `#`-stub
mechanism from [ADR-0054](ADR-0054-local-build-stubs-missing-translations.md)
shows up as an unexpected file change), and the .NET acceptance suite's
`WebLocalizationAndDesignSteps`, which also shells out to
`check-locales.mjs`. Observed concretely in #172 (the member-login stub):
one new set of English-only keys failed `i18n`, `web`, `test`, and
`coverage` simultaneously, all from the same cause.

ADR-0021's security reasoning for keeping translation off `pull_request`
is sound and does not change: `pull_request` runs fork-authored code, so a
translator credential must never be in scope there — a malicious fork PR
must never be able to spend the DeepL credential or smuggle unreviewed
French text through a modified `tools/translator.mjs`. But that risk comes
specifically from *untrusted* branches. A pull request opened from a branch
of this repository itself (never a fork) is exactly as trusted as a direct
push to `main` — the same people who can push branches here can push to
`main` directly, modulo review norms this project doesn't enforce with
branch protection.

## Decision

Add a `pull_request_target` trigger to `i18n-translate.yml`, gated to
`github.event.pull_request.head.repo.full_name == github.repository`. This
is the one condition that matters: `pull_request_target` runs with the
base repository's secrets and permissions regardless of where the event
came from, so the job must check the PR's source itself rather than trust
the trigger name.

For a same-repo PR, the job:
1. Checks out the PR's head commit by SHA (not by branch name — avoids a
   time-of-check/time-of-use race if the branch moves between the event
   firing and checkout).
2. Runs `translate-locale.mjs --generate`, unchanged from the push-to-main
   path — same credential, same change-detection, same glossary pinning.
3. If anything changed, commits `locales/fr-CA.json` and
   `locales/fr-CA.meta.json` directly onto the PR's own branch and pushes,
   using `TRANSLATION_PR_TOKEN` so the resulting `synchronize` event still
   triggers `ci.yml` (a push authored by the default `GITHUB_TOKEN` does
   not re-trigger workflows — the same loop-protection rule ADR-0021 already
   works around for opening the translation PR).

A fork-authored pull request's `head.repo.full_name` never equals
`github.repository`, so the gate excludes it unconditionally — it keeps
today's behavior exactly: `pull_request` only ever verifies, and French for
a merged fork PR's new keys arrives the same way it always has, via the
post-merge `chore/fr-CA-translations` pull request.

The human-reviews-the-French invariant from ADR-0021 is preserved, not
weakened: for a same-repo PR, the French commit lands on a branch that is
itself under review before it can reach `main` — the reviewer sees it in
the same place they see everything else in the change, rather than in a
second pull request. Nothing here lets any French reach `main` unreviewed.

## Alternatives

- **Run the translator inside `ci.yml`'s existing `pull_request` job.**
  Rejected: `pull_request` runs for every PR, including forks, and there is
  no branch check that can retroactively make that trigger safe — the whole
  point of `pull_request_target` is that the job explicitly opts in to
  running with secrets against a specific, checked source.
- **Gate on PR author/actor instead of `head.repo.full_name`.** A username
  allowlist is one more thing to maintain and drifts (a collaborator added
  to the repo isn't automatically added to the list). `head.repo.full_name
  == github.repository` is exactly the fork/no-fork boundary GitHub itself
  uses for permission purposes, so it can't drift out of sync with who
  actually has push access.
- **Leave the gap and instead soften `check-locales.mjs`** to warn rather
  than fail when English has a key French lacks. Considered as a
  complementary, separate change (tracked in its own issue) — it would
  quiet the false-positive checks but wouldn't get real French into a PR
  before merge, and this project's whole point is a bilingual product, not
  a green checkmark. Both can coexist: this ADR gets real French onto
  trusted PRs before they merge; a softened check would still help fork
  PRs, where French genuinely isn't available yet.

## The pre-commit hook had to follow (#207)

This decision was made for CI and initially left the local hook alone, which
put the two in direct conflict: the workflow existed to fill a `#` stub on a
branch, and the hook refused to let one be committed at all. With no DeepL
credential locally there was nothing a developer could do to satisfy it
except `git commit --no-verify` — which also skipped the hook's unrelated
`dotnet format` check, because `set -e` let the locale failure abort the
script before formatting ran.

The hook now runs both checks independently and tolerates a pending French
stub on a branch, while `main` and CI stay strict. A guardrail that has to
be bypassed routinely is not a guardrail.

## Consequences

- `.github/workflows/i18n-translate.yml` gains a `pull_request_target`
  trigger and a same-repo-only path that commits directly to the PR branch.
- A same-repo PR that adds English-only keys self-heals within the same PR:
  the bot's commit brings `fr-CA.json` back into parity, and `i18n`/`web`/
  `test`/`coverage` go green on the next run without anyone hand-editing
  French.
- A fork PR's experience is completely unchanged: still red on those same
  checks until the post-merge translation PR lands, exactly as ADR-0021
  describes.
- `DEEPL_API_KEY` and `TRANSLATION_PR_TOKEN` are now read from two trigger
  paths instead of one; both remain repository secrets, never exposed to
  `pull_request`-triggered runs, and still never touch a developer machine.

## Related

- [ADR-0021](ADR-0021-ci-translation-opens-a-pull-request.md)
- [ADR-0054](ADR-0054-local-build-stubs-missing-translations.md)
