---
title: A report page is addressed by its question key
description: Each page of the report form has its own address, /report/<question-key>, pushed as a history entry; the address follows the form and never picks the page on arrival, and the saved page stays in the 15-day local-storage draft.
type: adr
status: accepted
date: 2026-09-23
decision-makers: Chase Florell
keywords: report form, routing, address bar, browser history, draft, local storage, question key
---

# ADR-0099 — A report page is addressed by its question key

## Status

Accepted. It builds on
[ADR-0051](ADR-0051-react-router-for-client-side-navigation.md), whose
declarative router it uses, and leaves AGENTS.md invariant 2 (the 15-day,
browser-only draft) unchanged.

## Context

The report form pages through its questions one at a time, but its address
was `/report` on every page. The browser's Back button left the form instead
of going back a page, and the address gave no clue where the reporter was.
The owner asked for the address to track the page, and for "continue where I
left off" to land on it (#366).

## Decision

- The introduction is `/report`. Every other page is
  `/report/<question-key>`, named by the question heading it. One route with
  an optional segment serves both, so paging never remounts the form.
- Next and Back push a history entry. The browser's Back and Forward then
  page too, under the form's rules: a page past an unanswered required
  question cannot be reached that way.
- The address follows the form and never leads it on arrival. With a saved
  report, the continue dialog decides the page; without one, the form opens
  at its introduction.
- The saved page stays in the existing 15-day local-storage draft, named by
  the same question key.

## Consequences

- The address holds an administrator-authored question key and nothing a
  reporter typed.
- A link to a page is not a way in. Arriving on one gives the same start as
  arriving on `/report`.
- A question key is stable across revisions and forks and unique among live
  questions, so a saved page survives a reworded question. A draft saved
  before this change names its page by revision ID and still reopens it.

## Alternatives rejected

- **Keeping the page in `sessionStorage`.** It is cleared when the tab closes,
  which is exactly when a reporter comes back to continue.
- **A page number, `?step=4`.** The number of a page changes as conditional
  pages appear and disappear, so the same address would name different
  questions.
- **The key in a query parameter, `?step=<key>`.** It says the same thing as
  a path segment, less readably.
- **Replacing the address in place.** The browser's Back would still leave the
  form, which is half of what was asked.
- **Letting the address pick the page on arrival.** A link could then skip
  required questions, or put a reporter on a page before the continue dialog
  asks whether to restore their answers.

## Related

- [ADR-0051](ADR-0051-react-router-for-client-side-navigation.md)
- `features/report-submission/report-submission.feature`: REQ-SUB-053 to
  REQ-SUB-057
