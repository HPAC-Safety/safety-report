---
title: Dependent choices
description: Supporting detail for choices offered under the answer to an earlier single-select or type-ahead question.
type: spec
area: dependent-choices
prefix: REQ-DCH
---

# Dependent choices

Supporting detail for [`dependent-choices.feature`](dependent-choices.feature)
that doesn't fit Gherkin.

## Choices that depend on another question

A single-select or type-ahead question's choices may **depend on** another
single-select or type-ahead question, its *parent*: a paraglider's model
depends on its make. Every live choice of the *child* then names **one or
more** parent choices, and the form offers it whenever the parent's answer is
any one of them (`REQ-QB-179`–`REQ-QB-228`,
[ADR-0151](../../decisions/ADR-0151-one-dependent-choice-may-be-offered-under-several-parent-choices.md),
which supersedes
[ADR-0146](../../decisions/ADR-0146-a-choice-list-may-depend-on-another-questions-answer.md)).
It is not a condition: a condition decides whether a question is shown, a
dependency decides which of its choices are offered, and a question may be
both.

- **Shape.** One level only: a child is nobody's parent, and a parent depends
  on nothing. The parent is asked before the child, on every save and every
  reorder, judged by where each is asked: a grouped question on its group's
  page (`REQ-QB-180`, `REQ-QB-181`, `REQ-QB-206`, `REQ-DCH-001`, `REQ-DCH-002`,
  `REQ-DCH-009`). The manage page shows a
  refused reorder (`REQ-QB-205`).
- **Links sit outside revisions.** The dependency is on the question and each
  link beside its choice, so setting, changing, or clearing either never
  revises or forks a question (`REQ-QB-184`). A model sold under two makes, or
  a catch-all such as "Other", is one choice offered under each make it
  applies to. Wording is unique on the child, in either language, ignoring
  case and whitespace (`REQ-QB-212`, `REQ-QB-213`, `REQ-DCH-010`). An unticked link is
  marked removed, never erased, and ticking it again restores it.
- **The editor.** With a parent set, every choice row, a new one included, has
  an "Offered under" multi-select of the parent's live choices, listed as the
  form lists them. A save with any choice offered under nothing is refused,
  naming the choices (`REQ-QB-212`, `REQ-QB-222`, `REQ-DCH-013`). Only an earlier
  single-select or type-ahead that depends on nothing is offered as the parent
  (`REQ-QB-195`, `REQ-DCH-003`). Clearing the parent keeps every link; they
  stop filtering (`REQ-QB-185`, `REQ-DCH-004`).
- **Following the parent.** A replaced picker parent choice, a merged
  type-ahead parent value, and a forked parent question each pass their links
  on at once, without revising the child (`REQ-QB-187`–`REQ-QB-190`). A link
  passed onto a parent choice the child choice already names collapses into
  one (`REQ-QB-215`). A parent choice is removed only while every child choice
  under it keeps another live parent; its links then stay and filter nothing,
  and the child still saves and its values still take new parents, since a
  save checks only the parent choices it newly ticks. Otherwise the removal is refused, naming the child choices, and the parent
  choice is replaced or merged instead (`REQ-QB-214`, `REQ-DCH-011`).
- **The form.** The child is disabled until the parent is answered, then offers
  only the choices under that answer. Changing the parent keeps a picked choice
  that is also under the new answer, and clears one that is not; typed words
  stay and are sent as typed, even when they read as a choice under another
  answer (`REQ-QB-197`, `REQ-QB-198`, `REQ-QB-223`, `REQ-QB-227`, `REQ-DCH-005`,
  `REQ-DCH-006`, `REQ-DCH-014`, `REQ-DCH-016`). A
  disabled child never holds the reporter back, even when required; nor does
  a single-select child with nothing under the parent's answer, which says so.
  The form leaves such a child out of the submission, and the API records
  nothing for it (`REQ-QB-201`, `REQ-QB-204`, `REQ-DCH-008`, `REQ-SUB-114`). A polite live
  region tells a screen reader when the parent's answer opens the child. A parent
  answered with a new typed value leaves the child a value to type
  (`REQ-QB-199`, `REQ-DCH-007`). A saved report restores both
  answers, dropping a child answer no longer under the parent's
  (`REQ-QB-200`, `REQ-DCH-015`).
- **A parent the form does not ask** — deactivated, or deleted rather than
  forked — filters nothing, as a condition whose parent is missing hides
  nothing, and the API does not check the link (`REQ-QB-203`).
- **Reporter-added values.** A value typed into a dependent type-ahead is
  matched against the whole question, ignoring case and whitespace. A match
  already under the parent's answer is named as it is (`REQ-QB-216`). A live
  match under another answer gains a link to the parent's answer and is
  flagged for review (`REQ-QB-217`); a merged match does the same through its
  target (`REQ-QB-218`); a removed match comes back flagged and is not revived
  (`REQ-QB-219`). A new value is offered under the parent's answer, even when
  that answer is itself a new value (`REQ-QB-192`).
- **Review.** A reviewer adds or removes a value's
  parents on the type-ahead review page, never down to none. A merged value's
  parents are not changed: it reads as its target (`REQ-QB-220`,
  `REQ-QB-224`, `REQ-DCH-012`, `REQ-DCH-017`); a merge keeps every parent (`REQ-QB-221`).
- **The API.** A submission naming a child choice not offered under the
  parent's answer, or answering the child while the parent is unanswered, is
  refused by question key before anything is written (`REQ-SUB-113`,
  `REQ-SUB-115`). An answer still names only its own choice; nothing about the
  parent is copied into it.
- **The migration** folds each old single link into the join table, then
  merges a dependent question's live choices whose English and French wording
  both match: the oldest survives under every parent the copies had, and each
  other copy is retired into it without rewriting any answer. A pair matching
  in one language only is left for an Administrator (`REQ-QB-225`,
  `REQ-QB-226`, `REQ-QB-228`).

## Out of scope

What not to build here. The global list in
[system overview](../../system-overview.md) still holds; this narrows it
to this area ([ADR-0083](../../decisions/ADR-0083-specification-driven-development.md)).

- For choices that depend on another question
  ([ADR-0151](../../decisions/ADR-0151-one-dependent-choice-may-be-offered-under-several-parent-choices.md)):
  - a multi-select parent or child;
  - chains deeper than one level (make → model → size);
  - a child choice under no parent choice, shown whatever the parent's answer.
    A choice for every parent ticks every parent. The one unlinked choice is a
    value a reporter typed while the parent was off the form; it waits for a
    reviewer to link it;
  - bulk linking: pasting a list, or ticking a parent across many rows at
    once. An Administrator ticks each choice's parents on its own row;
  - merging duplicates whose wording differs, in either language;
  - copying the parent's answer, or anything about it, into a child answer.
