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
