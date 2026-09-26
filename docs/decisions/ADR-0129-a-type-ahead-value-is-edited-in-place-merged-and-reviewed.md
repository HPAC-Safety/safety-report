---
title: A type-ahead value is edited in place, merged, and reviewed by a safety officer
description: A type-ahead value is corrected in place for every answer that names it, removed by soft delete, merged into another without rewriting answers, machine-translated by the Worker when a reporter adds it, and flagged for a Safety Officer or Administrator to review.
type: adr
status: accepted
date: 2026-09-25
decision-makers: Chase Florell
keywords: type-ahead, autocomplete, reporter-added, choices, merge, review, safety officer, soft delete, translation, Worker, ADR-0063, ADR-0095, ADR-0112
---

# ADR-0129 — A type-ahead value is edited in place, merged, and reviewed by a safety officer

## Status

Accepted. This ADR:

- **supersedes**
  [ADR-0063](ADR-0063-a-reporter-may-add-a-type-ahead-choice.md). What still
  stood of it (a reporter may add a value; one row per value; no approval
  gate) is restated here.
- **amends**
  [ADR-0095](ADR-0095-a-question-owns-its-choices-outside-its-revisions.md):
  a reporter-added value is reviewed by a Safety Officer or an Administrator,
  on a review page as well as in the question editor, and a removed value a
  reporter types again is flagged again.
- **amends**
  [ADR-0112](ADR-0112-only-answers-that-need-it-get-a-second-language.md): the
  Worker translates a reporter-added value's missing label on the choice, not
  on each answer.
- **amends**
  [ADR-0077](ADR-0077-typeform-json-import-and-export.md): its "an
  administrator merges by hand afterward through the existing option-set
  editor" is replaced by the merge below.
- **amends** AGENTS.md invariant 1 (the reporter type-ahead bullet and
  "Second language").

It builds on
[ADR-0128](ADR-0128-an-answer-names-its-choice-and-a-picker-option-is-fixed-or-replaced.md):
an answer names its choice by ID.

Amended by
[ADR-0146](ADR-0146-a-choice-list-may-depend-on-another-questions-answer.md):
on a type-ahead whose values depend on another question's answer, typed words
are matched only among the values under that answer, a new value is offered
under it, a reviewer may change a value's link, and two values merge only
under the same parent choice.

## Context

A type-ahead is where reporters name things the form cannot list in advance:
flying sites, mostly. Its values are typed by people in a hurry, in two
languages. "Coopers" and "Cooper's" arrive as two values that mean one site,
and a site name arrives misspelled or in the wrong case.

A picker option is authored by an Administrator and its meaning must never
drift under an old answer (ADR-0128). A type-ahead value is different: its
purpose is to name one real thing consistently, so a correction should reach
everybody who named it.

Until now only an Administrator could touch a reporter-added value, a
reporter-added value stayed one-language until someone typed its other label,
and there was no way to fold two values into one. The owner ruled (#477) that
the people who read reports, safety officers, curate these values.

## Decision

### A type-ahead value is edited in place

- A Safety Officer or an Administrator may change a type-ahead value's wording,
  in either language. The choice keeps its ID, so every answer that names it,
  past and future, shows the correction.
- There is no replace for a type-ahead value. Its meaning is the thing it
  names, and a correction is always a fix.

### Removal is a soft delete

- A removed value stops being offered to reporters.
- Every answer that names it still names it and shows its label.
- A reporter who types a removed value's wording again gets an answer naming
  that removed value. It is not revived, and it is flagged for review again, so
  a reviewer sees it is still in use.

### Two values merge into one

- A Safety Officer or an Administrator may merge value B into value A of the
  same type-ahead question. B is removed and records that it was merged into A.
- **Answers are not rewritten.** An answer naming B resolves to A wherever it
  is read, following merges in a chain, and shows A's labels.
- **A later reporter typing B's wording gets A.** The new answer names A
  directly.
- A merge cannot form a cycle, and a value cannot merge into a removed value.
- Only type-ahead values merge. A picker option is replaced instead
  (ADR-0128).

### A reporter-added value is flagged, offered at once, and translated

- A value a reporter types that the question does not offer joins the
  question's choices in the same transaction as the report, in the language it
  was typed, as ADR-0095 says. Two reporters typing the same wording produce
  one value, and a reporter's spelling never replaces a reviewer's wording.
- **It is flagged for review**, and it is offered to the next reporter at
  once. There is no approval gate: a missing site must not block the next
  report.
- **The Worker supplies its other language**, off the submission path, through
  the question-authoring translation port. The label records that it was
  machine-translated (`auto`); a reviewer's edit records `human`. Until the
  Worker runs, the value is offered in the language it has (ADR-0095).
- The submission path still calls no translation provider.

### Matching a typed value

When a reporter submits text rather than a choice ID, the API matches it,
ignoring case, against the question's values in both languages, in this order:

1. a live value: the answer names it;
2. a value merged into another: the answer names the value it was merged into;
3. a removed value: the answer names it, and it is flagged again;
4. otherwise, a new reporter-added value.

### Review

- A reviewed value is one a Safety Officer or an Administrator has approved,
  edited, merged, or removed. Each is an audited write with the reviewer's
  token subject, which joins to nothing (AGENTS.md invariant 7).
- A review page lists every flagged value across questions, for both roles,
  with its question, language, and how many answers name it. Each value can be
  approved, edited, merged, or removed from there. The Admin menu counts the
  flagged values for both roles.
- The question editor still shows an Administrator each flagged value.
- A picker option is never reviewed this way: only an Administrator authors
  it, and no reporter adds to it.

## Rejected alternatives

- **Hold a new value until it is approved.** The next reporter at the same
  site would type it again, producing duplicates for the reviewer to merge,
  and would not see it offered (ADR-0063 rejected this for the same reason).
- **Treat a type-ahead value like a picker option (fix or replace).** A
  site's name has one right spelling. Replacing would leave the old spelling on
  every earlier answer, which is exactly what the owner wants corrected.
- **Merge by rewriting answers to name A.** It rewrites immutable answers
  (ADR-0080). Following the merge link on read gives every reader A without
  touching them.
- **Administrator-only curation.** Safety officers read every report and are
  the ones who know two names mean one site.
- **Leave the other language to a human.** A French reporter would see an
  English-only site until someone typed its French label. The Worker already
  owns a translation port for exactly this kind of off-path work.

## Consequences

- `question_choices` gains a review flag, the time it was last reviewed, a
  merged-into link, a creation time, and a per-label source (`auto` or
  `human`).
- A new Reviewer-policy API and page review type-ahead values. It adds an
  Admin menu option and a pending count for a Safety Officer, who until now
  saw only reports.
- The Worker gains one outbox job: translate a reporter-added value's missing
  label.
- A type-ahead answer no longer needs the answer-level `machine` translation:
  both labels come from its value.
- A reviewer who removes identifying text from a value (a person's name typed
  as a site) removes it for every answer and every future reporter at once.

## Related

- [ADR-0128](ADR-0128-an-answer-names-its-choice-and-a-picker-option-is-fixed-or-replaced.md) — answers name choices; pickers.
- [ADR-0063](ADR-0063-a-reporter-may-add-a-type-ahead-choice.md) — superseded.
- [ADR-0095](ADR-0095-a-question-owns-its-choices-outside-its-revisions.md), [ADR-0112](ADR-0112-only-answers-that-need-it-get-a-second-language.md), [ADR-0077](ADR-0077-typeform-json-import-and-export.md) — amended.
