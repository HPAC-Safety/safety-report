---
title: RustFS replaces MinIO as the development and test S3 server
description: MinIO withdrew its public images and archived its repository, so the test containers and docker-compose now run RustFS, pinned in one place and proven by the unchanged blob-store contract suite.
type: adr
status: accepted
date: 2026-09-24
decision-makers: Chase Florell
keywords: S3, MinIO, RustFS, Testcontainers, docker-compose, blob storage, contract suite, ADR-0026, ADR-0096
---

# ADR-0110 — RustFS replaces MinIO as the development and test S3 server

**Status:** Accepted. Amends
[ADR-0096](ADR-0096-an-attachment-uploads-on-attach-and-is-claimed-at-submission.md)
and, through it,
[ADR-0026](ADR-0026-presigned-urls-and-private-blob-storage.md) on one point:
the S3-compatible server that development and the tests run is RustFS, not
MinIO. Everything else those records decide stands.

## Context

`S3BlobStore` is the one storage adapter (ADR-0096). Production points it at
S3. Development and three test suites point it at a local S3-compatible server
in a container: the Infrastructure contract suite, the API tests, and the
acceptance tests. That server was MinIO, pinned to
`quay.io/minio/minio:RELEASE.2025-04-22T22-12-26Z`.

On 2026-09-24, `main` and every pull request went red (#406). Each failure was
the same Testcontainers pull error, `unauthorized: access to the requested
resource is not authorized`. We checked:

- `quay.io/minio/minio` now refuses an anonymous pull for every tag, while
  another repository on quay.io (`prometheus/prometheus`) still answers.
- Docker Hub's `minio/minio` refuses the same way.
- `github.com/minio/minio` has been archived since 2026-04-24.
- No public amd64 copy of the pinned image remains.

A developer machine only kept working because the image sat in its local
Docker cache.

## Decision

**The development and test S3 server is RustFS, pinned to `rustfs/rustfs:1.0.0`.**
RustFS is Apache-2.0 licensed and actively maintained. It publishes amd64 and
arm64 images to Docker Hub.

- **It earned the place by passing the contract suite unchanged.** All 21 tests
  in `EmulatedS3BlobStoreContractTests` pass against it, with no change to a
  single assertion, including:
  - bucket versioning, and a delete that purges every version rather than
    leaving a marker
  - a pre-signed read refused when it is retargeted at another key
  - a forced-download `Content-Disposition` carrying an accented filename
  - copy from quarantine to a report's original
- **One pin for the tests.** `tests/Shared/S3Emulator.cs` builds the container,
  waits on `/health/ready`, and creates the private, versioned bucket. It is
  compiled into each test project that needs it. `docker-compose.yml` pins the
  same image.
- **Readiness means `/health/ready`, not `/health`.** Plain `/health` answers
  as soon as the process is up, while S3 requests still get `503` until
  storage and IAM are ready. Under parallel test load, waiting on `/health`
  let requests through too early.
- **The bucket is configured with the AWS CLI, not the server's own tool.** The
  `s3-init` one-shot in `docker-compose.yml` runs `amazon/aws-cli`, creates the
  bucket, turns on versioning, and applies the same quarantine lifecycle rule
  as `infra/storage.tf`. The next server swap then changes one image line.
- **The compose service is named `s3`, not after its engine.** The API and
  Worker reach it as `s3:9000`, so replacing the server again renames nothing.
- **Production does not change.** `S3BlobStore` and its configuration are
  untouched. Only development and test configuration moved.

## Alternatives considered

- **Build the pinned MinIO release from its archived source and publish it to
  our own registry.** Rejected. It keeps behavior identical, but it freezes us
  on an unmaintained server, and we would own a multi-architecture build of
  AGPL software indefinitely.
- **SeaweedFS or versitygw.** Both are maintained, S3-compatible, and publish
  public images. They were kept as fallbacks and not needed, because RustFS
  passed the contract suite first.
- **Chainguard's MinIO image.** Rejected. Only `latest` is free, so it cannot
  be pinned, and a server that moves underneath the suite is a failure nobody
  can reproduce.
- **LocalStack.** Rejected. It is a much larger emulator than one bucket
  needs.

## Consequences

- A clean machine and CI can pull the server again, and `test` and `coverage`
  pass.
- A developer's existing `minio` container is removed by `./dev-up.sh`
  (`--remove-orphans`). Any attachments uploaded to the old `minio-data` volume
  are not carried over. They were development data under a 15-day expiry.
- Renovate reads the image in `S3Emulator.cs` through a regex custom manager
  and the one in `docker-compose.yml` through its compose manager. Both are
  grouped as `RustFS`, so one pull request moves both, and the contract suite
  judges that pull request.
- If RustFS ever fails the contract suite, the suite names the broken
  guarantee, and the next server is chosen the same way this one was.
