# Web, localization, and design

## Sites and technology

The product has two independently deployed static sites:

- the public site contains the report form, public feed, and public detail;
- the admin site contains sign-in, review, question editing, and allowlist
  management.

They may share committed assets and small JavaScript modules but have separate
origins/distributions and deployment permissions. Both use semantic HTML,
progressive plain JavaScript, and compiled Tailwind CSS. There is no SPA
framework, client router requirement, Node production server, or bundler.

## Localization

English Canadian (`en-CA`) and French Canadian (`fr-CA`) are first-class. The
initial locale is selected in this order:

1. the user's explicit stored language choice;
2. a supported browser language preference; then
3. English fallback.

A visible language toggle is available on every page. Switching it rerenders
labels, help, validation, navigation, and formatting without clearing or
remapping answers. The document `lang` attribute and page title update too.

Application chrome and stable validation/error messages use committed locale
catalogues with key parity. Question labels/help/options come from the bilingual
database revision and are authored by an Administrator in both languages.
There is no runtime or CI auto-translation service for question rendering.
Summary texts are returned together by the one runtime model call; neither is a
UI-catalogue string.

Dates, numbers, and accessible labels use locale-aware formatting. Stored
codes/values remain invariant. Free-text report answers are never translated.

## Form behavior

The form renders sections, statements, questions, help, and options in database
order. Only publication consent displays required treatment. Optional questions
offer a natural blank/skipped state; controls never coerce an answer. Consent
requires an explicit yes or no and has no selected default.

The browser preserves locale, revision IDs, and answers locally for 15 days and
shows a privacy explanation before submission. Attachment selection appears
last with type/count/size guidance and a warning that files are not restored
after reload. Submission shows bounded progress, disables accidental duplicate
clicks, and clears saved state only after a definite `202`.

Server validation remains authoritative. Client validation exists to explain
errors early, using the same stable type/option rules and localized messages.

## Public and admin rendering

Public pages render only the public DTO. The active locale selects the primary
summary text and permits switching to its counterpart; the HTML/JS must not
receive private fields and merely hide them.

Admin pages clearly distinguish private context, ordinary report content,
summary output, processing failures, approval state, safe image/video
derivatives, and unredacted private document downloads.
Dangerous actions require clear confirmation. Editing either summary visibly
invalidates approval. Question editing explains that saving always creates a
new immutable revision.

## Visual system

Use the existing restrained HPAC token system: Tailwind standalone CLI, CSS
custom-property tokens, Aleo for display headings, Poppins for interface/body
copy, self-hosted WOFF2 files, and committed imagery. Assets are never loaded
from third-party CDNs. The current logo is explicitly a placeholder and must be
replaced only with an approved HPAC asset.

Dark mode is a token redefinition driven by user/OS preference, not duplicated
component markup or scattered dark variants. Contrast, focus, error, disabled,
and success states must work in both themes and languages.

## Accessibility and resilience

Target WCAG 2.2 AA. Every control has a programmatic label and usable keyboard
order; groups use fieldset/legend; errors are linked to fields and summarized;
focus is visible; status updates use appropriate live regions; motion respects
reduced-motion; touch targets and contrast are sufficient; media previews do
not become required to complete a report.

Core content and navigation render without relying on a framework. JavaScript
failures must not expose data, silently publish, or erase saved answers. A
network failure keeps local report state and explains how to retry.
