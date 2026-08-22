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

An AI never infers the class either. If the answer cannot be normalized,
the result is `class not determined`, which a reviewer may correct by hand.

## Vocabulary

**Paragliders:** `EN-A`, `low EN-B`, `high EN-B`, `EN-C`, `EN-D`, `CCC`,
`uncertified`.

The low/high B split carries most of the safety signal. "EN-B" alone spans
nearly the entire recreational market and says almost nothing.

**Hang gliders** are not EN-rated: `single-surface`,
`double-surface kingposted`, `topless`, `rigid`.

**Mini wings and speedwings:** `mini wing` / `speedwing`, plus the EN class if
the wing carries one.

**Tandems** carry the marker with the class: `tandem paraglider`,
`tandem hang glider`.

## Normalizing

Today's Typeform collects certification as free text, so real answers vary:
`"EN B"`, `"low B"`, `"LTF 1-2"`, `"B (high)"`, `"topless"`, `"n/a"`.
`IAircraftClassifier` normalizes against the vocabulary and returns either a
class or the unknown state — never a guess. `VocabularyAircraftClassifier` in
`HpacSafety.Core` is the whole of it: deterministic, synchronous, offline, and
reading exactly two inputs — the reporter's verbatim answer and the aircraft
type they chose. See [ADR-0029](decisions/ADR-0029-classification-is-deterministic-and-refuses-to-guess.md).

Case, punctuation, and spacing are irrelevant: `"EN-B (low)"`, `"en_b, low"` and
`"  LOW   B "` are the same answer.

| The reporter wrote | It normalizes to |
|---|---|
| `EN A`, `en-a`, `EN 926 A` | `EN-A` |
| `low B`, `EN B (low)`, `low EN-B` | `low EN-B` |
| `B (high)`, `high EN B` | `high EN-B` |
| `EN C`, `en-d`, `CCC` | `EN-C`, `EN-D`, `CCC` |
| `uncertified`, `not certified`, `prototype` | `uncertified` |
| `topless`, `rigid`, `single surface`, `kingpost` | the hang glider class |
| `tandem, high EN-B` | `high EN-B` **and** the tandem marker |
| `tandem` (aircraft type: paraglider) | `tandem paraglider` |
| `mini wing, EN A` | `EN-A` **and** the mini wing marker |
| `EN B`, `LTF 1-2`, `n/a`, `Ozone Rush 6` | `class not determined` |

### What it refuses, on purpose

- **`EN B` with no band.** There is no plain EN-B in the vocabulary, and the
  band is the part that carries the signal. An answer naming *both* bands is
  refused the same way — a contradiction is not resolved by picking a side.
- **LTF and DHV answers.** A different certification scheme. How its bands map
  onto EN bands is HPAC's judgement to make, not the classifier's, and
  `"LTF 1-2"` sits inside the B band without saying where. **Open question with
  HPAC**; if a mapping is agreed it lands here and in the vocabulary tests.
- **An EN class on a hang glider.** Hang gliders are not EN-rated. The two
  vocabularies are scoped by the aircraft type the reporter chose, so the
  paraglider one cannot leak across.
- **A make or model.** `"Ozone Rush 6"` in the certification field normalizes to
  nothing, because there is no table to look it up in.

### Markers travel with the class

A tandem is still a high EN-B, and a mini wing may hold an EN class of its own,
so the result is a class *plus* markers — `AircraftClassification`, rendered as
invariant codes like `["tandem", "high_en_b"]`. The `tandem paraglider`,
`tandem hang glider`, `mini wing` and `speedwing` members of `AircraftClass`
stand in as the class only when no certification class was determined. See
[ADR-0030](decisions/ADR-0030-classification-carries-markers-with-the-class.md).

**Fix it at the source:** the new form should ask for certification as a
selection scoped to the aircraft type already chosen, with a free-text escape
hatch. That turns parsing into validation and every future report arrives clean.
Free-text normalization still has to exist for the historical shape of the
question.

## Related

- `skills/aircraft-classification/SKILL.md`
- `docs/anonymization-policy.md`
- `docs/form-spec.md`
