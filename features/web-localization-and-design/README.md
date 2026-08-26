# Web, localization, and design

Supporting detail for
[`web-localization-and-design.feature`](web-localization-and-design.feature)
that doesn't fit Gherkin.

## Sites

- the public site contains the report form, public feed, and public detail;
- the admin site contains sign-in, review, question editing, and allowlist
  management.

They may share committed assets and small JavaScript modules but have
separate origins/distributions and deployment permissions. Both use semantic
HTML, progressive plain JavaScript, and compiled Tailwind CSS.

## Localization scope

Dates, numbers, and accessible labels use locale-aware formatting. Stored
codes/values remain invariant. Free-text report answers are never translated.
Summary texts are returned together by the one runtime model call; neither is
a UI-catalogue string. Terms in `locales/glossary.json` are pinned and must
not be machine-translated.

## Visual system

Use the existing restrained HPAC token system: Tailwind standalone CLI, CSS
custom-property tokens, Aleo for display headings, Poppins for
interface/body copy. Target WCAG 2.2 AA across both themes and languages.
