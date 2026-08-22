# ADR-0034: Every row is identified by an eleven-character tiny id

**Status:** Accepted
**Date:** 2026-08-22

## Context

Every table in this system needs a primary key, and the choice is usually made
once and lived with forever. The obvious candidates — a sequential integer, a
UUIDv4, a UUIDv7 — differ in what they say about the row they identify, and in
this domain that matters more than it usually does.

A report identifier is not an internal detail. It appears in the admin URL a
safety officer opens, in the blob key a photo is stored under (#16 namespaces
uploads by report id), in the notification email link, and in every log line —
because `docs/data-handling.md` says to log report **identifiers** and never
report content. Whatever an identifier leaks, it leaks in all of those places.

And this system is deliberately careful about time. A published summary carries
a **month and a year**, never a date, so an occurrence cannot be tied back to a
moment and from there to a person who was flying that afternoon.

## Decision

**One identifier type for every table: `TinyId`, eleven characters over
`A-Za-z0-9-_`.** Sixty-four symbols, six bits each, sixty-six bits in total.
Case-sensitive. The alphabet is URL-safe base64's, which is the one YouTube uses
for a video id.

- `TinyId` is a readonly record struct in `HpacSafety.Core.SharedKernel`.
  `RandomNumberGenerator` is in the BCL, so `Core` keeps its zero package
  references. `#16` uses the same type for the report segment of a blob key
  rather than re-deriving the rule.
- **Malformed is unrepresentable.** The constructor is private; `Parse` and
  `TryParse` are the only ways in from text, and both reject anything that is
  not exactly eleven characters of the alphabet.
- **Minted from a cryptographically secure source.** `RandomNumberGenerator`,
  masked to six bits per symbol — sixty-four divides two hundred and fifty-six,
  so the masking stays uniform and no symbol is more likely than another.
- **Stored as `char(11)`**, not `uuid`. One column type everywhere, so there are
  no mixed-type joins and a row is readable in `psql` without decoding anything.
- **A collision is handled, not assumed away.** The primary key is unique, so a
  collision is a rejected write rather than an overwritten report; `SaveChanges`
  catches it, mints a fresh identifier, and retries — three times, because three
  consecutive collisions at sixty-six bits is a broken random source rather than
  bad luck. When the caller has opened a transaction the retry rolls back to a
  savepoint, so ADR-0002's report-plus-outbox guarantee survives.
- **Seeded rows derive their identifier deterministically.** `SeedIds` hashes a
  fixed namespace and a name with SHA-256 and encodes the leading bytes into the
  same alphabet. A hash is already unpredictable, so the result is an ordinary
  tiny id, indistinguishable from a minted one — and the seed stays idempotent,
  which is what stops a re-run duplicating the question bank. See ADR-0020.

### Why this fits *this* system

This is not a preference about how identifiers look.

- **A tiny id encodes no time.** A UUIDv7 or a sequential key can be read
  backwards into "this row was created at 14:32 on the fourteenth". This system
  spends real effort narrowing a published occurrence date to a month and a
  year; an identifier that carries a timestamp hands that back on the next line.
- **It is not enumerable.** A report id in a URL or a blob key cannot be
  decremented to find the previous report. A sequential key can, and it also
  announces how many reports HPAC has ever received.
- **It survives being seen.** Identifiers here appear in URLs, blob keys, email
  links, and logs. Sixty-six bits of unguessable identifier is not an access
  control — human review and the admin allowlist are — but it means a leaked
  link is one leaked report rather than a way in to the rest.

### The trade-off, honestly

Random identifiers have **worse B-tree insert locality** than sequential or
time-ordered keys. Every insert lands at a random point in the index rather than
at the right-hand edge, so the working set of index pages is larger and page
splits are more frequent. At scale that is a real cost, and it is the reason
UUIDv7 exists.

At HPAC's volume it does not matter. This is a national association that
receives **dozens of occurrence reports a year**. The largest table in the
system is `report_answers`, at roughly thirty rows per report; the question bank
is a few hundred rows and changes when a safety officer edits the form. The
entire database fits in memory several times over, and it will still fit in
memory when it is a hundred times larger. Trading index locality nobody will
measure for a property the domain actually needs is the right way round — but it
is a trade, and this paragraph exists so that nobody has to rediscover it.

## Consequences

- One convention for every table. Nothing for a developer to remember, and no
  join across two different key types.
- `Core` gained `TinyId` and every entity's `Id` and foreign key changed type.
  No data had to be migrated: the initial migration had not been applied
  anywhere.
- **A retried identifier has to be chased down.** EF fixes up real
  relationships, but `outbox_messages.aggregate_id` and `audit_log.target_id`
  name a row by value with no foreign key, on purpose, because they point at
  more than one kind of thing. The retry rewrites those explicitly, and a test
  pins it: an outbox message written alongside a retried report names the
  identifier the report ended up with.
- An identifier is eleven bytes of text rather than sixteen bytes of binary.
  Slightly larger on disk, considerably more readable in a log.
- Sixty-six bits is not a secret. It is unguessable, which is not the same
  thing, and nothing may be authorised by possession of an identifier.

## Alternatives rejected

**Sequential integers (`bigint identity`).** The best insert locality, the
smallest key, and the easiest to read. Rejected on two counts, both of which
matter here: `/admin/reports/41` is enumerable, so anyone with one link can walk
the others, and the number itself announces how many reports HPAC has received
and in what order — volume and ordering this system has no reason to publish.

**UUIDv4.** Unguessable, standard, and supported by every tool. Rejected for
shape rather than substance: thirty-six characters is unpleasant in a URL and
worse in a blob key like `<blob>/9f1c8e2a-…/photo.jpg`, and it is long enough
that people start truncating it in logs, which quietly reintroduces collisions.
Its randomness has exactly the same insert-locality cost as the option chosen,
so nothing is gained by it either.

**UUIDv7.** Fixes the insert locality, keeps the standard, and is the modern
default. **Rejected because it leaks creation time by design** — that is its
entire selling point. In a system that publishes a month and a year precisely so
a report cannot be pinned to a moment, an identifier that carries a millisecond
timestamp into every URL, blob key, and log line undoes that work. The property
being paid for is one this domain specifically does not want.

**A hash of the row's content.** Deterministic and deduplicating. Rejected:
report content is Restricted, and an identifier derived from it is a fingerprint
of it — two identical reports would announce that they are identical, and an
identifier would change if a reviewer corrected a typo.

**Shorter than eleven characters.** Eight characters is forty-eight bits, which
is fine at this volume and stops being fine quietly rather than loudly.
Eleven costs three characters and removes the question.

## Related

- [ADR-0002](ADR-0002-transactional-outbox.md), [ADR-0019](ADR-0019-application-side-field-encryption.md), [ADR-0020](ADR-0020-seeding-by-migration.md)
- `docs/data-handling.md`, `src/HpacSafety.Infrastructure/Persistence/README.md`
- Issues #7, #16
