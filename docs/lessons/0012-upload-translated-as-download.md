---
title: Upload translated as download
description: Every French attachment string said télécharger, which Canadian French reads as download, because nothing told the translator the word and nothing checked it afterwards.
type: lesson
date: 2026-09-23
issue: 377
status: accepted
---

# Lesson 0012 — Upload translated as download

## Symptom

The French attachment step told reporters their files were *téléchargés*
("downloaded") as they attached them. Nine strings had the same mistake: the
guidance, the privacy notice, Cancel, and every refusal reason. Another agent
session spotted it while working on #373.

## Root cause

DeepL rendered "upload" as *télécharger*, a common machine translation that
Canadian French reads as download. The OQLF term is *téléverser*. Nothing in
the pipeline knew this:

- `glossary.json` pins whole strings by key, not words.
- `--check` verifies parity, provenance, and placeholders, not meaning.
- Every string arrived with `reviewed: false`, and was merged anyway.

## Spec delta

- `features/web-localization-and-design`: two new claims.
  - REQ-WLD-026: French that renders a listed term the forbidden way fails
    verification, whoever wrote it.
  - REQ-WLD-027: the machine translator is told the required rendering of
    every listed term.
- The README describes `locales/terms.json` and says what is out of scope.
- [ADR-0102](../decisions/ADR-0102-a-term-list-holds-the-french-translator-to-a-word.md)
  records the design.

## Scenario

REQ-WLD-026 and REQ-WLD-027 in
`features/web-localization-and-design/web-localization-and-design.feature`.

## Fix

#378 corrected the nine strings by hand. #379 adds `locales/terms.json`
(`upload` → *téléverser*, never *télécharg…*), sends each term to the
translator as an instruction, and makes `--check` fail on a forbidden form.
