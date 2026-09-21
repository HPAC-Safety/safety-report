---
status: accepted
date: 2026-09-21
decision-makers: Chase Florell
keywords: anonymity, reporter, authentication, membership, privacy, submission
---

# ADR-0067 — A reporter must be a member, and is not recorded

**Status:** Accepted.

## Context

Reporting has been open. `docs/glossary.md` said "authentication is not
required for public submission," `docs/authentication.md` said the public
surfaces "require no account," and
[ADR-0005](ADR-0005-authentication.md) opened with "Reporting is anonymous;
only the admin surface needs authentication."

Two things sat uneasily with that. An open endpoint accepting free text and
file uploads is an abuse surface that Turnstile and rate limiting could only
partly answer, and reports from outside the association were never the point —
this is HPAC's safety programme, for HPAC's members.

The obvious way to close that gap is to record who submitted what. That is
precisely what must not happen. A safety reporting system that attributes
reports gets fewer reports, and the ones it does get are less honest.

## Decision

**Submitting a report requires a signed-in HPAC member. The stored report
records nothing about them.**

The API requires a valid member token on `POST /api/v1/reports` and then
discards the identity. No subject, no member identifier, no user id, no
foreign key, no join table, no audit entry, no outbox message, and no log line
records who submitted a report. Nothing written during submission can be used,
now or later, to work out who filed it.

Authentication answers exactly one question — *is this person an HPAC member?*
— and its answer is not kept.

Any of the three roles may submit. Submission is a membership capability, not a
privileged one.

**The form says so.** A reporter sees, in their own language, that signing in
confirms their membership and that the report is not linked to their account.
An anonymity guarantee the reporter cannot see is worth nothing, because the
only thing that changes their behaviour is what they believe while typing.

## Why

The gate and the record are separable, and separating them is the whole
decision. Knowing that every submission came from a member is worth a great
deal for abuse control. Knowing *which* member is worth almost nothing
operationally, and costs the candour the programme depends on.

Because nothing is stored, the guarantee does not rest on policy or on access
control. There is no column to query, no permission to get wrong, and no
retention rule to enforce. A future request to "just look up who filed this"
has nothing to look up, which is the strongest form the promise can take.

## Alternatives

- **Stay open.** Rejected. It leaves an unauthenticated write endpoint
  accepting narrative text and file uploads, and it accepts reports from people
  the programme does not cover.
- **Store the submitter's subject "just for abuse handling."** Rejected. It
  creates exactly the record this decision exists to avoid, and the abuse it
  would address is already addressed by requiring membership at all. Once the
  column exists, every future feature request will find a reason to read it.
- **Store a one-way hash of the subject.** Rejected. A hash over a small, known
  population is reversible by enumeration. It is attribution wearing a
  disguise.
- **Authenticate but say nothing to the reporter.** Rejected. A login wall
  with no explanation reads as tracking, and reporters would reasonably assume
  the worst.

## Consequences

- Turnstile is removed from submission
  ([ADR-0068](ADR-0068-the-member-token-replaces-turnstile-on-submission.md)).
- The reporter-facing form gains a signed-out prompt and a bilingual notice.
- "My reports" is impossible by construction, now and permanently. A reporter
  cannot retrieve, amend, or withdraw a submission, because nothing links them
  to it.
- Abuse attribution after the fact is impossible by the same construction.
  That is the accepted cost.
- Every page asserting that reporting requires no account is corrected in the
  same pull request.

## Related

- [ADR-0064](ADR-0064-jwt-bearer-authentication-with-three-roles.md) — the `User` role that submission requires
- [ADR-0068](ADR-0068-the-member-token-replaces-turnstile-on-submission.md) — what this replaces Turnstile with
- [ADR-0063](ADR-0063-a-reporter-may-add-a-type-ahead-choice.md) — whose spam argument assumed an open form
- [`features/report-submission`](../../features/report-submission/report-submission.feature)
