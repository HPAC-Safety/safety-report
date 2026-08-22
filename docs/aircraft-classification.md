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
class or the unknown state — never a guess.

**Fix it at the source:** the new form should ask for certification as a
selection scoped to the aircraft type already chosen, with a free-text escape
hatch. That turns parsing into validation and every future report arrives clean.
Free-text normalization still has to exist for the historical shape of the
question.

## Related

- `skills/aircraft-classification/SKILL.md`
- `docs/anonymization-policy.md`
- `docs/form-spec.md`
