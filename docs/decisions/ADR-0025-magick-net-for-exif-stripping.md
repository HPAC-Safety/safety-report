# ADR-0025: Magick.NET strips EXIF and sniffs content types

**Status:** Accepted
**Date:** 2026-08-22

## Context

A report may carry one photo. `docs/data-handling.md` makes two promises about
it: **EXIF is stripped on ingest — GPS above all**, and **content type is
sniffed, not trusted from the client**. Both need an imaging library, and
`HpacSafety.Core` may not depend on one, so the library sits in
`HpacSafety.Infrastructure` behind ports declared in `Core`
(`IExifStripper`, `IMediaSniffer`).

Three .NET libraries can do the job. The choice is not only technical: this is a
volunteer-run association's system, and a licence that starts charging on a
revenue threshold is a liability nobody here will be watching for.

## Decision

**Magick.NET** — the `Magick.NET-Q8-AnyCPU` package — implements both ports.

| | Licence | Metadata removal | Verdict |
|---|---|---|---|
| **Magick.NET** | Apache-2.0, no revenue threshold | `Strip()` removes every profile: EXIF, IPTC, XMP, ICC, comments | **Chosen** |
| SixLabors.ImageSharp | Six Labors Split Licence from v3 — commercial terms above a revenue threshold | Capable | Rejected |
| SkiaSharp | MIT | Decodes to a bitmap and re-encodes; there is no strip-in-place path | Rejected |

**SixLabors.ImageSharp** was rejected on the licence. v3 moved off Apache-2.0 to
the Six Labors Split Licence, which is free only below a revenue threshold. HPAC
is comfortably below it today, and that is exactly the kind of condition that
is nobody's job to re-check.

**SkiaSharp** was rejected on behaviour. It has no concept of stripping
metadata: the only route is decode to a bitmap and encode a new file, which
discards the original's encoder settings and loses quality in the derivative for
no gain. A library that can only re-encode also cannot tell "this JPEG has EXIF"
from "this JPEG does not".

Two consequences of the choice are written into the adapters:

- **The read is pinned to the format the sniffer already agreed on.**
  ImageMagick will otherwise guess at a format and can reach for an external
  delegate to handle one this system does not accept. `MagickReadSettings.Format`
  closes that off.
- **Sniffing asks twice and requires both answers to agree.** A magic-number
  check against the closed set of accepted formats runs first, so content this
  system will never accept does not reach an imaging library at all; Magick.NET
  then parses the header and reports its own format. A file whose leading bytes
  say JPEG and whose structure says otherwise is *unrecognised*, not a JPEG.

## The accepted set is closed, and small

`MediaType` accepts **JPEG, PNG, and WebP**. A format belongs in that list only
once this system can strip its metadata, because a file whose EXIF cannot be
removed has no derivative a reviewer may safely be shown. "When in doubt,
redact" applies to formats too: the safe failure is a refused upload, not an
un-stripped one.

This is narrower than the issue's wording of "one photo or video per report",
and narrower than HEIC-by-default iPhone camera rolls. Both gaps are recorded in
`docs/data-handling.md` under "Formats deliberately not accepted yet" and are
open questions for HPAC rather than decisions taken here.

## Stripping re-encodes, and that is acceptable

`Strip()` followed by a write re-encodes a JPEG. The derivative is therefore not
bit-identical to the original and loses the ICC profile along with everything
else. That is acceptable here for one reason: **the original bytes are retained
untouched in the Restricted record**. The derivative exists so a safety officer
can look at a photo without being handed a GPS fix; it is not the evidentiary
copy. The adapter carries the source image's quality setting across the
re-encode so the derivative is as close as stripping allows.

## Consequences

- `HpacSafety.Infrastructure` gains a native dependency. `Magick.NET-Q8-AnyCPU`
  ships native binaries for every runtime this system targets; Q8 rather than
  Q16 because eight bits per channel is ample for a review thumbnail and the
  package is smaller.
- `Core` stays clean. `MediaType` is a domain value object; ImageMagick's
  `MagickFormat` never crosses the boundary — the mapping lives in one internal
  class, `MagickFormats`.
- ImageMagick has a long history of delegate-related CVEs. The mitigations above
  are deliberate, and the dependency is on Renovate's watch list in its own
  group so a security release is not batched behind something else.

## Related

- [ADR-0026](ADR-0026-presigned-urls-and-private-blob-storage.md)
- `docs/data-handling.md`
- `src/HpacSafety.Infrastructure/Media/README.md`
