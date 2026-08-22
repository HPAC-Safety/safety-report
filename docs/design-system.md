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

Components reference semantic names. **Raw hex values do not appear in markup.**

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

## Fonts are self-hosted

Poppins and Aleo ship from `src/web/assets`, not Google's CDN. Someone filing a
report about a crash should not have that visit logged by a third party, and the
form must work on a bad connection at a landing zone.

## Logo

The best HPAC mark publicly available is **260×125** (`2024/04/logoNL.png`); the
commonly linked one is 49×50. No SVG of the mark exists in their media library.
A vector or high-resolution source is needed before launch — tracked as an open
item.

## Related

- `docs/decisions/ADR-0006-theme-engine.md`
- `docs/localization.md`
