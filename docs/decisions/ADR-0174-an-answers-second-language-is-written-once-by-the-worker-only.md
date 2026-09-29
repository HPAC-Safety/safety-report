---
title: An answer's second language is written once, by the Worker only
description: The human translation path for a report answer is removed. Only the Worker ever writes value_translated, and only once; a second attempt is refused.
type: adr
status: accepted
date: 2026-09-29
decision-makers: Chase Florell
keywords: translation, ITranslator, worker, answers, immutability, ADR-0080, ADR-0112, ADR-0174
---

# ADR-0174 — An answer's second language is written once, by the Worker only

**Status:** Accepted. Supersedes the human-translation path of
[ADR-0080](ADR-0080-every-answer-gets-a-worker-translated-second-language.md)
and amends [ADR-0112](ADR-0112-only-answers-that-need-it-get-a-second-language.md).
Part of [#665](https://github.com/HPAC-Safety/safety-report/issues/665), whose
audit found that `ReportAnswer.SupplyHumanTranslation`, reached through
`AnswerTranslationEndpoints`, let an Administrator overwrite `TranslatedValue`
— including a translation the Worker had already produced — contradicting the
reporter's account being locked at submission.

## Context

ADR-0080 gave an answer's second language two writers: the Worker,
mechanically, through `ITranslator` (`TranslationSource.Auto`), and an
Administrator, by hand or by pressing Translate and saving the draft
(`TranslationSource.Human`), on the admin "awaiting translation" page. The
human path could overwrite an existing `Auto` translation, and could be
called again to overwrite an earlier `Human` one — the one column in this
area that anything could rewrite after the fact.

[#665](https://github.com/HPAC-Safety/safety-report/issues/665) re-examined
every place a stored value can still change after submission. The owner's
ruling: `value` and `locale` are already immutable (ADR-0072); the second
language should be too, once the Worker has written it. These translations
are read only by a Safety Officer or an Administrator reviewing a report in
their own language, never by the public and never by the reporter, so a
weak machine translation is an acceptable cost, and there is no reader who
needs a human correction badly enough to justify a second writer, a queue
page, and the drift risk of "whichever of two writers touched it last."

## Decision

**Only the Worker ever writes a free-text answer's second language, and it
writes it exactly once.**

- `ReportAnswer.SupplyHumanTranslation` and its `allowOverwrite` parameter on
  the private `SupplyTranslation` helper are deleted.
  `ReportAnswer.SupplyAutoTranslation` is the only way to set
  `TranslatedValue`, and it now refuses when `TranslatedValue` is already
  set — not only when the source differs. There is no overwrite path left in
  the domain, by any source.
- `src/HpacSafety.Api/Admin/AnswerTranslationEndpoints.cs` is deleted:
  `GET /api/admin/answers/awaiting-translation` and
  `PUT /api/admin/answers/{id}/translation`, and their DTOs
  (`AwaitingTranslationResponse`, `AwaitingTranslationView`,
  `SupplyTranslationRequest`), are gone. Both routes now return `404`, like
  any other unmapped route.
- The admin "answers awaiting translation" page
  (`ManageAnswerTranslationsPage.tsx`), its route (`/admin/answer-translations`),
  its Admin-menu nav entry, and its locale strings (`answerTranslations.*`,
  `nav.manageAnswerTranslations`) are deleted from the web app.
- The Admin menu's pending-work badge no longer includes a translation count,
  since there is no page for it to link to. `GET /api/admin/counts` is
  unchanged: it still gives an Administrator the number of answers the
  `answers_awaiting_translation` view holds, as an operational signal a
  future monitoring surface could use — nothing currently reads it but the
  count-agreement tests this ADR leaves in place (REQ-MOD-084..086).
- The Worker's `TranslateAnswersProcessor` is unchanged: it already only
  claims answers with `TranslatedValue IS NULL`, so a failed translation
  keeps retrying through the outbox exactly as before, and a second delivery
  of the same message is already a no-op once the first succeeds.
- `TranslationSource.Human` is kept as an enum member. It is never written
  again, but a row already stored under it — from before this ADR shipped to
  any environment — is left exactly as stored; nothing rewrites it
  retroactively.

### What does not change

- `ITranslator`/DeepL is untouched, and still reached from exactly two
  places: the Worker's `TranslateAnswersProcessor` (this ADR) and the
  question-authoring `POST /api/admin/translate` endpoint (ADR-0062),
  unrelated to answers.
- A choice answer's second language is still a lookup on its choice
  (`TranslationSource.Choice`, ADR-0112, ADR-0128) — this ADR touches only
  the `machine`-mode, free-text path.
- Comment translation (ADR-0114) and summary-language drafting (ADR-0108)
  are untouched; both are separate mechanisms this ADR does not reach.

## Consequences

- `AGENTS.md` invariant 1 "Second language" drops "or by an administrator by
  hand." Machine-translation purpose 3 ("an administrator correcting or
  supplying that language by hand") is deleted, and purposes 4–6 renumber to
  3–5.
- `features/report-submission/report-submission.feature`'s
  `@REQ-SUB-027` scenario ("An administrator's correction always wins over
  the Worker's translation") is deleted — the behavior it described no
  longer exists. Two scenarios replace it: `@REQ-SUB-119` (a second automatic
  translation is refused) and `@REQ-SUB-120` (the old endpoints are gone).
  `features/question-bank-and-form/question-bank-and-form.feature`'s
  `@REQ-QB-083` and `@REQ-QB-084` (the admin queue page) are deleted outright,
  and `@REQ-QB-066` drops its line about the same Translate action being
  offered for an answer. `features/moderation-authentication-and-publication/moderation-authentication-and-publication.feature`'s
  Admin-menu scenarios (`@REQ-MOD-007`, `@REQ-MOD-092`, `@REQ-MOD-087`,
  `@REQ-MOD-093`, `@REQ-MOD-089`) drop the removed option and its count, its
  `@REQ-MOD-043` outline drops the `/admin/answer-translations` row, and a
  new `@REQ-MOD-184` proves the route now shows the not-found page. The
  API-level pending-count scenarios (`@REQ-MOD-084`..`@REQ-MOD-086`) are
  unchanged: the count itself is not removed, only its consumer.
- No migration: no column, table, or enum value changes shape.
  `translation_source` keeps its three values; `human` simply stops being
  written.

## Alternatives rejected

**Remove `TranslationSource.Human` and rewrite existing rows to `Auto`.**
Rejected: the owner decided existing `human` rows stay exactly as stored
([issue #666](https://github.com/HPAC-Safety/safety-report/issues/666)).
Rewriting them would falsify recorded provenance — the row would then claim
the Worker wrote a translation it never touched — which invariant 8's
"never physically delete application records" already argues against for
the record itself, and the same reasoning extends to rewriting one in place.
Keeping the enum value and leaving old rows alone costs nothing.

**Drop the `answers_awaiting_translation` view and its count from
`GET /api/admin/counts` along with the page.** Considered, since nothing
currently links to it. Rejected for this change: the count is a real,
already-tested signal (an Administrator can tell whether the Worker still
has translation work outstanding), removing it needs its own schema and API
contract argument, and the issue that opened this decision scoped the cut to
the human-writer path and its page, not to this diagnostic.

## Related

- [ADR-0080](ADR-0080-every-answer-gets-a-worker-translated-second-language.md) — the decision this narrows
- [ADR-0112](ADR-0112-only-answers-that-need-it-get-a-second-language.md) — amended: drops the human-supplied and human-corrected paths
- [ADR-0072](ADR-0072-every-answer-is-stored-as-a-string.md) — the immutability `value`/`locale` already had; this extends it to the second language
- [issue #665](https://github.com/HPAC-Safety/safety-report/issues/665), [issue #666](https://github.com/HPAC-Safety/safety-report/issues/666)
