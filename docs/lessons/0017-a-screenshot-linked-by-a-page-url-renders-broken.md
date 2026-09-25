---
title: A screenshot linked by a page URL renders broken
description: PR screenshots rendered as broken images, first with relative paths and then with github.com blob URLs, because neither resolves to image bytes in a PR body.
type: lesson
date: 2026-09-24
issue: 28
status: accepted
---

# Lesson 0017 — A screenshot linked by a page URL renders broken

## Symptom

Every screenshot in PR #414's description rendered as a broken-image link.
The images were committed under `docs/screenshots/public-report-feed/`
and referenced by fully qualified URLs of the form
`https://github.com/HPAC-Safety/safety-report/blob/<sha>/docs/screenshots/…png`.
Earlier pull requests, such as #389, had the same symptom with relative paths
(`docs/screenshots/…`).

## Root cause

A PR body has no base path, so a relative path points nowhere. Replacing it
with a `github.com/…/blob/…` URL looked like the fix, because it opens the
image in a browser. But that URL serves GitHub's file-viewer page
(`content-type: text/html`), not the image, and an `<img>` pointing at HTML
is broken. The rule we followed said "fully qualified", and nobody checked
what the URL actually returned.

## Spec delta

None to the product. The delivery rule now names the one URL form that
returns image bytes:
`https://raw.githubusercontent.com/HPAC-Safety/safety-report/<sha>/<path>`,
which answers `content-type: image/png`. It is pinned to the commit that
added the file, so a later rebase or force-push cannot break it.

## Scenario

No scenario. This is a property of how a pull request is written, not of the
system `features/` describes. The check is mechanical: before reporting a PR,
`curl -sI` each image URL and confirm it answers `image/png`.

## Skill

[`deliver-hpac-change`](../../skills/deliver-hpac-change/SKILL.md) step 6
now requires each screenshot to be referenced by its raw URL, pinned to the
commit that added it. It says why neither a relative path nor a blob URL
works, and requires checking each URL's content type before the PR is
reported.

Since #492 the general rule lives in the generic
[`deliver-change`](../../skills/deliver-change/SKILL.md) skill; the project skill named above
keeps this repository's commands, paths, and references.
