# ADR-0030: A classification carries its markers alongside the class

**Status:** Superseded by [ADR-0036](ADR-0036-classification-moves-to-the-summarization-prompt.md)
**Date:** 2026-08-22

## Context

The published vocabulary has two kinds of term in it. Most are *classes* — the
certification or structural class of the wing: `EN-A`, `low EN-B`, `topless`,
`rigid`. Two are *qualifiers* that the policy says accompany a class rather than
replace it:

> **Tandems** carry the marker with the class: `tandem paraglider`,
> `tandem hang glider`.
>
> **Mini wings and speedwings:** `mini wing` / `speedwing`, plus the EN class if
> the wing carries one.

`AircraftClass` — written before there was a classifier — models both kinds as
members of one flat enum: `TandemParaglider`, `TandemHangGlider`, `MiniWing`,
`Speedwing` sit beside `HighEnB` and `Topless`. A single `AircraftClass` field
therefore cannot express "a tandem, high EN-B": choosing `TandemParaglider`
throws away the band, and choosing `HighEnB` throws away the tandem.

Throwing away the band is the worse of the two, and the whole reason the low/high
B split exists.

## Decision

`IAircraftClassifier` returns an `AircraftClassification` value object:

```csharp
public sealed record AircraftClassification(AircraftClass Class, AircraftMarker Markers);

[Flags]
public enum AircraftMarker { None = 0, Tandem = 1, MiniWing = 2, Speedwing = 4 }
```

- **Markers are read from the reporter's own answer and from the aircraft type
  they chose.** Both are the reporter's words. Nothing else sets them.
- **A class and a marker compose.** `"tandem, high EN-B"` is
  `(HighEnB, Tandem)`; `"mini wing, EN A"` is `(EnA, MiniWing)`.
- **The four qualifier members of `AircraftClass` stand in as the class only
  when no certification class was determined.** `"tandem"` with a paragliding
  aircraft type is `(TandemParaglider, Tandem)` — a tandem paraglider of
  undetermined class. `"mini wing"` alone is `(MiniWing, MiniWing)`.
- **`AircraftClassification.Codes`** renders the pair as invariant codes in
  reading order — `["tandem", "high_en_b"]` — with the marker omitted where the
  class already implies it, so nothing is said twice. Domain values are stored
  as codes and localized at the edge, so `Codes` is never user-facing text, and
  a make or model can never appear among them.
- `ReportAircraft` gains a `Markers` property beside `Class`, and a
  `Classify(AircraftClassification)` overload. The existing
  `Classify(AircraftClass)` stays — that is the reviewer correcting a class by
  hand.

`AircraftClass` itself is unchanged. Its members are stored values and other
work in flight maps them.

## Consequences

- The low/high B band survives a tandem answer, which is the point.
- One aircraft carries one class and a set of markers, so nothing downstream has
  to parse a compound enum member back apart.
- **The schema needs a column for `Markers`** (an invariant code list, or a
  small integer for the flags). That belongs to the schema and migrations work,
  not here, and is called out in the pull request.
- Two members of `AircraftClass` are now reachable in two ways —
  `TandemParaglider` as a class, and `Tandem` as a marker. `Codes` resolves the
  redundancy in one place and the classifier never emits both.

## Alternatives rejected

**Return a bare `AircraftClass`.** Simplest, and it silently drops the band on
every tandem report — the exact information `docs/aircraft-classification.md`
says carries most of the safety signal.

**Add compound members to `AircraftClass`** — `TandemHighEnB`,
`MiniWingEnA`, and so on. The enum becomes the cross-product of classes and
qualifiers, which is a table nobody wants to maintain and a set of stored values
that grows every time a qualifier is added.

**A separate `IsTandem` boolean on `ReportAircraft` only.** It handles tandems
and not mini wings or speedwings, and it puts the qualifier somewhere the
classifier's own result cannot express — so the port would still be lossy.

**Free-text markers.** Unbounded values in a published field, defeating the
point of an invariant code.

## Related

- [ADR-0029](ADR-0029-classification-is-deterministic-and-refuses-to-guess.md)
- [ADR-0018](ADR-0018-feature-folders-in-core.md)
- `docs/aircraft-classification.md`, `skills/aircraft-classification/SKILL.md`
