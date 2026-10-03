---
title: A published report page shows the language it was written in
description: A published report's own page carries the locale the reporter wrote it in, from the public_reports view, and labels the summary "Translated from French" or "Translated from English" when that differs from the site's language. The owner accepted the slight identifying hint.
type: adr
status: accepted
date: 2026-09-29
decision-makers: Chase Florell
keywords: public report, public_reports, report language, translated from, privacy, allowlist, ADR-0055, ADR-0116, ADR-0117
---

# ADR-0176 — A published report page shows the language it was written in

**Status:** Accepted. It amends the public DTO allowlist stated by
[ADR-0116](ADR-0116-a-read-rule-lives-in-a-view.md) and REQ-MOD-036, which
listed "report language" among what the public never sees. Part of
[#682](https://github.com/HPAC-Safety/safety-report/issues/682).

## Context

A reader of a published report sees its summary in the site's display
language. Both summaries exist for every published report, but one of them is
the model's rendering of a report the reporter wrote in the other language.
The reader has no way to know that what they are reading was translated.

The reporter's language (`Report.Language`) was deliberately private: in a
small community, it is a faint hint about who wrote a report. The admin
detail page already shows it ("Written in English" / "Written in French").

## Decision

- **The public report detail carries the report's language.** The
  `public_reports` view gains a `language` column (`en-CA` or `fr-CA`), read
  from `reports.language`. Only `GET /api/v1/public/reports/{id}` selects and
  serializes it, as `language`. The feed, its search, and every other public
  read leave it out, so it appears only where a reader has already chosen one
  report.
- **The page labels a translated summary.** When the report's language differs
  from the site's current language, a muted line under the published date
  reads "Translated from French" or "Translated from English" (French:
  "Traduit du français" or "Traduit de l'anglais"). When the two match it shows
  nothing. It follows the header's language toggle without a reload.
- **The privacy trade-off is accepted by the owner** (2026-09-29): showing the
  reporter's language publicly is a slight identifying hint in a small
  community, and the owner chose to show it for the reader's benefit. It does
  not generalize: no other private field becomes public by this decision, and
  a report without publication consent never reaches a public read at all.

## Considered options

None were recorded when this decision was accepted.

## Consequences

- `REQ-MOD-036` now lists the language among the public detail's fields and no
  longer claims the response never contains it; `REQ-MOD-190` to
  `REQ-MOD-193` cover the label, its absence, the toggle, and the API field.
- The public feed and search are unchanged; `PublicReportView` gains nothing.
- Out of scope: labelling comments or attachments, and the admin detail page,
  which already shows the language.
