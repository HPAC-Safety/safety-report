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

Dates, numbers, and accessible labels use locale-aware formatting. Stored
codes/values remain invariant. Free-text report answers are never translated.
Summary texts are returned together by the one runtime model call; neither is
a UI-catalogue string. Terms in `locales/glossary.json` are pinned and must
not be machine-translated.

## Visual system

Use the existing restrained HPAC token system: Tailwind v4 via
`@tailwindcss/vite`, CSS custom-property tokens, Aleo for display headings,
Poppins for interface/body copy. Target WCAG 2.2 AA across both themes and
languages.

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
- A third language, or a locale the association has not adopted.
- Hand-editing `locales/fr-CA.json`. A French correction is a recorded
  provenance event
  ([ADR-0070](../../docs/decisions/ADR-0070-a-hand-edited-french-value-is-a-recorded-correction.md)).
