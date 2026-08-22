# fonts

Poppins (UI) and Aleo (display), self-hosted. `src/web/styles/tailwind.css`
declares the `@font-face` rules; nothing here is referenced from anywhere else.

**Not from `fonts.googleapis.com`.** Someone filing a report about a crash —
sometimes their own, sometimes a fatal one — should not have that visit logged
by a third party, and the form has to work on a bad connection at a landing
zone. That is an acceptance criterion, not a preference: no request leaves this
origin when a page loads.

## Licence

Both families are **SIL Open Font Licence 1.1**, which permits redistribution.
The licence texts are committed beside the fonts and must stay there.

| Family | Licence | Upstream |
|---|---|---|
| Poppins | `OFL-Poppins.txt` | [`google/fonts/ofl/poppins`](https://github.com/google/fonts/tree/main/ofl/poppins) |
| Aleo | `OFL-Aleo.txt` | [`google/fonts/ofl/aleo`](https://github.com/google/fonts/tree/main/ofl/aleo) |

## What is here

Poppins is a static family, so there is one file per weight. Aleo is a variable
font covering 100–900 in a single file, which is why the display face costs one
request per subset rather than one per weight.

| File | Family | Weight | Subset |
|---|---|---|---|
| `poppins-400-latin.woff2` | Poppins | 400 | latin |
| `poppins-400-latin-ext.woff2` | Poppins | 400 | latin-ext |
| `poppins-500-latin.woff2` | Poppins | 500 | latin |
| `poppins-500-latin-ext.woff2` | Poppins | 500 | latin-ext |
| `poppins-600-latin.woff2` | Poppins | 600 | latin |
| `poppins-600-latin-ext.woff2` | Poppins | 600 | latin-ext |
| `poppins-700-latin.woff2` | Poppins | 700 | latin |
| `poppins-700-latin-ext.woff2` | Poppins | 700 | latin-ext |
| `aleo-latin.woff2` | Aleo | 100–900 (variable) | latin |
| `aleo-latin-ext.woff2` | Aleo | 100–900 (variable) | latin-ext |

Weights 100, 200, 300, 800 and 900 of Poppins, every italic, and every non-latin
subset are deliberately absent. Add one only when something actually uses it —
each is a file a phone on a bad connection has to fetch.

`latin` covers en-CA and fr-CA in full, accents included. `latin-ext` is carried
for place names and is fetched only when a page contains a character in its
range, because the `unicode-range` boundaries are kept exactly as Google
publishes them.

## Refreshing them

The bytes are the woff2 subsets Google serves, not the TTFs in `google/fonts` —
same fonts under the same licence, already hinted, compressed, and split on
those `unicode-range` boundaries. Converting the TTFs here instead would put a
Python font toolchain into a build whose premise is that it has no build
dependencies. ADR-0023 has the reasoning.

To refresh, ask Google's CSS API for the same families with a desktop
user-agent, then download each `latin` and `latin-ext` URL it returns:

```sh
UA='Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0 Safari/537.36'
curl -sS -A "$UA" 'https://fonts.googleapis.com/css2?family=Poppins:wght@400;500;600;700&family=Aleo:wght@100..900&display=swap'
```

Without the user-agent it answers with TTF. Copy the `unicode-range` values into
`tailwind.css` alongside the new files — they move when Google re-cuts a subset,
and a stale range means a character silently falls back to Helvetica.

Then rebuild and check the page again with the network panel open: the only
font requests should be to this origin.

## Related

- [`../README.md`](../README.md)
- [`../../../docs/design-system.md`](../../../docs/design-system.md)
- [ADR-0023](../../../docs/decisions/ADR-0023-pinned-and-vendored-web-assets.md)
