# Private attachment storage

The target store accepts bounded streams from the API into private quarantine,
moves verified originals/derivatives to server-minted keys, and grants
short-lived authorized reviewer reads. It never creates a public object URL.

Uploads arrive only inside final `POST /api/v1/reports`; there is no pre-submit
upload-slot flow. A database failure leaves only unreferenced quarantine bytes
for lifecycle expiry.

Reviewer access is limited to verified image/video derivatives and validated
document originals. Documents are forced downloads; originals are never used as
a fallback preview. Report-linked objects remain private after soft deletion.

Current `S3BlobStore`/`FileSystemBlobStore` and their contract tests implement a
legacy pre-signed capability design. Retain useful private streaming and
short-lived read behavior while removing upload-slot assumptions as described
in [`features/media/media.feature`](../../../features/media/media.feature).
