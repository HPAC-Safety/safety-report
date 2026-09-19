# Web site

One React/TypeScript single-page application, built with Vite. The
anonymous report form is the default route; the authenticated
review/administration UI lives at `/admin` in the same app and build — see
[ADR-0043](../../docs/decisions/ADR-0043-react-typescript-vite-web-front-end.md)
and
[ADR-0048](../../docs/decisions/ADR-0048-one-website-admin-as-a-route.md).

```bash
npm install
npm run dev
npm run build
```

Tailwind v4 runs via the `@tailwindcss/vite` plugin, not the standalone CLI.

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
[`features/web-localization-and-design/web-localization-and-design.feature`](../../features/web-localization-and-design/web-localization-and-design.feature),
not old issue closure state.
