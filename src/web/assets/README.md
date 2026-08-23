# assets

Everything the two static front ends load that is not HTML, CSS, or JavaScript.
All of it is committed, and none of it is fetched at page load from anywhere
but this origin — see
[ADR-0023](../../../docs/decisions/ADR-0023-pinned-and-vendored-web-assets.md).

| | |
|---|---|
| `fonts/` | Poppins and Aleo as woff2, with their OFL licence texts. [`fonts/README.md`](fonts/README.md) |
| `hpac-logo.png` | The HPAC mark, 260×125. **Placeholder** — see below. |

## The logo is a placeholder

`hpac-logo.png` is `2024/04/logoNL.png` from hpac.ca's media library, at
260×125. It is the largest HPAC mark publicly available; the commonly linked one
is 49×50, and there is no SVG in that library.

That is not good enough for launch, for two reasons.

It is soft at any size worth using on a retina display. And it is the
**reversed** artwork: the "HPAC ACVL" wordmark is white, so on a light surface
only the red maple leaf shows and the wordmark vanishes. Open
`../styles/theme-preview.html` and toggle the theme to see it.

**A vector or high-resolution source, with a dark-ink variant, is an open
item** — recorded in
[`docs/design-system.md`](../../../docs/design-system.md#logo). Replacing the
file is the whole fix: nothing references its dimensions or its colours.

## Adding an asset

- Commit it. Do not link to hpac.ca, to a CDN, or to any other origin: a pilot
  filing a report after a crash should generate requests to this site and
  nowhere else.
- Keep the licence next to the file when it has one.
- Say where it came from, here, so the next person can refresh it.
