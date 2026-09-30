---
title: Gemini translates everything between Canadian English and Canadian French
description: Gemini replaces DeepL for every machine translation, runtime and CI, en-CA to fr-CA and back; a separate ITranslator call with its own model and reasoning setting, the same Gemini key, one versioned prompt, and the term list sent with every request. Translation is carved out of invariant 3.
type: adr
status: accepted
date: 2026-09-29
decision-makers: Chase Florell
keywords: translation, Gemini, DeepL, ITranslator, en-CA, fr-CA, terms, prompt, invariant 3, ADR-0104, ADR-0115
---

# ADR-0179 — Gemini translates everything between Canadian English and Canadian French

**Status:** Accepted. Supersedes
[ADR-0115](ADR-0115-the-english-translation-target-is-configuration.md) in
full, the DeepL clause of
[ADR-0022](ADR-0022-translation-provider-is-configuration.md) (the CI
provider), and the DeepL mechanism in
[ADR-0102](ADR-0102-a-term-list-holds-the-french-translator-to-a-word.md) and
[ADR-0109](ADR-0109-no-translation-stand-in-in-any-environment.md).
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

1. **Gemini does all machine translation, in every environment and in CI.**
   DeepL is removed: the adapter, its options, the secret, Terraform, the
   workflow secret, the docs, and the tests.
2. **The pair is `en-CA` ⇄ `fr-CA`.** French to English is written in
   Canadian spelling (colour, centre). `EN-GB`, `EN-US`, and
   `Translation:EnglishTarget`, with its startup check, are gone. The prompt
   names the target variant; nothing is mapped to a provider language code.
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
     `DEEPL_API_KEY` secret is deleted, and the key revoked at DeepL, by the
     owner after merge; nothing in this change creates, deletes, or edits a
     GitHub secret.

   The model and reasoning effort are the `Translation` section's own,
   `Model` = `gemini-3.7-flash` and `ReasoningEffort` = `low`, so translation
   is tuned apart from summaries. The provider strategy is the existing
   `AiChatClient` one (`IAiChatClient`, ADR-0104); the translator asks it for
   one JSON object and validates the reply itself. A blank model or an
   undefined reasoning level stops the host at startup, naming the setting.
   With no key, translation is unavailable in every environment, Development
   included, and there is still no stand-in (ADR-0109).
5. **CI moves too.** `tools/translator.mjs` gets a Gemini adapter and
   `i18n-translate.yml` reads `GEMINI_API_KEY_DEV`. ADR-0022's CI-provider
   clause is superseded; its adapter-swap design and the `stub` provider
   stand. The generic `chat-completions` adapter is replaced by the Gemini
   one, which is that same OpenAI-shaped call with Gemini's endpoint,
   the model, and the reasoning effort fixed by settings.
6. **One term list, one prompt, for every translation.**
   - `locales/terms.json` (ADR-0102) is sent with every translation request,
     runtime and CI alike, so "upload" becomes "téléverser" everywhere.
     `--check` still enforces the forbidden forms on the catalogue. The
     ten-term and 300-character ceilings were DeepL's and go.
   - The instruction is one sentence per term, stated once and applied in
     both directions: the French rendering, the forms never to use, and that
     French text written with that rendering is the English term.
   - **The prompt is one current versioned file**,
     `locales/translation-prompt.v1.md`, beside the term list. Both runtimes
     read that one file: `tools/translator.mjs` from disk, and
     `HpacSafety.Infrastructure` as an embedded resource. A behavior change is
     a new version file (`v2`); a used version is never edited. It is not a
     Worker prompt: it is shared with CI and the API, so it does not live
     under `src/HpacSafety.Worker/Prompts/`, and like them its bytes are the
     model payload, so the frontmatter check exempts it.
7. **No new provenance.** Each translated value keeps its `auto` / `human` /
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

## Rejected alternatives

- **Google Cloud Translation LLM.** It offers `en-CA`, but only in `global` or
  `us-central1`, adds a second vendor and a service-account credential, and
  gains nothing over the Gemini key the system already pays for.
- **AWS Translate or Azure.** Neither offers `en-CA`.
- **Keeping DeepL for French and adding Gemini for English.** Two providers,
  two keys, two term mechanisms, and French would still need DeepL-specific
  quirks (`FR-CA` is target-only). One provider is simpler to keep true.
- **A new Secrets Manager entry, or a second Gemini key, for translation.**
  The owner confirmed the existing key in each environment is the one to use;
  a second key buys nothing.
- **Recording the model or prompt version per translated value.** Out of
  scope (decision 7); it would need a migration for a value nobody reads.
- **Carrying the prompt as a C# string and a JS string.** The two would drift;
  a single file is one thing to review and version.
- **Reusing the summary's model setting.** Translation is a literal, low-effort
  job with different failure modes; tuning it must not touch summaries.

## Consequences

- French comments, answers, and reviewer drafts now translate into Canadian
  English. Existing `auto` values that DeepL wrote in `EN-US` stay as stored.
- The API now calls Gemini, so it holds the Gemini secret's ARN and IAM read.
  The DeepL secret leaves `infra/secrets.tf` and `infra/iam.tf`. Its entry
  carries `prevent_destroy`, so it is moved out of the entries map and a
  `removed` block destroys it; `terraform apply` schedules it for deletion
  (Secrets Manager's recovery window). The deploy workflow no longer requires
  or copies `DEEPL_API_KEY`.
- A real Gemini translation run cannot be verified before this merges: CI's
  `i18n-translate.yml` on the base branch still runs the old tool until then.
  The issue keeps `verify:translation-run`
  ([ADR-0103](ADR-0103-a-translation-run-reports-to-the-issues-waiting-on-it.md)).
- After merge the owner deletes the `DEEPL_API_KEY` GitHub secrets (repository
  and both environments) and revokes the DeepL key.
- Claims: REQ-WLD-027 (the CI translator is told every term), REQ-WLD-033 and
  REQ-WLD-034 (the target variant is named), REQ-WLD-035 (the runtime prompt
  carries every term), REQ-WLD-036 (no key, unavailable), REQ-WLD-037 through
  REQ-WLD-039 (the batch contract), REQ-WLD-040 (its own model setting), and
  REQ-WLD-041 (the summary's key). REQ-WLD-028 and REQ-WLD-029 are deleted
  with ADR-0115, and their IDs are never reused.
