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

## The accepted set is closed, and every member declares its derivative

`MediaType` accepts **JPEG, PNG, WebP, HEIC, MP4, and QuickTime**, and every
member carries the one fact that matters downstream: what its stripped form is,
or that it does not have one.

| Format | `StrippedForm` | Why |
|---|---|---|
| JPEG, PNG, WebP | itself | Magick.NET strips and rewrites it in place |
| HEIC | **JPEG** | Magick.NET decodes HEIC but cannot encode it, and a reviewer needs something every browser renders anyway |
| MP4, QuickTime | **none** | nothing here can strip a video's metadata yet — see issue #65 |

A format with no stripped form is still **accepted and retained**: the original
is the private source record either way, and refusing a reporter's video after a
crash to satisfy a limitation of ours is the wrong trade. What it does not get is
a derivative, and therefore a reviewer link — `MediaIngestStatus.AwaitingStripping`
is an explicit state, and asking it for a derivative throws rather than falling
back to the unstripped original. A reviewer sees nothing rather than something
unsafe. Media is never published in any case.

## HEIC needs libheif, so the runtime is checked at startup

Magick.NET ships native binaries per platform and the delegates compiled into
them are not guaranteed to be identical everywhere. A deployment that accepts
HEIC on a runtime without libheif would refuse **every iPhone upload** as
unrecognisable content, and nothing in the logs would say why. That is the worst
class of bug: correct-looking code, a silent degradation, and a reporter who
cannot file.

So `ImagingCapabilities.EnsureCanDecode` runs over the accepted set and throws
`MissingImagingCodecException`. `MagickNetExifStripper`'s constructor calls it,
which makes it a **failure to start** rather than a stream of unexplained
rejections — construct the stripper eagerly in the composition root.

Verified on the runtime this ships with: `Magick.NET-Q8-AnyCPU` 14.16.0 reports
HEIC `SupportsReading = true`, `SupportsWriting = false`. Decoding is what we
need; the write side is why the derivative is a JPEG.
`ImagingCapabilitiesTests` asserts this in CI, unconditionally, so the answer
comes from the machine that will run the code rather than from this paragraph.

## Video is sniffed, never decoded

ImageMagick reports MP4 and MOV as readable, because it can shell out to a
delegate for them. Handing an attacker-supplied video to a delegate is a
category of vulnerability this system has no reason to be exposed to, so video
is recognised by `VideoContainerSniffer` — twelve bytes of `ftyp` box and a brand
table — and never opened by an imaging library. `MagickFormats` has no video
mapping at all, so there is nothing to reach for by accident.

HEIC and MP4 share the same ISO base media container and differ only by brand,
which is why the sniffers run as a **Chain of Responsibility** with images first:
`MediaSnifferChain`. Order is the only thing keeping a photo out of the video
path, so the chain rewinds the stream between links and a test asserts a later
link still sees the whole of it.

## Stripping re-encodes, and that is acceptable

`Strip()` followed by a write re-encodes a JPEG. The derivative is therefore not
bit-identical to the original and loses the ICC profile along with everything
else. That is acceptable here for one reason: **the original bytes are retained
untouched in the private source record**. The derivative exists so a safety officer
can look at a photo without being handed a GPS fix; it is not the evidentiary
copy. The adapter carries the source image's quality setting across the
re-encode so the derivative is as close as stripping allows.

## The one committed binary fixture

`AGENTS.md` says to generate binary fixtures at run time. HEIC cannot be:
there is no encoder in this runtime, on any platform CI runs on. So
`tests/.../Media/fixtures/gps.heic` is committed — a 682-byte synthetic
sky-blue square carrying the same fabricated EXIF as the generated JPEG, with
its provenance and its regeneration command written down beside it. The
alternative was no HEIC test at all, on the format most likely to arrive
carrying a GPS fix.

## Consequences

- `HpacSafety.Infrastructure` gains a native dependency. `Magick.NET-Q8-AnyCPU`
  ships native binaries for every runtime this system targets; Q8 rather than
  Q16 because eight bits per channel is ample for a review thumbnail and the
  package is smaller.
- `Core` stays clean. `MediaType` is a domain value object; ImageMagick's
  `MagickFormat` never crosses the boundary — the mapping lives in one internal
  class, `MagickFormats`.
- ImageMagick has a long history of delegate-related CVEs. The mitigations above
  are deliberate — a magic-number gate, a pinned read format, and no video ever
  reaching the library — and the dependency is on Renovate's watch list in its
  own group so a security release is not batched behind something else.
- Accepting HEIC ties this deployment to a Magick.NET build with libheif. That
  is now a startup-checked requirement rather than an assumption.

## Related

- [ADR-0026](ADR-0026-presigned-urls-and-private-blob-storage.md)
- `docs/data-handling.md`
- `src/HpacSafety.Infrastructure/Media/README.md`
