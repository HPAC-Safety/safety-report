---
name: build-hpac-web-ui
description: Build HPAC Safety's accessible bilingual public and admin React/TypeScript sites with Vite, Tailwind tokens, and self-hosted assets. Use for web UI changes.
---

# Build the HPAC web UI

## Stack

- React and TypeScript, built with Vite
  ([ADR-0043](../../docs/decisions/ADR-0043-react-typescript-vite-web-front-end.md)).
- Tailwind v4 via `@tailwindcss/vite` is the only CSS build step.
- **No inline JavaScript.** Every script is an external, type-checked `.ts`
  module under `src/web/src/`, loaded with `<script type="module" src="...">`
  ([ADR-0052](../../docs/decisions/ADR-0052-no-inline-script-typescript-only.md)).
- Design tokens, not raw colors. Dark mode redefines tokens rather than adding
  `dark:` variants
  ([ADR-0024](../../docs/decisions/ADR-0024-dark-mode-is-a-token-redefinition.md)).
- Self-hosted assets only.

## Accessibility and locale

- Semantic HTML, visible focus, 44px touch targets, reduced-motion support,
  WCAG AA contrast.
- Every user-facing string and accessible label lives in the locale catalogues
  (see [`localize-hpac-app`](../localize-hpac-app/SKILL.md)).
- Resolve locale: explicit choice, then browser, then English. Keep answers when
  switching language.

## The report form

- Render the ordered current bilingual question-revision DTO.
  - Honor each revision's authored required state (ADR-0061).
  - Publication consent is always required, and so is media consent when it is
    asked (ADR-0117).
  - No consent has a selected default.
- **Saved report**: answer values, revision IDs, and each finished upload's ID,
  name, and size, only in the same browser, for 15 days from the first save or
  until a successful submit. Never a file's bytes. Abandoning the saved report
  deletes its uploads
  ([ADR-0100](../../docs/decisions/ADR-0100-an-attachment-is-kept-as-long-as-the-saved-report.md)).
- **The only write before final submission is an attachment upload.**
- **Uploads**
  ([ADR-0096](../../docs/decisions/ADR-0096-an-attachment-uploads-on-attach-and-is-claimed-at-submission.md),
  [ADR-0126](../../docs/decisions/ADR-0126-an-attachment-uploads-straight-to-quarantine-by-pre-signed-put.md)):
  - upload each file at once: mint it via `POST /api/v1/uploads` (declared
    type and size, never a filename), then `PUT` it to the returned URL, one
    request per file with its own `AbortController`;
  - while uploading: an indeterminate indicator and Cancel; once uploaded:
    Remove;
  - hold Next and Submit while any upload is in flight;
  - submit one JSON request naming the upload IDs; mark each upload it
    refuses as expired or invalid on its own row, and keep everything else.

## Admin and authentication

- Public and admin are routes in one application, build, and container
  ([ADR-0048](../../docs/decisions/ADR-0048-one-website-admin-as-a-route.md)).
- **API authorization is the boundary**, not hidden markup. Every `/admin/*`
  route is wrapped in `AdminRouteGuard`: signed out redirects to `/login`, and
  the wrong role gets a real 403 view (ADR-0092). The guard only decides what
  to render; the API still answers 401/403 on every request.
  - `User`: no Admin menu. `SafetyOfficer`: review options. `Administrator`:
    authoring too.
- The browser never parses a JWT. Role and expiry come from the token response
  body; the token travels as `Authorization: Bearer`.
- Ask the API its authentication mode (`GET /api/auth/config`); never branch on
  a build flag. Where no provider is configured, the third-party sign-in button
  is hidden, not disabled
  ([ADR-0066](../../docs/decisions/ADR-0066-a-development-identity-provider-signed-with-a-dev-key.md)).

## Tests

- Every UI behavior change ships:
  - a `.feature` scenario in the same pull request (`AGENTS.md`
    "Specification-driven development") — a Playwright test alone does not
    satisfy this;
  - a Playwright test. For a `@ui` scenario, its steps in `tests/e2e/steps/`
    **are** that test, via `playwright-bdd`
    ([ADR-0053](../../docs/decisions/ADR-0053-ui-scenarios-execute-via-playwright-bdd.md));
  - a server-side test when it touches API behavior
    ([ADR-0045](../../docs/decisions/ADR-0045-ui-changes-require-playwright-and-server-tests.md)).
- Plain `.spec.ts` files outside `tests/e2e/steps/` are broad smoke coverage
  only, never a substitute for a scenario.

## Never add

- server drafts or reserved report IDs;
- pre-submit API or database writes other than attachment uploads;
- resumable upload sessions;
- third-party font or asset calls;
- client access to private report data beyond authorized admin DTOs.
