---
title: Web assets
description: Everything the front end loads that is not HTML, CSS, or JavaScript, all of it committed.
type: readme
---

# assets

Everything the two static front ends load that is not HTML, CSS, or JavaScript.
All of it is committed, and none of it is fetched at page load from anywhere
but this origin — see
[ADR-0023](../../../docs/decisions/ADR-0023-pinned-and-vendored-web-assets.md).

| | |
|---|---|
| `fonts/` | Poppins and Aleo as woff2, with their OFL licence texts. [`fonts/README.md`](fonts/README.md) |
| `hpac-light.svg` | The HPAC mark for the light theme. |
| `hpac-dark.svg` | The HPAC mark for the dark theme. |

## The logo

`hpac-light.svg` and `hpac-dark.svg` are theme-matched vector variants of the
HPAC mark, replacing the earlier raster placeholder (`hpac-logo.png`, a
reversed, soft 260×125 crop of hpac.ca's `2024/04/logoNL.png`) that only read
correctly in dark mode. `Header.tsx` picks between them using the same theme
state `ThemeToggle` reads. See
[`docs/design-system.md`](../../../docs/design-system.md#logo).

## Adding an asset

- Commit it. Do not link to hpac.ca, to a CDN, or to any other origin: a pilot
  filing a report after a crash should generate requests to this site and
  nowhere else.
- Keep the licence next to the file when it has one.
- Say where it came from, here, so the next person can refresh it.
