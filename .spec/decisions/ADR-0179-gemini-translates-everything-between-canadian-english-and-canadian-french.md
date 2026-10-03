---
title: Gemini translates everything between Canadian English and Canadian French
description: Gemini does every machine translation, runtime and CI, en-CA to fr-CA and back, while DeepL is kept dormant so it can be switched back; a separate ITranslator call with its own model and reasoning setting, the same Gemini key, one versioned prompt, and the term list sent with every request. Translation is carved out of invariant 3.
type: adr
status: accepted
date: 2026-09-29
decision-makers: Chase Florell
keywords: translation, Gemini, DeepL, ITranslator, en-CA, fr-CA, terms, prompt, invariant 3, ADR-0104, ADR-0115
---

# ADR-0179 — Gemini translates everything between Canadian English and Canadian French

**Status:** Accepted. Supersedes, for the running system and for CI, the
DeepL clause of
[ADR-0022](ADR-0022-translation-provider-is-configuration.md) (the CI
provider) and the DeepL mechanism in
[ADR-0102](ADR-0102-a-term-list-holds-the-french-translator-to-a-word.md) and
[ADR-0109](ADR-0109-no-translation-stand-in-in-any-environment.md).
[ADR-0115](ADR-0115-the-english-translation-target-is-configuration.md)'s
`EN-US` / `EN-GB` target no longer governs the running system, because Gemini
has no English target to configure, but `DeepLOptions` keeps it for the dormant
DeepL adapter, so ADR-0115 is partially superseded, not removed.
[ADR-0080](ADR-0080-every-answer-gets-a-worker-translated-second-language.md),
[ADR-0108](ADR-0108-a-reviewer-may-machine-translate-a-summary-language.md),
[ADR-0112](ADR-0112-only-answers-that-need-it-get-a-second-language.md), and
[ADR-0114](ADR-0114-members-may-comment-on-a-published-report.md) were checked:
they say which strings are translated, when, and by whom, none of which
changes. Where they name DeepL as the provider, this ADR replaces it.
Issue #614.

## Context

DeepL has no Canadian English. It answers `EN-CA` with a 400
([lesson 0019](../lessons/0019-a-language-code-the-provider-never-offered.md)),
so ADR-0115 made the English target configuration, limited to `EN-US` or
`EN-GB`. That was always a stopgap: the association writes Canadian English.

Of the providers compared, none that offers `en-CA` also stays in Canada or
AWS. AWS Translate and Azure offer only `en`; Google's Translation LLM offers
`en-CA` only in `global` or `us-central1`. Gemini is already a vendor with a
paid key and accepted processing outside Canada
([ADR-0104](ADR-0104-summaries-are-generated-by-gemini-through-a-paid-key.md)),
and it can be told to write Canadian English and Canadian French and to follow
the term list.

## Decision

1. **Gemini translates; DeepL is kept, dormant.** Gemini does all machine
   translation, in every environment and in CI. DeepL is not removed: its
   code (`DeepLTranslator`, `DeepLOptions`, and the `tools/i18n/translator.ts`
   adapter), its tests, its secret, its IAM grant, its Lambda setting, its
   deploy step, and its dev and CI wiring all stay, so it can be switched back
   if Gemini disappoints. Nothing registers it, so nothing calls it.
   `ITranslator` has two implementations, `OpenAiTranslator` (registered) and
   `DeepLTranslator` (kept). `OpenAiTranslator` is provider-neutral: it asks
   whatever OpenAI-compatible provider `AiChatClient` is configured for, which
   today is Gemini through its OpenAI-compatible chat-completions endpoint, so
   another OpenAI-compatible model is a configuration change, not new code.
2. **The pair is `en-CA` ⇄ `fr-CA`.** French to English is written in
   Canadian spelling (colour, centre). For the running system, `EN-GB`,
   `EN-US`, and `Translation:EnglishTarget` no longer apply: the prompt names
   the target variant, and nothing is mapped to a provider language code. The
   setting and its startup check live on only in `DeepLOptions`, for the
   dormant adapter (ADR-0115).
3. **Translation is carved out of invariant 3.** "One model call, only with
   consent" governs the summary call and nothing else.
   - A translation call is a separate port, `ITranslator`, which receives the
     strings to translate and the term list, and nothing else: no report ID,
     no other answers, no report context.
   - It runs for a report with or without publication consent, as DeepL did.
     What it sees is what ADR-0112 already sent: free text an administrator
     marked for translation, a reporter-added type-ahead label, a member's
     comment, an administrator's question wording, a reviewer's summary text.
   - The Worker's summary pair still comes only from its one anonymized call,
     never from the translator (ADR-0082, ADR-0104).
   - Nothing on the submission path calls it (ADR-0072, ADR-0109); the
     existing guard stands.
4. **The same Gemini key, its own model setting.** Translation reads the key
   the summary call already uses, in each environment:
   - deployed: the ADR-0104 secret in that environment's Secrets Manager, read
     at cold start by ARN (`AiChatClient__ApiKeySecretArn`). **No new key,
     secret, or Secrets Manager entry is created.** The API now needs it too,
     so its role gets read access to that same existing secret and its Lambda
     gets the same ARN setting;
   - Development: `GEMINI_API_KEY`, as `AiChatClient__ApiKey`;
   - CI: the existing `GEMINI_API_KEY_DEV` repository secret. The
     `DEEPL_API_KEY` secrets and the DeepL key stay; nothing in this change
     creates, deletes, or edits a GitHub secret.

   The model and reasoning effort are the `Translation` section's own,
   `Model` = `gemini-3.7-flash` and `ReasoningEffort` = `low`, so translation
   is tuned apart from summaries. The provider is picked by the model's name
   through the `IAiMediator` (decision 6); the translator asks it for one JSON
   object and validates the reply itself. A blank model, a model no handler
   claims (while a key is held), or an undefined reasoning level stops the host
   at startup, naming the setting.
   With no key, translation is unavailable in every environment, Development
   included, and there is still no stand-in (ADR-0109).
5. **CI moves too.** `tools/i18n/translator.ts` gets a Gemini adapter (the same
   OpenAI-compatible call), which is its default, and `i18n-translate.yml` reads `GEMINI_API_KEY_DEV`. The
   DeepL adapter stays beside it, used only when `TRANSLATION_PROVIDER` is
   `deepl`, and the workflow still passes `DEEPL_API_KEY`. ADR-0022's
   CI-provider clause is superseded; its adapter-swap design and the `stub`
   provider stand. The generic `chat-completions` adapter is replaced by the
   Gemini one, which is that same OpenAI-shaped call with Gemini's endpoint,
   the model, and the reasoning effort fixed by settings.
6. **An AI mediator picks the provider by the model's name, for both callers.**
   - `IAiMediator` (hand-written, no package) takes one OpenAI-style request:
     the model, the messages, and optionally a reasoning effort and a response
     format. It returns the completion. It is the only place that tells
     providers apart: it hands the request to the `IAiHandler` whose
     model-name prefix matches. It replaces `IAiChatClient` and its `Provider`
     setting and strategy map (ADR-0104).
   - One handler is built, `GeminiHandler` (the former `GeminiChatClient`),
     claiming `gemini-*` and calling Gemini's OpenAI-compatible endpoint with
     the Gemini key. An OpenAI handler is not built; it would be one new class
     and one registration line.
   - Two callers, neither knowing any provider: `OpenAiTranslator`
     (`ITranslator`) sends its instructions, the terms, and the strings with
     `Translation:Model` and `Translation:ReasoningEffort`, and applies the
     batch contract below; `OpenAiSummarizer` (the Worker's summary caller,
     formerly `PromptDrivenSummarizer`) sends the one versioned summary prompt
     with `AiChatClient:Model` and `AiChatClient:ReasoningEffort`. The summary's
     behavior is unchanged: one prompt, one call per attempt, the marking pass
     first, a strict English/French pair.
   - Configuration states only models. `AiChatClient:Provider` is removed. An
     unrecognised model stops the host at startup while a key is held, naming
     its setting (`AiChatClient:Model` or `Translation:Model`); with no key,
     translation and summaries stay unavailable, as before. The
     `AiChatClient` section keeps its name and keys.
   - CI's `tools/i18n/translator.ts` mirrors this in small form: the model-name
     prefix picks the adapter, `gemini-` is Gemini's endpoint, and an unknown
     model fails.
7. **One term list, one prompt, for every translation.**
   - `locales/terms.json` (ADR-0102) is sent with every translation request,
     runtime and CI alike, so "upload" becomes "téléverser" everywhere.
     `--check` still enforces the forbidden forms on the catalogue. The
     ten-term and 300-character ceilings were DeepL's: they no longer limit the
     list, and the dormant DeepL adapter enforces them itself if it is ever
     used.
   - The instruction is one sentence per term, stated once and applied in
     both directions: the French rendering, the forms never to use, and that
     French text written with that rendering is the English term.
   - **The prompt is one current versioned file**,
     `locales/translation-prompt.v2.md` since the amendment below, beside the term list. Both runtimes
     read that one file: `tools/i18n/translator.ts` from disk, and
     `HpacSafety.Infrastructure` as an embedded resource. A behavior change is
     a new version file (`v2`); a used version is never edited. It is not a
     Worker prompt: it is shared with CI and the API, so it does not live
     under `src/HpacSafety.Worker/Prompts/`, and like them its bytes are the
     model payload, so the frontmatter check exempts it.
8. **No new provenance.** Each translated value keeps its `auto` / `human` /
   `choice` source. Neither the translation prompt version nor the model is
   recorded per value, so no migration is needed. The CI locale stamp in
   `fr-CA.meta.json` already names its provider; it is now
   `gemini:<model>`. Existing `deepl:FR-CA:prefer_more` stamps stay as they
   are: they are history, and a stale one is re-translated only when its
   English changes (ADR-0021).

### The batch contract

Kept from DeepL, and now enforced by the adapter because a model, unlike DeepL,
can chatter or drop a field:

- one request carries every string; the user message is
  `{"texts": [...]}` and the reply is `{"translations": [...]}`;
- strings come back in order, one for one. A wrong count, a reply that is not
  that object, an empty translation of a non-empty string, or a string that
  loses or alters a `{placeholder}` token or a markup tag is refused;
- the failure is a `TranslationUnavailableException` carrying the status or a
  fixed sentence, never the provider's body, the credential, or the text.

## Considered options

- **Google Cloud Translation LLM.** It offers `en-CA`, but only in `global` or
  `us-central1`, adds a second vendor and a service-account credential, and
  gains nothing over the Gemini key the system already pays for.
- **AWS Translate or Azure.** Neither offers `en-CA`.
- **Keeping DeepL for French and adding Gemini for English.** Two providers
  in use at once, two keys, two term mechanisms, and French would still need
  DeepL-specific quirks (`FR-CA` is target-only). One provider in use is
  simpler to keep true; DeepL is kept only as a dormant switch-back.
- **Removing DeepL outright.** Rejected by the owner (2026-09-30): it costs
  little to keep and is the fallback if Gemini's translations disappoint.
- **A new Secrets Manager entry, or a second Gemini key, for translation.**
  The owner confirmed the existing key in each environment is the one to use;
  a second key buys nothing.
- **Recording the model or prompt version per translated value.** Out of
  scope (decision 8); it would need a migration for a value nobody reads.
- **Carrying the prompt as a C# string and a JS string.** The two would drift;
  a single file is one thing to review and version.
- **Reusing the summary's model setting.** Translation is a literal, low-effort
  job with different failure modes; tuning it must not touch summaries.

## Consequences

- French comments, answers, and reviewer drafts now translate into Canadian
  English. Existing `auto` values that DeepL wrote in `EN-US` stay as stored.
- The API now calls Gemini, so it holds the Gemini secret's ARN and IAM read.
  The DeepL secret, its IAM grant, its Lambda setting, and the deploy
  workflow's `DEEPL_API_KEY` step all stay, dormant: nothing reads the secret
  while Gemini translates.
- A real Gemini translation run cannot be verified before this merges: CI's
  `i18n-translate.yml` on the base branch still runs the old tool until then.
  The issue keeps `verify:translation-run`
  ([ADR-0103](ADR-0103-a-translation-run-reports-to-the-issues-waiting-on-it.md)).
- The owner keeps the `DEEPL_API_KEY` GitHub secrets and the DeepL key.
- Claims: REQ-WLD-027 (the CI translator is told every term), REQ-WLD-033 and
  REQ-WLD-034 (the target variant is named), REQ-WLD-035 (the runtime prompt
  carries every term), REQ-WLD-036 (no key, unavailable), REQ-WLD-037 through
  REQ-WLD-039 (the batch contract), REQ-WLD-040 (its own model setting), and
  REQ-WLD-041 (the summary's key), REQ-WLD-042 through REQ-WLD-044 (the model
  name picks the translator's handler, and an unclaimed model stops startup
  when a key is held), and REQ-AI-030 with REQ-AI-023 (the same for the
  summary). REQ-WLD-028 and REQ-WLD-029 stay, now
  asserting the kept DeepL adapter's English target (ADR-0115).

## Amendment (2026-09-30) — places are localized, not copied

Version 1 of the prompt told the model to copy "the names of people, places,
aircraft, and organizations" unchanged. Nearly every reporter-added
type-ahead value is a place (a launch, a landing field, a town), so the
reviewer's Translate button returned it in English: "Prairie Mountain, AB"
came back as its own French. DeepL had localized the same text ("Mont Yamaska
Nord", "Lumby, C.-B.") (#704).

`locales/translation-prompt.v2.md` replaces it for the API, the Worker, and
CI. It still copies the names of people, aircraft, and organizations. It
localizes places in both directions: a place's established name in the
target language (Colombie-Britannique, Québec, Mexique), the Canadian
province and territory abbreviations (BC ↔ C.-B., AB ↔ Alb.), and the generic
word in a name (Mount ↔ mont, Lake ↔ lac), copying the specific part
(Yamaska, Cochrane) and a municipality's official name (Saint-Pie,
Mont-Saint-Pierre). Version 1 stays in the repository unedited. Values
already saved are not re-translated. Claim: REQ-WLD-047.
