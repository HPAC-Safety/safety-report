---
title: A deploy deleted the catalogue an open page still needed
description: Lazily loaded locale catalogues were content-hashed chunks; a deploy deleted them under an open tab, CloudFront answered with index.html, and a swallowed error left the page showing raw keys until a full reload.
type: lesson
date: 2026-10-03
issue: 805
status: accepted
---

# Lesson 0043 — A deploy deleted the catalogue an open page still needed

## Symptom

- After release `2026.10.03-2`, a staging tab opened the evening before showed
  catalogue keys instead of text: `page.login.title`,
  `page.login.submitButton`, `footer.copyright`. Switching language did not
  help; only a full reload did
  ([#805](https://github.com/HPAC-Safety/safety-report/issues/805)).

## Root cause

- `loadCatalogue.ts` imported each `locales/*.json` lazily, so Vite emitted
  each one as a hashed chunk (`assets/en-CA-<hash>.js`).
- The deploy's `aws s3 sync --delete` removed the previous build's chunks. The
  restored tab still ran the old bundle and requested the old hash.
- CloudFront's SPA fallback answered the missing file with `index.html` at
  200 (`text/html`, `nosniff`), so the module import failed.
- `loadRaw` caught the failure and returned nothing, and `englishCache` kept
  the resulting empty catalogue for the life of the page. `t()` then returned
  each key.
- `index.html` had no `Cache-Control`, so a browser could also reuse an old
  page that names deleted files.

## Spec delta

- REQ-WLD-049: a page left open across a deploy keeps its interface text in
  both languages.
- CON-INF-027: hashed assets are uploaded first and immutable, `index.html`
  follows as `no-cache`, and old assets are deleted last.
- The area README records that both catalogues ship in the page bundle, and
  rules out loading one on demand.
- [ADR-0190](../decisions/ADR-0190-the-interface-catalogues-ship-in-the-page-bundle-and-the-page-is-never-cached-stale.md).

## Scenario

REQ-WLD-049. After the page loads, its browser step answers every `/assets/`
request the way CloudFront answers a deleted file, then switches to French.
The step fails against the lazy catalogue and passes against the bundled one.
