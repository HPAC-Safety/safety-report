---
status: accepted
date: 2026-09-20
decision-makers: Chase Florell
keywords: translation, DeepL, question bank, authoring, ITranslator, API key
---

# ADR-0062 — An administrator may machine-translate question text while authoring; the database still stores only what they saved

## Context

Every question is stored as one complete bilingual revision, and an
administrator has had to type both languages by hand. That rule was written
down three times and meant literally:

- AGENTS.md product invariant #1;
- [ADR-0016](ADR-0016-data-driven-question-bank.md) — *"Administrators provide
  both language versions; no translation service writes question content"*;
- `skills/localize-hpac-app` — *"no runtime or authoring-time translation
  service fills question text."*

The reasoning behind it is sound and is not in dispute. A question is what a
reporter is actually asked. A mistranslated question does not merely read
badly — it asks a different question of the French-speaking half of the
membership, and every answer given to it is an answer to that different
question. "Were you flying solo?" and a translation that drifts to "Were you
flying alone [unaccompanied]?" collect different data under one key.

What the rule got wrong is *who* it was protecting against. It treats machine
translation as a pipeline that writes into the database. The thing a safety
officer actually wants is a draft in the empty box, which they then read and
correct — the same way they would use a dictionary. Forbidding that does not
improve the French; it means a volunteer administrator who reads French
imperfectly types something worse than DeepL would have suggested, or leaves
the question unwritten.

Meanwhile the repository already translates its UI catalogue with DeepL in CI
(ADR-0021, ADR-0022), with a credential, a glossary, and placeholder
protection already worked out.

## Decision

**An administrator may press Translate while authoring a question. Nothing
else changes about how a question is stored.**

### What survives from the old rule

- **The database still only ever holds what an administrator saved.** There is
  no translation step between the authoring screen and the question bank, no
  background job, and no queue. Translate fills a form field, exactly as
  typing does.
- **Save is disabled until both languages are present.** The control cannot be
  used to store a half-written question, and a machine translation that nobody
  looked at is still a save somebody performed.
- **No reporter content is ever translated.** This is for question wording
  only — never a narrative, never an answer, never a summary. A translated
  account of a crash is a paraphrased account of a crash, and that remains
  forbidden.

### What changes

Product invariant #1, ADR-0016, and `skills/localize-hpac-app` are reworded:
an administrator **authors** both languages, and may use machine translation
as a drafting aid while doing so.

### Translation happens on the server, behind a port

`ITranslator` lives in `HpacSafety.Core` with one implementation,
`DeepLTranslator`, in `HpacSafety.Infrastructure`. Nothing above the adapter
names DeepL, because the provider is expected to change
([ADR-0033](ADR-0033-third-party-libraries-behind-owned-abstractions.md)).

The browser calls `POST /api/admin/translate`, admin-gated like the rest of
`/api/admin`, and **never calls a translation provider directly**. That is not
a style preference: a credential shipped to a page is a credential published.
The endpoint returns translations positionally and nothing else.

`DeepLTranslator` deliberately mirrors `tools/translator.mjs` — the same
provider, the same `{placeholder}` protection through `tag_handling`/
`ignore_tags`, the same `preserve_formatting`, and the same `prefer_more`
formality default. Two translators that ask differently would produce UI
chrome and question text in noticeably different French.

One thing the CI translator never had to solve: **DeepL's `FR-CA` is a
target-only variant.** English is always the source in CI, but here an
administrator may write the French first and translate back, so French is
requested as plain `FR` when it is the source and `FR-CA` when it is the
target.

### The credential

The same `DEEPL_API_KEY` secret the translation workflow already uses, passed
by the deploy workflow into the running task as `Translation__ApiKey`. The
adapter also accepts a bare `DEEPL_API_KEY` environment variable so a
developer who already exports it for the CLI tooling gets a working button
without learning a second name.

**When no credential is configured, nothing fails** — but what happens next
depends on the environment.

*In Development*, the container resolves `EchoTranslator`, a stand-in that
returns every string unchanged. The browser posts to the same endpoint, the
endpoint calls the same `ITranslator`, and the same code fills the same field;
only the adapter differs. Disabling the control locally instead would mean the
one path most likely to break is the one nobody exercises until production.
`GET /api/admin/translate` reports `standIn: true` and the screen says plainly
that the text was copied across unchanged, so a developer seeing their English
in the French box knows why.

*Outside Development*, there is no stand-in. An unconfigured server reports
`available: false` and the screen disables the control with an explanation.
Copying English into the French column of a live question bank would put
untranslated English in front of French-speaking pilots, which is worse than
having no button. The opt-in parameter defaults to false for exactly this
reason, and the only caller passes `builder.Environment.IsDevelopment()`.

The key never reaches the browser, never enters a log, and never appears in a
problem response. A provider error is reported as its status code only —
DeepL's error bodies can echo the submitted text, and the submitted text is
question wording being drafted.

## Consequences

- An administrator can write one language and get a usable draft of the other.
  The French in the database is still whatever a human approved.
- The API gains an outbound network dependency on a third-party service, on an
  admin-only path. It is optional by construction: no key, no button.
- `HpacSafety.Infrastructure` gains `Microsoft.Extensions.Http`.
- A developer sees Translate work locally without a credential, and sees a note
  saying what it really did.
- Machine-translated text is **not marked** in the database. This was
  considered and rejected below.
- The glossary in `locales/glossary.json` does **not** apply here. It is a map
  of pinned French for specific *catalogue keys* ("app.title" → "ACVL
  Sécurité"), not a term glossary, and a question is not a catalogue key.
  Pinning terminology for free text would need DeepL's glossary API and a
  term list nobody has written yet; it is a separate piece of work, not
  something to fake.

## Alternatives rejected

**Keep the rule; no translation at authoring time.** The strongest version of
the "a question is what a reporter is asked" argument. Rejected by the owner:
it does not produce better French, it produces less French, and the human
review it was protecting is still there — the administrator reads and saves.

**Record that a field was machine-translated, and show it to a reviewer.**
Genuinely tempting, and it was considered. Rejected because the provenance
would be a lie almost immediately: the field is editable, so a value that was
machine-translated and then corrected word by word is indistinguishable from
one that was not, and a flag that is wrong half the time is worse than no flag.
The accountability that matters is already unambiguous — an administrator
pressed Save.

**Translate in the browser.** Simpler, no endpoint. Rejected outright: it
requires shipping the credential to a page, which publishes it.

**Disable the control in development rather than standing in for it.** The
first version of this change did exactly that. Rejected on the owner's ruling,
and it is the better call: a control nobody can press locally is a control
nobody tests, and the interesting failures — a field filled in the wrong place,
an option label landing on a label — are in the code above the adapter, which
the stand-in exercises fully.

**Let the stand-in apply everywhere a credential is missing.** One rule, no
environment check. Rejected: a production deployment that lost its key would
silently start writing English into French, and an administrator pressing
Translate would have no way to tell. Failing visibly is the right behaviour
there.

**Reuse `tools/translator.mjs` by shelling out to Node from the API.** No
second implementation of the request shape. Rejected — it makes the API depend
on a Node runtime inside its container and on a CLI's argument format, to save
about eighty lines of C# that the type system otherwise checks.

**Store the key in AWS Secrets Manager and inject it as a task secret.** The
better long-term home, and where the other runtime secrets live. Rejected for
now by the owner in favour of passing the existing GitHub secret through the
deploy workflow; moving it is a self-contained later change.

## Related

- [ADR-0021](ADR-0021-ci-translation-opens-a-pull-request.md) — CI translation of the UI catalogue
- [ADR-0022](ADR-0022-translation-provider-is-configuration.md) — the provider choice this reuses
- [ADR-0016](ADR-0016-data-driven-question-bank.md) — updated by this ADR
- [ADR-0033](ADR-0033-third-party-libraries-behind-owned-abstractions.md) — why `ITranslator` exists
- [`/features/question-bank-and-form/question-bank-and-form.feature`](../../features/question-bank-and-form/question-bank-and-form.feature)
