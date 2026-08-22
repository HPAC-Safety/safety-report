# ADR-0019: Restricted fields are encrypted by the application, not by the database

**Status:** Accepted
**Date:** 2026-08-22

## Context

`docs/data-handling.md` puts reporter and pilot names, phone numbers, email
addresses, HPAC member numbers, and the raw narrative in the **Restricted**
tier: encrypted at rest, admin-only, never logged, never sent to a translation
service.

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
for every row or for none, and "encrypt only the rows whose question is
Restricted" is not a thing EF can express.

Encrypting the whole column is also the answer `docs/data-handling.md` already
gives: the narrative is Restricted too, a question added through the admin UI is
Restricted until someone decides otherwise, and when in doubt it is Restricted.
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

### 4. Domain values are stored as invariant codes

Every domain enum goes to the database as its `EnumCode` — `high_en_b`, not `3`
— and `Locale` as `en-CA`. A row is then readable without the enum beside it,
and reordering an enum cannot silently reinterpret history. A stored code that
no longer names a domain value throws rather than defaulting to zero.

### 5. `Core` grows a persistence constructor, and nothing else

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
schema ADR-0016 exists to avoid: the form is data, "which fields are contact
fields" is an administrator's decision, and a typed table would have to be
migrated every time they change their mind.

**Deterministic encryption, so values stay comparable.** Rejected: equal
ciphertexts would show, in a dump, which reports share a phone number — which is
precisely the linkage this is meant to prevent.

**AES-CBC with a separate HMAC.** Works, and is more code for the same
guarantee. GCM is one primitive that authenticates as it encrypts.

**A key derived from a passphrase in configuration.** Rejected: it hides the
question of key custody behind something that looks like a password, and a weak
passphrase silently weakens a 256-bit key.

## Related

- `docs/data-handling.md`, `docs/architecture.md`
- [ADR-0016](ADR-0016-data-driven-question-bank.md), [ADR-0020](ADR-0020-seeding-by-migration.md)
- `src/HpacSafety.Infrastructure/Persistence/README.md`
- Issue #7
