---
title: The interface catalogues ship in the page bundle, and the page is never cached stale
description: Both locale catalogues are built into the main script bundle instead of fetched as hashed chunks, and a deploy uploads hashed assets as immutable before index.html as no-cache, so a deploy can neither leave an open tab showing raw keys nor serve a returning visitor an old page.
type: adr
status: accepted
date: 2026-10-03
decision-makers: Chase Florell
keywords: localization, catalogue, Vite, import.meta.glob, code splitting, deploy, S3, CloudFront, Cache-Control, immutable, stale chunk, ADR-0021, ADR-0031, ADR-0123
---

# ADR-0190 — The interface catalogues ship in the page bundle, and the page is never cached stale

**Status:** Accepted. Decided by the owner on 2026-10-03 in
[#805](https://github.com/HPAC-Safety/safety-report/issues/805).

## Context

On staging, a phone tab left open overnight showed raw keys
(`page.login.title`, `footer.copyright`) after a release
([lesson 0043](../lessons/0043-a-deploy-deleted-the-catalogue-an-open-page-still-needed.md)).
Each catalogue was a lazy, content-hashed chunk. The deploy's
`s3 sync --delete` removed the previous build's chunks, and CloudFront
answered the missing chunk with `index.html` at 200. The code swallowed the
failed import and cached an empty catalogue. `index.html` carried no
`Cache-Control`, so a browser could also keep using an old page.

## Decision

- **Both catalogues are bundled.** `src/web/src/i18n/catalogueFor.ts` imports
  `locales/{en-CA,fr-CA}.json` with an eager `import.meta.glob`. The text is in
  memory with the code that reads it, and the lookup is synchronous. There is
  no load to fail, cache, or retry (REQ-WLD-049).
- **A deploy orders and labels its uploads** (CON-INF-027):
  - `assets/*` is uploaded first, as `public, max-age=31536000, immutable`;
  - `index.html` and every other unhashed file follow, as `no-cache`;
  - the previous release's assets are deleted last.

## Consequences

- About 21 KB gzipped more on first load, for both languages.
- A missing `fr-CA.json` still builds; French falls back to English per key,
  as before (ADR-0021).
- A tab open across a deploy keeps its own release's text until it next loads
  `index.html`. Its other hashed files, such as fonts not yet fetched, can
  still go missing. That costs a fallback font, never content.

## Alternatives rejected

- **Reload the page on a chunk-load error** (Vite's `vite:preloadError`). It
  works, but it reloads in front of a reporter who may be partway through a
  form. Bundling removes the failure instead of recovering from it.
- **Keep the previous release's assets in the bucket.** It needs a retention
  rule and still leaves the swallowed error. Not needed once nothing is
  fetched late.
- **Fetch the catalogue from an unhashed `/locales/*.json` path.** An old tab
  would receive new text for old code, and it adds a request on every load.
