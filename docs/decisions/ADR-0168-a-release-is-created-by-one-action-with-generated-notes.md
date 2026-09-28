---
title: A release is created by one Action, with notes generated from the merged pull requests
description: create-release.yml, run by hand on main, tags the next YYYY.MM.DD-N, creates a GitHub Release whose notes list every pull request merged since the previous one grouped by label, and dispatches release.yml on the tag to build and deploy staging. Nobody makes a tag or release by hand.
type: adr
status: accepted
date: 2026-09-28
decision-makers: Chase Florell
keywords: release, create-release.yml, release notes, generate-notes, workflow_dispatch, GITHUB_TOKEN, tag, ADR-0158, ADR-0166, CON-INF-012
---

# ADR-0168 — A release is created by one Action, with notes generated from the merged pull requests

## Status

Accepted. Builds on [ADR-0158](ADR-0158-two-aws-accounts-staged-and-promoted-by-approval.md)
(the `YYYY.MM.DD-N` tag and release-driven staging deploy) and
[ADR-0166](ADR-0166-a-release-deploys-staging-and-a-separate-workflow-promotes-to-production.md)
(promotion). Changes neither.

## Context

Publishing the first staging release (#606) meant choosing a tag in the GitHub
UI, typing a date, and creating the tag by hand. The owner wants no manual
steps: one Action that creates the release and deploys it, with release notes
that gather everything merged since the last release, as
[chaseflorell/codetoneo4j](https://github.com/chaseflorell/codetoneo4j)'s
release workflow does (owner, 2026-09-28).

Two GitHub constraints shape it:

- A release or tag created with `GITHUB_TOKEN` triggers no other workflow, so
  `release.yml`'s `release: published` never fires for it. A
  `workflow_dispatch` is the exception GitHub allows.
- `hpac-safety-staging` accepts deployments only from refs matching `20*`
  (infra/SETUP.md 2.1), so the staging job must run on the tag, not on `main`.

## Decision

- **`create-release.yml`**, `workflow_dispatch` on `main` only, is the whole
  human step. It:
  1. picks the next tag: today's UTC date, `YYYY.MM.DD`, and the next free `N`;
  2. creates the tag at the run's `main` commit and a GitHub Release titled
     with it. The notes open with a provenance table (commit, creating run)
     and then GitHub's generated notes: every pull request merged since the
     previous release, grouped by label through `.github/release.yml`
     (Features, Bug fixes, Documentation, Maintenance, Dependencies, Other
     changes);
  3. dispatches `release.yml` on the new tag with `tag` set to it.
- `release.yml` is unchanged in what it does: it builds once and deploys
  staging. Its dispatch path already refuses a run not on the tag it names.
  `release: published` stays, for a release published by hand in the UI.
- One run at a time (`concurrency: create-release`), so two runs never pick
  the same tag.

## Alternatives considered

- **Semver tags (`vMAJOR.MINOR.run`)**, as codetoneo4j uses. Rejected (owner):
  ADR-0158's date tags, the environments' `20*` rule, and `promote.yml`'s
  validation all stay as they are.
- **Create only, and deploy staging as a separate step.** Rejected (owner):
  creating a release and deploying it to staging stay one click.
- **Build and deploy inside `create-release.yml` itself**, with `main` added
  to `hpac-safety-staging`'s allowed refs. Rejected: it widens the
  environment's policy to a branch, and `promote.yml` finds the build it
  promotes by the `Release <tag>` run title, which only `release.yml` gives.
- **A personal access token** so the created release fires
  `release: published`. Rejected: a long-lived credential for something a
  dispatch does with the workflow's own token.

## Consequences

- No one creates a tag or release by hand; infra/SETUP.md 2.5 is one click.
- The notes are only as well sorted as the pull requests' labels, which
  every pull request carries from its issue (deliver-hpac-change "File a new
  issue"). An unlabelled one lands under Other changes.
- The first release's notes list every pull request merged so far.
