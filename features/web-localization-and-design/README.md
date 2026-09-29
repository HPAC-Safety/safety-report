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
([ADR-0048](../../docs/decisions/ADR-0048-one-website-admin-as-a-route.md)):

- the public routes contain the report form, public feed, and public detail;
- the member-login route signs a member in;
- `/admin` contains review and question editing, and appears only for a member
  whose token carries the SafetyOfficer or Administrator role.

There is no allowlist management screen, because there is no allowlist
([ADR-0065](../../docs/decisions/ADR-0065-no-user-records-identity-is-the-token-subject.md)).

Hiding `/admin` from the navigation is a convenience, never a security
boundary — the API authorizes every data request on its own. It is a
React/TypeScript application built with Vite
([ADR-0043](../../docs/decisions/ADR-0043-react-typescript-vite-web-front-end.md)),
using semantic HTML and compiled Tailwind CSS.

## Localization scope

The initial locale is resolved in order: an explicit stored choice, then the
hostname (`safety.hpac.ca` → English, `securite.acvl.ca` → French, #463), then
the browser's languages, then English. The hostname→locale mapping is a small
literal object in `src/web/src/i18n/locales.ts` — one entry per production
hostname, nothing speculative. An unrecognized host, including staging's
`*.cloudfront.net` address, has no entry and falls through to the browser
languages. Switching the language toggle changes only the locale, never the
host.

Dates, numbers, and accessible labels use locale-aware formatting. Stored
codes/values remain invariant. A free-text answer gets a second language only
when an administrator marked its question for translation, and then off the
submission path
([ADR-0112](../../docs/decisions/ADR-0112-only-answers-that-need-it-get-a-second-language.md));
the reporter's own words are never changed. Summary texts are returned together
by the one runtime model call; neither is a UI-catalogue string. Terms in `locales/glossary.json` are pinned and must
not be machine-translated.

`locales/glossary.json` pins whole strings by key. `locales/terms.json` holds
single words instead: for each English term, the French it must become and
the forms it must never become. `upload` is *téléverser*, never *télécharger*,
which Canadian French reads as download. The CI translator is told every term
with each request (REQ-WLD-027). Verification fails on any French value, from
a machine or a person, that uses a forbidden form where the English has the
term (REQ-WLD-026). A term is a correctness rule, not a provenance rule: a
hand-edited value is recorded as a correction, but it still has to say the
term correctly.

## Which English a machine translation produces

DeepL has no Canadian English. It rejects `EN-CA` with a 400, and plain `EN`
is a deprecated alias for American English. The English variant is therefore
configuration: `Translation:EnglishTarget` in both the API's and the Worker's
`appsettings.json`, because both call DeepL. The Worker translates answers and
comments, and the API serves authoring and reviewers' drafts. Today both are
`EN-US`. `EN-GB` is the only other accepted value, and anything else stops the
process at startup, so a bad value cannot become a stream of failed
translations (REQ-WLD-028, REQ-WLD-029). French is always `FR-CA`, which
DeepL does offer.

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
| Report form | `/report/:stepKey?` | `report-submission.feature` REQ-SUB-121..123 |
| Question editor | `/admin/questions` | `question-bank-and-form.feature` REQ-QB-238 |
| Type-ahead value correction | `/admin/type-ahead-values` | `moderation-authentication-and-publication.feature` REQ-MOD-176 |
| Summary review editor | `/admin/reports/:reportId` | `moderation-authentication-and-publication.feature` REQ-MOD-173 |
| Private notes composer | `/admin/reports/:reportId` | `moderation-authentication-and-publication.feature` REQ-MOD-175 |
| Published-report comment composer | `/reports/:reportId` | `comments.feature` REQ-COM-021 |

A multi-step form's own step navigation (the report form's
`/report/<question-key>` addresses, ADR-0099) never counts as leaving: the
hook's optional `withinPath` lets navigation within that prefix proceed
without a prompt.

Out of scope for this mechanism, decided with issue #659:

- The private-attachment drop zone (`PrivateAttachments.tsx`, issue #658):
  built separately; #658 adopts the shared hook rather than this pull request
  editing that file.
- The member sign-in form (`/login`): re-entering a username and password is
  not the kind of loss this mechanism protects against, unlike free text a
  person wrote.
- The Typeform import dialog's review step: its state is re-derived by
  re-uploading the same files, not authored content that is lost.
- The "awaiting translation" admin answer page: removed by a concurrent
  change (issue #666).
- A server-side draft that would make the warning unnecessary — invariant 2
  forbids one.

## Out of scope

What not to build here. The global list in
[system overview](../../docs/system-overview.md) still holds; this narrows it
to this area ([ADR-0083](../../docs/decisions/ADR-0083-specification-driven-development.md)).

- Loading a font, script, style, or icon from a third-party CDN at page load.
  Everything the site needs is committed and self-hosted
  ([ADR-0023](../../docs/decisions/ADR-0023-pinned-and-vendored-web-assets.md)).
- A separate admin site or deployment. `/admin` is a route on the one built
  application ([ADR-0048](../../docs/decisions/ADR-0048-one-website-admin-as-a-route.md)).
- Runtime machine translation of application chrome. Catalogues are committed
  and reviewed.
- Treating client validation as the authority. It is a convenience in front of
  server validation, never a replacement for it.
- A per-page opt-out of the scroll reset. Following a link to another page
  starts it at its top on every page (REQ-WLD-032,
  [ADR-0173](../../docs/decisions/ADR-0173-a-fresh-navigation-starts-at-the-top-and-only-a-return-restores.md)). The reset does not
  apply to a change to the query string alone (the search box, a status
  filter), a link to an in-page anchor, or Back and Forward, where the
  browser or a report list restores the earlier position.
- A third language, or a locale the association has not adopted.
- Hand-editing `locales/fr-CA.json`. A French correction is a recorded
  provenance event
  ([ADR-0070](../../docs/decisions/ADR-0070-a-hand-edited-french-value-is-a-recorded-correction.md)).
- Rewriting a translation after the fact to replace a forbidden term. French
  conjugates and agrees (*téléverser*, *téléversés*, *téléversement*), so a
  string substitution produces wrong French. A forbidden form fails
  verification and a person corrects it.
- Holding the server-side `ITranslator` (question authoring, Worker answer
  translation) to the term list. The term list governs the UI catalogue only.
