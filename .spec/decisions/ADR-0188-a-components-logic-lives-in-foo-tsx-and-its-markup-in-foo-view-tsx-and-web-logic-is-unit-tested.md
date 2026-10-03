---
title: "A component's logic lives in Foo.tsx and its markup in Foo.view.tsx, and web logic is unit-tested"
description: "Every web component splits into Foo.tsx (the useFoo view model and a Foo that renders the view) and Foo.view.tsx (markup only). Logic is held to 100% line, branch, function and statement coverage by Vitest and Testing Library, scoped by the files on disk; Playwright covers views. Test code is never part of a release, and a guard and a bundle check enforce it."
type: adr
status: accepted
date: 2026-10-02
decision-makers: Chase Florell
keywords: strict-type-checked, typescript-eslint, projectService, eslint-comments, web, React, component, view model, hook, Foo.view.tsx, Vitest, Testing Library, jsdom, coverage, 100%, lcov, ratchet, type check, tsc, guard, release, bundle, test code, ESLint, useModalDialog, ignore hint, ADR-0014, ADR-0017, ADR-0043, ADR-0045, ADR-0053, ADR-0073, ADR-0090, ADR-0165
---

# ADR-0188 — A component's logic lives in `Foo.tsx` and its markup in `Foo.view.tsx`, and web logic is unit-tested

**Status:** Accepted. Decided by the owner on 2026-10-02 in
[#756](https://github.com/HPAC-Safety/safety-report/issues/756); the foundation
is [#757](https://github.com/HPAC-Safety/safety-report/issues/757). Amends
[ADR-0043](ADR-0043-react-typescript-vite-web-front-end.md) (the web front end)
and [ADR-0045](ADR-0045-ui-changes-require-playwright-and-server-tests.md) (what
a UI change ships with). Applies
[ADR-0014](ADR-0014-coverage-gate.md),
[ADR-0017](ADR-0017-ratchet-judges-added-code.md),
[ADR-0073](ADR-0073-a-ui-scenario-is-skipped-by-reqnroll-itself.md) and
[ADR-0165](ADR-0165-the-coverage-baseline-walks-back-to-the-newest-run-that-ran-coverage.md).

## Context

An audit of `src/web/src` on 2026-10-02 found:

- 63 `.tsx` files: 25 markup only, 16 with a little logic, 22 with heavy logic
  (`QuestionEditor` 1057 lines, `ReportForm` 801).
- No TypeScript unit test and no Vitest. Behavior was proved only by Playwright,
  which is slow, coarse, and cannot reach a branch such as an upload that
  fails halfway.
- `tsconfig.json` is `strict`, but nothing runs `tsc`: Vite builds without
  type-checking.
- No web coverage in the ratchet
  ([ADR-0014](ADR-0014-coverage-gate.md)).

## Decision

### The convention

A component's logic and its markup are separate files, side by side.

1. **`Foo.tsx` is the logic.** It exports `useFoo(props)`, the view model:
   state, effects, refs, derived values, handlers and fetching, returned as
   one object. It also exports `Foo`, which does nothing but render
   `<FooView {...props} {...useFoo(props)} />`, so every import site stays
   unchanged. A markup-only component gets the same `Foo`, a pass-through with
   no `useFoo`.
2. **`Foo.view.tsx` is the markup.** It exports `FooView` and `FooViewProps`.
   - It calls no hook except `useLocale`.
   - It imports nothing from `api/`.
   - It uses no storage, `fetch`, timer, `XMLHttpRequest` or module-level
     mutable state.
   - A DOM ref comes in as a prop; the view model creates it.
3. **A stateful private sub-component gets its own pair.** A stateless one
   moves into the view that uses it.
4. **A pure helper goes into a camelCase `.ts` file** beside the component or
   in `lib/`.
5. **A provider** keeps its value logic in `Foo.tsx`; its view renders
   `Ctx.Provider`. **A class error boundary** keeps the class in `.tsx`, and its
   fallback markup moves to the view.
6. **`main.tsx` and `routes.tsx` are exempt.** They wire the application; they
   are not components.
7. **The DOM stays identical.** Same elements, attributes, ids, `data-*`,
   classes, text and order. No file under `tests/e2e/**` changes; if one would
   need to, the split is wrong. Playwright relies on exactly those.

### Why every component, including markup-only ones

[`coding-conventions`](../../skills/coding-conventions/SKILL.md) says a seam
earns its pattern and a pattern never earns its seam. A markup-only component has
no logic to separate, so by that rule a split there earns nothing. The owner
chose uniformity over that economy, and this ADR records the choice rather than
arguing it away:

- **One shape to look for.** A reader, a reviewer and an agent find logic in
  `Foo.tsx` and markup in `Foo.view.tsx` in all 63 components, with no judgment
  call about whether *this* one is "enough" logic. A component that gains its
  first handler later gains it in the file already waiting for it, and a diff
  does not also move the markup.
- **The guard is mechanical.** "Every component has a view" is a file-name
  check. "Every component with enough logic has a view" is not, and would be
  argued on each pull request.
- **The cost is small and bounded.** For a markup-only component the pass-through
  is a few lines.

This does not change the rule for abstractions elsewhere: no new shared
abstraction is introduced by the split (a `useModalDialog` for the dialogs is a
follow-up idea, not part of it).

### Web logic is unit-tested

- **Runner.** Vitest with Testing Library and jsdom, configured in
  `src/web/vite.config.ts`. `npm --prefix src/web run test` and
  `test:coverage`. `typecheck` runs `tsc --noEmit` over `src`, tests included.
- **Tests sit beside the code** as `Foo.test.tsx` (`foo.test.ts` for a helper).
  A test title is prose, as for `node:test` and Playwright
  ([ADR-0069](ADR-0069-scannable-given-when-then-test-names.md) governs C#
  only).
- **Division of labor.** Logic (`Foo.tsx` and helpers) is held to **100% line,
  branch, function and statement coverage** by Vitest. Views are covered by
  Playwright and are excluded from the Vitest report; they hold no logic to
  cover. A UI behavior change still ships a scenario and a Playwright step
  ([ADR-0045](ADR-0045-ui-changes-require-playwright-and-server-tests.md),
  [ADR-0053](ADR-0053-ui-scenarios-execute-via-playwright-bdd.md)); a unit test
  adds to that, it replaces neither.
- **Test-only edits.** `src/web/**/*.test.ts(x)` counts as a test for
  `feature-coverage`'s `test-only` category, though it lives under `src/`
  ([ADR-0090](ADR-0090-an-exemption-cites-the-claims-it-preserves.md)). The
  hardcoded-strings scan skips test files: they hold fixtures and assert on
  copy.

### The 100% scope follows the files

Eight area pull requests split components in parallel, so no list is edited to
bring a file under the threshold. The scope is computed from the files on disk
by `tools/web/web-coverage-scope.mjs`, which `vite.config.ts` reads:

- a `Foo.tsx` with a sibling `Foo.view.tsx` is in scope: the split *is* the
  opt-in;
- a `foo.ts` (or a `.tsx`) with a colocated `foo.test.ts(x)` is in scope: writing
  the test is the opt-in;
- `*.view.tsx`, `main.tsx`, `routes.tsx`, tests and `*.d.ts` never are;
- with nothing in scope, the scope is a glob that matches no file, never an
  empty list, which Vitest would read as "everything".

Coverage's `include` is that list and the threshold is 100% per file, so an
in-scope file no test loads counts as 0%, not as absent. An area pull request
therefore splits its components, writes their tests, and edits no configuration.
An untested helper outside the scope is not held to 100%; backfilling untouched
`lib/*.ts` is out of scope for the split.

### The web lcov joins the ratchet

`test:coverage` writes `lcov.info` to `artifacts/coverage/web/`. The CI `test`
job runs it, so the file rides the existing `coverage-raw` artifact into the
`coverage` job, whose ReportGenerator glob already merges every `lcov.info`
under `artifacts/coverage`. The floor and the ratchet
([ADR-0014](ADR-0014-coverage-gate.md), [ADR-0017](ADR-0017-ratchet-judges-added-code.md),
[ADR-0165](ADR-0165-the-coverage-baseline-walks-back-to-the-newest-run-that-ran-coverage.md))
count it with no change to them. The `web` job runs the same command as its own
gate, with `typecheck`, the split guard and the bundle check.

The lcov names each file from the repository root (`src/web/src/lib/…`), like
the .NET and `tools/` reports, so the merged report shows web source. Because
the `coverage` job runs only on GitHub and under `tools/dev/ci-local.sh --full`,
the `web` job also runs `tools/web/check-web-lcov.mjs`. That check proves the lcov
exists, sits under a `-reports:` glob read from `ci.yml` itself, and names files
that exist. It runs in the fast local gate, so a broken hand-off fails before
GitHub ([#769](https://github.com/HPAC-Safety/safety-report/issues/769)).

### The guard

`tools/web/check-component-split.mjs`, run by the `web` job and pre-commit, fails
with the file and line when:

- a `*.view.tsx` has no sibling `Foo.tsx`, calls a hook other than
  `useLocale`, imports a value from `api/` or a runtime value from its own
  sibling `Foo.tsx`, uses `localStorage`, `sessionStorage`, `fetch(`,
  `setTimeout`, `setInterval` or `XMLHttpRequest`, or holds a module-level
  `let` or `var` (comments and string literals are not code, and a type-only
  import is allowed; see the 2026-10-02 amendment);
- any file that is not a test imports a `*.test.*` file, `vitest`, `@vitest/*`
  or `@testing-library/*`.

A **strict** mode additionally requires a `.view.tsx` for every component `.tsx`
(`main.tsx`, `routes.tsx` and tests excepted). It is a constant in the script,
not a command-line flag, so it holds in an editor and a local run as well as in
CI ([ADR-0073](ADR-0073-a-ui-scenario-is-skipped-by-reqnroll-itself.md)). It is
`false` while the split was under way; the last pull request of #756 set it
`true`, and it stays `true`.

### Test code is never part of a release

- The test packages stay in `devDependencies`.
- The import rule above stops test code at the source.
- `tools/web/check-web-bundle.mjs` reads what was built: it fails if a file under
  `src/web/dist` carries a marker only tests hold (`vitest`,
  `@testing-library`, `user-event`, `jsdom`, `renderHook`, `describe(`,
  `expect(`, `.test.`). The markers were checked against a real build and are
  absent from it; React's and the application's own words are not among them.
  The `web` job and the release build both run it.
- The release ships `src/web/dist` and nothing else: `release.yml` tars the
  built directory, and `deploy-environment.yml` syncs it to the site bucket. No
  Dockerfile builds or copies `src/web`.

## Consequences

- Behavior that Playwright could reach only slowly or not at all (a failed
  upload, a race, a branch of a form) is unit-tested.
- A type error now fails CI. The audit found none standing, so enabling
  `typecheck` required no code fix.
- `main` gains a second JavaScript test runner next to `node:test`. They do not
  overlap: `node:test` for `tools/`, Vitest for `src/web`.
- Each of the 63 components becomes two files, and every import site is
  unchanged.
- The foundation moved no component. Until the last area pull request, strict
  mode was off and unsplit components were neither guarded nor held to 100%.

## Alternatives considered

- **Split only components with enough logic.** The economical reading of "the
  seam earns the pattern". Rejected by the owner for uniformity; see above.
- **Test through Playwright only.** Rejected: it is the status quo this ADR
  exists to change.
- **A hand-kept list of files under the threshold.** Rejected: eight parallel
  pull requests would all edit it and conflict.
- **A global 100% threshold.** Rejected: no file has a test today, so it would
  fail until the last pull request, and the ratchet would carry nothing meanwhile.
- **Test files kept out of `src/` in a `tests/web` tree.** Rejected by the owner:
  tests sit next to the code they test.
- **A pass over `dist` as an inline `grep` in the workflow.** Rejected: a check
  that lives only in a CI step is not a rule
  ([ADR-0073](ADR-0073-a-ui-scenario-is-skipped-by-reqnroll-itself.md)); it is a
  script with its own test.

## Amendment (2026-10-02)

The last pull request of [#756](https://github.com/HPAC-Safety/safety-report/issues/756)
([#766](https://github.com/HPAC-Safety/safety-report/issues/766)) finished the
split and settled what the area pull requests turned up.

### The guard learned three things

- **A type-only import from `api/` is allowed in a view** (`import type …`, or
  `import { type X }` where every specifier is a type). Types are erased at
  compile time and carry no data access. A value import from `api/` stays
  banned. The guard reads import statements, so a statement split across lines
  is judged whole.
- **A view imports no runtime value from its own sibling `Foo.tsx`.** `Foo.tsx`
  imports `Foo.view.tsx`, so a runtime import back is a circular import. A type
  import is fine; a shared value goes in a small module both files import.
- **Comments and string literals are not code.** The banned words
  (`localStorage`, `fetch(`, timers…) are matched on code only, so a catalogue
  key such as `"report.privacy.localStorage"` is not a use of `localStorage`.

### A coverage-ignore hint is for an unreachable defensive branch, with its reason

A `v8 ignore` or `istanbul ignore` hint is allowed **only** for a branch no
input can reach (a defensive `?? []` over untrusted storage; a fallback the
compiler's indexed-access check asks for). The comment **directly above** it
gives the reason. The guard fails any such hint in any file without one. It is
not a way to reach 100% on code a test could reach: write the test, or delete
the branch in its own pull request.

### Every component is split, and the guard says so

Strict mode is on. The 5 attachment components, the type-ahead review page, the
dialogs and everything before them are pairs; a `.tsx` without a `.view.tsx`
fails, `main.tsx` and `routes.tsx` excepted.

### One hook opens every native dialog

`hooks/useModalDialog` opens a `<dialog>` as a modal when it mounts and starts
focus on the control `focusRef` is attached to. The six confirmation dialogs and
the attachment lightbox use it, instead of each carrying the same effect. The
DOM and behaviour are unchanged (proved against the originals on `main`: same
markup, same focus, one `showModal()`, Escape still keeps).

### Every TypeScript and JavaScript file is linted

- **ESLint, flat config at the root** (`eslint.config.mjs`), over `src/web`
  (tests included), `tools/*.mjs`, `tests/js` and `tests/e2e`. Dependencies are
  in a root `package.json`, because no single package owns all four trees; the
  web app's and the browser suite's own `package.json` stay about their runtime
  and tests. ESLint 9 is used because `eslint-plugin-jsx-a11y` supports 9, not 10.
- **Rules, all errors:** `@eslint/js` recommended and `typescript-eslint`
  recommended (not the type-checked presets) everywhere; in `src/web` also
  `react-hooks/rules-of-hooks`, `react-hooks/exhaustive-deps` and `jsx-a11y`
  recommended. A name starting with `_` is unused on purpose. An unused
  `eslint-disable` is itself an error.
- **A rule is turned off on the line, with the reason after `--`,** never for a
  directory. A fix that would change behaviour or the DOM is never made to
  satisfy a rule: an `exhaustive-deps` dependency that would change when an
  effect runs, or a role or caption that would change the markup, is disabled
  on its line with the reason.
- **It runs** as the `lint` job in CI (no path filter), in `tools/ci-local.sh`'s
  fast set, and in pre-commit over the staged files (over everything when the
  config or the root `package.json` is staged).
- **Type-aware, strict rules** came next; see the strict-linting amendment
  below ([#781](https://github.com/HPAC-Safety/safety-report/issues/781)).

### Every web helper has a test

A `.ts` under `src/web/src` with no test was outside the coverage scope, which
is what "the writing of the test is the opt-in" meant. Each now has a colocated
test at 100%, so all web logic falls under the gate. `vite.config.ts` and the
scope rule are unchanged; the files simply have tests.

## Amendment: strict, type-checked linting ([#781](https://github.com/HPAC-Safety/safety-report/issues/781))

Applies on the date of the pull request that closes #781. It replaces the
"recommended presets" decision above; everything else in "Every TypeScript and
JavaScript file is linted" stands.

### What runs

- **TypeScript** (`src/web`, `tests/e2e`): `typescript-eslint`
  `strict-type-checked`, every rule an error.
  - Type information comes from `parserOptions.projectService`. Each file
    belongs to the nearest `tsconfig.json`: `src/web/tsconfig.json` and a new
    `tests/e2e/tsconfig.json`. `src/web/vite.config.ts` is in neither (the web
    job installs no `@types/node`, so it cannot join the first), and gets the
    strict preset without type information.
  - Type-aware rules are only as good as the types. The lint job therefore
    installs the web app's and the browser suite's packages as well as the root
    ones (`npm --prefix src/web ci`, `npm --prefix tests/e2e ci`, each its own
    one-command step, ADR-0189), and pre-commit refuses to lint without them.
    Without React's and Playwright's types every value from them is `any`.
  - `tests/e2e/tsconfig.json` is for ESLint and editors. Nothing gates it:
    16 type errors stood in the suite before this change and still stand,
    because Playwright transpiles without checking.
- **JavaScript** (`tools`, `tests/js`, `eslint.config.mjs`): the **strict**
  preset without type information, plus four rules that need only inference —
  `no-floating-promises`, `no-misused-promises`, `await-thenable`,
  `require-await` — against a root `tsconfig.json` (`allowJs`, not `checkJs`).
  - Why not the full type-checked preset: plain `.mjs` carries no annotations,
    so it reported about 3,800 findings that were `any` propagating
    (`no-unsafe-*`, `restrict-template-expressions`), and 1,186 that were
    `node:test`'s `test()` call. Nothing else in `tools/` and `tests/js` was
    wrong in a way the four rules miss: they found one `require-await` in
    `tools/` and nothing else.
  - Typing the scripts with JSDoc would make the full preset useful; that is a
    separate, large change nobody has asked for.
- **Every disable comment states its reason** after `--`:
  `@eslint-community/eslint-comments/require-description` (an error), with
  `no-unlimited-disable`. An unused disable was already an error.

### Rules narrowed or turned off, and why

| Setting | Where | Reason |
|---|---|---|
| `no-confusing-void-expression`: `ignoreArrowShorthand` | TypeScript | `onClick={() => setOpen(true)}` is how every handler here is written (750 occurrences); braces around each would be churn with no behavioural content. A void call used as a value anywhere else is still an error. |
| `restrict-template-expressions`: `allowNumber` | TypeScript | A number in a template is predictable (`Step ${index + 1}`, a count). An object, array, `null`, `undefined`, boolean, `any` and `unknown` are still refused. |
| `no-non-null-assertion` off | `*.test.ts(x)`, `tests/e2e`, `tests/js` | A test indexes a fixture it built a line earlier; a `!` that is wrong fails the test at the next line. In shipped code the rule stays on. |
| `unbound-method` off | same | `expect(mock.method).toHaveBeenCalled()` reads the method to assert on it, never to call it. The rule cannot tell, and its Vitest-aware replacement is another dependency for no gain. |
| `require-await` off | same | `await act(async () => result.current.save())` and Playwright steps need the `async` callback without an `await` in it. |
| `no-floating-promises` off | `tests/js` | `node:test` registers a test with a call whose promise the runner awaits (1,186 calls). |

Everything else is on, in shipped code and in tests.

### Where the code changed instead

No behaviour or DOM changed. The fixes were, by kind:

- **Types said more than runtime does, so a guard looked dead.** Reads such as
  `items[index]`, `answers[id]`, `performance.getEntriesByType(...)[0]` and
  `match[3]` are typed as present because `noUncheckedIndexedAccess` is off. The
  guard stays; the type now says what the guard knows
  (`as T | undefined`, or `Partial<Record<string, T>>` for the answer maps).
  A guard on untrusted data — a saved draft, an API body — is never removed
  because its type says it cannot fail.
- **`any` at a boundary** (`response.json()`, `request.postDataJSON()`,
  `JSON.parse`, an untyped `vi.fn()`) is given the shape its reader already
  assumed.
- **Handlers that returned a promise to a `void` slot** (`no-misused-promises`)
  gain `void` at the call, or the slot's type gains `Promise<void>`. React and
  the tests that `await` them see the same value as before.
- **`navigate(...)`** returns `void | Promise<void>` in React Router 7; it is
  called with `void`, as before in effect.
- **Method shorthand in a returned object** (`onDragEnter(event) { … }`) became
  an arrow property, so destructuring it in the view is not an `unbound-method`.
- A non-null assertion in shipped code either went (the code narrows instead)
  or carries a line-level disable naming the invariant that makes it safe.
- `delete obj[key]` became a rest-destructure or `Reflect.deleteProperty`.

### Cost

`npm run lint` took about 4 s before and about 16 s after, cold, on a laptop: the
type checker now loads the web app. CI's lint job also installs two more
packages. Locally, pre-commit lints only the staged files.

