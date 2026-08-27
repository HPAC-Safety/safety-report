# Attachment processing

This slice detects and safely processes files streamed from the final multipart
report submission. The normative matrix is in [`features/media/media.feature`](../../../features/media/media.feature).

Accepted images are JPEG, PNG, WebP, and HEIC; videos are MP4 and QuickTime;
documents are PDF, DOC, DOCX, RTF, MD, TXT, and ODT. Sniff actual format,
require declared/actual agreement, enforce 50 MB while streaming, and run
malware controls before reviewer access.

- Decode/re-encode images to remove metadata; HEIC may produce a safe JPEG.
- Safely remux/transcode video and expose only a verified derivative.
- Preserve a validated document original unchanged. Never parse, transform,
  anonymize, send it to AI, inline-render it, or publish it.

Current main implements strong image detection/re-encoding and video container
detection, but video derivatives and document support are not yet implemented.
Tests use generated synthetic fixtures except the documented tiny HEIC fixture.
