---
name: build-hpac-web-ui
description: Build HPAC Safety's accessible bilingual public and admin React/TypeScript sites with Vite, Tailwind tokens, and self-hosted assets. Use for web UI changes.
---

# Build the HPAC web UI

Use React and TypeScript, built with Vite, per
[ADR-0043](../../docs/decisions/ADR-0043-react-typescript-vite-web-front-end.md).
Tailwind v4 via `@tailwindcss/vite` is the only CSS build step. Use semantic
HTML, visible focus, 44px touch targets, reduced-motion support, WCAG AA
contrast, and self-hosted assets.

- Put every user-facing string and accessible label in the locale catalogues.
- Resolve locale explicitly, then from the browser, then English; preserve
  answers when switching language.
- Render the ordered current bilingual question-revision DTO. Only consent is
  required and it has no selected default.
- Persist answer values and revision IDs only in the same browser for 15 days
  or until successful submit. Never persist or restore file inputs, and make no
  report-data write request before the final submission.
- Submit one multipart request containing the JSON DTO and selected files.
- Public and admin are routes within the same application, build, and
  container ([ADR-0048](../../docs/decisions/ADR-0048-one-website-admin-as-a-route.md)).
  Treat API authorization, not hidden markup, as the admin boundary.
- No `<script>` tag in any HTML file contains JavaScript. Every script is an
  external, type-checked `.ts` module under `src/web/src/`, referenced with
  `<script type="module" src="...">`
  ([ADR-0052](../../docs/decisions/ADR-0052-no-inline-script-typescript-only.md)).
- Use design tokens rather than raw colors; dark mode redefines tokens rather
  than adding `dark:` variants
  ([ADR-0024](../../docs/decisions/ADR-0024-dark-mode-is-a-token-redefinition.md)) —
  this is unaffected by the framework.
- Every UI behavior change ships a Playwright test, plus a server-side test
  when it touches API behavior
  ([ADR-0045](../../docs/decisions/ADR-0045-ui-changes-require-playwright-and-server-tests.md)).

Do not introduce server drafts, reserved report IDs, pre-submit API/database/
object-storage writes, upload sessions, third-party font/asset calls, or
client-side access to private report data beyond authorized admin DTOs.
