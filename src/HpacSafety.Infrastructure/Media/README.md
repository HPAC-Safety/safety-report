# Media

Decides what an uploaded file actually is, and removes its metadata. Implements
`IMediaSniffer` and `IExifStripper`, declared in
`HpacSafety.Core/Features/Reporting`.

## What it owns

| Type | Role |
|---|---|
| `MagickNetMediaSniffer` | **Adapter** over Magick.NET. Answers what an image really is |
| `VideoContainerSniffer` | Recognises MP4 and QuickTime by magic number. No library involved, deliberately |
| `MediaSnifferChain` | **Chain of Responsibility** over the two. Images first: HEIC and MP4 share a container |
| `MagickNetExifStripper` | **Adapter** over Magick.NET. Removes every metadata profile; transcodes HEIC to JPEG |
| `ImagingCapabilities` | What this runtime can actually decode. Checked at startup |
| `MagickFormats` | The single point where `MediaType` meets `MagickFormat`, so the SDK enum never reaches `Core` |
| `MediaPolicyOptions` | The configured limits — 50 MB today |

## What it deliberately does not own

- **The accept/reject decision.** `MediaPolicy` in `Core` makes it; the sniffer
  only reports what it sees, and `null` — "I do not know what this is" — is an
  answer rather than a failure.
- **Reading or writing blobs.** That is `../Storage`.
- **Orchestration.** `MediaIngestor` in `Core` runs sniff → validate → strip →
  write, and it is in `Core` so that order is provable without a bucket.

## Video is never opened

ImageMagick reports MP4 and MOV as readable, because it can shell out to a
delegate. Handing an attacker-supplied video to a delegate is a category of
vulnerability this system has no reason to be exposed to, so video is recognised
by twelve bytes of `ftyp` box and never decoded. `MagickFormats` has no video
mapping at all, so there is nothing to reach for by accident.

There is no video stripper yet — issue #65 — so a video is retained and never
shown. `MediaType.StrippedForm` returns null for it, and the domain treats that
as an explicit state rather than an absence.

## HEIC needs libheif, and the runtime says so at startup

`MagickNetExifStripper`'s constructor runs `ImagingCapabilities.EnsureCanDecode`
over everything the deployment accepts and throws `MissingImagingCodecException`
if a codec is missing. Construct it eagerly: a HEIC-accepting deployment without
libheif would refuse every iPhone upload as unrecognisable content with nothing
in the logs to say why.

This runtime decodes HEIC and cannot encode it, which is why the derivative is a
JPEG. `ImagingCapabilitiesTests` asserts that in CI, unconditionally.

## Two answers, and both have to agree

A magic-number check against the closed set of accepted formats runs first, so
content this system will never accept never reaches an imaging library.
Magick.NET then parses the header and reports its own format. A file whose
leading bytes say JPEG and whose structure says otherwise is **unrecognised**,
not a JPEG.

The strip reads with the format pinned to the one already agreed on, which stops
ImageMagick guessing and stops it reaching for a delegate.

## Stripping re-encodes

It does, and that is accepted: the **original bytes are retained untouched** in
the private source record, and the derivative exists so a safety officer can look at
a photo without being handed a GPS fix. Source quality is carried across.
See [ADR-0025](../../../docs/decisions/ADR-0025-magick-net-for-exif-stripping.md).

## How it is exercised

`tests/HpacSafety.Infrastructure.Tests/Media`. Fixtures are generated at run
time — JPEGs with GPS coordinates, a camera make, and a capture timestamp
attached; PNGs; MP4 and QuickTime container headers — so nothing is reviewed
blind and no real photo is involved. HEIC is the single committed exception,
because the runtime has no HEIC encoder to generate one with; see
[`fixtures/README.md`](../../../tests/HpacSafety.Infrastructure.Tests/Media/fixtures/README.md).
The end-to-end assertion, `Given_a_photo_with_GPS_EXIF_When_it_is_ingested_Then_the_derivative_has_no_location_data`,
lives in the storage contract suite and runs against both blob stores.

## Deployment

Not deployable. A namespace in a class library.

## Related

- [`docs/data-handling.md`](../../../docs/data-handling.md)
- [ADR-0025](../../../docs/decisions/ADR-0025-magick-net-for-exif-stripping.md)
