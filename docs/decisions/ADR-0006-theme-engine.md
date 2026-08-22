# ADR-0006 — Tailwind v4 standalone CLI

**Status:** Accepted

## Context

The UI is plain HTML and JavaScript with no SPA framework. It still needs a
coherent visual system, and it should resemble hpac.ca while looking current.

## Decision

Tailwind v4 via the **standalone CLI binary** — no npm, no node in the web build.
HPAC's palette and type are declared once as `@theme` tokens.

## Alternatives

- **Open Props.** Closest to "pure HTML and CSS", zero build step, ~1.5KB of
  tokens. Rejected because far more CSS ends up hand-authored, and agents have
  much less training data for it.
- **UnoCSS.** Faster engine, Tailwind-compatible syntax — but requires a node
  build step and has a smaller community.
- **Pico CSS.** Excellent for classless semantic forms; too limiting once custom
  layout is needed.
- **Bootstrap.** Recognisable, but dated in a way that works against "visibly
  better".

The deciding factor was agent reliability: agents write correct Tailwind far more
consistently than any niche framework, and this codebase is written primarily by
agents.

## Consequences

- One binary in the build, no `node_modules` for the web layer.
- Raw hex values are banned in markup; components reference semantic tokens.
- Dark mode is a token redefinition rather than a parallel stylesheet.
