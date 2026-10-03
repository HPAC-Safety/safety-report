---
name: build-hpac-web-ui
description: Build HPAC Safety's accessible bilingual public and admin React/TypeScript sites with Vite, Tailwind tokens, and self-hosted assets. Use for web UI changes.
---

# Build the HPAC web UI

## Stack

- React and TypeScript, built with Vite
  ([ADR-0043](../../.spec/decisions/ADR-0043-react-typescript-vite-web-front-end.md)).
- Tailwind v4 via `@tailwindcss/vite` is the only CSS build step.
- **No inline JavaScript.** Every script is an external, type-checked `.ts`
  module under `src/web/src/`, loaded with `<script type="module" src="...">`
  ([ADR-0052](../../.spec/decisions/ADR-0052-no-inline-script-typescript-only.md)).
- Design tokens, not raw colors. Dark mode redefines tokens rather than adding
  `dark:` variants
  ([ADR-0024](../../.spec/decisions/ADR-0024-dark-mode-is-a-token-redefinition.md)).
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
  ([ADR-0100](../../.spec/decisions/ADR-0100-an-attachment-is-kept-as-long-as-the-saved-report.md)).
- **The only write before final submission is an attachment upload.**
- **Uploads**
  ([ADR-0096](../../.spec/decisions/ADR-0096-an-attachment-uploads-on-attach-and-is-claimed-at-submission.md),
  [ADR-0126](../../.spec/decisions/ADR-0126-an-attachment-uploads-straight-to-quarantine-by-pre-signed-put.md)):
  - upload each file at once: mint it via `POST /api/v1/uploads` (declared
    type and size, never a filename), then `PUT` it to the returned URL, one
    request per file with its own `AbortController`;
  - while uploading: an indeterminate indicator and Cancel; once uploaded:
    Remove;
  - hold Next and Submit while any upload is in flight;
  - submit one JSON request naming the upload IDs; mark each upload it
    refuses as expired or invalid on its own row, and keep everything else.

## Components: logic and markup
([ADR-0188](../../.spec/decisions/ADR-0188-a-components-logic-lives-in-foo-tsx-and-its-markup-in-foo-view-tsx-and-web-logic-is-unit-tested.md))

- **`Foo.tsx` is logic.** It exports `useFoo(props)`, the view model (state,
  effects, refs, derived values, handlers, fetching), and `Foo`, which only
  renders `<FooView {...props} {...useFoo(props)} />`. Import sites never
  change. A markup-only component gets the pass-through with no `useFoo`.
- **`Foo.view.tsx` is markup.** It exports `FooView` and `FooViewProps`. It
  calls no hook but `useLocale`, imports no value from `api/` (a type-only
  import, `import type …` or `import { type X }`, is allowed) and no runtime
  value from its own sibling `Foo.tsx` (that is a runtime circular import: put a
  shared value in a small module both import; a type import is fine), and uses
  no storage, `fetch`, timer or module-level mutable state. A DOM ref arrives as
  a prop. The guard reads code only: a string literal or a comment that names
  `localStorage` is not a use of it.
- A stateful private sub-component gets its own pair; a stateless one moves into
  the view. A pure helper goes into a camelCase `.ts` file. A provider keeps its
  value logic in `Foo.tsx` and its view renders `Ctx.Provider`; a class error
  boundary keeps the class in `.tsx` and moves its fallback markup to the view.
  `main.tsx` and `routes.tsx` are exempt.
- **The DOM stays identical** (elements, attributes, ids, `data-*`, classes,
  text, order). No `tests/e2e/**` file changes for a split; if one would, the
  split is wrong.
- `node tools/web/check-component-split.ts` enforces the view rules and the test
  boundary below. Its strict mode (every component has a view) is a constant in
  the script, and it is on: a new component is a pair from the start.
- **One hook opens every native `<dialog>`:** `hooks/useModalDialog` shows it as
  a modal on mount and starts focus on the control `focusRef` is attached to
  (the choice that keeps things as they are). A new dialog uses it; it does not
  carry its own effect.

### Unit tests for logic

- **Vitest, Testing Library and jsdom**; `npm --prefix src/web run test`,
  `test:coverage`, `typecheck`. Tests sit beside the code as `Foo.test.tsx`
  (`foo.test.ts` for a helper); titles are prose.
- Logic is held to **100% line, branch, function and statement coverage**;
  Playwright covers views, and `*.view.tsx` is excluded from the report.
- **The scope needs no config edit.** `tools/web/web-coverage-scope.ts` puts in
  scope every `Foo.tsx` with a sibling `Foo.view.tsx`, and every `.ts` helper
  with a colocated test. Split a component and test it; the threshold follows.
- Test with Testing Library by role and label, as a user would. `renderHook` a
  view model that has no markup of its own.
- **Every `.ts` helper has a colocated test** (`lib/`, `api/`, `hooks/`, `i18n/`,
  `theme/`, `auth/`, `report-form/`): mock `fetch` and `XMLHttpRequest` for
  `api/`, so the whole web logic is under the gate.
- **A `v8 ignore` or `istanbul ignore` hint** is for a branch no input can
  reach, and the comment on the line directly above it says why. The guard fails
  one without a reason. Never use it for code a test could reach.

### Lint

- `npm ci && npm --prefix src/web ci && npm --prefix tests/e2e ci && npm --prefix tools/gherkin ci && npm run lint`
  runs ESLint over `src/web`, `tools`, `tests/js` and `tests/e2e` (flat config
  `eslint.config.mjs`; CI's `lint` job; pre-commit over the staged files). The
  type-aware rules need the web app's and the browser suite's packages
  installed. Rules are errors: `@eslint/js` and `typescript-eslint`
  strict-type-checked for every TypeScript file, `tools` and `tests/js`
  included; in `src/web` also `react-hooks` (`rules-of-hooks`, `exhaustive-deps`)
  and `jsx-a11y`.
- Fix the code, never the markup to please a rule. Give a value its real type
  at the boundary (`response.json()`, a saved draft) and keep the runtime guard
  on untrusted data even where the type says it cannot fail. Where a fix would
  change behaviour or the DOM, disable that rule on that line with the reason:
  `// eslint-disable-next-line react-hooks/exhaustive-deps -- <why>`; a disable
  without `-- <why>` is an error. A name starting with `_` is unused on purpose.
  The rules relaxed for a kind of file are listed in ADR-0188's strict-linting
  amendment.

### Tests are never part of a release

- The test packages stay in `devDependencies`. No file that is not a test
  imports a `*.test.*` file, `vitest`, `@vitest/*` or `@testing-library/*`
  (`check-component-split`).
- `node tools/web/check-web-bundle.ts src/web/dist` fails if the built bundle
  carries a test marker. The `web` job and the release build run it. The release
  ships the built `dist` only, never `src/`.

## Markdown and label colons

- **One renderer.** `components/Markdown.tsx` is the only place Markdown becomes
  markup: headings, paragraphs, bold, italic, lists, line breaks. Raw HTML shows
  as text, a link as its text, an image not at all. Use it for a summary and for
  a long-text answer with its translation; never `dangerouslySetInnerHTML`
  (ADR-0180).
- **Never advertise it.** No Markdown editor, toolbar, preview, or hint; a
  textarea stays plain.
- **The colon is the interface's.** A label is stored without one; show it with
  `labelWithColon` (`lib/questionPrompt.ts`): `Label:` in en-CA, `Label :` in
  fr-CA, none after a statement, a group, or a label ending in `?` (ADR-0181).

## Admin and authentication

- Public and admin are routes in one application, build, and container
  ([ADR-0048](../../.spec/decisions/ADR-0048-one-website-admin-as-a-route.md)).
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
  ([ADR-0066](../../.spec/decisions/ADR-0066-a-development-identity-provider-signed-with-a-dev-key.md)).

## Tests

- Every UI behavior change ships:
  - a `.feature` scenario in the same pull request (`AGENTS.md`
    "Specification-driven development") — a Playwright test alone does not
    satisfy this;
  - a Playwright test. For a `@ui` scenario, its steps in `tests/e2e/steps/`
    **are** that test, via `playwright-bdd`
    ([ADR-0053](../../.spec/decisions/ADR-0053-ui-scenarios-execute-via-playwright-bdd.md));
  - a server-side test when it touches API behavior
    ([ADR-0045](../../.spec/decisions/ADR-0045-ui-changes-require-playwright-and-server-tests.md)).
- Logic you add or move is unit-tested as above; a unit test adds to the
  Playwright test and replaces neither.
- Plain `.spec.ts` files outside `tests/e2e/steps/` are broad smoke coverage
  only, never a substitute for a scenario.

## Never add

- server drafts or reserved report IDs;
- pre-submit API or database writes other than attachment uploads;
- resumable upload sessions;
- third-party font or asset calls;
- client access to private report data beyond authorized admin DTOs.
