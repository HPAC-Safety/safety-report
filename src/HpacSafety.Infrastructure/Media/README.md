# Media

Decides what an uploaded file actually is, and removes its metadata. Implements
`IMediaSniffer` and `IExifStripper`, declared in
`HpacSafety.Core/Features/Reporting`.

## What it owns

| Type | Role |
|---|---|
| `MagickNetMediaSniffer` | **Adapter** over Magick.NET. Answers what the bytes really are |
| `MagickNetExifStripper` | **Adapter** over Magick.NET. Removes every metadata profile |
| `MagickFormats` | The single point where `MediaType` meets `MagickFormat`, so the SDK enum never reaches `Core` |

## What it deliberately does not own

- **The accept/reject decision.** `MediaPolicy` in `Core` makes it; the sniffer
  only reports what it sees, and `null` — "I do not know what this is" — is an
  answer rather than a failure.
- **Reading or writing blobs.** That is `../Storage`.
- **Orchestration.** `MediaIngestor` in `Core` runs sniff → validate → strip →
  write, and it is in `Core` so that order is provable without a bucket.

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
the Restricted record, and the derivative exists so a safety officer can look at
a photo without being handed a GPS fix. Source quality is carried across.
See [ADR-0025](../../../docs/decisions/ADR-0025-magick-net-for-exif-stripping.md).

## How it is exercised

`tests/HpacSafety.Infrastructure.Tests/Media`. Fixtures are generated at run
time — a JPEG is built with GPS coordinates, a camera make, and a capture
timestamp attached — so no binary is committed and no real photo is involved.
The end-to-end assertion, `Given_a_photo_with_GPS_EXIF_When_it_is_ingested_Then_the_derivative_has_no_location_data`,
lives in the storage contract suite and runs against both blob stores.

## Deployment

Not deployable. A namespace in a class library.

## Related

- [`docs/data-handling.md`](../../../docs/data-handling.md)
- [ADR-0025](../../../docs/decisions/ADR-0025-magick-net-for-exif-stripping.md)
