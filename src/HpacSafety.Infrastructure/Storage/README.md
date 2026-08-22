# Storage

Private object storage for uploaded media. Implements `IBlobStore`, declared in
`HpacSafety.Core/SharedKernel`.

## What it owns

| Type | Role |
|---|---|
| `S3BlobStore` | **Adapter** over the AWS S3 SDK. S3-compatible, so it also serves Cloudflare R2 and MinIO |
| `FileSystemBlobStore` | **Adapter** over the local filesystem, for development. Signs its URLs exactly as S3 does |
| `S3BlobStoreOptions`, `FileSystemBlobStoreOptions` | Configuration |
| `PresignedUrlRejectedException` | The local store's equivalent of S3's `403` |

## What it deliberately does not own

- **Serving bytes.** Nothing here is reachable over HTTP from the API. A browser
  fetches a blob from storage directly, through a pre-signed URL, or not at all.
- **Deciding what a file is.** That is `../Media`.
- **Persistence of `report_files`.** The row belongs to the database slice.
- **Minting keys.** A `BlobKey` arrives already parsed; this slice never sees a
  raw string.

## The guarantee

A pre-signed URL is a capability for **one key**, and it expires. Retarget it at
another key and S3 answers `403`; `FileSystemBlobStore` throws
`PresignedUrlRejectedException`. Every lifetime passes
`BlobUrlLifetime.Validate`, capped at 15 minutes.

Which bytes are *safe* to hand a reviewer is not this slice's question — storage
signs whatever key it is given. `ReviewerMediaLink` in `Core` is what refuses a
link to an original, and `MediaUploadSlot` is what keeps every upload in
quarantine.

Nothing here deletes. A refused upload expires through the bucket's lifecycle
rule instead, so no code path exists that could later be pointed at a real
report's media. The rule the bucket needs is written out in
[`docs/data-handling.md`](../../../docs/data-handling.md); the Terraform belongs
to issue #32.

The local store signs an HMAC over the operation, the key, the content type, and
the expiry, and verifies it in fixed time. That is not ceremony — a development
stand-in that skips the production adapter's guarantee is how the guarantee
stops being tested. See
[ADR-0026](../../../docs/decisions/ADR-0026-presigned-urls-and-private-blob-storage.md).

## How it is exercised

`tests/HpacSafety.Infrastructure.Tests/Storage` — one contract suite,
`BlobStoreContractTests`, run unchanged against both adapters. The MinIO
subclass carries `[Trait("Category", "Integration")]` and needs Docker; the
filesystem subclass runs anywhere.

```bash
dotnet test tests/HpacSafety.Infrastructure.Tests --filter "Category!=Integration"
```

## Deployment

Not deployable. A namespace in a class library. Production registers
`S3BlobStore` against a private bucket in `ca-central-1`; development registers
`FileSystemBlobStore`.

## Related

- [`docs/data-handling.md`](../../../docs/data-handling.md)
- [ADR-0026](../../../docs/decisions/ADR-0026-presigned-urls-and-private-blob-storage.md)
