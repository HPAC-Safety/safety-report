# fixtures

One committed binary, and a note on why it is the exception.

`AGENTS.md` says to generate binary fixtures at run time rather than commit
them: nothing to review blind, and nothing that could be mistaken for a real
photograph. Every other fixture in this suite follows that rule — see
[`ExifFixtures`](../ExifFixtures.cs), which builds JPEGs, PNGs, and video
containers in memory.

## `gps.heic` (682 bytes)

HEIC cannot be generated at run time here. The imaging library this project
ships, `Magick.NET-Q8-AnyCPU`, **decodes HEIC but cannot encode it** —
`SupportsReading` is true and `SupportsWriting` is false, because libheif is
built in as a decoder only. There is no encoder to generate a fixture with, on
any platform CI runs on.

So this one is committed. It is a **64×64 solid sky-blue square** carrying the
same synthetic EXIF as the generated JPEG fixture: GPS coordinates in the middle
of the Pacific, the camera make `HpacFixtureCamera`, and a fabricated capture
timestamp. There is no photograph here and no real location.

It was produced once, from the generated JPEG fixture, with a system ImageMagick
build that *does* have a HEIC encoder:

```bash
magick gps.jpg gps.heic
```

Regenerate it the same way if it ever needs to change, and keep it synthetic.

## Why it is worth having at all

HEIC is an iPhone's default photo format and one of the most common carriers of
GPS this system will ever see. A transcode-and-strip path with no HEIC test is a
path nobody has watched work.
