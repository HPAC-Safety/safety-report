---
title: The Worker image installs Ubuntu's ffmpeg
description: The Worker's container image is a short Dockerfile over the host's dotnet publish output that installs ffmpeg from Ubuntu's archive on the digest-pinned .NET runtime image; no NuGet package, static download, or second base image.
type: adr
status: accepted
date: 2026-09-24
decision-makers: Chase Florell
keywords: ffmpeg, worker, container image, dockerfile, PublishContainer, video, supply chain, ADR-0094
---

# ADR-0118 — The Worker image installs Ubuntu's ffmpeg

**Status:** Accepted. **Amends**
[ADR-0094](ADR-0094-video-is-remuxed-not-transcoded-and-never-refused.md)'s
consequence that "the Worker and API container images need ffmpeg": only the
Worker's does. Since
[ADR-0098](ADR-0098-submission-copies-the-original-and-the-worker-makes-the-derivative.md)
the API never processes a video.

## Context

ADR-0094 remuxes video through ffmpeg, run as a child process, and keeps an
original with no derivative when ffmpeg is missing. The Worker image was built
by the .NET SDK's `PublishContainer` target, on
`mcr.microsoft.com/dotnet/runtime:10.0` (Ubuntu 24.04), and nothing put ffmpeg
in it. Every video, in development and deployed, was therefore kept without a
derivative. Since ADR-0117 a published report shows only a verified derivative,
so no video was ever public (#423).

`PublishContainer` has no step that installs an operating-system package. ffmpeg
is also the code that parses files strangers upload, so where the binary comes
from, and who patches it, matters more than for most dependencies.

## Decision

**The Worker image is `src/HpacSafety.Worker/Dockerfile`, over the host's
`dotnet publish` output. It installs ffmpeg from Ubuntu's archive on the same
.NET runtime image, pinned by digest, and runs as that image's non-root `app`
user.**

- **One build script.** `tools/build-worker-image.sh` publishes the Worker and
  runs `docker build` with the publish output as the entire build context. It
  is the only way the image is built: `dev-up.sh`, CI, and `deploy-worker.yml`
  all call it, so the three cannot drift apart. The SDK still compiles the
  Worker; Docker only wraps the result, so no .NET SDK or restore runs inside
  the image.
- **One architecture per image.** The script publishes for the architecture
  the local Docker engine builds: `linux-x64` for the x86_64 Fargate task,
  `linux-arm64` on an Apple-silicon development machine. A portable publish
  carried Windows, macOS, and musl builds of every native dependency, about
  190 MB the image could never load.
- **Ubuntu patches ffmpeg.** A rebuild picks up Ubuntu's security fixes.
  Renovate's Dockerfile manager moves the base image's digest pin.
- **It is the ffmpeg the tests use.** CI installs ffmpeg from the same archive
  on an Ubuntu runner, and `FfmpegVideoRemuxerTests` prove the remux command
  against it.
- **CI builds the image** and proves that `ffmpeg` and `ffprobe` run inside it
  and that the Worker is not root.
- **No new service.** ffmpeg runs inside the existing Worker ECS service, and
  the API image is unchanged.

## Rejected alternatives

- **A NuGet package.** None ships a trustworthy Linux `ffmpeg` and `ffprobe`.
  - FFMpegCore, Xabe.FFmpeg, and xFFmpeg.NET are wrappers that expect ffmpeg
    to be installed already.
  - FFmpeg.GPL and FFmpeg.LGPL are Windows DLLs from an individual publisher.
  - FFmpeg.AutoGen links ffmpeg into our process, which ADR-0094 rejects.
  - The Linux executable packages are single-maintainer with no track record
    (DevEnvy, TqkLibrary) or built in 2021 and never updated (Curiosity).
  - Any of them would put an unvetted binary on the path of hostile input,
    patched only when one person chooses to.
- **A checksum-pinned static build**, downloaded by MSBuild into the
  `PublishContainer` output. It would keep one build mechanism, but it needs a
  custom download-and-verify target, a binary CI never tested, and a person to
  track ffmpeg's security advisories and bump the pin.
- **A custom base image under `PublishContainer`**, with the runtime plus
  ffmpeg built separately and named as `ContainerBaseImage`. That is two images
  to build, push, and keep in step, in development and in ECR, for the same
  result.
- **Keeping video private** (public media as images only), a managed-code MP4
  box rewriter, and AWS MediaConvert. The owner chose to ship ffmpeg.
  MediaConvert was already rejected by ADR-0094.

## Consequences

- The Worker and the API are built differently: the API by `PublishContainer`,
  the Worker by a Dockerfile.
- Ubuntu's ffmpeg is large. Its device and filter libraries pull in LLVM and
  Mesa, which a stream-copy remux never uses, and apt cannot leave them out.
  - Uncompressed, the ffmpeg layer is about 400 MB.
  - Compressed, the whole image is about 234 MB, against 76 MB for the bare
    runtime.
  - Fargate pulls the compressed layers, so a Worker task starts a couple of
    seconds later. Nothing changes at runtime.
  - This is the cost of Ubuntu owning the patches. A static build would be
    smaller, at the price of tracking ffmpeg's security fixes ourselves.
- Every deployed and development video now gets a verified derivative, so a
  published report whose reporter consented to media shows its video
  (ADR-0117).
- The existing Worker task size, 0.5 vCPU and 1 GB, is unchanged, because a
  remux copies compressed packets and never decodes.
