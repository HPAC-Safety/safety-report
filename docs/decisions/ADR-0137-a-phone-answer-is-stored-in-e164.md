---
title: A phone answer is stored in E.164
description: A phone answer is validated for its country with libphonenumber in the browser and the API and stored in E.164; an email answer is held to one shared syntactic rule; both are refused malformed at submission, and existing answers are left as stored.
type: adr
status: accepted
date: 2026-09-26
decision-makers: Chase Florell
keywords: answers, phone, email, E.164, libphonenumber, libphonenumber-js, libphonenumber-csharp, validation, storage form, submission, ADR-0072, ADR-0130
---

# ADR-0137 — A phone answer is stored in E.164

## Status

Accepted. This ADR **extends**
[ADR-0072](ADR-0072-every-answer-is-stored-as-a-string.md)'s storage forms
with a phone and an email form, beside ADR-0072's date and time forms and
[ADR-0130](ADR-0130-a-yes-or-no-answer-is-stored-as-a-boolean.md)'s boolean.

## Context

The form rendered an email question as `type="email"` and a phone question as
`type="tel"`, and the API stored whatever string arrived. A phone number
reached the database as `604-555-1234`, `(604) 555 1234`, or `6045551234`
with no country, so a reader could not dial it reliably and two answers
holding the same number did not compare equal.

The owner decided (#513, 2026-09-26) that both types are validated in the
form and in the API, that the phone field is international with a country
picker defaulting to Canada and a per-country mask, and that the storage form
and the libraries were to be settled while building.

## Decision

**Phone storage form.** A phone answer is stored in E.164: `+`, the country
calling code, and the national number, with no spaces or punctuation
(`+16045551234`). The form builds it from the chosen country and the typed
number; the API refuses anything else. A reader's view formats it
internationally (`+1 604 555 1234`) and shows any value that does not parse
as stored.

**Phone validity.** A number must be valid for the country it belongs to, by
Google's libphonenumber metadata:

- in the browser, [`libphonenumber-js`](https://www.npmjs.com/package/libphonenumber-js),
  imported from its `max` build, which carries the full validation patterns
  rather than the `min` build's length checks. It also supplies the country
  list, the as-you-type formatter behind the mask, and the example number each
  mask is drawn from;
- in the API,
  [`libphonenumber-csharp`](https://www.nuget.org/packages/libphonenumber-csharp),
  the maintained .NET port of the same library. It is referenced by the API,
  not by `Core`, which takes no runtime package; `Core` holds only the E.164
  shape.

**Email form.** An email answer is one address: a non-empty local part and a
domain joined by one `@`, no whitespace, a domain of at least two non-empty
dot-separated labels whose last is at least two characters, and at most 254
characters. The same rule is written once in the browser and once in `Core`,
with no package.

**Refusal.** A malformed email or phone answer is refused with `400` naming the
question by its key, never the value, before anything is written (invariant
2). A blank one is a skip.

**Existing answers.** Answers are immutable. Those stored before this decision
are neither rewritten nor revalidated.

## Consequences

- The two libraries carry separate copies of Google's metadata, released on
  separate cadences. A number assigned between their releases may be accepted
  by one and refused by the other until both update; Renovate keeps each
  current.
- The browser bundle grows by the `max` metadata. It buys agreement with the
  API: the `min` build would accept numbers the API refuses.
- Numbers compare equal as strings, and a reader in either language sees the
  same formatted number, so a phone answer needs no second language
  ([ADR-0112](ADR-0112-only-answers-that-need-it-get-a-second-language.md)).

## Alternatives considered

- **Store the number as typed.** Rejected: it keeps the ambiguity this change
  exists to remove, and loses the country the reporter chose.
- **Store the national number and a country code in two columns.** Rejected:
  E.164 already names the country's calling code, and a second column would
  be the only answer type stored in two parts.
- **Validate the phone shape only, with a regular expression, on the server.**
  Rejected: the form validates against the chosen country's rules, and a
  server with looser rules would accept what the form refuses from any other
  client.
- **A third-party phone-input or email-suggestion widget.** Rejected: the
  picker is a native `select` and the suggestions a small combobox, styled
  with the design tokens and worded from the locale catalogues.
