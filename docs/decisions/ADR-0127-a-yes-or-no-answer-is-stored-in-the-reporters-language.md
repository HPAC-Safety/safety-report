---
title: A yes or no answer is stored in the reporter's language
description: A French reporter's answer to a yes/no, checkbox, or consent question is stored as oui or non, its other language is the fixed counterpart written at submission, and every reader accepts all four words.
type: adr
status: accepted
date: 2026-09-25
decision-makers: Chase Florell
keywords: answers, yes/no, checkbox, consent, oui, non, locale, translation, conditional questions, ADR-0072, ADR-0112
---

# ADR-0127 — A yes or no answer is stored in the reporter's language

## Status

Accepted. This ADR:

- **partially supersedes**
  [ADR-0072](ADR-0072-every-answer-is-stored-as-a-string.md): its boolean row
  (`yes` or `no`, always) and its consequence that a stored `oui` is a bug. Its
  date, time, and date-and-time forms stand: they stay ISO 8601 whatever the
  reporter's language.
- **partially supersedes**
  [ADR-0112](ADR-0112-only-answers-that-need-it-get-a-second-language.md): its
  `none` row no longer lists yes/no and checkbox. Everything else it decided
  stands.
- **amends** AGENTS.md invariant 1 ("Second language" and "Storage forms"),
  and the checkbox refusal #453 built for REQ-QB-118, which now expects the
  reporter's language.

## Context

Every other answer is stored in the reporter's own words and language: a
French reporter's picked label is the French label, and their free text is
what they typed. A yes/no answer was the exception. ADR-0072 stored it as the
invariant tokens `yes` and `no` so that a conditional question's check
(ADR-0060) and the consent projection would not depend on the reporter's
language. A French reporter's answer therefore read `yes` in their own report.

The owner ruled (#452) that a yes/no answer follows the same rule as every
other answer: a French reporter's yes is `oui`.

## Decision

**Which answers.** A French reporter's answer to a `yes_no` or `checkbox`
question, including both consent questions, is stored as `oui` or `non`. An
English reporter's is stored as `yes` or `no`. Dates, times, date-times, and
numbers keep ADR-0072's invariant forms.

**Validated against the report's language.** The submission accepts `yes` or
`no` from an English report and `oui` or `non` from a French one, and refuses
anything else. The form keeps its own language-free token while the reporter
answers, and writes the submission language's word when it submits. Switching
language mid-form loses nothing.

**The other language is the fixed counterpart, written at submission.** `yes`
pairs with `oui`, and `no` with `non`. It is a lookup, not a translation: no
provider is called, and nothing is queued for the Worker. The answer records
`translation_mode = fixed` and `translation_source = fixed`.

**Every reader accepts all four words.** The consent projection, a
conditional question's check (server and form), and the display read `yes` or
`oui` as yes and `no` or `non` as no. Nothing else is yes: an unanswered or
unreadable consent is still not consent.

**Existing answers stay as they are.** Answers are immutable. A French
reporter's answer stored as `yes` before this decision keeps `yes` and has no
second language. Every reader accepts it permanently.

## Rejected alternatives

**Keep the invariant tokens (ADR-0072).** It is simpler for readers, but it
leaves the one answer type that is not in the reporter's own words.

**Machine-translate the second language in the Worker.** It would route a
two-word vocabulary through a paid provider off the submission path and leave
the answer without a second language until the Worker ran. The pair is fixed;
a lookup is exact and immediate.

**No second language at all.** The readers would accept both spellings anyway,
but a reviewer reading in the other language would see the reporter's word
untranslated. The counterpart costs nothing.

**Accept any of the four words from either language.** It would let an
English report store `oui`. The stored word would then not be the reporter's
own, which is the point of the change.

**Rewrite existing answers.** Answers are immutable (ADR-0080), and a
migration that rewrote them would break that for no reader's benefit, since
every reader accepts both forms.

## Consequences

- `QuestionRevision.YesNoCodes` gives way to one vocabulary that knows both
  languages' words, their counterparts, and which word means yes.
- `translation_mode` and `translation_source` each gain `fixed`, with their
  `CHECK` constraints recreated.
- The form's yes/no and checkbox inputs keep a language-free token and write
  the report language's word at submission.
- A consumer comparing a boolean answer to `"yes"` alone is a bug; it uses the
  vocabulary.
