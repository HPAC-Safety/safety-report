---
title: Question translation
description: Supporting detail for machine-translating a question's wording and its choices while authoring.
type: spec
area: question-translation
prefix: REQ-QTR
---

# Question translation

Supporting detail for [`question-translation.feature`](question-translation.feature)
that doesn't fit Gherkin.

## Out of scope

What not to build here. The global list in
[system overview](../../system-overview.md) still holds; this narrows it
to this area ([ADR-0083](../../decisions/ADR-0083-specification-driven-development.md)).

- Machine translation on the submission path. Translation is Administrator-
  initiated while authoring, or Worker-run off the submission path
  ([ADR-0080](../../decisions/ADR-0080-every-answer-gets-a-worker-translated-second-language.md)).
- Translating every choice at once, or automatically. An Administrator
  translates one choice at a time from the Choices panel, in the direction its
  switch shows: a choice missing its other language, or one whose source was
  edited. The wording's Translate translates the question and help text that
  need it, never a choice or a placeholder. Each result is a draft saved only
  on Save
  (`REQ-QB-164`–`REQ-QB-170`, `REQ-QB-172`–`REQ-QB-178`, `REQ-QTR-001`,
  `REQ-QTR-003`–`REQ-QTR-009`,
  [ADR-0141](../../decisions/ADR-0141-a-choice-is-translated-on-request-in-a-chosen-direction.md),
  [ADR-0144](../../decisions/ADR-0144-the-wording-is-translated-on-request-in-a-chosen-direction.md)).
- Translating a choice on the submission path. A reporter-added type-ahead
  value gets its other language from the Worker
  ([ADR-0129](../../decisions/ADR-0129-a-type-ahead-value-is-edited-in-place-merged-and-reviewed.md)).
- Keeping a brand name untranslated. The Administrator corrects the draft.
- Saving a question in one language (`REQ-QB-071`, `REQ-QTR-002`).
