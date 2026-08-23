---
name: build-hpac-web-ui
description: Build HPAC Safety's accessible bilingual public and admin static sites with plain HTML, JavaScript, Tailwind tokens, and self-hosted assets. Use for web UI changes.
---

# Build the HPAC web UI

Use plain HTML and JavaScript with no SPA framework or bundler. Tailwind's
standalone CLI is the only CSS build step. Use semantic HTML, visible focus,
44px touch targets, reduced-motion support, WCAG AA contrast, and self-hosted
assets.

- Put every user-facing string and accessible label in the locale catalogues.
- Resolve locale explicitly, then from the browser, then English; preserve
  answers when switching language.
- Render the ordered current bilingual question-revision DTO. Only consent is
  required and it has no selected default.
- Persist answer values and revision IDs only in the same browser for 15 days
  or until successful submit. Never persist or restore file inputs, and make no
  report-data write request before the final submission.
- Submit one multipart request containing the JSON DTO and selected files.
- Keep public and admin bundles as separately deployed static sites. Treat API
  authorization, not hidden markup, as the admin boundary.
- Use design tokens rather than raw colors; dark mode redefines tokens rather
  than adding `dark:` variants.

Do not introduce server drafts, reserved report IDs, pre-submit API/database/
object-storage writes, upload sessions, third-party font/asset calls, or
client-side access to private report data beyond authorized admin DTOs.
