---
title: The .NET major moves in one pull request
description: Renovate is held to the .NET major the projects target, and a check fails unless global.json, the target framework, the Worker's base image, and Renovate's allowance all name the same major, so an upgrade moves all four together.
type: adr
status: accepted
date: 2026-09-24
decision-makers: Chase Florell
keywords: .NET, Renovate, global.json, TargetFramework, Dockerfile, allowedVersions, major upgrade, feature-coverage, ADR-0111, ADR-0118
---

# ADR-0120 — The .NET major moves in one pull request

**Status:** Accepted. Extends
[ADR-0111](ADR-0111-renovate-cites-the-claims-a-web-dependency-bump-preserves.md)
to the Worker's Dockerfile, and pins the base image
[ADR-0118](ADR-0118-the-worker-image-installs-ubuntus-ffmpeg.md) introduced to
the projects' .NET major.

## Context

The .NET major is declared in four files:

| File | What it pins |
|---|---|
| `global.json` | the SDK that builds |
| `Directory.Build.props` | `<TargetFramework>`, which every project compiles to |
| `src/HpacSafety.Worker/Dockerfile` | the `mcr.microsoft.com/dotnet/runtime` image the Worker runs on |
| `renovate.json` | the major Renovate may offer |

Renovate updates each file on its own and can't see the target framework. #429
moved the Worker's base image from `runtime:10.0` to `runtime:11.0` while every
project still targeted `net10.0`. The image runs a framework-dependent publish,
and that does not roll forward to a new major, so the Worker would not have
started. `11.0` was also a pre-release tag at the time. CI passed the build: it
proves ffmpeg runs in the image, but it never starts the Worker.

The same pull request also failed `feature-coverage`, because the Dockerfile is
under `src/**`. Every digest-only patch of that base image would fail the same
way, for the reason ADR-0111 fixed for web packages.

Separately, the existing "never automerge the SDK" rule matched
`matchManagers: ["dotnet-sdk"]`. Renovate has no such manager. It reads
`global.json` with its `nuget` manager as the dependency `dotnet-sdk`, so that
rule matched nothing.

## Decision

**One major, four declarations, one pull request.**

- A `renovate.json` rule matching `dotnet-sdk` and `mcr.microsoft.com/dotnet/**`
  sets `allowedVersions` to `/^N\./`, where N is the current major. Digest,
  minor, and patch updates still flow. Renovate never offers the next major.
- `tools/dotnet-major.mjs` reads the major from all four files and fails when
  they disagree. It runs in the required `docs` job, which runs
  unconditionally, so a change to `renovate.json` alone is checked too.
- The next major is a hand-made pull request that moves all four together, plus
  any `Microsoft.*` package whose major follows .NET's. The check won't let one
  go without the others.

**A Worker base-image bump cites what it preserves.** A `renovate.json` rule on
`src/HpacSafety.Worker/Dockerfile` appends a `dependency` exemption citing
REQ-MED-007 and REQ-MED-015, the video claims the image's ffmpeg carries.
`feature-coverage` counts a `Dockerfile` as a dependency manifest. The required
`build` job re-proves the citation: it builds the image and runs ffmpeg and
ffprobe inside it.

**The SDK rule matches the dependency by name**, `matchPackageNames:
["dotnet-sdk"]`, so SDK updates are no longer automerged.

## Alternatives considered

- **Disable major updates for these packages outright.** Rejected. It fixes
  #429, but nothing would name the major in `renovate.json`, so an upgrade
  would have nothing to forget and nothing to check. Naming the major in the
  allowance makes Renovate's side one of the four declarations the check
  compares.
- **Let Renovate group the SDK, the image, and the target framework.** Rejected.
  Renovate has no datasource for `<TargetFramework>`. A regex manager could
  extract it, but it would still offer a pre-release major as soon as the image
  tag exists. A major upgrade also brings package and code changes that a bot
  PR can't make.
- **Start the Worker in CI to catch a runtime mismatch.** Rejected for this
  purpose. The Worker needs a database and storage to start. The declared
  majors are the cheaper, earlier signal, and they fail before an image is
  built.

## Consequences

- #429 is closed. Renovate won't reopen a .NET 11 image or SDK PR while the
  allowance says 10.
- Upgrading to .NET 11 means editing `global.json`, `Directory.Build.props`, the
  Dockerfile's `FROM` (tag and digest), and `allowedVersions` to `/^11\./`, in
  one pull request. `node tools/dotnet-major.mjs` confirms they agree.
- A Worker base-image digest bump passes `feature-coverage` on its own.
- A new Dockerfile on a `mcr.microsoft.com/dotnet` image joins the check
  automatically.
