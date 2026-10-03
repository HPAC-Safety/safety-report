---
title: Web, localization, and design
description: Supporting detail for the bilingual React sites, design system, and accessibility scenarios.
type: spec
area: web-localization-and-design
---

# Web, localization, and design

Supporting detail for
[`web-localization-and-design.feature`](web-localization-and-design.feature)
that doesn't fit Gherkin.

## One site, with an admin route

There is one website on one origin
([ADR-0048](../../decisions/ADR-0048-one-website-admin-as-a-route.md)):

- the public routes contain the report form, public feed, and public detail;
- the member-login route signs a member in;
- `/admin` contains review and question editing, and appears only for a member
  whose token carries the SafetyOfficer or Administrator role.

There is no allowlist management screen, because there is no allowlist
([ADR-0065](../../decisions/ADR-0065-no-user-records-identity-is-the-token-subject.md)).

Hiding `/admin` from the navigation is a convenience, never a security
boundary — the API authorizes every data request on its own. It is a
React/TypeScript application built with Vite
([ADR-0043](../../decisions/ADR-0043-react-typescript-vite-web-front-end.md)),
using semantic HTML and compiled Tailwind CSS.

## Localization scope

The initial locale is resolved in order: an explicit stored choice, then the
hostname (`safety.hpac.ca` → English, `securite.acvl.ca` → French, #463), then
the browser's languages, then English. The hostname→locale mapping is a small
literal object in `src/web/src/i18n/locales.ts` — one entry per production
hostname, nothing speculative. An unrecognized host, including staging's
`*.cloudfront.net` address, has no entry and falls through to the browser
languages. The toggle never changes the host (`REQ-WLD-031`).

Both interface catalogues, `locales/en-CA.json` and `locales/fr-CA.json`, are
built into the page's own script bundle, not fetched afterwards
(REQ-WLD-049,
[ADR-0190](../../decisions/ADR-0190-the-interface-catalogues-ship-in-the-page-bundle-and-the-page-is-never-cached-stale.md)). A deploy replaces every hashed script file, so a
catalogue fetched on demand could be gone by the time a tab left open asks for
it, and the page would show raw keys until a reload. Bundled, the text is
already in memory and both languages switch without a request. They cost
about 21 KB gzipped together. A key missing from French still falls back to its
English text, as does a build with no French file at all.

Dates, numbers, and accessible labels use locale-aware formatting. Stored
codes/values remain invariant. A free-text answer gets a second language only
when an administrator marked its question for translation, and then off the
submission path
([ADR-0112](../../decisions/ADR-0112-only-answers-that-need-it-get-a-second-language.md));
the reporter's own words are never changed. Summary texts are returned together
by the one runtime model call; neither is a UI-catalogue string. Terms in `locales/glossary.json` are pinned and must
not be machine-translated.

`locales/glossary.json` pins whole strings by key. `locales/terms.json` holds
single words instead: for each English term, the French it must become and
the forms it must never become. `upload` is *téléverser*, never *télécharger*,
which Canadian French reads as download. The CI translator is told every term
with each request, and so is the runtime translator (REQ-WLD-027,
REQ-WLD-035). Verification fails on any French value, from
a machine or a person, that uses a forbidden form where the English has the
term (REQ-WLD-026). A term is a correctness rule, not a provenance rule: a
hand-edited value is recorded as a correction, but it still has to say the
term correctly.

## Machine translation: Gemini, en-CA and fr-CA

An OpenAI-compatible translator (`OpenAiTranslator`) does every machine
translation, at runtime and in CI, between `en-CA` and `fr-CA`. It sends its
own model to the `IAiMediator`, which picks the provider handler by the
model's name (`gemini-*` goes to Gemini, REQ-WLD-042); there is no provider
setting, and a model no handler claims stops startup while a key is held
(REQ-WLD-043, REQ-WLD-044)
([ADR-0179](../../decisions/ADR-0179-gemini-translates-everything-between-canadian-english-and-canadian-french.md)).
French to English is written in Canadian spelling (colour, centre); English to
French is Canadian French. The prompt names the variant, so the running system
has no language code to configure and no `EnglishTarget` setting
(REQ-WLD-033, REQ-WLD-034).

DeepL is kept, dormant: `DeepLTranslator`, `DeepLOptions`, their tests, and
the `tools/i18n/translator.ts` adapter stay, but nothing registers or selects them,
so it can be switched back. DeepL has no Canadian English, so that adapter
still asks for `EN-US` or `EN-GB` by `Translation:EnglishTarget`, and anything
else stops it at startup (REQ-WLD-028, REQ-WLD-029;
[ADR-0115](../../decisions/ADR-0115-the-english-translation-target-is-configuration.md)).

- **A separate call.** `ITranslator` is not the summary call and is outside
  the "one model call, only with consent" rule. It receives the strings and
  the term list and nothing else, and comes back one translation per string,
  in order (REQ-WLD-037). A reply with the wrong count, an empty translation, a
  sentence instead of the JSON, or a changed `{placeholder}` or markup tag is
  refused, and the failure carries none of the reply
  (REQ-WLD-038, REQ-WLD-039).
- **The term list goes with every request**, runtime and CI: the API's
  authoring and reviewer drafts, the Worker's answers, labels, and comments,
  and `tools/i18n/translator.ts` (REQ-WLD-027, REQ-WLD-035).
- **One versioned prompt**, `locales/translation-prompt.v2.md`, read by both
  runtimes. A behavior change is a new version file.
- **Places are localized, never copied.** A place takes its established name
  in the target language, the Canadian province or territory abbreviation
  (BC ↔ C.-B.), and a translated generic word (Mount ↔ mont), keeping the
  specific part and a municipality's official name. The names of people,
  aircraft, and organizations are copied (REQ-WLD-047).
- **The summary's key, its own model.** No new key or secret exists. The
  `Translation` settings hold only `Model` (`gemini-3.7-flash`) and
  `ReasoningEffort` (`low`), tuned apart from summaries (REQ-WLD-040,
  REQ-WLD-041).
- **No key, no translation**, in every environment, Development included.
  There is no stand-in that echoes the text (REQ-WLD-036).

Not built: a second translation provider in use at once (DeepL is kept, not
running); a per-value
record of the model or prompt version (a value keeps only its `auto` /
`human` / `choice` source); re-translating existing values when the prompt or
model changes; a language code chosen by configuration.

## Visual system

Use the existing restrained HPAC token system: Tailwind v4 via
`@tailwindcss/vite`, CSS custom-property tokens, Aleo for display headings,
Poppins for interface/body copy. Target WCAG 2.2 AA across both themes and
languages.

## Leaving a form with unsaved changes (#659)

Every editable form across the public and admin sites warns before it is left
with unsaved changes, through one shared hook,
`useUnsavedChangesGuard(dirty, withinPath?)`
(`src/web/src/hooks/useUnsavedChangesGuard.tsx`): a `beforeunload` listener for
closing the tab, reloading, or typing a new address (the browser's own
prompt, which cannot carry custom text), and a React Router route-change
block for an in-app navigation, which shows the shared bilingual
`UnsavedChangesDialog` (`src/web/src/components/UnsavedChangesDialog.tsx`) —
the same focus-on-keep, Escape-keeps pattern the report form's
`DiscardReportDialog` already used.

A form may pass its own dialog wording and turn off the unload prompt, through
the hook's optional third argument. Only the report form does: its answers are
already saved in the browser, so its dialog says so and it prompts on unload
only while a file is still uploading (#748; `report-submission/README.md`).

React Router allows only one active `useBlocker` per router, so the hook
itself never calls it: `UnsavedChangesGuardRoot`, mounted once in `App.tsx`,
owns the one `useBlocker` and the one dialog, and each form's
`useUnsavedChangesGuard` call just registers its own dirty predicate with it
(through context) and unregisters when it becomes clean or unmounts. The
blocker checks every registered form's predicate on each navigation attempt
and blocks if any one of them says to. `useBlocker` needs a data router, so
`main.tsx` builds one with `createBrowserRouter`/`RouterProvider` from
`routes.tsx`'s route config (`App.tsx` is that router's root layout,
rendering each page into an `Outlet`) instead of the plain `<BrowserRouter>`
it used before.

Every editable form calls the hook with its own `dirty` condition:

| Form | Route | Scenarios |
|---|---|---|
| Report form | `/report/:stepKey?` | `report-submission.feature` REQ-SUB-121, REQ-SUB-122, REQ-SUB-128..130 |
| Question editor | `/admin/questions` | REQ-QB-238, REQ-QB-239 |
| Type-ahead value correction | `/admin/type-ahead-values` | REQ-MOD-186 |
| Summary review editor | `/admin/reports/:reportId` | REQ-MOD-185 |
| Private notes composer | `/admin/reports/:reportId` | REQ-MOD-187 |
| Private-attachment staging area | `/admin/reports/:reportId` | `media.feature` REQ-MOD-177 |
| Published-report comment composer | `/reports/:reportId` | REQ-COM-021 |

A multi-step form's own step navigation (the report form's
`/report/<question-key>` addresses, ADR-0099) never counts as leaving: the
hook's optional `withinPath` lets navigation within that prefix proceed
without a prompt.

Out of scope for this mechanism, decided with issue #659:

- The member sign-in form (`/login`): re-entering a username and password is
  not the kind of loss this mechanism protects against, unlike free text a
  person wrote.
- The Typeform import dialog's review step: its state is re-derived by
  re-uploading the same files, not authored content that is lost.
- The "awaiting translation" admin answer page: removed by a concurrent
  change (issue #666).
- A server-side draft that would make the warning unnecessary — invariant 2
  forbids one.

## Markdown

A summary, and a reporter's paragraph answer with its Worker translation, are
read as Markdown, through one shared component
([ADR-0180](../../decisions/ADR-0180-a-summary-is-markdown-with-one-section-per-public-paragraph-question.md)):

- **The safe subset.** Headings, paragraphs, bold, italic, lists, and line
  breaks. Raw HTML is never rendered: it shows as written. A link shows as its
  text only, and an image is dropped, so a summary can never send the reader's
  browser anywhere or make it fetch anything (`REQ-WLD-045`). A single newline
  is a line break, so a reporter's own breaks survive.
- **Public typography.** On the public report page a summary is set like the
  prose of the other public pages: body `text-ink-muted` at the default size,
  and a section heading in the home page's section-heading style
  (`font-display text-2xl font-bold`), still an `h2` under the page's `h1`.
  The same in the light and dark themes (`REQ-WLD-046`). The admin pages'
  rendering is unchanged.
- **Where.** The public report page; the admin review page and its revision
  history (`REQ-MOD-208`); a long-text answer and its translation on the admin
  report detail (`REQ-MOD-209`). The public feed shows the first section's body
  as plain text (`REQ-MOD-210`).
- **Hidden.** Nothing tells a person they may use Markdown: textareas stay
  plain, with no editor, toolbar, preview, or hint (`REQ-MOD-211`).

## Out of scope

What not to build here. The global list in
[system overview](../../system-overview.md) still holds; this narrows it
to this area ([ADR-0083](../../decisions/ADR-0083-specification-driven-development.md)).

- Markdown beyond the safe subset: links, images, tables, code blocks, or
  embedded HTML. Markdown in member comments or in question help text.
- A Markdown editor, toolbar, preview, or hint anywhere in the interface.
- Loading a font, script, style, or icon from a third-party CDN at page load.
  Everything the site needs is committed and self-hosted
  ([ADR-0023](../../decisions/ADR-0023-pinned-and-vendored-web-assets.md)).
- A separate admin site or deployment. `/admin` is a route on the one built
  application ([ADR-0048](../../decisions/ADR-0048-one-website-admin-as-a-route.md)).
- Runtime machine translation of application chrome. Catalogues are committed
  and reviewed.
- Treating client validation as the authority. It is a convenience in front of
  server validation, never a replacement for it.
- A per-page opt-out of the scroll reset. Following a link to another page
  starts it at its top on every page (REQ-WLD-032,
  [ADR-0173](../../decisions/ADR-0173-a-fresh-navigation-starts-at-the-top-and-only-a-return-restores.md)). The reset does not
  apply to a change to the query string alone (the search box, a status
  filter), a link to an in-page anchor, or Back and Forward, where the
  browser or a report list restores the earlier position.
- Loading a catalogue on demand, or reloading the page to recover one a
  deploy removed. Both catalogues ship inside the page's bundle (REQ-WLD-049).
- A third language, or a locale the association has not adopted.
- Hand-editing `locales/fr-CA.json`. A French correction is a recorded
  provenance event
  ([ADR-0070](../../decisions/ADR-0070-a-hand-edited-french-value-is-a-recorded-correction.md)).
- Rewriting a translation after the fact to replace a forbidden term. French
  conjugates and agrees (*téléverser*, *téléversés*, *téléversement*), so a
  string substitution produces wrong French. A forbidden form fails
  verification and a person corrects it.
- Holding the server-side `ITranslator` (question authoring, Worker answer
  translation) to the term list. The term list governs the UI catalogue only.
