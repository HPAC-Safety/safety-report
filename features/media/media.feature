@xunit:collection(MeasuresAllocation)
Feature: Attachments
A reporter may attach images, videos, and documents to the finalized
report. Every attachment is validated by content rather than by name, and
only images/videos get a safe derivative. Image and video originals stay
private. A published report shows its verified image and video derivatives
when the reporter also consented to sharing media, and offers its validated
documents, unchanged, as downloads when that consent named documents. A
reviewer may hide any of them (ADR-0117, ADR-0119).

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
  Then the upload expires, its key stopping resolving fifteen days after it was written and its bytes gone about a day after that

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
  And the Worker records that the document was validated
  And the Worker never extracts its text, and the document is never sent to the model and never rendered inline
  And the document remains the reporter-supplied original, available for download, and the review UI labels it as unredacted evidence

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

@REQ-MED-024
Scenario: Processing never holds a whole attachment in memory
  Given a stored 50 MB document original
  When the Worker processes that original
  Then the original is read in bounded chunks into temporary storage while it is hashed
  And no buffer the size of the file is ever allocated

@REQ-MED-025
Scenario: A published report lists its verified photos and video when media was consented to
  Given a published report whose reporter consented to publication and to sharing media
  And the report has a processed image and a video with a verified derivative
  When the public API returns the report
  Then the report lists both files in the order they were attached
  And each file carries only its opaque id and whether it is an image or a video

@REQ-MED-026
Scenario Outline: A file that is not a verified derivative is never public
  Given a published report whose reporter consented to publication and to sharing media
  And the report has <file>
  When a visitor asks for that file's public link
  Then the API returns 404
  And the report lists no media

Examples:
  | file                                     |
  | a document the Worker has not validated yet |
  | a document whose validation failed       |
  | a video retained without a derivative    |
  | an image whose processing failed         |
  | an image the Worker has not processed yet |

@REQ-MED-027
Scenario Outline: Media is public only when the reporter consented to sharing it
  Given a published report with a processed image whose media consent is <consent>
  When the public API returns the report
  Then the report lists no media
  And a visitor asking for the image's public link gets 404

Examples:
  | consent    |
  | no         |
  | unanswered |

@REQ-MED-028
Scenario: A visitor gets a short-lived inline link to a public file
  Given a published report shows a processed image
  When an anonymous visitor asks for the image's public link
  Then the visitor receives a pre-signed URL to the image's derivative that expires within fifteen minutes
  And the URL serves the derivative inline, under the derivative's own image content type
  And the response carries the header X-Content-Type-Options: nosniff
  And the response names no file name, size, or storage key

@REQ-MED-029
Scenario Outline: A file stops being public when its report or a reviewer withdraws it
  Given a published report shows a processed image
  When <withdrawal>
  Then the report lists no media
  And a visitor asking for the image's public link gets 404

Examples:
  | withdrawal                              |
  | a safety officer hides the image        |
  | a reviewer unpublishes the report       |
  | an administrator deletes the report     |

@REQ-MED-030
Scenario: A reviewer hides a file and shows it again, and both are audited
  Given a published report shows a processed image
  When a safety officer hides the image
  Then the report lists no media
  And the audit log records who hid the image
  When the safety officer shows the image again
  Then the report lists the image
  And the audit log records who showed the image
  And the file is kept in storage throughout

@REQ-MED-031
Scenario: A member who is not a reviewer cannot hide or show a file
  Given a published report shows a processed image
  And a member who is not a reviewer is signed in
  When the member tries to hide the image
  Then the API answers 403
  And the report still lists the image

@REQ-MED-032
@ui
Scenario: The report page embeds its photos and video with a generic label
  Given a published report shows an image and a video
  When a visitor opens the report
  Then the image is shown in the page, labelled "Photo 1 of 1"
  And the video can be played in the page with its controls, labelled "Video 1 of 1"

@REQ-MED-033
@ui
Scenario: An expired link is replaced and the video resumes where it was
  Given a visitor is part-way through a public video
  When the video's link stops working
  Then the page fetches a new link
  And the video resumes from where it was

@REQ-MED-034
@ui
Scenario: Media that is no longer public is removed from the page
  Given a visitor opens a published report showing an image
  When the image's link stops working because the image is no longer public
  Then the page removes the image

@REQ-MED-035
@ui
Scenario: A reviewer hides a file from the public report page
  Given a safety officer is signed in and a published report shows an image
  When the safety officer opens the report
  Then the image offers to hide it
  When the safety officer hides the image and confirms
  Then the image is no longer shown

@REQ-MED-036
@ui
Scenario: The admin report page shows whether each file is public
  Given a published report has a public image and a hidden image
  When a safety officer opens the report in the admin area
  Then the public image reads as shown publicly and offers to hide it
  And the hidden image reads as hidden from the public and offers to show it

@REQ-MED-037
Scenario: A published report lists its validated documents when media consent names documents
  Given a published report whose reporter consented to publication and to sharing media under wording that names documents
  And the report has a validated PDF document and a processed image
  When the public API returns the report
  Then the report lists both files in the order they were attached
  And the document carries only its opaque id, the kind document, and the format pdf

@REQ-MED-038
Scenario Outline: A document is public only when its media consent named documents
  Given a published report with a processed image and a validated document
  And the reporter answered media consent <consent>
  When the public API returns the report
  Then the report lists <listed>
  And a visitor asking for the document's public link gets 404

Examples:
  | consent                                              | listed          |
  | yes, to wording that named only photos and video     | only the image  |
  | no                                                   | no media        |
  | not at all                                           | no media        |

@REQ-MED-039
Scenario: A visitor gets a short-lived forced download of a public document
  Given a published report offers a validated PDF document
  When an anonymous visitor asks for the document's public link
  Then the visitor receives a pre-signed URL to the document's unchanged original that expires within fifteen minutes
  And the URL forces a download under a name made from the file id and the format, never the reporter's file name
  And the response carries the header X-Content-Type-Options: nosniff
  And the response names no reporter file name or size

@REQ-MED-040
Scenario: A reviewer hides a document and shows it again, and both are audited
  Given a published report offers a validated document
  When a safety officer hides the document
  Then the report lists no media
  And a visitor asking for the document's public link gets 404
  And the audit log records who hid the document
  When the safety officer shows the document again
  Then the report lists the document

@REQ-MED-041
@ignore
@ui
Scenario: The report page offers a public document as a download, never inline
  Given a published report offers a validated PDF document
  When a visitor opens the report
  Then the document is offered as a download labelled "Document 1 of 1 (PDF)"
  And the page never embeds the document's content

@REQ-MED-042
@ignore
@ui
Scenario: The admin report page shows whether each document is public
  Given a published report has a public document and a hidden document
  When a safety officer opens the report in the admin area
  Then the public document reads as shown publicly and offers to hide it
  And the hidden document reads as hidden from the public and offers to show it
