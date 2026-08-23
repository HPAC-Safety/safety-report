# Localization

The reporter and reviewer interfaces support Canadian English (`en-CA`) and
Canadian French (`fr-CA`).

## Questions

Every immutable question revision stores both languages, including help text
and option labels. A revision cannot be created with a missing label. The UI
selects the appropriate columns at read time while stored answers use invariant
question keys and option codes.

An administrator authors a question in one language; the other is filled by
`ITranslator` as the next step, before the revision is saved. Do not require
an administrator to type both languages by hand.

## Summaries

The Worker asks for one candidate summary in the report's own language, then
translates it through `ITranslator` to produce the second official language.
Both candidates are stored and both require independent human approval before
a report is publishable — see `Report.IsPublishable`. Report content and
private context remain in the submitted language and are never translated;
only the LLM-anonymized summary crosses the translation boundary.

## Site UI chrome

**No hardcoded user-facing strings anywhere.** `locales/en-CA.json` is the
source of truth for every piece of UI chrome, consumed by both the web apps
and the API, enforced by a CI lint. `locales/fr-CA.json` is generated **in
CI**, never hand-edited — see `tools/translate-locale.mjs` and
`.github/workflows/i18n-translate.yml`. Terms in `locales/glossary.json` are
pinned and never machine-translated.

Fixed UI copy should be checked in and reviewed in both languages as the UI is
implemented; see [`skills/localize-hpac-app/SKILL.md`](../skills/localize-hpac-app/SKILL.md)
for the full CI translation workflow, and ADR-0021/ADR-0022 for how and why.
