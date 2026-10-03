---
title: A bot push stands down when its own output already landed
description: tools/github/push-to-pr-branch.ts gains a fourth outcome — when the branch gained a commit with this commit's subject and no trigger file changed, an earlier run of the same workflow already landed its output over the same inputs, so the run exits 0 instead of replaying into a conflict.
type: adr
status: accepted
date: 2026-09-26
decision-makers: Chase Florell
keywords: traceability, translation, pull_request_target, push race, replay, cherry-pick, ADR-0113
---

# ADR-0149 — A bot push stands down when its own output already landed

**Status:** Accepted. Amends
[ADR-0113](ADR-0113-a-bot-pushing-onto-a-pull-request-replays-past-another-bot.md)
by adding a fourth outcome to what a rejected push means.

## Context

On #548 the author pushed 2fc5b869 and then 77323839 seconds apart. Each push
started `i18n-translate.yml`, and the two runs queued in the pull request's
group. The first run's push was rejected. The branch had gained only the
author's second commit, which changed no trigger file, so the first run
replayed its translation onto 77323839 as 6158426f. The second run translated
the same keys over the same `locales/en-CA.json`. Its push was rejected too.
The branch had gained only `locales/fr-CA*.json`, which are not triggers, so it
replayed as well. The cherry-pick conflicted with 6158426f and failed the job
(run 36277768613), though the branch already had its French.

ADR-0113's third outcome assumes that a branch which gained no trigger file
gained another workflow's output. Here it had gained this workflow's own
output.

## Decision

When a push is rejected, the tool also lists the subjects of the commits the
branch gained since the event's head SHA. After ADR-0113's first two outcomes:

3. **A gained commit has this commit's subject.** An earlier run of this same
   workflow already landed its output. No trigger file changed since this
   run's event, so the earlier run used the same inputs. This run emits a
   notice and exits 0.
4. **Otherwise** the tool replays, as in ADR-0113's third outcome.

A hand edit to the same output file carries a different subject. It still
conflicts and fails the job, so a person's edit is never silently kept or
overwritten.

The tool now fetches the branch without `--depth=1`. The checkout holds the
event SHA, so the fetch stops there and brings exactly the gained commits.

## Considered options

- **Stand down whenever the branch gained any file this commit writes.** That
  would also swallow a human's hand edit. The job would stay green while the
  output might be stale.
- **Treat a cherry-pick conflict as success.** It hides every real conflict.
- **One run per pull request with `cancel-in-progress: true`.** A newer run
  cancels the older one only after it has spent its provider call. A run that
  already pushed cannot be cancelled at all. The race only moves.

## Consequences

- Two queued runs of one workflow over the same inputs end green, with one
  commit.
- `traceability.yml` runs the base branch's copy of the tool, so it gains the
  outcome once this merges. `i18n-translate.yml` gains it on any pull request
  based on the merge.
