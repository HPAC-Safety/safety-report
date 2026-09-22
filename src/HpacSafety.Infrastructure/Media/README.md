---
title: Attachment processing
description: How files streamed from the final multipart submission are detected and safely processed.
type: readme
---

# Attachment processing

This slice detects and safely processes files streamed from the final multipart
report submission. The normative matrix is in [`features/media/media.feature`](../../../features/media/media.feature).

Accepted images are JPEG, PNG, WebP, and HEIC; videos are MP4 and QuickTime;
documents are PDF, DOC, DOCX, RTF, MD, TXT, and ODT. Sniff actual format,
require declared/actual agreement, and enforce 50 MB while streaming. There is
no malware scan (ADR-0089) — the format allowlist and sniffing are the gate.

- Decode/re-encode images to remove metadata; HEIC may produce a safe JPEG.
- Safely remux/transcode video and expose only a verified derivative.
- Preserve a validated document original unchanged. Never parse, transform,
  anonymize, send it to AI, inline-render it, or publish it.

Current main implements strong image detection/re-encoding, video container
detection, and document sniffing/validation (magic number, internal package
shape for DOCX/ODT, bounded text decoding for MD/TXT); video derivatives are
not yet implemented. A reviewer reaches an image/video derivative or a
document's original only through `ReviewerMediaLink`, the one chokepoint over
`IBlobStore.CreateReadUrlAsync` — enforced by a source scan, not only by
convention — which the `/api/admin/reports/{reportId}/attachments/{attachmentId}/view`
and `.../download` endpoints call. Tests use generated synthetic fixtures
except the documented tiny HEIC fixture.
