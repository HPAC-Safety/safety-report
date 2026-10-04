---
title: A scenario holds one behavior
description: A scenario has at most one action and at most 8 steps, never drives the browser in its words, names a key only as a noun phrase in an Examples cell, and asserts in its Then without acting; lint-scenarios refuses the first three.
type: convention
status: accepted
date: 2026-10-03
---

# CONV-006 — A scenario holds one behavior

## Rule

- **At most one action** (`one-when`, J14): one When, with the And or But
  steps that continue it, and never an action after a Then. A scenario of
  Givens and Thens only is allowed.
  - Several actions before the first Then: the earlier ones become Givens
    (`And …` under the Given), or merge into one When. The claim ID stays.
  - An action after a Then is a second behavior. It becomes its own scenario
    with a new claim ID from `node tools/spec/claim-prefixes.ts --next
    <area>`; its Givens reach the state the earlier steps reached. The
    original ID stays with the main behavior, and every assertion survives in
    exactly one scenario.
- **At most 8 steps** (`max-steps`, J15), the Background not counted. Split
  the scenario, or fold its setup into one Given.
- **No browser mechanics** (`no-ui-mechanics`), in a title, a step, or an
  Examples cell a step reads: no click, tap, hover, scroll, drag, or press, no
  keystroke or tabbing, no typing as a verb ("types into"; the noun "content
  type" is fine), no element role (`combobox`), selector, `data-` or `aria-`
  attribute, DOM, CSS, viewport, or pixel size. Say what the actor does or
  sees: "enter … in the question", "the menu toggle", "a phone-width screen",
  "large enough to touch". "Focus" and "keyboard" are allowed where the user
  observes the effect. A pixel number lives in the step definition and the
  area README.
- **A key is named only in an Examples cell, as a noun phrase**: "the Escape
  key", "the down arrow key twice", "the Alt and down arrow keys", "the m
  key". The step reads it through a placeholder ("uses <key>", "closes the
  lightbox with <key>"), and a cell names an input, never a verb ("the pointer
  outside the question", not "clicking outside"). `tests/e2e/steps/keys.ts`
  turns the cell into key presses; add a key's spoken name to its map.
- **A Then asserts; it never acts.** A step definition behind a Then does not
  perform a further behavior the scenario claims — submitting, saving,
  cancelling, re-attaching, going on. That act is a When, in its own scenario
  when the scenario already has one. Bringing the asserted thing into view
  (opening a history panel, going back to the page that shows it, reloading
  to prove a choice persisted) is how the Then looks, not a behavior.

## Why

A scenario that runs several actions with assertions between them is a
script: a reader cannot tell which behavior its claim ID names, and one
failure hides the claims after it. Keystroke scripts ran to 31 steps, and 118
scenarios had more than one When. The owner ruled one action per scenario,
Given/Then-only scenarios allowed (J14), at most 8 steps (J15), and no pointer
or keystroke mechanics in a step, with key names only in Examples cells and
pixel numbers only in step definitions and READMEs
([#815](https://github.com/HPAC-Safety/safety-report/issues/815#issuecomment-5973100915),
[#831](https://github.com/HPAC-Safety/safety-report/issues/831)). The
key-cell form is the one the report form already used for most of its keys,
because a noun phrase also names an input that is not a key ("the pointer on
the question").

## Enforced by

- `node tools/gherkin/lint-scenarios.ts`, in the `cucumber` job of `ci.yml`,
  which `tools/dev/ci-local.sh` runs: `one-when`, `max-steps`, and
  `no-ui-mechanics`. No git hook runs it.
- `node tools/spec/generate-traceability.ts` (the `docs` job) fails a new
  scenario whose step no definition binds.
- "A Then never acts" is written, not checked: the signal is in the step
  definition, so the reviewer reads it.
