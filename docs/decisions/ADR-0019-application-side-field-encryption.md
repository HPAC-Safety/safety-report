# ADR-0019: Report values are encrypted by the application, not by the database

**Status:** Superseded by
[`/../data-and-persistence.md`](../data-and-persistence.md). The target
uses managed encryption at rest and TLS, not application AES or field ciphers.
**Date:** 2026-08-22

## Context

Reports contain names, contact details, member identifiers, precise timing, and
narrative accounts of real accidents. These values require application-side
encryption at rest regardless of whether a question is private model context or
non-private report content under the later ADR-0038.

"Encrypted at rest" has several possible meanings, and they protect against
different things:

- **Volume encryption** (EBS, RDS storage encryption) protects a stolen disk. It
  does not protect a `pg_dump`, a replica, a support engineer with a `psql`
  session, or a connection string that leaked.
- **`pgcrypto` in the database** protects the dump, but the key travels in SQL
  text, lands in `pg_stat_statements` and in query logs, and is known to the
  database that holds the ciphertext.
- **Application-side encryption** means the database never sees plaintext or key.
  A dump is inert without a key held somewhere else entirely.

The question set is data (ADR-0016), so contact details are not columns on
`reports` — they are rows in `report_answers`, one per question answered. That
shapes what "encrypt the contact fields" can mean.

## Decision

**AES-256-GCM, applied in the application, behind a port declared in `Core`.**

- `HpacSafety.Core.SharedKernel.IFieldCipher` declares `Encrypt`, `Decrypt`, and
  a non-secret `KeyId`. `Core` still depends on nothing.
- `HpacSafety.Infrastructure.Persistence.Encryption.AesGcmFieldCipher`
  implements it. The stored form is `v1.` followed by base64 of
  `nonce ‖ tag ‖ ciphertext`, with a fresh 96-bit nonce per value.
- An EF Core `ValueConverter` binds the cipher to the column, so nothing in the
  application has to remember to call it.
- The key is a base64 256-bit value read from configuration at
  `HpacSafety:FieldEncryption:Key` — a throwaway literal in
  `appsettings.Development.json`, a Secrets Manager reference in production.
- `FieldCipherModelCacheKeyFactory` puts the key identifier into EF's model
  cache key, so two contexts opened with different keys cannot share a model.

Five decisions inside that one:

### 1. The whole `report_answers.value` column is encrypted

A `ValueConverter` runs per value, not per row. A column is therefore encrypted
for every row or for none. Encrypting the whole column is deliberate: question
privacy controls the summarization section, not storage protection.
Option codes live in a separate `selected_option_codes` array and stay readable,
because a code is a controlled vocabulary and not free text.

The cost is accepted and real: an encrypted column cannot be searched, sorted,
or indexed by value in the database. Nothing queries answer text today — the
answers logic reads are projected onto typed columns on `reports` (ADR-0016) —
and a future need for search is a search index over already-decrypted text, not
a reason to store contact details in the clear.

### 2. GCM, so a wrong key fails loudly

An authenticated mode means a value written under a different key, or altered in
the database, throws `FieldDecryptionException` rather than decrypting into
something plausible. A silently mangled contact detail is a wrong phone number
attached to a real crash.

The exception message never carries the ciphertext, the plaintext, or the key.

### 3. `admin_users.member_identifier` stays in plaintext

It is the lookup key at sign-in, and a randomized-nonce ciphertext cannot be
looked up. It is an administrator's own working identity rather than a
reporter's, and it never reaches a published summary. Encrypting it would buy
very little and would break the one query it exists for.

### 4. Domain values are stored as invariant codes, and a date is a date

Every domain enum goes to the database as its `EnumCode` — `high_en_b`, not `3`
— and `Locale` as `en-CA`. A row is then readable without the enum beside it,
and reordering an enum cannot silently reinterpret history. A stored code that
no longer names a domain value throws rather than defaulting to zero.

The same reasoning covers dates. When the system did something is a moment and
is a `DateTimeOffset` in `timestamptz`. `DateTime` is not used anywhere. See
ADR-0035.

### 5. The occurrence is a local date and a local time, not an instant

The reporter submits an actual date and time and the coarse time-of-day bucket
is **derived** from it (#68). Both halves are stored as local wall clock:
`DateOnly` in `date`, `TimeOnly` in the encrypted column below. Not
`DateTimeOffset`.

This is not a contradiction of ADR-0035, it is an application of it: that rule
says use the type that says what is actually known, and what is known here is a
date and a clock reading at a site.

- **"Morning" is what the clock on the wall said.** The bucket has to come from
  the local reading, not from an instant that then needs converting back.
- **An offset would have to be invented.** This system collects a *province*,
  not site coordinates, and provinces span time zones — British Columbia alone
  spans two. Any offset stored would be inferred, and an inferred offset that is
  wrong moves the derived bucket. Storing a local time stores what the reporter
  actually knew; storing an instant would store a guess dressed as precision.
- **The cost, stated plainly:** this is not a globally-orderable instant. Two
  reports filed in different provinces cannot be strictly sequenced by when they
  happened. This system never needs that — it groups by month, by province, and
  by bucket, and `submitted_at` is a real instant when ordering by *arrival* is
  what is wanted.

The boundaries — morning before 11:00, mid-day 11:00 to 14:00, afternoon 14:00
to 17:00, evening from 17:00 — live in exactly one place, `TimeOfDayBuckets`, as
`TimeOfDay.FromLocalTime(TimeOnly)` in `Core`. The projection onto `Report` and
summarization input preparation both call it, so "when does the afternoon start"
has one answer in this system rather than one per caller. The vocabulary is the
existing `TimeOfDay`, unchanged, so answers carried over from Typeform — where
the reporter picked the bucket directly — sit on the same scale as derived ones.

**The time is optional, and absent is a defined state.** #68 lets a reporter who
does not remember file anyway. A missing time is `TimeOfDay.Unknown` — "do not
know" — which is distinct from `NotAnswered` (no time question on the form at
all) and is never a null that logic reads as midnight. Midnight is a real answer
a reporter can give. This is the same shape as the `QuestionRole` rule in
ADR-0016: a missing role is a defined state, never a zero.

### 6. The precise time is private, so it is encrypted

`docs/anonymization-policy.md` narrows a published date to a month and a year
because province, date, aircraft type, and injury severity together identify one
person in a small flying community. The precise time is another term in that
same aggregation, and a sharper one — "the EN-B accident in Alberta on a Tuesday
at 15:20" is one flight.

So `reports.occurred_at_local` is encrypted with the same cipher as the contact
fields, and the coarse `time_of_day` bucket sits beside it in the clear. The
bucket is what a summary publishes and what analysis groups by; the precise time
is for a reviewer looking at the raw report, and for nothing else.

Storing it as encrypted text rather than as a `time` column costs the ability to
range-query the precise time in SQL. Nothing does. The alternative — a private
value sitting in the clear because the column type was prettier — is the thing
`AGENTS.md` now forbids outright.

### 7. `Core` grows a persistence constructor, and nothing else

EF Core materializes an entity by calling a constructor and then setting mapped
properties. The aggregates here have no constructor EF can bind, so each gained
a private parameterless one, marked as existing for the ORM. Domain code still
has to go through the real constructor or factory, so no caller can reach a
half-built aggregate, and `Core` still references nothing.

## Consequences

- A stolen database dump is inert. That is the property being bought.
- Losing the key loses the contact details and the narrative, permanently. Key
  custody is now an operational responsibility, and key rotation is unbuilt
  work — the `v1.` prefix and `KeyId` exist so that it can be built without
  guessing what wrote a given row.
- Answer text cannot be queried in SQL. See above.
- Two contexts with different keys are two models. Without the cache key factory
  the second would silently read through the first one's key, which is exactly
  what the "unreadable without the key" integration test catches.
- Scaffolded migrations are exempt from `CA1062`, `CA1861`, and `IDE0161` in
  `.editorconfig`. `dotnet ef` writes those files and has no option to write
  them differently; hand-editing them would be undone by the next scaffold, and
  `coverlet.runsettings` already treats them as generated code. The seed data
  they call into is ordinary code under `Persistence/Seeding` and is analysed
  and measured like everything else.

## Alternatives rejected

**Volume or RDS storage encryption only.** Free, and it protects a stolen disk.
Rejected as insufficient on its own: it leaves plaintext in every dump, replica,
and `psql` session, which is where this data is actually most likely to leak.
It remains in place underneath this decision — the two are not exclusive.

**`pgcrypto` (`pgp_sym_encrypt`) in SQL.** Keeps the application simple and
keeps decryption where the data is. Rejected because the key travels in the
query text: into `pg_stat_statements`, into slow-query logs, into any error
message that echoes SQL. Handing the key to the machine holding the ciphertext
also removes most of the benefit.

**A separate `report_contacts` table with typed encrypted columns.** Would let
non-contact answers stay searchable. Rejected because it re-introduces the fixed
schema ADR-0016 exists to avoid: the form is data, and a typed table would need
a migration whenever a newly created question collected another kind of private
value.

**Deterministic encryption, so values stay comparable.** Rejected: equal
ciphertexts would show, in a dump, which reports share a phone number — which is
precisely the linkage this is meant to prevent.

**AES-CBC with a separate HMAC.** Works, and is more code for the same
guarantee. GCM is one primitive that authenticates as it encrypts.

**Storing the occurrence as a `DateTimeOffset`.** One column, globally
orderable, and the type ADR-0035 reaches for when time matters. Rejected because
the offset would be fabricated: the form asks for a province, and a province is
not a time zone. A wrong offset does not fail loudly — it quietly moves an
accident into the wrong bucket, and near midnight into the wrong day and month.

**Storing the occurrence time as a plain `time` column.** Readable in `psql`,
range-queryable, and the obvious mapping for `TimeOnly`. Rejected because the
precise time is private and is not stored in the clear for convenience. Nothing
range-queries it; the bucket beside it answers every
question anything actually asks.

**Keeping "morning" as a question the reporter answers.** It is what Typeform
did, and it needs no derivation. Rejected by #68: a reporter reaching for a
bucket has already rounded, the rounding is inconsistent between people, and the
precise time is worth keeping for a reviewer even though it is never published.
Deriving the bucket also means the boundaries can be corrected once, in one
place, without reinterpreting old answers — they are on the same scale either
way.

**A key derived from a passphrase in configuration.** Rejected: it hides the
question of key custody behind something that looks like a password, and a weak
passphrase silently weakens a 256-bit key.

## Related

- `docs/data-handling.md`, `docs/architecture.md`
- [ADR-0016](ADR-0016-data-driven-question-bank.md), [ADR-0020](ADR-0020-seeding-by-migration.md)
- `src/HpacSafety.Infrastructure/Persistence/README.md`
- Issues #7, #18, #68
