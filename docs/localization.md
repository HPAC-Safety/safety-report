# Localization

HPAC Safety supports Canadian English and Canadian French.

## Application chrome

Stable UI strings live in reviewed `locales/en-CA.json` and
`locales/fr-CA.json` catalogues with matching keys. Existing CI generation may
help prepare a catalogue PR, but runtime pages never call a translation service
and generated French still receives human review.

Resolve locale in this order: explicit user selection, browser preference,
English fallback. Persist the explicit selection, set the HTML `lang`, and keep
form answers/revision IDs when switching language.

## Database questions

Each complete immutable question revision stores its English and French label,
help text, and option labels. Administrators provide and review both versions.
Question text is not generated from UI catalogues and is never automatically
translated at authoring or render time.

## Reports and summaries

Raw report answers and attachments are never translated. The Worker's one model
call returns both `AiSummaryEn` and `AiSummaryFr` for the same eligible facts.
There is no source-summary translation stage or per-language approval; a safety
officer reviews and approves the pair.

Validation and problem details use stable machine codes plus localized safe
copy. Never echo private input merely to localize an error.

See [`features/web-localization-and-design/web-localization-and-design.feature`](../features/web-localization-and-design/web-localization-and-design.feature).
