---
title: A condition follows its parent through a fork
description: When a conditional question's parent forks, the dependent question is neither revised nor forked; every reader resolves the retired parent to the live question that replaced it, and the retired choice to its copy there.
type: adr
status: accepted
date: 2026-09-25
decision-makers: Chase Florell
keywords: conditional questions, fork, dependency, choices, question bank, ADR-0071, ADR-0074, ADR-0128
---

# ADR-0132 — A condition follows its parent through a fork

## Status

Accepted. This ADR **amends**:

- [ADR-0071](ADR-0071-an-answered-question-forks-instead-of-revising.md): a fork leaves the questions that depend on the forked one as they are;
- [ADR-0074](ADR-0074-a-single-select-parent-may-enable-a-conditional-question.md): a condition resolves its parent through a fork;
- [ADR-0128](ADR-0128-an-answer-names-its-choice-and-a-picker-option-is-fixed-or-replaced.md): its "a condition follows a replacement" extends to the parent's fork.

## Context

A conditional question stores the parent question it depends on, and for a
single-select parent the required choice, by ID. Editing an answered parent's
wording forks it (ADR-0071). The parent is retired, and a new question with a
new ID and the same key takes its place, carrying copies of its choices with
new IDs and the same codes.

The dependent question still named the retired parent. The report form could
not find that parent among today's questions and showed the dependent
unconditionally. The editor named a parent that no longer existed (#504).

## Decision

**A dependent question is neither revised nor forked when its parent forks.**
Every reader resolves the condition as it stands today:

- **The parent** is the question it names while that question is live. Once
  that question has forked, it is the live question with its key. A key is
  shared only by one fork chain and never reused (REQ-QB-096), so that
  question is the one that replaced it.
- **The required choice** is the named choice's copy on that question (the copy
  keeps the code), followed through any replacement (ADR-0128).

The readers that resolve it are the report form's view, the editor's view,
choice-removal and cycle checks, and the Typeform export. They load the
retired parents live questions still name, alongside the live bank.

Saving a condition back unchanged keeps what is stored, the same as for a
replaced choice, so opening and saving the dependent does not revise it.

## Rejected alternatives

- **Revise each dependent when its parent forks.** An answered dependent would
  fork too, for an edit its Administrator never made. ADR-0128 rejected the
  same cost for replacements.
- **Refuse to fork a parent something depends on.** An Administrator would have
  to detach and reattach every dependent to reword a question.
- **Store a forked-into link on the retired question.** It duplicates what the
  shared key already says, and a second record of the same fact can diverge.

## Consequences

- A condition's stored parent can be a retired question for good. Every reader
  resolves it; none may compare it with a live question's ID directly.
- A question deleted rather than forked has no live successor, so a condition
  naming it resolves to nothing, as before.

## Related

- [#504](https://github.com/HPAC-Safety/safety-report/issues/504).
