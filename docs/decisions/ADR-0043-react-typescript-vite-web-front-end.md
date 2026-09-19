---
status: accepted
date: 2026-09-18
decision-makers: Chase Florell
keywords: React, TypeScript, Vite, Tailwind, SPA, web front end
---

# ADR-0043 — React, TypeScript, and Vite replace the no-framework web build

**Status:** Accepted. Supersedes [ADR-0006](ADR-0006-theme-engine.md) (Tailwind
v4 standalone CLI, "no SPA framework") and the "no npm, no node in the web
build" stance of [ADR-0023](ADR-0023-pinned-and-vendored-web-assets.md).
ADR-0023's vendoring rationale for fonts and the logo — self-hosted, never a
third-party CDN — is unchanged; only the "no `node_modules`" mechanism it
argued from is reversed.

## Context

ADR-0006 chose plain HTML/JS specifically because "agents write correct
Tailwind far more consistently than any niche framework" and to keep the web
build free of `node_modules`. ADR-0023 built the vendoring/pinning story on
top of that same "no npm" premise, and the
[`web-localization-and-design`](../../features/web-localization-and-design/web-localization-and-design.feature)
feature file asserts "neither requires a SPA framework, client router, Node
production server, or bundler" and "core content and navigation do not depend
on JavaScript" as product invariants.

The owner has now decided the opposite, deliberately: React + TypeScript,
built with a real bundler, and the public site's no-JS requirement is
dropped — both sites become JavaScript-required SPAs. This is a reversal, not
an addition, so it is recorded as a new ADR per `AGENTS.md`'s rule that a
reversed decision keeps its reasoning in a new record rather than an edit to
the old one.

## Decision

**React 18 + TypeScript**, built with **Vite**, styled with **Tailwind v4**
via `@tailwindcss/vite` (replacing the standalone CLI). One `package.json` and
one committed lockfile (`package-lock.json`) at `src/web/`, built into a
static asset bundle (`dist/`) that a container serves — see
[ADR-0044](ADR-0044-containerized-web-hosting.md) for hosting.

Public site and admin site stay **two separately built and deployed
applications** — each its own Vite entry point/build output, own
`package.json` or own workspace package — per the standing invariant in
[`web-localization-and-design`](../../features/web-localization-and-design/web-localization-and-design.feature)
("separate origins/distributions and deployment permissions"). This ADR does
not touch that separation; only the build tooling for each changes.

## Why these choices

**React over a lighter alternative (Preact, Svelte, Solid).** The owner asked
for React by name. React's ecosystem size also matters here specifically
because this codebase is written primarily by agents (the same reasoning
ADR-0006 used for Tailwind over niche CSS frameworks) — agents have
disproportionately more training data for React/TypeScript than any
alternative.

**Vite over Create React App or a hand-rolled bundler config.** CRA is
unmaintained. Vite is the current default for a React+TS SPA: fast dev server,
first-class TypeScript, a maintained Tailwind v4 plugin, and a production
build that is plain static files — no Node process required at runtime,
which keeps ADR-0044's hosting choice a genuine choice rather than a forced
one.

**`@tailwindcss/vite`, not the standalone CLI.** ADR-0006 chose the standalone
CLI specifically to avoid a node build. That constraint is gone — the app
already needs Vite — so the Vite plugin is strictly less moving parts than
running two separate build tools.

**npm with a committed lockfile, not the hand-pinned/checksummed approach in
ADR-0023.** ADR-0023's checksum-file mechanism existed because there was no
package manager to trust. A `package-lock.json` is the standard, tool-verified
version of the same guarantee (exact resolved versions and integrity hashes
for every transitive dependency), and Renovate — already used across this
repository for .NET, Terraform, and container dependencies — gets an `npm`
manager for free instead of a bespoke unmanaged pin file.

**Fonts and the logo stay vendored, not fetched from Google Fonts at
build or runtime.** ADR-0023's privacy reasoning (a reporter's visit should
not be logged by a third party) and its rejection of `fonts.googleapis.com`
are untouched by this ADR. They move from hand-committed `woff2` files to an
npm-resolved, self-hosted package (e.g. `@fontsource/poppins`) whose output is
still bundled into the built assets and served from the same origin — no
runtime request leaves the app's own origin either way.

**Dropping the no-JS requirement, not working around it.** Server-rendering or
static-prerendering React to preserve no-JS content was considered and
rejected: it is a materially larger build (a meta-framework, a rendering
runtime, a second thing to keep in sync with the client bundle) for a
guarantee the owner explicitly chose to give up. The public reporting form's
other resilience invariants — local state survives a network failure, a
script error never silently publishes or erases data — are unaffected and
remain in force; they are about a script *failing after load*, not about
whether a script is required to load the page.

## Alternatives

- **Keep the standalone-CLI/no-framework build, add TypeScript only via
  `tsc --noEmit` type-checking of plain scripts.** Fits ADR-0006 without
  reversing it. Rejected: the owner asked for React specifically, not just
  type safety.
- **Preact or Solid for a smaller runtime.** Rejected: smaller community,
  less agent training data, and the traffic/performance profile here (a
  low-volume occurrence-reporting form and an internal review queue) does not
  need the runtime-size optimization.
- **Next.js or Remix, to keep SSR/no-JS.** Rejected in this pass by the
  owner's explicit choice to drop the no-JS invariant rather than pay for
  SSR. Revisit if that invariant is ever reinstated.
- **pnpm or yarn instead of npm.** No stated reason to deviate from the
  ecosystem default; npm's lockfile format is what Renovate and most CI
  tooling assume with zero configuration.

## Consequences

- `src/web/` gains `package.json`, `package-lock.json`, and `node_modules`
  (gitignored). CI's `web` job runs `npm ci` before build/test, matching the
  pattern the `agent-config` and other Node-touching jobs already use
  elsewhere in `ci.yml`.
- `tools/build-css.sh` and `tools/tailwind.pin` are removed; `vite build`
  (invoking the Tailwind Vite plugin) replaces them.
- The `web-localization-and-design` feature file's "neither requires a SPA
  framework..." and "core content and navigation do not depend on
  JavaScript" scenarios are rewritten — see that file's current text, updated
  alongside this ADR.
- Component authors keep writing `bg-surface`/`text-ink`/etc. utility classes;
  [ADR-0024](ADR-0024-dark-mode-is-a-token-redefinition.md)'s token-redefinition
  approach to dark mode is unaffected by the framework change and needs no new
  ADR.
- Renovate's `npm` manager needs enabling in whatever Renovate config this
  repo uses; follow-up work, not part of this decision.

## Related

- [ADR-0006](ADR-0006-theme-engine.md) — superseded
- [ADR-0023](ADR-0023-pinned-and-vendored-web-assets.md) — partially
  superseded (build tooling only; font/logo vendoring stands)
- [ADR-0024](ADR-0024-dark-mode-is-a-token-redefinition.md) — unaffected
- `features/web-localization-and-design/web-localization-and-design.feature`
  — separate public/admin deployment, unaffected in shape
- [ADR-0044](ADR-0044-containerized-web-hosting.md) — where the built output
  runs
