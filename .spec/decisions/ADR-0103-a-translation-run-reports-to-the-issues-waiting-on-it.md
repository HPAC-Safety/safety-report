---
title: A translation run reports to the issues waiting on it
description: i18n-translate.yml comments on, and when its result passes the i18n check closes, every open issue labelled verify:translation-run whenever it actually calls the provider, and comments with the provider's error when it fails.
type: adr
status: accepted
date: 2026-09-23
decision-makers: Chase Florell
keywords: i18n, DeepL, verification, pull_request_target, issues permission, verify:translation-run, ADR-0057, ADR-0102
---

# ADR-0103 — A translation run reports to the issues waiting on it

**Status:** Accepted. Extends
[ADR-0057](ADR-0057-same-repo-pull-requests-translate-in-pr.md). It uses the
term list from [ADR-0102](ADR-0102-a-term-list-holds-the-french-translator-to-a-word.md)
as its first case.

## Context

Some questions can only be answered by a real translation run. ADR-0102 sends
the term list to DeepL as `custom_instructions`, and whether DeepL accepts that
alongside `FR-CA`, `prefer_more` formality and XML tag handling is only known
once a run calls it. A run only calls the provider when an English string
changes, so when that will happen can't be predicted. The run right after
ADR-0102 merged printed "Nothing to translate" and settled nothing (#381).

## Decision

`i18n-translate.yml` reports its own outcome to every open issue labelled
`verify:translation-run`.

- `translate-locale.mjs --generate` outputs `translated`, the number of keys
  actually sent to the provider, and `translated_keys`. The existing `keys`
  also counts glossary pins, hand-edits recorded as human, and removals. None of
  those reach a provider, so `keys` can't show that the provider was exercised.
- When a run translated at least one key and succeeded, the job runs
  `--check` on its own result without failing the job on it. It then comments
  on each labelled issue with the run, the event, the keys, and the check's
  verdict, and closes the issue if the check passed.
- When a run fails, the job comments on each labelled issue with the run link,
  the failed step, and the provider's status line
  (`The translation provider answered NNN …`) from the translate step's
  captured output. The issue stays open for a person.
- The translate job gains `issues: write`. The built-in `GITHUB_TOKEN` posts
  the comments, and `TRANSLATION_PR_TOKEN` is not used for them.

## Alternatives rejected

- **A scheduled cloud agent that polls for the run.** It needs GitHub
  credentials that a cloud session may not have, reads logs after the fact,
  starts a session every day whether or not anything happened, and has to be
  switched off by hand afterwards.
- **Trigger a translation on purpose.** `workflow_dispatch` only translates
  what differs, so a meaningful call would need a throwaway English edit that
  a person then has to revert.
- **Report only in the job log.** Nothing reads it unprompted, and the
  question stays open on the issue.

## Consequences

- The label is the only thing to maintain. Any future "settle this on the next
  real translation" question is filed with it and closes itself.
- The `pull_request_target` path keeps its same-repo gate from ADR-0057, so a
  fork PR still never reaches the translate job or these steps. The new
  permission is `issues: write` on that same gated job. The only content it
  posts is UI locale keys, a status line, and links. No report data ever
  enters this workflow.
- A run that fails for reasons unrelated to the provider, such as a rejected
  push, still comments. The comment names the failed step, so a reader can
  tell.

## Related

- [ADR-0021](ADR-0021-ci-translation-opens-a-pull-request.md)
- [ADR-0057](ADR-0057-same-repo-pull-requests-translate-in-pr.md)
- [ADR-0102](ADR-0102-a-term-list-holds-the-french-translator-to-a-word.md)
