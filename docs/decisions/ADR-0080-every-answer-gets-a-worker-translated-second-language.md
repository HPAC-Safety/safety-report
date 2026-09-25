---
title: Every answer gets a Worker-translated second language; the submitted value is immutable
description: Every answer with a value gets translated into the other official language, mechanically, off the request path.
type: adr
status: partially-superseded
date: 2026-09-22
decision-makers: Chase Florell
keywords: translation, DeepL, ITranslator, worker, answers, provenance, immutability
---

# ADR-0080 — Every answer gets a Worker-translated second language; the submitted value is immutable

**Status:** Narrowed by
[ADR-0112](ADR-0112-only-answers-that-need-it-get-a-second-language.md): only
free text marked as needing translation, and a type-ahead value naming no
bilingual choice, are machine-translated. A select answer copies its choice's
other label at submission, and every other answer has no second language. The
immutability of `value` and `locale` stands, with one exception:
[ADR-0130](ADR-0130-a-yes-or-no-answer-is-stored-as-a-boolean.md) converts
every stored yes/no and checkbox word to a boolean, once, in one migration.

## Context

[ADR-0072](ADR-0072-every-answer-is-stored-as-a-string.md) gave
`report_answers` a `locale` and a `needs_translation` flag, but scoped the
flag to select/picker/type-ahead answers only. A free-text answer is stored
in the reporter's language and stays that way forever — nobody, human or
machine, ever produces its other-language counterpart. That rule traces back
to [ADR-0062](ADR-0062-administrators-may-machine-translate-question-text.md):
*"No reporter narrative is ever translated. A translated account of a crash
is a paraphrased account of a crash, and that remains forbidden."*

The owner's ruling this session is that every answer — narrative included —
needs a second-language value in the database, because a safety officer or
administrator reviewing a report in one language cannot be blind to half of
it. The objection ADR-0062 raised is still correct about what it was
actually objecting to: an LLM asked to translate a paraphrases, summarizes,
and drifts. It is not an objection to mechanical translation. DeepL, the
provider `ITranslator` already wraps, is asked to translate one string and
return one string; it does not decide what the crash was about the way a
summarization model does. The distinction the repository already draws
between the anonymized AI summary (AGENTS.md invariant #3, one model call,
scrubbed identity) and a literal machine translation (ADR-0062, DeepL,
admin-drafting-aid) is the distinction that resolves this: this ADR extends
the second kind, not the first.

## Decision

**Every answer with a value gets translated into the other official
language, mechanically, off the request path. The submitted value is never
touched again.**

### Immutability

`report_answers.value` and `report_answers.locale` are write-once, at
submission, in `POST /api/v1/reports`
([issue #14](https://github.com/HPAC-Safety/safety-report/issues/14)). No
endpoint, admin screen, or background job ever updates either column. This
is the answer of record: whatever the reporter actually wrote, in whichever
language they actually used.

### Schema

- `value` — unchanged, immutable, the reporter's own words.
- `locale` — unchanged, immutable, the source language.
- `value_translated` — nullable string. Null until filled; the other
  language once it is.
- `translation_source` — nullable enum, `auto` or `human`. Null exactly
  when `value_translated` is null.
- `needs_translation` is **dropped**. It is now redundant with
  `value_translated IS NULL`, and unlike the earlier decision, that
  condition now applies uniformly to every answer shape, not just
  select-shaped ones, so a second column tracking the same fact adds
  nothing.

### Who fills `value_translated`, and how

**Auto — the Worker, mechanically, off the request path.** At submission,
`POST /api/v1/reports` enqueues one additional outbox message (alongside the
existing summarization and per-file attachment messages already specified
for #14) requesting translation. The Worker claims it and calls the existing
`ITranslator` port — `DeepLTranslator` in `HpacSafety.Infrastructure`, the
same adapter [ADR-0062](ADR-0062-administrators-may-machine-translate-question-text.md)
built for admin-drafted translation — once per answer that has a `value` and
no `value_translated` yet. It writes `value_translated` and
`translation_source = 'auto'`. This is not the AI summarization call:
AGENTS.md invariant #3's "exactly one model call per summary attempt"
still means exactly one call, to the summarization model, for the summary
pair. Answer translation is a separate, non-AI, mechanical job using
infrastructure that already exists for a different admin-facing feature.

`ITranslator` already lives in `HpacSafety.Core`; both `HpacSafety.Api` and
`HpacSafety.Worker` already carry a `ProjectReference` to
`HpacSafety.Infrastructure`. No new assembly, no new abstraction — the
Worker takes the same DI dependency the admin translate endpoint already
uses.

**Human — an administrator edits `value_translated`.** The existing
translation-queue screen (ADR-0072, previously select-only) widens to
include every answer type. Saving an edit writes `value_translated` and
sets `translation_source = 'human'`, whether the prior state was `auto` or
unset.

### What does not change

- The reporter never sees or produces a translation. They submit once, in
  one language, and that is what `value`/`locale` record.
- `value` is never overwritten by a translation, auto or human. There is
  exactly one column that is ever the paraphrase-risk ADR-0062 named, and
  it is `value_translated`, not the answer of record.
- The public bilingual summary (AGENTS.md invariant #3/#4) is unaffected:
  it is still produced by exactly one anonymized model call from
  `report_content`/`private_context`, and it still never reads
  `value_translated` — the two bilingual mechanisms serve different
  readers (an internal reviewer reading a full answer vs. the public
  reading an anonymized summary) and stay independent.

## Consequences

- `report_answers` grows two columns and loses one (`needs_translation`).
  A migration is owed as part of #14, before any row exists in a deployed
  environment (no submission endpoint has ever run outside a discarded
  Testcontainers database, same argument ADR-0072 already made for dropping
  `selected_option_codes`).
- The Worker gains a second responsibility and a second outbox message
  type, distinct from summarization, and a DI dependency on `ITranslator`
  already available through the existing `HpacSafety.Infrastructure`
  reference. This ships as its own PR, not folded into #14:
  `HpacSafety.Api`, which owns #14, does not need to depend on
  `ITranslator` at all — it only enqueues the outbox message.
- Every submitted answer costs one DeepL call per report language pair,
  a real per-report cost where there was none for narrative answers before.
- `translation_source` distinguishes a DeepL draft from a human-reviewed
  one, so a reviewer can tell which they are reading — narrower than a full
  audit trail (no history of who/when), matching the level of provenance
  ADR-0062 already found sufficient ("an administrator pressed Save").

## Alternatives rejected

**Use the summarization model to also translate each answer, in the same
call.** One call total instead of one-plus-DeepL-calls. Rejected: it would
put raw, non-anonymized answer text into the input/output of the one model
call AGENTS.md invariant #3 scopes to the summary pair, and an LLM rewrite
is exactly the paraphrase risk ADR-0062 named. DeepL's literal translation
is not.

**Translate synchronously on the submission path.** Considered and rejected
earlier this session: it adds an external-service round trip (and a new
failure mode — submission blocked by a translation outage) to the one
write path this system deliberately keeps minimal, and contradicts
ADR-0062's "translation is administrator/Worker-initiated, never on the
submission path."

**Keep narrative answers untranslated, add a `translation_source` only to
select answers.** The narrower, previously-decided scope. Rejected by the
owner: a reviewer reading a report needs the whole thing in their language,
not just the structured fields.

## Related

- [ADR-0062](ADR-0062-administrators-may-machine-translate-question-text.md) — `ITranslator`/DeepL, the mechanism this reuses; its "no narrative" clause is narrowed by this ADR
- [ADR-0072](ADR-0072-every-answer-is-stored-as-a-string.md) — the per-answer bilingual mechanism this widens from select-only to every answer
- [issue #14](https://github.com/HPAC-Safety/safety-report/issues/14) — submission endpoint, owns the immutable `value`/`locale` write and the new outbox message
- `features/report-submission/report-submission.feature`
