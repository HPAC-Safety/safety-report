# Static web sites

`public/` is the anonymous report form and public feed; `admin/` is the
authenticated review/administration UI. They are separate static deployments
built with plain HTML and JavaScript. There is no SPA framework or bundler.

Tailwind v4's standalone CLI builds the shared stylesheet:

```bash
./tools/build-css.sh
./tools/build-css.sh --watch
```

Use semantic HTML, visible focus, 44px touch targets, reduced-motion support,
WCAG AA contrast, self-hosted assets, and design tokens. Dark mode redefines
tokens; do not add raw colors or `dark:` utility variants to markup.

All UI copy comes from matching English/French locale catalogues. Resolve an
explicit locale choice first, then browser preference, then English. Database
questions already contain both languages.

The public form renders current question revisions, requires only consent,
keeps answers/revision IDs only in the browser for 15 days, never restores files
or writes unfinished report state to any server, and submits one final multipart
request. The admin site consumes only authorized DTOs; the API remains the
security boundary.

Current main contains the design system and asset tooling but not the complete
pages. Implement against
[`spec/web-localization-and-design.md`](../../spec/web-localization-and-design.md),
not old issue closure state.
