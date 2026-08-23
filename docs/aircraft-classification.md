# Aircraft classification

A published summary describes **"a high EN-B glider"**, never "an Ozone Rush 6".

Make and model are collected and kept privately. HPAC needs them — a pattern
across one model is exactly what a reporting system should surface — but the
wing a pilot flies identifies them within a local community, so it is never
published.

## The class comes from the reporter

The form asks for the aircraft's certification and **that answer is the only
source**. There is no model-to-class lookup table in this system, and nothing
derives a class from a model name.

This is deliberate:

- The pilot knows what they were flying. A table would be a second-hand guess at
  something the reporter states first-hand.
- Hundreds of wings, new certifications every season, per-size differences — a
  table is a permanent maintenance burden that is always slightly out of date.
- A stale row publishes a confident, wrong, permanent fact about a real accident.

**`HpacSafety.Core` stores the reporter's certification answer verbatim and
does nothing else with it.** `ReportAircraft.CertificationAnswer` is exactly
what the reporter typed — no normalization, no lookup, no classification —
the same as every other answer on the form. Determining the published class is
work the summarizer does at summarization time, from that raw text, under an
explicit prompt instruction: state a class only when the answer names one in
the vocabulary below, and write "an aircraft" rather than guess when it does
not. See [ADR-0036](decisions/ADR-0036-classification-moves-to-the-summarization-prompt.md).

## Vocabulary

**Paragliders:** `EN-A`, `low EN-B`, `high EN-B`, `EN-B`, `EN-C`, `EN-D`,
`CCC`, `uncertified`.

The low/high B split carries most of the safety signal. "EN-B" alone spans
nearly the entire recreational market and says almost nothing — which is why the
form should ask for the band, not why a reporter who gave a bare `EN-B` should
have their answer thrown away. **Plain `EN-B` is a class of its own.** It is
published as given and is never widened into `low EN-B` or `high EN-B`, and
neither band is ever narrowed to it.

**Hang gliders** are not EN-rated: `single-surface`,
`double-surface kingposted`, `topless`, `rigid`, `uncertified`. Uncertified hang
gliders exist; the term is shared with the paraglider vocabulary and is the only
one that is.

**Mini wings and speedwings:** `mini wing` / `speedwing`, plus the EN class if
the wing carries one.

**Tandems** carry the marker with the class: `tandem paraglider`,
`tandem hang glider`.

## Normalizing

Today's Typeform collects certification as free text, so real answers vary:
`"EN B"`, `"low B"`, `"LTF 1-2"`, `"B (high)"`, `"topless"`, `"n/a"`.
`ReportAircraft.CertificationAnswer` in `HpacSafety.Core` stores that text
exactly as given — no normalization happens in `Core`. The summarizer reads
the raw text at summarization time and states a class only when the answer
names one in the vocabulary below — never a guess. See
[ADR-0036](decisions/ADR-0036-classification-moves-to-the-summarization-prompt.md).

The table below is what `prompts/summarize.v1.md` and
`prompts/redaction-rules.v1.md` instruct the model to do with case, punctuation,
and spacing variation: `"EN-B (low)"`, `"en_b, low"` and `"  LOW   B "` are the
same answer.

| The reporter wrote | It normalizes to |
|---|---|
| `EN A`, `en-a`, `EN 926 A` | `EN-A` |
| `low B`, `EN B (low)`, `low EN-B` | `low EN-B` |
| `B (high)`, `high EN B` | `high EN-B` |
| `EN C`, `en-d`, `CCC` | `EN-C`, `EN-D`, `CCC` |
| `uncertified`, `not certified`, `prototype` | `uncertified` |
| `topless`, `rigid`, `single surface`, `kingpost` | the hang glider class |
| `EN B`, `en-b`, `B`, `low or high B, not sure` | `EN-B` |
| `uncertified` (hang glider or paraglider) | `uncertified` |
| `tandem, high EN-B` | `high EN-B` **and** the tandem marker |
| `tandem` (aircraft type: paraglider) | `tandem paraglider` |
| `mini wing, EN A` | `EN-A` **and** the mini wing marker |
| `LTF 1-2`, `n/a`, `Ozone Rush 6` | `class not determined` |

### What it refuses, on purpose

- **LTF and DHV answers.** A different certification scheme. How its bands map
  onto EN bands is HPAC's judgement to make, not the model's, and
  `"LTF 1-2"` sits inside the B band without saying where. Ruled on: it stays
  undetermined ("an aircraft"), and a reviewer states the class by hand in the
  summary, on the record.
- **An EN class on a hang glider.** Hang gliders are not EN-rated. The two
  vocabularies are scoped by the aircraft type the reporter chose, so the
  paraglider one cannot leak across.
- **A make or model.** `"Ozone Rush 6"` in the certification field normalizes to
  nothing, because there is no table to look it up in — and the redaction rules
  separately forbid the model from ever naming a manufacturer or model in
  published output, whether or not it appears in the certification answer.
- **A stray letter in prose that never named a certification.** `"it's a really
  nice wing"`, `"c'est un bon jour"` — a bare `a`/`b`/`c`/`d` only counts as an
  EN letter when it is the whole answer or sits next to a certification word
  (`EN`, `high`, `low`); an article, a pronoun, or half of a contraction never
  does.

What it does **not** refuse is a bare `EN-B`. Refusing a true answer is its own
kind of error: the reporter answered, and `EN-B` is in the vocabulary. It is
kept as given and never widened into a band.

### Markers travel with the class

A tandem is still a high EN-B, and a mini wing may hold an EN class of its own.
The summary states both — "a tandem, high EN-B glider" — rather than the marker
standing in for the class or the class crowding the marker out. `tandem
paraglider`, `tandem hang glider`, `mini wing`, and `speedwing` are stated on
their own only when no certification class could be determined alongside them.

**Fix it at the source:** the new form should ask for certification as a
selection scoped to the aircraft type already chosen, with a free-text escape
hatch. That turns parsing into validation and every future report arrives clean.
Free-text interpretation by the summarizer still has to happen for the
historical shape of the question.

## Related

- `prompts/summarize.v1.md`, `prompts/redaction-rules.v1.md` — where this is enforced at runtime
- [ADR-0036](decisions/ADR-0036-classification-moves-to-the-summarization-prompt.md)
- `skills/aircraft-classification/SKILL.md`
- `docs/anonymization-policy.md`
- `docs/form-spec.md`
