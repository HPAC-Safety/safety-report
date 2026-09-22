---
title: React Router for client-side navigation
description: Adopt react-router-dom (declarative mode — BrowserRouter, Routes, Route; no loaders/actions, which this app has no server-rendered data dependency to justify) as the client-side router.
type: adr
status: accepted
date: 2026-09-19
decision-makers: Chase Florell
keywords: React Router, routing, SPA, navigation, web front end
---

# ADR-0051 — React Router for client-side navigation

**Status:** Accepted

## Context

[ADR-0043](ADR-0043-react-typescript-vite-web-front-end.md) adopted React,
TypeScript, and Vite; [ADR-0048](ADR-0048-one-website-admin-as-a-route.md)
put the public site and the admin review queue in one application, with
`/admin` as a route rather than a separate deployment. Neither ADR named a
routing mechanism, and no router dependency exists in `src/web/package.json`
yet. The homepage spike (issue #140) is the first work that needs one: a
header with links to view reports, submit a report, contact, and member
login, each its own route, plus an `/admin` stub confirming ADR-0048's
shape.

Choosing a redirect-based identity provider
([ADR-0064](ADR-0064-jwt-bearer-authentication-with-three-roles.md)) will add
one more route this ADR did not contemplate — the provider's callback — which
the router handles like any other.

## Decision

Adopt `react-router-dom` (declarative mode — `BrowserRouter`, `Routes`,
`Route`; no loaders/actions, which this app has no server-rendered data
dependency to justify) as the client-side router.

## Why this choice

Same reasoning ADR-0043 used for React itself: the largest ecosystem and
training-data footprint for an agent-authored codebase, and the de facto
standard for React SPA routing. Nothing about this app's shape (one bundle,
client-only state, no server-rendered loaders) calls for more than its
declarative mode.

## Alternatives

- **TanStack Router.** Smaller ecosystem, a less agent-familiar API, and its
  type-safe route generation buys nothing here — this app has no complex
  nested data-loading to type against.
- **Hand-rolled `window.location`/`history` switch.** Reinvents path
  matching, nested layouts, and active-link state for no benefit at this
  scale, and is exactly the kind of pattern this repository avoids
  introducing without a real boundary forcing it.
- **A meta-framework (Next.js, Remix).** Already rejected by ADR-0043 for
  reintroducing a Node production server and SSR, which this app does not
  need.

## Consequences

- `src/web/package.json` gains a `react-router-dom` dependency.
- `src/web/src/main.tsx` wraps `<App />` in `<BrowserRouter>`; `App.tsx`
  holds only the `<Routes>` table.
- **Follow-up, not solved here:** the deployed web server (nginx/CloudFront,
  ADR-0044/ADR-0048) must serve `index.html` for any unmatched path so a
  deep link or a reload on a client-side route doesn't 404. This is
  infrastructure work; this spike is frontend-only.

## Related

- [ADR-0043](ADR-0043-react-typescript-vite-web-front-end.md)
- [ADR-0048](ADR-0048-one-website-admin-as-a-route.md)
