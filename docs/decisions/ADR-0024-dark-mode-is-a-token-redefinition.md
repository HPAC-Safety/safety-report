# ADR-0024 — Dark mode is a token redefinition, not a variant

**Status:** Accepted

## Context

`docs/design-system.md` commits to dark mode in one sentence: *"Same tokens,
redefined. Safety officers triage at 11pm, and a fatality report is not
something to read on a bright white screen."* This records how that is built,
and what the sentence rules out.

Tailwind offers a `dark:` variant, and it is the obvious route. It is also the
route that makes every future component author responsible for remembering it:
`bg-surface` and `dark:bg-neutral-900` have to be written together, forever, on
every element, in two static front ends built by two other issues (#12, #25).
One omission is a white card in a dark queue at midnight.

## Decision

**The palette is declared once as roles, and dark mode redefines those roles.
No `dark:` utility appears in any markup in this repository.**

Tailwind v4 compiles `bg-surface` to `background-color: var(--color-surface)`.
Redefining `--color-surface` therefore re-themes every existing use of it and
every use anyone adds later, without that author knowing dark mode exists.

Three things follow.

**Token names are roles, not swatches.** `--color-ink` means "body text", not
"dark grey"; `--color-surface-3` means "third step of the elevation ramp", not
"#f3f3f3". A role can be honestly restated in another theme. A swatch name
cannot, and `--color-gray-100: #1a1a1a` is how a palette starts lying.

**The override is written twice, unlayered.** `@theme` output lands in
`@layer theme`; an unlayered rule outranks it regardless of specificity, which
is what lets the dark block win. It appears under
`@media (prefers-color-scheme: dark)` scoped to
`:root:not([data-theme="light"])`, and again under `:root[data-theme="dark"]`,
giving three states: no attribute follows the system, `data-theme="light"`
stays light against a dark system, `data-theme="dark"` is dark regardless.

**The brand values do not change between themes.** hpac.ca has one red.
Inventing a second for dark mode would be inventing an HPAC brand colour, which
this project has already declined to do once — `#2ea3f2` was dropped for being
Divi's, not HPAC's. Red stays reserved for filled primary actions and error
state, where white on `--color-brand-700` measures 4.6:1 in both themes. It is
never body text on either surface, where it would only reach AA-large.

The dark neutrals — ink `#ededed`, muted `#a8a8a8`, rule `#3d3d3d`, and the
surface ramp `#1a1a1a` → `#363636` — are **derived, not extracted**: hpac.ca has
no dark theme to take them from. They are a neutral ramp chosen to mirror the
light one step for step and to clear WCAG AA for body text. They are the one
part of the palette without provenance, and they are recorded as such here and
in `docs/design-system.md` so a designer can replace them without archaeology.

## Alternatives

- **`dark:` variants in markup.** Idiomatic Tailwind, and every component
  states its own dark appearance locally. Rejected: it doubles the class list
  on every element, and correctness depends on nobody ever forgetting. The
  failure is silent and lands on the surface used at night.
- **A second stylesheet loaded by media query.** Rejected: two files that must
  be kept in step, and every component is then defined in two places.
- **A `.dark` class on `<html>` plus `@custom-variant`.** Solves the manual
  toggle but not the duplication — it is still a variant per element.
  `data-theme` on `:root` gives the same override without any variant at all,
  and reads as data rather than as styling.
- **Follow `prefers-color-scheme` only, with no attribute.** Simpler, and it is
  the default path here. Rejected as the *only* path: a safety officer on a
  shared machine may want the choice, and #25 can wire a control to one
  attribute without touching CSS.
- **Sample dark values from another HPAC property.** There is none.

## Consequences

- Component authors on #12 and #25 write `bg-surface text-ink border-rule` and
  get dark mode for free. They should not add `dark:` classes; a review that
  sees one should ask why.
- A new colour must be added to `@theme` **and** to both dark blocks, or it will
  be a light-mode colour sitting on a dark surface. The two blocks are adjacent
  and identical for exactly that reason.
- Dark mode needs no JavaScript. The toggle in
  `src/web/styles/theme-preview.html` sets one attribute and exists only to
  demonstrate the flip.
- The derived neutrals are provisional. Replacing them is an edit to two blocks
  in `src/web/styles/tailwind.css` and changes nothing else.
