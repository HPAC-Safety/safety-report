---
date: 2026-09-21
issue: 215
status: accepted
---

# Lesson 0002 — Provenance that hashes only one side of a pair

## Symptom

A human improved a French string in `locales/fr-CA.json`. Every check passed
and the edit survived — under a stamp that read
`provider: "deepl:FR-CA:prefer_more"`, claiming a machine had written text a
person wrote.

Then the English changed. The key was queued for re-translation, the human's
wording was overwritten, and nothing anywhere recorded that it had existed.

## Root cause

`fr-CA.meta.json` stamped a `source_hash` — a hash of the **English**. Nothing
hashed the French. Provenance covered the input to the translation and not the
output, so an edit to the output was invisible to every check that read it.

"Never hand-edit `fr-CA.json`" was a sound rule, and it still is. But the file
is hand-editable in practice, and a rule enforced by nothing, whose violation
is invisible and whose punishment is silent data loss, is not a rule. It is a
trap.

## Spec delta

[ADR-0070](../decisions/ADR-0070-a-hand-edited-french-value-is-a-recorded-correction.md)
hashes the French as well, so a hand-edited value is detected rather than
assumed, and records it as a correction with its own provenance instead of
overwriting it on the next run.

The general rule that came out of it: **when a record claims where a value came
from, the record covers the value, not the thing the value was derived from.**
Provenance over an input tells you nothing about what happened to the output.

## Scenario

`REQ-WLD-012` — "A French value edited by hand is recorded rather than
overwritten" — and `REQ-WLD-013`, which covers both languages changing at once.

## Skill

[`localize-hpac-app`](../../skills/localize-hpac-app/SKILL.md) carries the
general rule: a record of where a value came from covers the value, not the
input it was derived from. Hash what you are claiming authorship of.
