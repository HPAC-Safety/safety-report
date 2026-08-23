# Localization

The reporter and reviewer interfaces support Canadian English (`en-CA`) and
Canadian French (`fr-CA`).

Every immutable question revision stores both languages, including help text
and option labels. A revision cannot be created with a missing label. The UI
selects the appropriate columns at read time while stored answers use invariant
question keys and option codes.

There is no automatic translation service or generated-locale workflow.
Administrators enter and review both languages when creating a question
revision. Fixed UI copy should likewise be checked in and reviewed in both
languages when the UI is implemented.

The Worker asks for one candidate summary in the report's language. Translating
the summary into a second language is outside the current product flow.
