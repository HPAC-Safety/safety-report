---
title: Scenarios describe behavior, not implementation
description: A scenario's steps and title say what a reader observes, in the glossary's outcome phrases, never a status code, a storage name, a transport detail, or a reason; lint-scenarios refuses each, and the step definition keeps the detail.
type: convention
status: accepted
date: 2026-10-03
---

# CONV-004 — Scenarios describe behavior, not implementation

## Rule

- Write each step and scenario title as what a reader of the product
  observes. The detail that proves it — the status code, the table and
  column, the route, the SQL — lives in the step definition, and a
  storage rule's column names in its `CON-*` claim.
- **No HTTP status code** (`no-http-status`). Say the outcome with the
  glossary's [outcome phrases](../glossary.md#outcome-phrases): "the request
  is refused as forbidden", not "the API answers 403". The step definition
  still asserts the code.
- **No storage identifier** (`no-storage-identifiers`): no snake_case or
  camelCase/PascalCase name, no configuration key (`Translation:Model`), no
  `Postgres`, `SQL`, `column`, `join table`, database `trigger`, or `enum`, no
  `outbox`, `DTO`, `JSON`, or `boolean`. Name the thing as the reader knows
  it: "a stored answer's words", "a Worker job", "the publication consent
  question", "a type-ahead question", "true or false".
- **No transport term** (`no-transport-terms`): no HTTP verb, `HTTP`,
  endpoint, route or path (`/report`), URL, request or response body, query
  string, or HTTP header, and never "the API" (J3). The subject is the
  request, the record, the page, or the actor: "the report is refused as
  invalid", "the comment is created". The site's header is not an HTTP header.
- **No rationale** (`no-rationale`): no "because", "instead of", "rather
  than", "so that", "in order to", or a causal ", since" in a title, a step,
  or a cell a step reads. A contrast is an assertion: "shows a sign-out action
  and no member sign-in action". The reason goes in the area README or an ADR.
- **No locale code in a step** (`no-locale-codes`, J16): steps say English
  and French; `en-CA` and `fr-CA` appear only in Examples cells.
- What the rules read: the Feature, Rule, Background, and Scenario names,
  every step, and each Examples cell a step or the title reads through its
  `<placeholder>`. Quoted `"interface copy"` is skipped, and so is a cell
  every step reads inside quotes; a `` `code span` `` is not, because an
  identifier in backticks is still an identifier. Product names and key
  names (`CloudFront`, `ArrowDown`) are not identifiers.
- Rewording a step keeps its meaning: the step definition asserts exactly
  what it asserted before, its claim ID stays, and the renamed text binds
  nowhere else, as
  [CONV-003](CONV-003-scenarios-and-area-readmes-use-the-glossary.md) asks
  of a rename. In the Reqnroll suite, `Outcomes.Status` gives the status an
  outcome phrase stands for, and `GlossaryNames.QuestionType` the type a
  glossary name ("type-ahead") stands for.

## Why

Steps that read "Then the API answers 403" or "Then Postgres refuses it,
naming report_answers.value" say how the system is built, not what it does,
so a reader needs the code to understand the claim, and a refactor that keeps
the behavior still breaks the scenario. The owner ruled that every scenario
states behavior declaratively, with no baseline, the status codes replaced by
the glossary's outcome phrases, "the API" banned as a subject (J3), the
database guarantees rewritten declaratively with their SQL kept in the step
definitions and the `CON-*` claims (J13), and locale codes only in Examples
cells (J16)
([#815](https://github.com/HPAC-Safety/safety-report/issues/815#issuecomment-5973100915),
[#824](https://github.com/HPAC-Safety/safety-report/issues/824)).

## Enforced by

- `node tools/gherkin/lint-scenarios.ts`, in the `cucumber` job of `ci.yml`,
  which `tools/dev/ci-local.sh` runs. It parses each feature with the pinned
  Gherkin parser, so a later rule can judge a scenario's shape as well as its
  words. No git hook runs it.
- `node tools/spec/generate-traceability.ts` (the `docs` job) fails a built
  claim whose reworded step no definition binds.
- Whether a reworded step still asserts the same thing is written, not
  checked: the reviewer compares the step definition before and after.
