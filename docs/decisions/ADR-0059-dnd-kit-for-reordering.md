---
status: accepted
date: 2026-09-20
decision-makers: Chase Florell
keywords: drag and drop, dnd-kit, accessibility, reordering, owned abstraction
---

# ADR-0059 — Reordering uses @dnd-kit, behind one owned component, and never requires a pointer

## Context

The question bank is ordered, and an administrator rearranging a form expects
to drag a question up the list. `question_revisions.display_order` already
carries the order; what was missing was a way to change it that is not a number
field per row.

Drag and drop is one of the few browser interactions that is genuinely hard to
build correctly. The HTML5 drag-and-drop API is notoriously inconsistent across
browsers, does not work on touch at all without a parallel pointer-event
implementation, and — decisively here — has no keyboard story. Building it by
hand means owning pointer capture, auto-scroll, collision detection, and a
keyboard fallback, indefinitely.

This application is also required to be operable without a pointer:
`docs/design-system.md` and `skills/build-hpac-web-ui` both require visible
focus, 44px targets, and WCAG AA behaviour throughout, and a safety officer
using a keyboard or a screen reader has to be able to rearrange the form.

## Decision

**Use `@dnd-kit` (`core`, `sortable`, `modifiers`, `utilities`), and wrap it in
one component this repository owns: `src/web/src/components/SortableList.tsx`.**

`@dnd-kit` is chosen over the alternatives because it ships a real
`KeyboardSensor` — arrow-key reordering with announcements is a first-class
feature rather than an afterthought — and because it is pointer-event based, so
touch works without a second code path.

Per [ADR-0033](ADR-0033-third-party-libraries-behind-owned-abstractions.md),
no screen imports `@dnd-kit` directly. `SortableList` takes items and a
callback and hands back an array of ids in their new order; callers never see a
sensor, a modifier, or a transform. Replacing the library is a change to that
one file.

**Reordering is never pointer-only.** Every row carries three controls: a drag
handle that is a real focusable button driven by the keyboard sensor, and
explicit move-up and move-down buttons. The buttons are not a degraded
fallback — on a touch screen and with a screen reader they are the primary way
this works, and they are what the `@ui` scenario for keyboard reordering
asserts.

## Consequences

- Three new runtime dependencies in `src/web/package.json`, Renovate-tracked
  like everything else there.
- About 15 kB gzipped added to the bundle, on an admin route.
- One component to change if `@dnd-kit` is ever replaced, rather than every
  screen that reorders something.
- A reorder is a **save**: each moved question gets a new revision, written in
  one transaction, and the list is re-read from the response rather than held
  optimistically. Dragging is therefore not free, and the UI does not pretend
  it is.

## Alternatives rejected

**Native HTML5 `dragstart`/`dragover`/`drop`.** No dependency at all.
Rejected on accessibility and touch: no keyboard path exists, and mobile
support requires writing the pointer implementation anyway — at which point the
native API has bought nothing.

**Hand-rolled pointer-event reordering.** Full control, no dependency.
Rejected as a standing maintenance cost for a solved problem: collision
detection, auto-scroll while dragging, and screen-reader announcements are each
a source of subtle bugs, and getting them wrong is invisible until someone who
depends on them cannot use the page.

**`react-beautiful-dnd`.** Excellent accessibility and the best-known option.
Rejected because it is no longer maintained and does not support React 18's
strict mode cleanly, which `src/web` runs in.

**Up/down buttons only, no dragging.** Accessible, trivial, no dependency.
Rejected as the product decision rather than the technical one: rearranging a
thirty-question form one click at a time is the behaviour this story set out to
replace. The buttons stay — they are simply not the only way.

## Related

- [ADR-0033](ADR-0033-third-party-libraries-behind-owned-abstractions.md) — third-party libraries sit behind owned abstractions
- [ADR-0043](ADR-0043-react-typescript-vite-web-front-end.md) — the front-end stack
- [ADR-0045](ADR-0045-ui-changes-require-playwright-and-server-tests.md) — the tests this ships with
