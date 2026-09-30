---
title: A one-time migration trims label colons in place
description: A question label is stored without its closing colon and the interface adds it in the locale's style. One migration trims every stored label in place without creating a revision, a carved exception to the immutable-revision rule of invariant 1; the editor and the API refuse a new label that ends in a colon, and Typeform import strips it.
type: adr
status: accepted
date: 2026-09-30
decision-makers: Chase Florell
keywords: question label, colon, migration, in place, revision, immutability, invariant 1, localization, typeform, ADR-0071, ADR-0180
---

# ADR-0181 — A one-time migration trims label colons in place

**Status:** Accepted. Decided by the owner on 2026-09-30 in
[#697](https://github.com/HPAC-Safety/safety-report/issues/697). A carved
exception to the immutable revisions of
[ADR-0071](ADR-0071-an-answered-question-forks-instead-of-revising.md) and
`AGENTS.md` invariant 1; it does not generalize. Companion to
[ADR-0180](ADR-0180-a-summary-is-markdown-with-one-section-per-public-paragraph-question.md),
which needs a section heading that is the label without a colon. No earlier ADR
is superseded.

## Context

The seeded question bank wrote its closing punctuation into the label:
`Description:` in English, `Description :` in French (with the space French
typography asks for). A summary heading built from the label would carry the
colon, and the colon is presentation: it belongs to the interface, which can
write it in the reader's locale. Until now a label was the same string in the
form, the admin pages, and the model's input.

Fixing a label is an edit of an immutable revision. The ordinary route, a new
revision (or, once answered, a forked question), would create a second question
with the same stable key for every labelled question in the bank, purely to
move a punctuation mark. Only the development environment holds reports, and
staging is disposable, so no published or reviewed record depends on the old
text.

## Decision

1. **A label is stored without a closing colon.** The interface adds it after
   every answerable question's label, in the locale's style: `Description:` in
   en-CA, `Description :` in fr-CA. It adds none after a statement, a group, or a
   label that ends in `?`. It does so on the reporter form, the admin report
   detail, and the question-bank previews.
2. **One migration trims every stored label, in place.** It removes a trailing
   `:` or ` :` (any kind of space before it) from `label_en` and `label_fr` of
   every question revision, and changes nothing else. It creates no revision, no
   fork, and no row, deletes none, and is not reversible in intent: its `Down`
   does not put colons back. Every answer still names the same revision, and a
   label that has no trailing colon is untouched. This is the one place a stored
   label is rewritten; it is a carved exception to invariant 1's rule that an
   answered question forks instead of being revised.
3. **The seed has no colons.** A clean database gets colonless labels from the
   start; the migration is then a no-op on it.
4. **New labels never end in a colon.** The admin question editor refuses one
   with a message in the current language, and the API refuses it with a problem
   that says so in English and French. Typeform import strips a trailing colon
   from a title silently.
5. **The Worker strips a trailing colon too** when it builds a summary heading,
   so a label stored before the migration on a database the migration has not
   reached still makes the heading the Worker expects.

## Why this exception is safe

- It changes presentation only: no fact, answer, choice, key, or privacy
  classification moves.
- No answer is rewritten, so what a reporter said and the wording they saw differ
  only by a closing colon the interface now draws itself.
- It runs once, and the editor and the API keep a colon from returning.
- Question revisions are not among the four tables the database locks
  ([ADR-0178](ADR-0178-the-database-refuses-changes-to-the-reporters-account-and-to-summary-revisions.md)),
  so no trigger is bypassed.

## Alternatives rejected

- **Fork every labelled question.** It creates a duplicate question per label for a
  punctuation mark, and breaks the "one live question per key" history for no
  reporter-visible benefit.
- **Strip the colon only when reading** (in the Worker and the UI), leaving the
  stored text. It keeps two spellings of one label and leaves the editor free to
  write either.
- **Add the colon in the database** (a view). The colon depends on the reader's
  locale and the question's kind, which the interface knows.

## Consequences

- A question label is a noun phrase without punctuation. A label that genuinely
  ends in a colon cannot be authored; a label meant as a question ends in `?`.
- The interface formats labels, so a change to the locale style (a narrow
  no-break space before a French colon, for instance) is one place in the web
  code.
- Any future rewrite of stored revision text needs its own ADR on its own facts.

## Related

- [ADR-0071](ADR-0071-an-answered-question-forks-instead-of-revising.md) — the
  rule this carves an exception to.
- [ADR-0180](ADR-0180-a-summary-is-markdown-with-one-section-per-public-paragraph-question.md)
  — the summary sections whose headings are labels.
- Claims `REQ-QB-240` to `REQ-QB-246` and `REQ-TF-024`;
  [`features/question-bank-and-form/README.md`](../../features/question-bank-and-form/README.md).
