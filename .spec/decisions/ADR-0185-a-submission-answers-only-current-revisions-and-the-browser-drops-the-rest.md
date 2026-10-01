---
title: A submission answers only current revisions, and the browser drops stale saved answers
description: The browser discards, when it restores a saved report, every saved answer whose question revision is no longer the current revision of a live question, and tells the reporter once. The API then refuses any answer naming a non-current revision, reversing the earlier rule that a known superseded revision is accepted.
type: adr
status: accepted
date: 2026-09-30
decision-makers: Chase Florell
keywords: submission, question revision, superseded revision, saved report, restore, notice, stale answer, ADR-0016, ADR-0071, invariant 2
---

# ADR-0185 — A submission answers only current revisions, and the browser drops stale saved answers

**Status:** Accepted. Decided by the owner on 2026-09-30 in
[#719](https://github.com/HPAC-Safety/safety-report/issues/719). Narrows
[ADR-0016](ADR-0016-data-driven-question-bank.md): its rule that an answer
references the exact revision shown no longer extends to a revision that has
since stopped being current. Reverses scenario REQ-SUB-009, "A submission may
answer a known superseded revision", which the specification carried from its
first draft and no ADR argued for; that scenario is deleted.

## Context

[ADR-0016](ADR-0016-data-driven-question-bank.md) pins every answer to the
revision it was given under, and
[ADR-0071](ADR-0071-an-answered-question-forks-instead-of-revising.md) forks an
answered question instead of revising it. The first specification also said the
API accepts an answer to a known, non-deleted, superseded revision, so that a
reporter whose browser session spanned an administrator's edit could still
submit, validated against that revision's historical type, options, and privacy.

Two things changed that. The browser already keeps a saved report for 15 days
(ADR-0100), so a session now spans edits routinely, and the form restores only
the saved answers whose revision is on the current form: an answer to a revision
the form no longer shows is not listed and not restored. After a restore the
form therefore never sends a superseded revision.
It served only a client that bypassed that restore, at the price of a validation
path that judges an answer by a type, privacy flag, and wording the form no
longer asks.

## Decision

1. **The browser discards stale answers when it restores a saved report.**
   An answer saved under a revision that is not the current revision of a
   question on the current form is dropped on restore, and the reporter
   re-answers. A saved attached file under such a question is dropped too, its
   upload erased. "Current" is the form's own: the revision the public
   current-questions endpoint returns. A question forked since the save (ADR-0071)
   is a different question: its old revision is not current, and the fork's
   revision, though it carries the same stable key, was never answered.
2. **The reporter is told once.** When one or more saved answers were dropped,
   one banner, in the interface language (en-CA and fr-CA catalogue keys), says
   the form changed since the report was saved and some answers were cleared.
   It marks no question: a cleared question simply shows empty. It shows after
   the reporter chooses to continue, and also when every saved answer was stale,
   in which case there is nothing to continue, no dialog is shown, the saved
   report is removed, and the banner sits on the fresh form. It does not show
   when nothing was dropped, nor after the reporter declines to continue.
3. **The API refuses any answer naming a non-current revision**, storing
   nothing. A non-current revision is one that is not the highest-numbered
   revision of a live (not soft-deleted) question: a superseded revision of a
   live question, and every revision of a forked or deleted question. This
   joins the existing refusals of an unknown revision, a deleted revision, a
   duplicate revision, and an answer naming a statement or a group.
4. **"Two answers naming revisions of one stable key" is not a separate case.**
   With only current revisions accepted it cannot occur: at most one live
   question carries a key (ADR-0071) and it has one current revision. That row of
   REQ-SUB-010 folds into "a superseded revision of a live question", and the
   fork case (a deleted question's revision beside the fork's) is the "deleted
   revision" row.
5. **Nothing new is stored or sent.** The saved report stays in the browser; the
   discard is a local decision, the notice is local, and the API is told
   nothing about it. Invariant 2 of `AGENTS.md` is unchanged.

## Consequences

- A reporter who saved a report before an edit re-answers the edited
  questions. This is the cost of the owner's choice; the alternative, honouring
  the old wording, kept a validation path nobody used.
- A request carrying a non-current revision, from a client that did not restore
  through the form, is now a 400 with the same safe error as an unknown
  revision, which echoes nothing submitted.
- An answered question edited while a report is being filled, without a reload,
  now fails at submission rather than succeeding: the form's own state still
  names the revision it loaded. The submit failure message is the generic one
  already shown for a refused submission; making it specific is out of scope
  until it is seen in practice.
- [ADR-0119](ADR-0119-a-published-report-offers-its-documents-for-download.md)
  recorded a yes to a superseded media-consent wording with document consent
  left unanswered. That answer is now refused at the API, which fails closed the
  same way; the rule stays in the domain for reports stored before. The
  scenario that exercised the old path now asserts the refusal (REQ-QB-247), and
  REQ-QB-116 keeps only the current-wording row.
- `Report.Answer` keeps its explicit-revision overloads for the Worker, import,
  and tests; the refusal is the endpoint's, where the lookup is.

## Alternatives considered

- **Keep accepting a superseded revision, and tell the reporter nothing.**
  Rejected by the owner: the answer is judged by wording the form no longer
  asks.
- **Re-map a stale answer onto the current revision automatically.** Rejected: an
  answer given to "Were you injured?" is not an answer to "Did you require medical
  attention?" (ADR-0071).
- **Highlight each cleared question.** Rejected by the owner: one notice,
  cleared questions simply empty.
