# Data handling

This system stores accounts of real accidents, including names, phone numbers,
injuries, and occasionally fatalities. Canadian personal information law
(PIPEDA) applies, and so does the promise the reporting form makes.

## Three tiers

A field's tier is a property of the field, not of the screen it appears on.

| Tier | Contents | Rules |
|---|---|---|
| **Restricted** | Reporter and pilot names, phone, email, member number, raw narrative, original media | Encrypted at rest. Admin-only. Never logged. Never sent to a translation service. |
| **Internal** | Manufacturer, model, precise site | Retained for HPAC trend analysis. Never published. |
| **Publishable** | Approved summary, certification class, province, severity, month and year | Public once a safety officer approves and consent was given. |

If you are unsure which tier something belongs to, it is Restricted.

Because the question set is data rather than code (
[ADR-0016](decisions/ADR-0016-data-driven-question-bank.md)), a **question
carries its own tier**, and a question added through the admin UI is
**Restricted** until someone decides otherwise. Nobody has to remember to
classify a new question for it to be handled safely; they have to remember in
order to *relax* it.

An answer copies the tier it was given under. Reclassifying a question later
therefore cannot retroactively downgrade the handling of text a reporter already
trusted us with.

## How "encrypted at rest" is done

Restricted text is encrypted **by the application**, with AES-256-GCM, before
PostgreSQL sees it. The key lives in configuration — a throwaway literal in
`appsettings.Development.json`, a Secrets Manager reference in production — and
never in the database that holds the ciphertext.

Because the question set is data, contact details are answers rather than
columns, so the whole of `report_answers.value` is encrypted rather than
selected rows of it. A value converter runs per value and cannot ask which tier
a row belongs to, and the tiering above already says the narrative is Restricted
and a new question is Restricted until someone decides otherwise.

Three consequences, all deliberate:

- Answer text cannot be searched, sorted, or indexed in SQL.
- The wrong key fails loudly. It never returns plausible-looking rubbish.
- Losing the key loses the data. Key custody is an operational responsibility.

`admin_users.member_identifier` is deliberately **not** encrypted: it is the
lookup key at sign-in, it is an administrator's own working identity, and it
never reaches a published summary.

See [ADR-0019](decisions/ADR-0019-application-side-field-encryption.md).

## Retention

Raw reports are **retained**, with contact fields column-encrypted and readable
only by administrators. They are kept because a summary can later be disputed,
corrected, or needed for a fatality investigation, and because deleting the
source makes every downstream error unfixable.

A scheduled purge of raw narrative and contact fields after a fixed window is a
reasonable future tightening. It is not implemented, and doing so is a policy
decision for HPAC rather than an engineering one.

## Uploads

One photo or video per report, matching the existing form.

- Private bucket, no public object URLs, ever. Admin views use short-lived
  pre-signed GETs.
- **EXIF is stripped on ingest** — GPS above all. A crash photo identifies a
  person and a site regardless of how clean the text is.
- Original bytes stay in the Restricted record; the stripped derivative is what
  a reviewer sees.
- Content type is sniffed, not trusted from the client.
- Media is **never** attached to a published summary. Publishing an image is a
  separate human decision, not in scope.

## What is sent to a third-party model

Only scrubbed text reaches Anthropic, and only the already-anonymized summary
reaches a translation step. The raw report never leaves the system.

The deterministic scrub running *before* the first model call is what makes that
statement true, which is why it lives in `Core` with no dependencies and is
provable in a plain unit test.

## Logging

- Never log request bodies on the report endpoints.
- Never log credentials, at any level.
- Log report **identifiers**, not report content. An identifier is a `TinyId`:
  eleven characters carrying no timestamp and no sequence number, so logging one
  does not quietly disclose when a report arrived or how many there are. See
  [ADR-0034](decisions/ADR-0034-tiny-ids.md).
- Notification emails carry a link, never the report — an inbox is outside this
  system's access controls.

## Access and audit

Admin access is an allowlist in `admin_users`. The initial migration seeds
**one obviously-fake local administrator**, `admin@localhost`, and only into a
database that has opted in with the PostgreSQL setting
`hpac.seed_development_admin`. Unset means no, which is what production is. The
real safety-officer allowlist is a later issue and will never be a migration —
see [ADR-0020](decisions/ADR-0020-seeding-by-migration.md). Every moderation action — view of
a raw report, edit, approval, rejection — is written to `audit_log` with who and
when. In a non-punitive reporting system, being able to show who saw what is
part of keeping the promise.

## Residency

Reports are filed by Canadians about incidents mostly in Canada. Hosting is
**AWS `ca-central-1`** — database, uploads bucket, and mail all in region. See
[ADR-0009](decisions/ADR-0009-hosting-on-aws.md).

**Do not relocate any of them to a US region** for cost or latency without
revisiting this document. Region choice is a data-protection decision here, not
an infrastructure preference.

## Related

- `docs/anonymization-policy.md`
- `docs/authentication.md`
