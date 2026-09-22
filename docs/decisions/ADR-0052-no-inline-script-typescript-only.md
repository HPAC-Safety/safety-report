---
title: No inline JavaScript in HTML; every script is an external TypeScript module
description: "No <script> tag in src/web/index.html, or any other HTML in this repository, contains JavaScript."
type: adr
status: accepted
date: 2026-09-19
decision-makers: Chase Florell
keywords: TypeScript, inline script, index.html, web front end
---

# ADR-0052 — No inline JavaScript in HTML; every script is an external TypeScript module

**Status:** Accepted

## Context

The homepage spike (issue #140) needed a small script that runs before
React mounts, to set `data-theme` before first paint and avoid a flash of
the wrong theme (ADR-0024). The first draft put that script inline in
`src/web/index.html` as untyped `<script>` JavaScript. The rest of the
application is TypeScript, type-checked and covered by the same
conventions (`skills/hpac-safety-conventions/SKILL.md`); an inline script
in the one HTML entry point sits outside all of that — no type checking,
no lint, no test, and a second place to look for logic that changes
`document.documentElement`.

## Decision

No `<script>` tag in `src/web/index.html`, or any other HTML in this
repository, contains JavaScript. Every script is an external `.ts` file
under `src/web/src/`, and `index.html` references exactly one entry point —
`/src/main.tsx` — because a build with two HTML `<script type="module">`
entry points merges them into one bundle in a build-tool-dependent order
(observed directly here: Vite/Rollup combined a second entry with `main.tsx`
in a way that broke "runs before React mounts"). Where a script must run
before anything else, including before React mounts (the pre-paint theme
script is the current example), it is a plain side-effect import at the top
of `main.tsx` — `import "./theme-init"` before any other import — so
execution order follows the one well-defined rule ES modules guarantee: an
imported module's top-level code finishes before the importing module's own
code runs.

## Why this choice

**One language, one set of guarantees, for all application logic.** An
external `.ts` file is type-checked by `tsc`, importable from a test, and
reviewed the same way as every other module — an inline script is none of
those things and is easy to forget exists.

**No practical cost.** Module scripts in `<head>` still execute before any
content exists to paint (the `<body>` is empty until React mounts), so
externalizing the theme-init script loses nothing for the flash-avoidance
requirement it exists for.

## Alternatives

- **Keep the theme-init script inline, everything else external.** Rejected:
  a special case for exactly the one script most worth type-checking (it
  touches `localStorage` and a DOM attribute other code also touches) is
  the wrong place to carve out an exception.

## Consequences

- `src/web/src/theme-init.ts` is the current example of this pattern.
- `skills/build-hpac-web-ui/SKILL.md` documents the rule so a future script
  (analytics init, a feature flag check, anything else that might tempt an
  inline snippet) goes into `src/web/src/` instead.

## Related

- [ADR-0024](ADR-0024-dark-mode-is-a-token-redefinition.md)
- [ADR-0043](ADR-0043-react-typescript-vite-web-front-end.md)
