Feature: Attachments
A reporter may attach images, videos, and documents to the finalized
report. Every attachment stays private, is validated by content rather than
by name, and only images/videos get a safe reviewer-facing derivative.

Background:
  Given the maximum attachment count is configurable and defaults to five across all attachment kinds
  And each file is limited to 50 MB

@REQ-MED-001
Scenario Outline: Only allowlisted content types are accepted
  Given an attachment part has detected content type <mime>
  When the API validates the attachment's content type
  Then the attachment is accepted as an allowlisted <kind>

Examples:
  | mime                                                                    | kind     |
  | image/jpeg                                                              | image    |
  | image/png                                                               | image    |
  | image/webp                                                              | image    |
  | image/heic                                                              | image    |
  | video/mp4                                                               | video    |
  | video/quicktime                                                         | video    |
  | application/pdf                                                         | document |
  | application/msword                                                      | document |
  | application/vnd.openxmlformats-officedocument.wordprocessingml.document | document |
  | application/rtf                                                         | document |
  | text/rtf                                                                | document |
  | text/markdown                                                           | document |
  | text/plain                                                              | document |
  | application/vnd.oasis.opendocument.text                                 | document |

@REQ-MED-002
@ignore
Scenario: Declared content type must agree with detected content type
  Given an attachment's declared content type differs from its detected, allowlisted type
  When the API validates the attachment
  Then the API rejects the attachment
  And the file extension and client filename are never trusted as the basis for acceptance

@REQ-MED-003
Scenario: The client filename is kept only as a reviewer's download name
  Given a submission names an attachment with a client-supplied filename
  When the API claims the attachment
  Then the sanitized filename is stored on the report file
  And it is not logged, placed in an exception, used in a key, sent to the model, or included in any public DTO
  And the object key encodes only an opaque upload, report, or file identity and a managed compartment

@REQ-MED-004
Scenario: An accepted upload waits in a private quarantine compartment
  Given a reporter's upload passes the size bound and validation
  When the API stores it
  Then its bytes are written to a private quarantine key named only by a minted upload ID
  And no database row, report, or member is linked to it
  And no reviewer link can be issued for it

@REQ-MED-005
@ignore
Scenario: Unclaimed uploads expire automatically
  Given an upload that no committed submission claimed
  When the storage lifecycle rule runs
  Then the upload expires, its key stopping resolving after about a day and its bytes gone about a day after that

@REQ-MED-006
Scenario: Every image is re-encoded to strip metadata
  Given an accepted image attachment enters Worker processing
  When the Worker produces its derivative
  Then the image is decoded and re-encoded into a supported safe representation
  And EXIF, GPS, profiles, comments, thumbnails, and other metadata are removed

@REQ-MED-007
Scenario: Every video is remuxed to strip metadata, never transcoded
  Given an accepted video attachment enters processing
  When its derivative is produced
  Then the video is remuxed through a controlled toolchain without decoding its picture
  And the derivative carries no container metadata, location, device, creation, or filename fields
  And the derivative carries only the video and any audio stream, with timed-metadata, data, and subtitle tracks dropped
  And the derivative is verified to hold none of those before it is accepted
  And a byte-for-byte copy of the original video is never used as the derivative

@REQ-MED-015
Scenario: A video that cannot be stripped is kept rather than refused
  Given an accepted video attachment cannot be remuxed into a verified derivative
  When processing finishes
  Then the upload still succeeds and the original is retained
  And the attachment is marked as having no derivative to show
  And it is not marked as a processing failure, because nothing failed that the reporter should lose their footage over

@REQ-MED-008
Scenario: A document is validated but never transformed
  Given an accepted document attachment enters Worker processing
  When the Worker processes it
  Then the Worker validates its actual format, including internal package shape for DOCX/ODT and bounded text decoding for Markdown/plain text
  And the Worker never extracts its text, and the document is never sent to the model and never published
  And the document remains the reporter-supplied original, available for private download, and the review UI labels it as unredacted private evidence

@REQ-MED-009
Scenario: Each attachment fails and processes independently of the report
  Given a report has multiple attachments, one of which is slow or corrupt
  When the Worker processes the report's outbox items
  Then each file's processing is an independent outbox item
  And the slow or corrupt file neither rolls back the valid report nor forces an additional AI call

@REQ-MED-010
Scenario: A reviewer gets a short-lived URL only for successfully processed media
  Given an image or video attachment has finished processing successfully
  When an authorized reviewer requests to view it
  Then the reviewer receives a short-lived read URL to the derivative
  And the response forces download under the reporter's sanitized filename, or a server-minted name when there is none, with the header X-Content-Type-Options: nosniff
  And there is no API blob proxy or public URL

@REQ-MED-011
Scenario: A reviewer downloads a validated document as an unredacted original
  Given a document attachment has passed validation
  When an authorized reviewer requests it
  Then the reviewer receives a short-lived URL to the private original
  And the download is named with the reporter's sanitized filename, or a server-minted name when there is none
  And there is no API blob proxy or public URL

@REQ-MED-012
@ignore
@ui
Scenario: The admin site never inline-renders a private document
  Given an authorized reviewer opens a document attachment
  When the admin site presents it
  Then the admin site does not embed or inline-render the document content
  And the reviewer is warned that the document is unredacted before download

@REQ-MED-013
Scenario: A failed attachment is inaccessible to reviewers
  Given signature validation, decoding, metadata removal, writing, or verification fails for an image
  When processing finishes
  Then the file is marked failed
  And the file is inaccessible to any reviewer
  And a video whose remux fails is not a failure of this kind: it is retained under its own rule

@REQ-MED-014
@ignore
Scenario: Attachments are never exposed publicly, even after publication
  Given a report has been published
  When the public API returns the report
  Then the public DTO contains no file counts, types, keys, or links

@REQ-MED-016
Scenario: Removing an upload erases every version of it
  Given an unclaimed upload exists in quarantine
  When the reporter's browser deletes it
  Then every stored version of that quarantine object is deleted at once
  And deleting it again succeeds without error

@REQ-MED-017
Scenario: A cancelled upload leaves nothing in storage
  Given a reporter's upload is still being received
  When the browser aborts the request
  Then nothing is written to object storage for it

@REQ-MED-018
Scenario: A claimed upload is copied into the report's original compartment
  Given a submission claims an accepted upload
  When the API ingests it
  Then the upload's bytes are copied unchanged, inside storage, to the report's original compartment
  And the original is named by the report file's own id, never by the upload ID or the reporter's filename
  And no derivative is written before the Worker processes the file

@REQ-MED-019
Scenario Outline: A reporter's filename is sanitized before it is stored
  Given a submission names an attachment with the filename <given>
  When the API claims the attachment
  Then the stored filename is <stored>

Examples:
  | given                    | stored          |
  | launch-site.jpg          | launch-site.jpg |
  | reports/pilot/photo.jpg  | photo.jpg       |
  | ../../etc/passwd.pdf     | passwd.pdf      |
  | say "cheese";.png        | say cheese.png  |
  | a<b>c:d*e?f.txt          | abcdef.txt      |
  | (blank)                  | (none)          |

@REQ-MED-020
Scenario: A download's extension always matches the bytes served
  Given a reporter attached "IMG_0412.HEIC" and its derivative is a JPEG
  When an authorized reviewer requests to view it
  Then the download is named "IMG_0412.jpg"

@REQ-MED-021
Scenario: An attachment awaits the Worker before a reviewer may view it
  Given a submitted image the Worker has not yet processed
  When an authorized reviewer requests to view it
  Then no link is issued
  And the attachment reads as awaiting processing rather than failed

@REQ-MED-022
Scenario: Processing an attachment twice changes nothing
  Given the Worker has already processed an image attachment
  When that attachment's processing message is delivered again
  Then no second derivative is written
  And the attachment's record is unchanged

@REQ-MED-023
Scenario: The Worker skips an attachment whose report was deleted
  Given an attachment's report was deleted before the Worker processed it
  When the Worker handles that attachment's processing message
  Then no derivative is written
  And the message is marked complete
