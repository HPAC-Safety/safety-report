# Design system

The goal is **recognisably HPAC, visibly better**. Someone arriving from
hpac.ca should feel continuity; nobody should think they are looking at a 2014
WordPress theme.

## Tokens, and where they came from

hpac.ca is WordPress running Divi. These were extracted from its live markup and
stylesheet:

| Token | Value | Provenance |
|---|---|---|
| Brand red | `#f22312` | Dominant accent, ~20 occurrences — buttons, links |
| Brand red dark | `#e02b20` | Hover / pressed |
| Ink | `#474747` | Body text |
| Rule | `#d6d6d6` | Dividers |
| Surface tints | `#f7f7f7`, `#f3f3f3`, `#e2e2e2` | Section backgrounds |
| UI font | **Poppins** | Google Fonts, weights 100–900 |
| Display font | **Aleo** | `'Aleo', Georgia, serif` |

**Dropped deliberately:** `#2ea3f2`. It appears throughout hpac.ca but it is
Divi's default blue, not an HPAC brand colour. Also dropped: heavy drop-shadowed
cards, full-width hero imagery on a form page, and the `ETmodules` icon font.

Declared once as Tailwind v4 `@theme` tokens in `src/web/styles/tailwind.css`:

```css
@theme {
  --color-brand-600: #f22312;
  --color-brand-700: #e02b20;
  --color-ink:       #474747;
  --color-rule:      #d6d6d6;
  --font-sans:    "Poppins", Helvetica, Arial, sans-serif;
  --font-display: "Aleo", Georgia, serif;
}
```

Components reference semantic names. **Raw hex values do not appear in markup**,
and the `web` job in CI greps for them.

### The full set

The extracted values above are the palette. These are the roles built on it, and
they are what markup actually names. Every token is a role rather than a
swatch — `--color-ink` means "body text", not "dark grey" — which is what lets
dark mode restate them honestly.

| Token | Light | Dark | Where it came from |
|---|---|---|---|
| `--color-brand-600` | `#f22312` | unchanged | hpac.ca |
| `--color-brand-700` | `#e02b20` | unchanged | hpac.ca |
| `--color-ink` | `#474747` | `#ededed` | hpac.ca / derived |
| `--color-ink-muted` | `#6b6b6b` | `#a8a8a8` | derived |
| `--color-ink-inverse` | `#ffffff` | `#ffffff` | — |
| `--color-rule` | `#d6d6d6` | `#3d3d3d` | hpac.ca / derived |
| `--color-surface` | `#ffffff` | `#1a1a1a` | — / derived |
| `--color-surface-2` | `#f7f7f7` | `#212121` | hpac.ca / derived |
| `--color-surface-3` | `#f3f3f3` | `#2a2a2a` | hpac.ca / derived |
| `--color-surface-4` | `#e2e2e2` | `#363636` | hpac.ca / derived |
| `--color-focus` | `#e02b20` | `#e02b20` | brand-700 |
| `--container-measure` | `65ch` | — | the single-column measure |

Sections and cards step down the surface ramp instead of reaching for a drop
shadow. `--color-focus` is brand-**700** rather than brand-600 because it is the
one that clears 3:1 against both ends of that ramp.

`styles/theme-preview.html` renders all of it on one page, with both typefaces
and a light/dark toggle. It is a developer artefact — not deployed, not linked,
no user-facing copy.

## Restraint is the design

This is a form somebody fills out after a crash — sometimes a fatal one,
sometimes about themselves. It is not a marketing page.

- Single-column measure around 65ch, generous vertical rhythm. The Typeform's
  one-question-at-a-time pacing becomes grouped sections with a progress rail.
- **Red is reserved for primary action and error state.** Never decoration.
  That is what lets "Serious injury" and "Fatality" read as urgent instead of
  competing with the brand.
- No celebratory microcopy, no animation flourishes on submit.

## Accessibility

- Real focus rings. Never `outline: none` without a replacement.
- 44px minimum touch targets.
- `prefers-reduced-motion` respected.
- WCAG AA contrast verified. Brand red on white passes for large text; body text
  uses ink.
- Every control labelled, and every label from the locale files.

## Dark mode

Same tokens, redefined. Safety officers triage at 11pm, and a fatality report is
not something to read on a bright white screen.

Literally redefined: **there is no `dark:` variant in any markup in this
repository, and none should be added.** A utility written `bg-surface` compiles
to `var(--color-surface)`, so redefining that variable re-themes every use of it
— including uses written later by someone who never thought about dark mode.
Three states: no `data-theme` attribute follows `prefers-color-scheme`,
`data-theme="light"` stays light against a dark system, `data-theme="dark"` is
dark regardless. No JavaScript is involved. See
[ADR-0024](decisions/ADR-0024-dark-mode-is-a-token-redefinition.md).

Two things about the dark column in the table above.

**The brand red does not change.** hpac.ca has one red, and inventing a second
for dark mode would be inventing an HPAC brand colour — the same mistake as
keeping `#2ea3f2` would have been. Red stays a filled action and an error state,
where white on brand-700 measures 4.6:1 in both themes. It is never body text on
either surface.

**The dark neutrals are derived, not extracted.** hpac.ca has no dark theme to
take them from. They are a neutral ramp mirroring the light one step for step
and clearing AA for body text. They are the only part of the palette without
provenance, and replacing them is an edit to two adjacent blocks in
`tailwind.css` and nothing else — **an open item for a designer**, alongside the
logo.

## Fonts are self-hosted

Poppins and Aleo ship from `src/web/assets/fonts`, not Google's CDN. Someone
filing a report about a crash should not have that visit logged by a third
party, and the form must work on a bad connection at a landing zone.

Both are SIL Open Font Licence 1.1 and are committed with their licence texts.
Poppins is static, so weights 400/500/600/700 are separate files; Aleo is
variable and covers 100–900 in one. `latin` and `latin-ext` subsets only, on
Google's own `unicode-range` boundaries, so a page fetches latin-ext only if it
contains a character in it. Provenance and the refresh procedure:
[`src/web/assets/fonts/README.md`](../src/web/assets/fonts/README.md). Why they
are committed rather than fetched at build time:
[ADR-0023](decisions/ADR-0023-pinned-and-vendored-web-assets.md).

## Logo

The best HPAC mark publicly available is **260×125** (`2024/04/logoNL.png`); the
commonly linked one is 49×50. No SVG of the mark exists in their media library.
A vector or high-resolution source is needed before launch — tracked as an open
item.

That file is now committed at `src/web/assets/hpac-logo.png` so the theme has a
mark to lay out against. **Committing it does not close the open item**, and
rendering it revealed a second problem: `logoNL.png` is the *reversed* artwork.
Its "HPAC ACVL" wordmark is white, so on a light surface only the red maple leaf
is visible and the wordmark disappears entirely. It is legible in dark mode and
invisible in light mode.

So the open item is now two things, and both need the same answer:

1. A **vector or high-resolution** source. At 260×125 the mark is soft on a
   retina display at any size worth using.
2. A **dark-ink variant** for light surfaces, or a single-colour mark that can
   be recoloured with `currentColor`. The only other mark on hpac.ca —
   `2021/10/HPAC-ACVL_logo.png` — is 113×109 and carries no wordmark, so it is
   not the answer either.

Replacing the file is the entire fix; nothing in the CSS references its
dimensions or its colours.

## Related

- `docs/decisions/ADR-0006-theme-engine.md` — why Tailwind's standalone CLI
- `docs/decisions/ADR-0023-pinned-and-vendored-web-assets.md` — why the binary, fonts and logo are pinned and committed
- `docs/decisions/ADR-0024-dark-mode-is-a-token-redefinition.md` — why there is no `dark:` variant
- `src/web/README.md`
- `docs/localization.md`
