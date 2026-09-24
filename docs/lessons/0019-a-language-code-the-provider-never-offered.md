---
title: A language code the provider never offered
description: French-to-English machine translation always failed, because the DeepL adapter asked for EN-CA, a target DeepL does not have, and the unit test asserted the same code.
type: lesson
date: 2026-09-24
issue: 419
status: accepted
---

# Lesson 0019 — A language code the provider never offered

## Symptom

On PR #415, a comment written in French stayed "Translation on its way"
forever. Its outbox message was poisoned after five attempts with "The
translation service answered 400." English comments translated into French
normally.

## Root cause

`DeepLTranslator` mapped `en-CA` to the DeepL target `EN-CA`. DeepL offers
`FR-CA` but no Canadian English, so every French-to-English request was a
400: comments, answers marked for translation, and a reviewer's "Translate to
English" draft. The unit test for that direction asserted
`target_lang == "EN-CA"`, so it proved the adapter sent the wrong code rather
than catching it. The expected value was copied from the code, not from
DeepL's list of targets. Every report in development had been written in
English, so the broken direction never ran.

## Spec delta

- [ADR-0115](../decisions/ADR-0115-the-english-translation-target-is-configuration.md):
  the English target is `Translation:EnglishTarget` in the API's and the
  Worker's `appsettings.json`, `EN-US` today, required, and validated at
  startup.
- `features/web-localization-and-design/README.md` explains why the variant is
  configuration.

## Scenario

- REQ-WLD-028: French to English asks DeepL for the configured `EN-US` or
  `EN-GB`.
- REQ-WLD-029: a missing value, `EN-CA`, or `EN` stops startup, naming the
  setting.

## Skill

[`test-hpac-safety`](../../skills/test-hpac-safety/SKILL.md) now says that a
test of what an external provider is sent takes its expected values from the
provider's documentation, and that every direction of a translation is
exercised, not only the one development data happens to use.
