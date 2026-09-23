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
- Persist answer values, revision IDs, and each finished upload's ID, name,
  and size only in the same browser, for 15 days from the saved report's first
  save or until successful submit. Never persist a file's bytes. Abandoning the
  saved report deletes its uploads
  ([ADR-0100](../../docs/decisions/ADR-0100-an-attachment-is-kept-as-long-as-the-saved-report.md)).
  The only write before the final submission is an attachment upload.
- Upload each attached file at once through `POST /api/v1/uploads`, one
  request per file with its own `AbortController`: show an indeterminate
  indicator and Cancel while it uploads, Remove once it has, and hold Next and
  Submit while any upload is in flight. Submit one JSON request naming the
  upload IDs ([ADR-0096](../../docs/decisions/ADR-0096-an-attachment-uploads-on-attach-and-is-claimed-at-submission.md)).
- Public and admin are routes within the same application, build, and
  container ([ADR-0048](../../docs/decisions/ADR-0048-one-website-admin-as-a-route.md)).
  Treat API authorization, not hidden markup, as the admin boundary.
- Role-gate the chrome, never the route. A `User` sees no Admin menu; a
  `SafetyOfficer` sees review options; an `Administrator` sees authoring too.
  Do not add client-side route guards — the API authorizes every request.
- The browser never parses a JWT. Role and expiry come from the token response
  body, and the token travels as `Authorization: Bearer`.
- Ask the API which authentication mode it is in (`GET /api/auth/config`);
  never branch on a build flag. The third-party sign-in button is hidden, not
  disabled, where no provider is configured
  ([ADR-0066](../../docs/decisions/ADR-0066-a-development-identity-provider-signed-with-a-dev-key.md)).
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
- Every UI behavior change also gets a `.feature` scenario in the same PR —
  this is the same mandatory-not-discretionary rule as everywhere else in the
  repo (see AGENTS.md's Design authority section and
  [`deliver-hpac-change`](../deliver-hpac-change/SKILL.md)), not optional just
  because this skill is about React/TypeScript rather than delivery process.
  A Playwright test alone does not satisfy it: `@ui`-tagged scenarios execute
  through `playwright-bdd` in `tests/e2e/steps/` per
  [ADR-0053](../../docs/decisions/ADR-0053-ui-scenarios-execute-via-playwright-bdd.md),
  so the scenario and its step definitions are the test, not a separate
  document alongside it. Plain `.spec.ts` files outside `tests/e2e/steps/`
  are for broad smoke coverage only, never a substitute for scenario
  coverage of specific behavior.

Do not introduce server drafts, reserved report IDs, pre-submit API/database
writes other than attachment uploads, resumable upload sessions, third-party font/asset calls, or
client-side access to private report data beyond authorized admin DTOs.
