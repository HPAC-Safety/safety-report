---
name: handle-hpac-media
description: Handle HPAC Safety multipart attachments, private storage, safe image/video derivatives, and private documents. Use for upload, storage, validation, metadata, malware, or reviewer-access changes.
---

# Handle HPAC Safety attachments

Attachments arrive only with the final multipart `POST /api/v1/reports`. Stream
each part to private quarantine while enforcing a configurable total count
(default 5) and 50 MB per-file limit. Map file parts to file-upload answers by
validated index and mint every persisted name/key on the server.

Accepted formats:

- images: JPEG, PNG, WebP, HEIC;
- video: MP4, QuickTime;
- documents: PDF, DOC, DOCX, RTF, MD, TXT, ODT.

Sniff format and require declared/actual agreement. Run malware controls before
review access. Decode/re-encode images and safely remux/transcode videos to
remove metadata; reviewers may see only verified derivatives. Fail closed when
a derivative cannot be produced.

Documents are different: preserve the validated original, do not transform or
anonymize its contents, and never parse/extract it for AI. Allow only an
authorized, short-lived, forced download with active-content-safe headers.
Documents are never inline-rendered or public.

Database failure leaves only unreferenced quarantine bytes for lifecycle expiry.
Report-linked originals/derivatives remain private after soft deletion. Never
log client filenames, storage keys/URLs, or file contents.
