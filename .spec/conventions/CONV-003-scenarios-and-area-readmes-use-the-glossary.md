---
title: Scenarios and area READMEs use the glossary's words
description: Every scenario and area README writes each concept with the one word .spec/glossary.md gives it; check-glossary refuses a banned synonym, and a step definition's text changes with its step.
type: convention
status: accepted
date: 2026-10-03
---

# CONV-003 — Scenarios and area READMEs use the glossary's words

## Rule

- Write each concept in a `.feature` file or a `.spec/features/<area>/README.md`
  with the word [`.spec/glossary.md`](../glossary.md) gives it, never a
  synonym its **Banned in scenarios** column lists.
- Quote what the user literally sees — an interface string, a page title, a
  value — in `"double quotes"`, and an identifier in `` `code` ``. The lint
  skips both, and every `<placeholder>`.
- A new concept, or a word that keeps being misused, is a glossary change:
  add the term or the banned synonym in the same pull request as the
  scenarios that need it. An exemption is a whole area, named in the row's
  **Exempt areas** cell, and needs a reason on the issue.
- A narrower carve-out lives inside the pattern itself — a lookbehind or
  lookahead that leaves one fixed phrase alone, such as "landing field" or a
  file name like `photo.jpg` — and that row's definition says why. Never
  reword a scenario around a pattern that is too wide; narrow the pattern.
- Renaming a step renames its step definition's text in the same commit —
  the Reqnroll attribute or the playwright-bdd string — and never changes a
  claim ID. Before renaming, check that the new text binds nowhere else and
  that no `[Scope(Feature = …)]` binding would win it silently; afterwards
  `.spec/claims.json` shows no ambiguous and no unused step definition.
- Accepted ADRs keep their words: they are immutable
  ([ADR-0192](../decisions/ADR-0192-an-accepted-adr-is-immutable-and-process-rules-are-conventions.md)).

## Why

The scenarios named one concept several ways — "safety officer",
"SafetyOfficer", "authorized reviewer"; "soft-deleted", "stamped as deleted";
"option" and "choice" — so a reader could not tell whether two steps meant the
same thing, and a binding written for one spelling missed the other. The owner
approved one glossary, sixty terms and ten outcome phrases, with each
judgment call recorded on
[#815](https://github.com/HPAC-Safety/safety-report/issues/815#issuecomment-5973100915),
and ruled that it is enforced by a lint in CI with no baseline and no git-hook
change ([#823](https://github.com/HPAC-Safety/safety-report/issues/823)).

## Enforced by

- `node tools/spec/check-glossary.ts`, in the `docs` job of `ci.yml`, which
  `tools/dev/ci-local.sh` runs. It reads the banned synonyms from the
  glossary's tables, so a new ban needs no code change.
- `node tools/spec/generate-traceability.ts` (the `docs` job) fails a built
  claim whose renamed step no definition binds, and records any ambiguous or
  unused step definition in `.spec/claims.json`.
- Whether a word is used in the glossary's sense — "file", "media", "draft",
  "version" — is written, not checked: the lint bans only what a pattern can
  tell apart.
