---
title: The English machine-translation target is configuration
description: DeepL has no Canadian English, so the English it writes is set by Translation:EnglishTarget in the API's and the Worker's appsettings.json, EN-US today, and startup fails on anything DeepL does not offer.
type: adr
status: accepted
date: 2026-09-24
decision-makers: Chase Florell
keywords: translation, DeepL, English, EN-US, EN-GB, configuration, ADR-0022
---

# ADR-0115 — The English machine-translation target is configuration

**Status:** Accepted. Amends
[ADR-0022](ADR-0022-translation-provider-is-configuration.md) on one point:
the English target code is no longer fixed in the adapter.

## Context

`DeepLTranslator` mapped `en-CA` to the DeepL target `EN-CA`. DeepL has no
Canadian English. It answers `EN-CA` with a 400. Its English targets are
`EN-GB` and `EN-US`, and plain `EN` is a deprecated alias that returns American
spelling. So every French-to-English machine translation has failed: a
French comment, a French answer marked for translation, and a reviewer's
"Translate to English" draft. English to French (`FR-CA`) works, which is why
nobody noticed until a comment was written in French
([lesson 0019](../lessons/0019-a-language-code-the-provider-never-offered.md)).

## Decision

1. **The English target is `Translation:EnglishTarget`.** It is set in the
   `appsettings.json` of both processes that call DeepL: the API (question
   authoring, reviewers' summary drafts) and the Worker (answers and
   comments). It accepts `EN-US` or `EN-GB`, in any case. It is **`EN-US`**
   today, by the owner's choice.
2. **There is no default in code.** A deployment names its English, and the
   options are validated when the process starts. A missing or unsupported
   value stops the API or the Worker with a message naming the setting, so it
   cannot degrade into a stream of failed translations.
3. **French stays fixed at `FR-CA`**, which DeepL does offer as a target.
   French as a source is still plain `FR`.

## Rejected alternatives

- **Plain `EN`.** It works today, but DeepL documents it as deprecated, and it
  is American English anyway. When the alias goes, every French-to-English
  translation fails again.
- **Hard-coding `EN-GB` or `EN-US` in the adapter.** The choice of spelling is
  a product decision that may change, and it should not need a code change.
- **Terraform or deploy-workflow variables.** The value is not a secret and
  not per-environment infrastructure. It sits beside the rest of the
  application's settings, where the key's other settings already are.
- **A default in `DeepLOptions`.** A default would hide the setting. Required
  and validated makes each process's choice visible in its own config file.

## Consequences

- French comments, answers, and summary drafts translate into American
  English: "color", "center".
- Changing to British English is a one-line edit to both `appsettings.json`
  files, or an environment override (`Translation__EnglishTarget`).
- Claims: REQ-WLD-028 (the configured code is what DeepL is asked for) and
  REQ-WLD-029 (a missing or unsupported value stops startup).
