---
title: "@ui tags the .feature scenarios that need a Playwright companion"
description: "ADR-0049 makes every features/**/*.feature scenario execute via Reqnroll/xUnit, with no separate bucket for browser-driven scenarios."
type: adr
status: accepted
date: 2026-09-19
decision-makers: Chase Florell
keywords: testing, Gherkin, feature files, Playwright, Reqnroll, tags
---

# ADR-0050 — `@ui` tags the `.feature` scenarios that need a Playwright companion

**Status:** Accepted, partially superseded by
[ADR-0053](ADR-0053-ui-scenarios-execute-via-playwright-bdd.md): `@ui`
scenarios execute via `playwright-bdd`, not Reqnroll — the sentence below
saying otherwise no longer holds. The tag itself, and everything else in
this decision, is unchanged.

## Context

[ADR-0049](ADR-0049-reqnroll-for-executable-gherkin-scenarios.md) makes
every `features/**/*.feature` scenario execute via Reqnroll/xUnit, with no
separate bucket for browser-driven scenarios. [ADR-0045](ADR-0045-ui-changes-require-playwright-and-server-tests.md)
separately requires a Playwright test alongside any PR that changes UI
behavior. Nothing connects the two: reading a `.feature` file gives no
mechanical signal for which scenarios describe browser-observable behavior
(and therefore need a Playwright companion when their `@ignore` tag comes
off) versus which describe an API/domain contract that Reqnroll alone
covers.

An audit of all 7 `.feature` files (113 scenarios) also found scenarios
that blended a client-observable assertion with a server-authoritative
contract assertion in one scenario, several places where the same fact was
asserted from two different capability files, and a few scenarios phrased
as an admin-UI flow that asserted nothing browser-observable.

## Decision

Scenarios whose Then/And steps assert something a person or assistive
technology observes in the browser (rendering, focus, keyboard order,
locale switch, client-side storage/state, warnings shown before an action)
carry an `@ui` tag alongside `@ignore`. `@ui` scenarios still execute
through Reqnroll exactly like every other scenario — ADR-0049 is
unchanged — the tag exists so a reviewer or `deliver-hpac-change` can check
mechanically, when a PR removes a scenario's `@ignore`, whether it also
needs the Playwright test ADR-0045 requires. A scenario with no `@ui` tag
never needs a Playwright companion on that basis alone.

A scenario asserting only a source/build-time structural invariant (no
literal user-facing strings in code, no third-party CDN loads) is not
browser-observable interaction and stays untagged even though it is about
the web front end.

**Splitting rule:** a scenario that blends a client-observable assertion
with a server-authoritative contract assertion splits into two scenarios —
one tagged `@ui` for the client-observable half, one untagged for the
server contract — rather than carrying both concerns in one scenario. This
is the same two-layer pattern ADR-0045 already names for client vs. server
validation.

**De-duplication rule:** when two capability files assert the same fact,
the fact stays only in the file matching its capability area in
`features/README.md`'s specification index; the other file keeps only the
assertions specific to its own area, or is trimmed to nothing and removed
if none remain.

This ADR's decision was applied immediately to the existing `.feature`
files in the same pull request: three scenarios split
(`web-localization-and-design.feature`'s client/server validation
scenario, `media.feature`'s document-download scenario, and
`web-localization-and-design.feature`'s admin-route scenario had its
redundant authorization clause removed in favor of
`moderation-authentication-and-publication.feature`'s existing coverage),
one scenario reworded to remove UI-flavored phrasing with no
browser-observable assertions
(`question-bank-and-form.feature`), and four duplicate scenarios
consolidated into their canonical capability file.

## Why this choice

**A tag, not a directory or file split.** ADR-0049 already rejected moving
`.feature` files out of `features/`; scenarios are the canonical
specification regardless of which layer executes them. A tag is the
minimal addition that makes the UI/backend distinction visible without
touching that decision.

**Reuses the existing `@ignore` mechanism.** Reqnroll/Gherkin tags are
already load-bearing in this repository; adding a second tag costs nothing
new to tool around and appears in the same place a reader already checks.

## Alternatives

- **A separate `ui.feature` file per capability area.** Rejected: doubles
  the number of `.feature` files, fragments a capability's specification
  across two files, and still needs a tag or a naming convention to signal
  which scenarios in it are "real" UI versus non-observable invariants.
- **Track the UI/backend split only in `docs/testing-conventions.md` prose,
  no tag.** Rejected: not mechanically checkable from the `.feature` file
  itself, which is where a reviewer or step-definition author is already
  looking.
- **Leave it to reviewer judgment, as today.** Rejected for the same reason
  ADR-0045 rejected it: judgment calls are how the original gap (no
  Playwright requirement at all) happened.

## Consequences

- `deliver-hpac-change` and PR review treat a scenario whose `@ignore` is
  removed and that carries `@ui` as needing a Playwright test in the same
  PR per ADR-0045; a non-`@ui` scenario needs only its Reqnroll step
  definitions.
- `features/README.md` and `skills/test-hpac-safety/SKILL.md` document the
  `@ui` tag so future scenario authors apply it without re-deriving this
  rationale.
- No new scenario behavior; this PR only reorganizes existing `@ignore`d
  scenarios (tags, one reword, three splits, four de-duplications).

## Related

- [ADR-0045](ADR-0045-ui-changes-require-playwright-and-server-tests.md)
- [ADR-0049](ADR-0049-reqnroll-for-executable-gherkin-scenarios.md)
- [`features/README.md`](../../features/README.md)
- [`skills/test-hpac-safety/SKILL.md`](../../skills/test-hpac-safety/SKILL.md)
