---
title: A place name the translator was told to copy
description: After the switch to Gemini, a reviewer's Translate button returned type-ahead place names in English, because the translation prompt told the model to copy the names of places unchanged. No scenario said how a place should be translated.
type: lesson
date: 2026-09-30
issue: 704
status: accepted
kind: product
---

# Lesson 0039 — A place name the translator was told to copy

## Symptom

Reviewing reporter-added type-ahead values in dev, an administrator used
Translate on "Prairie Mountain, AB", "Cochrane, Alberta Canada", and
"Pincushion Mtn., Summerland BC". Each came back as its own English and was
saved as the French label. Values DeepL had translated before read "Mont
Yamaska Nord" and "Lumby, C.-B.".

## Root cause

- `locales/translation-prompt.v1.md` listed "the names of people, places,
  aircraft, and organizations" among the things to copy unchanged. Nearly
  every type-ahead value is a place, so Gemini followed the prompt.
- The prompt was written for interface labels, where a copied name is
  harmless. No scenario covered translating a place, so REQ-WLD-033 to
  REQ-WLD-035 passed on a prompt that got every type-ahead value wrong.

## Spec delta

- REQ-WLD-047: every translation, in either direction, localizes place names
  and gives every Canadian province and territory abbreviation in both
  languages. The prompt no longer tells the model to copy places.
- `locales/translation-prompt.v2.md` replaces v1 for the API, the Worker, and
  CI. ADR-0179 is amended, and `.spec/features/web-localization-and-design/README.md`
  says places are localized.

## Scenario

REQ-WLD-047.

## Skill

None; the claim is the remedy.
