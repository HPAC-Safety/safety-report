@xunit:collection(MeasuresAllocation)
Feature: Attachments
A reporter may attach images, videos, and documents to the finalized
report. Every attachment is validated by its content, never by its name, and
only images/videos get a safe derivative. Image and video originals stay
private. A published report shows its verified image and video derivatives
when the reporter also consented to sharing media, and offers its validated
documents, unchanged, as downloads when that consent named documents. A
reviewer may hide any of them (ADR-0117, ADR-0119). Staff may also add
private attachments to a report: kept byte for byte, never anonymized, and
never shown to anyone but a reviewer (ADR-0135).

Background:
  Given the maximum attachment count is configurable and defaults to five across all attachment kinds
  And each file is limited to 250 MB for a video and 25 MB for an image or a document

@REQ-MED-001
Scenario Outline: Only allowlisted content types are accepted
  Given an uploaded file has detected content type <mime>
  When the attachment's content type is validated
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
Scenario: A declared content type must agree with the one detected
  Given an attachment's declared content type differs from the allowlisted one detected in its bytes
  When the attachment is validated
  Then the attachment is refused
  And the file extension and client filename are never trusted as the basis for acceptance

@REQ-MED-003
Scenario: The client filename is kept only as a reviewer's download name
  Given a submission names an attachment with a client-supplied filename
  When the submission claims the attachment
  Then the sanitized filename is stored on the report file
  And it is not logged, placed in an exception, used in a key, sent to the model, or included in anything public
  And the object key encodes only an opaque upload, report, or file identity and a managed compartment

@REQ-MED-045
Scenario: A sent upload waits, unvalidated, in a private quarantine compartment
  Given a reporter's browser has sent a file through the pre-signed upload link minted for it
  Then its bytes sit at a private quarantine key named only by the minted upload ID
  And no database row, report, or member is linked to it
  And no reviewer link can be issued for it
  And it is not validated until a submission claims it

@REQ-MED-005
Scenario: Unclaimed uploads expire automatically
  Given an upload that no committed submission claimed
  When the storage lifecycle rule runs
  Then the upload expires, its key stopping resolving fifteen days after it was written and its bytes gone about a day after that
  And no file a committed submission claimed is expired by that rule

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
  And the derivative carries no container metadata, location, device, creation, or filename entries
  And the derivative carries only the video and any audio stream, with timed-metadata, data, and subtitle tracks dropped
  And the derivative is verified to hold none of those before it is accepted
  And a byte-for-byte copy of the original video is never used as the derivative
  And the derivative is an MP4 container, stored as video/mp4, whatever container the video arrived in

@REQ-MED-015
Scenario: A video that cannot be stripped is kept, not refused
  Given an accepted video attachment cannot be remuxed into a verified derivative
  When processing finishes
  Then the upload still succeeds and the original is retained
  And the attachment is marked as having no derivative to show
  And it is not marked as a processing failure

@REQ-MED-008
Scenario: A document is validated but never transformed
  Given an accepted document attachment enters Worker processing
  When the Worker processes it
  Then the Worker validates its actual format, including internal package shape for DOCX/ODT and bounded text decoding for Markdown/plain text
  And the Worker records that the document was validated
  And the Worker never extracts its text, and the document is never sent to the model and never rendered inline
  And the document remains the reporter-supplied original, available for download

@REQ-MED-009
Scenario: Each attachment fails and processes independently of the report
  Given a report has multiple attachments, one of which is slow or corrupt
  When the Worker processes the report's Worker jobs
  Then each file's processing is an independent Worker job
  And the slow or corrupt file neither rolls back the valid report nor forces an additional AI call

@REQ-MED-010
Scenario: A reviewer gets a short-lived inline URL only for successfully processed media
  Given an image or video attachment has finished processing successfully
  When a reviewer requests to view it
  Then the reviewer receives a short-lived link to the derivative, served inline and not as a forced download, marked so its content type is never guessed by a browser
  And no proxy and no public link serves the file

@REQ-MED-011
Scenario: A reviewer downloads a validated document as an unredacted original
  Given a document attachment has passed validation
  When a reviewer requests it
  Then the reviewer receives a short-lived URL to the private original
  And the download is named with the reporter's sanitized filename, or a server-minted name when there is none
  And the URL forces a download, marked so its content type is never guessed by a browser
  And no proxy and no public link serves the file

@REQ-MED-012
@ui
Scenario: The admin site never inline-renders a private document
  Given a reviewer opens a document attachment
  When the admin site presents it
  Then the admin site does not embed, preview, or inline-render the document content
  And the document is offered only as a download

@REQ-MED-013
Scenario: A failed image or video is never viewed inline, but its raw original downloads, audited
  Given signature validation, decoding, metadata removal, writing, or verification fails for an image
  When processing finishes
  Then the file is marked failed
  And no inline view link is issued for it
  And a reviewer instead receives a short-lived, forced download of the raw original, audited as a distinct action, under the reporter's sanitized filename, or a server-minted name when there is none
  And it is never offered inline and never opened in the lightbox
  And a video whose remux fails is not a failure of this kind: it is retained under its own rule

@REQ-MED-053
Scenario: A still-processing image or video is never viewed inline, but its raw original downloads, audited
  Given a submitted image the Worker has not yet processed
  When a reviewer requests it
  Then no inline view link is issued for it
  And a reviewer instead receives a short-lived, forced download of the raw original, audited as a distinct action, under the reporter's sanitized filename, or a server-minted name when there is none
  And it is never offered inline and never opened in the lightbox

@REQ-MED-054
Scenario: The raw-original download refuses once a derivative exists
  Given an image or video attachment has finished processing successfully
  When a reviewer requests its raw original, not its view link
  Then the raw-original download is refused

@REQ-MED-055
Scenario: The raw-original download refuses a document
  Given a document attachment has passed validation
  When a reviewer requests its raw original, not its download link
  Then the raw-original download is refused

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
  When the upload is ingested
  Then the upload's bytes are copied unchanged, inside storage, to the report's original compartment
  And the original is named by the report file's own id, never by the upload ID or the reporter's filename
  And no derivative is written before the Worker processes the file

@REQ-MED-019
Scenario Outline: A reporter's filename is sanitized before it is stored
  Given a submission names an attachment with the filename <given>
  When the submission claims the attachment
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
  When a reviewer requests to view it
  Then the download is named "IMG_0412.jpg"

@REQ-MED-021
Scenario: An attachment awaits the Worker before a reviewer may view it
  Given a submitted image the Worker has not yet processed
  When a reviewer requests to view it
  Then no link is issued
  And the attachment reads as awaiting processing, not failed

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
  Given a stored 25 MB document original
  When the Worker processes that original
  Then the original is read in bounded chunks into temporary storage while it is hashed
  And no buffer the size of the file is ever allocated

@REQ-MED-025
Scenario: A published report lists its verified images and video when media was consented to
  Given a published report whose reporter consented to publication and to sharing media
  And the report has a processed image and a video with a verified derivative
  When a visitor reads the report
  Then the report lists both files in the order they were attached
  And each file carries only its opaque id and whether it is an image or a video

@REQ-MED-026
Scenario Outline: A file that is neither a verified derivative nor a validated document is never public
  Given a published report whose reporter consented to publication and to sharing media
  And the report has <file>
  When a visitor asks for that file's public link
  Then the link is not found
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
  When a visitor reads the report
  Then the report lists no media
  And a visitor's request for the image's public link is not found

Examples:
  | consent    |
  | no         |
  | unanswered |

@REQ-MED-028
Scenario: A visitor gets a short-lived inline link to a public file
  Given a published report shows a processed image
  When an anonymous visitor asks for the image's public link
  Then the visitor receives a pre-signed URL to the image's derivative that expires within fifteen minutes
  And the URL serves the derivative inline, under the image content type of the derivative itself
  And the response is marked so its content type is never guessed by a browser
  And the response names no file name, size, or storage key

@REQ-MED-029
Scenario Outline: A file stops being public when its report or a reviewer withdraws it
  Given a published report shows a processed image
  When <withdrawal>
  Then the report lists no media
  And a visitor's request for the image's public link is not found

Examples:
  | withdrawal                              |
  | a Safety Officer hides the image        |
  | a reviewer unpublishes the report       |
  | an Administrator deletes the report     |

@REQ-MED-030
Scenario: A reviewer hides a file, and the hiding is audited
  Given a published report shows a processed image
  When a Safety Officer hides the image
  Then the report lists no media
  And the audit log records who hid the image

@REQ-MED-063
Scenario: A reviewer shows a hidden file again, the showing is audited, and the file was kept in storage throughout
  Given a published report shows a processed image that a Safety Officer has hidden
  When the Safety Officer shows the image again
  Then the report lists the image
  And the audit log records who showed the image
  And the file is kept in storage throughout

@REQ-MED-031
Scenario: A member who is not a reviewer cannot hide or show a file
  Given a published report shows a processed image
  And the visitor is a member who is not a reviewer
  When the member tries to hide the image
  Then the attempt is refused as forbidden
  And the report still lists the image

@REQ-MED-032
@ui
Scenario: The report page shows a thumbnail strip
  Given a published report shows an image and a video
  When a visitor opens the report
  Then the report page shows a thumbnail strip in place of stacked embeds

@REQ-MED-064
@ui
Scenario: Activating a thumbnail opens the lightbox with a generic label
  Given a visitor is reading a published report that shows an image and a video
  When the visitor activates the image's thumbnail
  Then the lightbox opens showing the image, labelled "Photo 1 of 1"

@REQ-MED-065
@ui
Scenario: The lightbox moves on to a video, playable with its controls and audio
  Given a visitor has a published report's image open in the lightbox, with a video after it
  When the visitor moves to the next item in the lightbox
  Then the lightbox shows the video, playable with its controls and audio, labelled "Video 1 of 1"

@REQ-MED-033
@ui
Scenario: An expired link is replaced and the video resumes where it was
  Given a visitor has paused a public video part-way through
  When the video's link stops working
  Then the page fetches a new link
  And the video resumes from where it was, still paused

@REQ-MED-034
@ui
Scenario: Media that is no longer public is removed from the page
  Given a visitor opens a published report showing an image
  When the image's link stops working once the image is no longer public
  Then the page removes the image

@REQ-MED-035
@ui
Scenario: A reviewer hides a file from the public report page, still marked in the staff strip
  Given a Safety Officer is reading a published report that shows an image
  When the Safety Officer hides the image and confirms
  Then the image now reads as hidden from the public and offers to show it, still on the report page

@REQ-MED-066
@ui
Scenario: A reviewer reading the public report page is offered to hide a file
  Given a Safety Officer is on the admin site and a published report shows an image
  When the Safety Officer opens the report
  Then the image offers to hide it

@REQ-MED-056
@ui
Scenario Outline: The lightbox steps between its images by arrow key, and wraps at either end
  Given a published report shows two images
  And a visitor has the <from> image open in the lightbox
  When the visitor uses <key>
  Then the lightbox shows the <to> image

Examples:
  | from   | key                 | to     |
  | first  | the right arrow key | second |
  | second | the right arrow key | first  |
  | first  | the left arrow key  | second |

@REQ-MED-067
@ui
Scenario Outline: The open lightbox keeps focus inside it
  Given a published report shows two images
  And a visitor has the first image open in the lightbox
  When the visitor moves on through the lightbox several times with <key>
  Then focus never leaves the lightbox while it is open

Examples:
  | key         |
  | the Tab key |

@REQ-MED-068
@ui
Scenario Outline: Closing the lightbox returns focus to the thumbnail that opened it
  Given a published report shows two images
  And a visitor has the first image open in the lightbox
  When the visitor closes the lightbox with <key>
  Then focus returns to the first image's thumbnail

Examples:
  | key            |
  | the Escape key |

@REQ-MED-057
@ui
Scenario: A document's thumbnail is never opened in the lightbox
  Given a published report offers a validated PDF document
  When a visitor activates the document's thumbnail
  Then the document downloads and the lightbox does not open

@REQ-MED-058
@ui
Scenario: A link that is not found removes the item from both the strip and an open lightbox
  Given a visitor has the lightbox open on a public image, and another item remains after it
  When the image's link is not found once the image is no longer public
  Then the image's thumbnail is removed from the strip and the lightbox steps to the remaining item without closing

@REQ-MED-061
@ui
Scenario: A link not found on the only remaining lightbox item closes it
  Given a visitor has the lightbox open on the one public image a report has
  When the image's link is not found once the image is no longer public
  Then the image's thumbnail is removed from the strip and the lightbox closes with nothing left to show

@REQ-MED-062
@ui
Scenario: A reporter's video in the lightbox carries no captions, and the lightbox does not suggest it might
  Given a published report shows a video
  And a visitor has the report open
  When the visitor activates the video's thumbnail
  Then the lightbox shows the video with the browser's own controls
  And the video offers no caption or subtitle track
  And the lightbox offers no caption control and no caption text of its own

@REQ-MED-059
@ui
Scenario: The admin report page uses the same strip and lightbox, and works for an unpublished report
  Given a Safety Officer is on the admin site and an unpublished report has an image and a hidden document
  When a Safety Officer opens the report in the admin area
  Then the report shows the same thumbnail strip and lightbox as the public report page
  And the hidden document's thumbnail is marked "Hidden from the public" and offers to show it

@REQ-MED-060
@ui
Scenario: A processing or failed image's staff tile offers a raw-original download, never inline or in the lightbox
  Given a Safety Officer is on the admin site and a report has a still-processing image
  When a Safety Officer opens the report in the admin area
  Then the image's tile is marked "processing"

@REQ-MED-083
@ui
Scenario: Activating a processing image's staff tile downloads the raw original
  Given a Safety Officer is on the admin site and a report has a still-processing image
  And a Safety Officer opens the report in the admin area
  When the Safety Officer activates the image's tile
  Then the raw original downloads and the lightbox does not open

@REQ-MED-036
@ui
Scenario: The admin report page shows whether each file is public
  Given a published report has a public image and a hidden image
  When a Safety Officer opens the report in the admin area
  Then the public image reads as shown publicly and offers to hide it
  And the hidden image reads as hidden from the public and offers to show it

@REQ-MED-037
Scenario: A published report lists its validated documents when media consent names documents
  Given a published report whose reporter consented to publication and to sharing media under wording that names documents
  And the report has a validated PDF document and a processed image
  When a visitor reads the report
  Then the report lists both files in the order they were attached
  And the document carries only its opaque id, the kind document, and the format pdf

@REQ-MED-038
Scenario Outline: A document is public only when its media consent named documents
  Given a published report with a processed image and a validated document
  And the reporter answered media consent <consent>
  When a visitor reads the report
  Then the report lists <listed>
  And a visitor's request for the document's public link is not found

Examples:
  | consent                                              | listed          |
  | yes, to wording that named only images and video     | only the image  |
  | no                                                   | no media        |
  | not at all                                           | no media        |

@REQ-MED-039
Scenario: A visitor gets a short-lived forced download of a public document
  Given a published report offers a validated PDF document
  When an anonymous visitor asks for the document's public link
  Then the visitor receives a pre-signed URL to the document's unchanged original that expires within fifteen minutes
  And the URL forces a download under a name made from the file id and the format, never the reporter's file name
  And the response is marked so its content type is never guessed by a browser
  And the response names no reporter file name or size

@REQ-MED-040
Scenario: A reviewer hides a document, and the hiding is audited
  Given a published report offers a validated document
  When a Safety Officer hides the document
  Then the report lists no media
  And a visitor's request for the document's public link is not found
  And the audit log records who hid the document

@REQ-MED-069
Scenario: A reviewer shows a hidden document again
  Given a published report offers a validated document that a Safety Officer has hidden
  When the Safety Officer shows the document again
  Then the report lists the document

@REQ-MED-041
@ui
Scenario: The report page offers a public document as a download, never inline
  Given a published report offers a validated PDF document
  When a visitor opens the report
  Then the document is offered as a download labelled "Document 1 of 1 (PDF)"
  And the page never embeds the document's content

@REQ-MED-042
@ui
Scenario: The admin report page shows whether each document is public
  Given a published report has a public document and a hidden document
  When a Safety Officer opens the report in the admin area
  Then the public document reads as shown publicly and offers to hide it
  And the hidden document reads as hidden from the public and offers to show it

@REQ-MED-043
Scenario: A QuickTime video downloads as an MP4
  Given a reporter attached "IMG_0412.MOV" and its derivative is an MP4
  When a reviewer requests to view it
  Then the download is named "IMG_0412.mp4"

@REQ-MED-044
Scenario: A published QuickTime video is served as an MP4
  Given a published report shows a processed QuickTime video
  When an anonymous visitor asks for the video's public link
  Then the URL serves the derivative inline, as video/mp4

@REQ-MED-046
Scenario Outline: Staff mint a private upload for a file of any content type, or of none
  Given a pending report that staff add private attachments to
  When a Safety Officer declares a <declared> file of <size> for that report's private attachments
  Then a pre-signed upload link is minted for a quarantine key named only by a new upload ID
  And the upload link is signed for the content type <signed> and exactly <size>
  And nothing about the upload is written to the database

Examples:
  | declared                 | size   | signed                   |
  | application/zip          | 1 GB   | application/zip          |
  | video/x-matroska         | 300 MB | video/x-matroska         |
  | application/x-msdownload | 10 KB  | application/x-msdownload |
  | typeless                 | 10 KB  | application/octet-stream |
  | malformed-type           | 10 KB  | application/octet-stream |

@REQ-MED-047
Scenario Outline: A private upload larger than the configured cap is refused before anything is minted
  Given the private attachment cap is configured as <cap>
  And a pending report that staff add private attachments to
  When a Safety Officer declares a file of <size> for that report's private attachments
  Then it is refused as invalid with the reason "<reason>", and nothing is minted
  And a file of exactly <cap> is minted

Examples:
  | cap  | size               | reason    |
  | 1 GB | 1 GB plus one byte | too_large |
  | 1 MB | 1 MB plus one byte | too_large |
  | 1 GB | zero bytes         | empty     |

@REQ-MED-048
Scenario: Adding a private attachment stores its bytes unchanged in the report's private compartment
  Given a Safety Officer's browser has sent a zip file through a private upload minted for a report
  When the Safety Officer adds that upload to the report as "Coroner report.zip"
  Then its bytes sit, byte for byte and nowhere else on the report, at the report's private key named by the attachment's id
  And the upload no longer sits in quarantine
  And no reporter attachment, derivative, or Worker job was created for it

@REQ-MED-049
Scenario: A private attachment downloads unchanged under its sanitized name, and each download is audited
  Given a report carries the private attachment "Coroner: report?.zip"
  When a Safety Officer asks for its download link twice
  Then each link is a pre-signed download link that lives at most 15 minutes and forces a download named "Coroner report.zip"
  And the bytes each link serves are identical to those uploaded
  And two private-attachment-downloaded audit entries record the Safety Officer's token subject and the attachment

@REQ-MED-050
Scenario: An unclaimed private upload waits in quarantine and expires with every other upload
  Given a Safety Officer's browser has sent a file through a private upload minted for a report
  Then its bytes wait under the quarantine key named by the minted upload ID, with no row or report linked to them
  And that key falls under the storage lifecycle rule that expires every unclaimed quarantine upload

@REQ-MED-051
Scenario: Only the private attachment link signs a URL for the private compartment
  Given a report carries the private attachment "Coroner: report?.zip"
  Then the reviewer media link and the public media link both refuse the private attachment's key
  And the private attachment link refuses every key outside the private compartment
  And every reviewer attachment request for the private attachment's id is not found

@REQ-MED-052
Scenario: Nothing anonymizes a private attachment
  Given a Safety Officer adds a JPEG image carrying its camera's location metadata as a private attachment
  When the Safety Officer downloads it
  Then the stored bytes and the downloaded bytes are identical to those uploaded, location metadata included
  And no derivative of it exists and no Worker job asks for one

@REQ-MOD-107
Scenario Outline: Only a reviewer may reach private attachments
  Given a report carrying one private attachment
  When <who> mints a private upload for, adds, lists, downloads, and removes private attachments on it
  Then every one of those private-attachment requests is <outcome>

Examples:
  | who                  | outcome                    |
  | an anonymous visitor | refused as unauthenticated |
  | a User               | refused as forbidden       |
  | a Safety Officer     | answered with success      |
  | an Administrator     | answered with success      |

@REQ-MOD-108
Scenario Outline: Staff add private attachments to a report in any status
  Given a <status> report that staff add private attachments to
  When a Safety Officer adds a private attachment with a description and then an Administrator adds one without
  Then both private attachments are listed, newest first
  And each lists its file name, size, description, adder's token subject, and when it was added
  And adding them queued no work for the Worker
  And the report detail lists neither among its attachments

Examples:
  | status         |
  | pending        |
  | published      |
  | unpublished    |
  | summary-failed |
  | no-consent     |

@REQ-MOD-109
Scenario: Removing a private attachment deletes it and keeps its bytes
  Given a report carrying one private attachment
  When an Administrator removes that private attachment
  Then the private attachment is no longer listed, and downloading or removing it is not found
  And its row is marked deleted with the Administrator's token subject, and its bytes are still stored
  And one audit entry records the Administrator's token subject, a private-attachment-removed action, the attachment, and the time

@REQ-MOD-110
Scenario Outline: A private attachment needs a usable name, a short description, and a sent upload
  Given a pending report that staff add private attachments to
  When a Safety Officer adds a private attachment whose <field> is <value>
  Then it is refused as invalid and no private attachment is stored

Examples:
  | field       | value                    |
  | file name   | empty                    |
  | file name   | only reserved characters |
  | description | 501 characters long      |
  | upload      | one that was never sent  |

@REQ-MOD-111
Scenario: A deleted report's private attachments go with it
  Given a report carrying one private attachment
  When a Safety Officer deletes the report carrying that private attachment
  Then the private attachment is marked deleted at the report's deletion time, and its bytes are still stored
  And every request to mint, add, list, or download private attachments on that report is not found

@REQ-MOD-112
Scenario: No public or member read ever returns a private attachment, not even a count
  Given a published report whose reporter consented to publication and media carries one private attachment
  When an anonymous visitor and a User read the public feed, that report's public page, and its public media
  Then no response carries the private attachment's name, description, or identifier, or any count of private attachments
  And asking for the private attachment's identifier as public media is not found
  And only the admin search reads private attachments

@REQ-MOD-113
Scenario: A private attachment never reaches the model
  Given a consented report carrying one private attachment is due for summarization
  When the Worker claims the job and builds the model input
  Then the model input carries nothing from the private attachment
  And no Worker job names the private attachment

@REQ-MOD-114
Scenario: A private note may refer to a private attachment on its own report only
  Given a report carrying one private attachment, and another report carrying one of its own
  When a Safety Officer adds a private note referring to the first report's private attachment
  Then the private note lists the private attachment it refers to, by identifier and file name
  And a private note referring to the other report's private attachment is refused as invalid and nothing is stored
  And a private note referring to a removed private attachment is refused as invalid

@REQ-MED-070
Scenario: Editing a private note away from its private attachment keeps the reference in its history
  Given a report carrying one private attachment, and a private note referring to it
  When an Administrator edits that private note to refer to no private attachment
  Then the private note refers to none, and its history shows the first revision still referring to it

@REQ-MOD-115
@ui
Scenario: A Safety Officer stages a private attachment on the report page
  Given a Safety Officer is on the admin site and a pending report exists
  And the Safety Officer has that report open
  When the Safety Officer stages the private attachment "coroner-report.zip"
  Then the staged attachment "coroner-report.zip" finishes uploading and offers a description box

@REQ-MED-071
@ui
Scenario: A Safety Officer adds a described private attachment on the report page
  Given a Safety Officer is on the admin site and a pending report exists
  And the Safety Officer has staged the private attachment "coroner-report.zip" on that report and described it as "Received from the coroner"
  When the Safety Officer adds the staged private attachments
  Then the private attachments section lists "coroner-report.zip" with its description, its adder, and when it was added

@REQ-MED-072
@ui
Scenario: A Safety Officer downloads a private attachment from the report page
  Given a Safety Officer is on the admin site and a pending report exists
  And the Safety Officer has added the private attachment "coroner-report.zip" to that report
  When the Safety Officer downloads the private attachment "coroner-report.zip"
  Then the browser saves a file named "coroner-report.zip"

@REQ-MED-073
@ui
Scenario: A Safety Officer removes a private attachment from the report page
  Given a Safety Officer is on the admin site and a pending report exists
  And the Safety Officer has added the private attachment "coroner-report.zip" to that report
  When the Safety Officer removes the private attachment "coroner-report.zip" and confirms
  Then the private attachments section lists no attachments

@REQ-MOD-116
@ui
Scenario: A private note refers to a private attachment on the report page
  Given a Safety Officer is on the admin site and a pending report exists
  And the report carries the private attachment "police-report.pdf"
  And the Safety Officer has that report open
  When the Safety Officer adds the private note "See the police report." referring to "police-report.pdf"
  Then that private note shows that it refers to "police-report.pdf"

@REQ-MOD-117
@ui
Scenario: A Safety Officer cancels a private attachment while it uploads
  Given a Safety Officer is on the admin site and a pending report exists
  And storage is slow to accept a private attachment
  And the Safety Officer has staged the private attachment "investigation-archive.zip" on that report, still uploading
  When the Safety Officer cancels the staged upload "investigation-archive.zip"
  Then the staged attachment "investigation-archive.zip" is gone from the staging list
  And the cancelled upload is erased

@REQ-MED-074
@ui
Scenario: A private attachment shows its progress and offers to cancel while it uploads
  Given a Safety Officer is on the admin site and a pending report exists
  And storage is slow to accept a private attachment
  And the Safety Officer has that report open
  When the Safety Officer stages the private attachment "investigation-archive.zip"
  Then the staged attachment "investigation-archive.zip" shows its upload progress, offers to cancel it, and "Add 0 attachments" stays disabled

@REQ-MOD-173
@ui
Scenario Outline: Several private attachments staged at once each upload independently
  Given a Safety Officer is on the admin site and a pending report exists
  And the Safety Officer has that report open
  When the Safety Officer <method> the private attachments "site-photo.jpg" and "weather-log.pdf" at once
  Then both staged attachments finish uploading independently, each with its own progress

Examples:
  | method                       |
  | drops                        |
  | chooses, through the picker, |

@REQ-MED-075
@ui
Scenario Outline: Several staged private attachments are added together, each with its own description
  Given a Safety Officer is on the admin site and a pending report exists
  And the Safety Officer has <staged> "site-photo.jpg" described as "Taken at the site" and "weather-log.pdf" described as "Environment Canada log" on that report
  When the Safety Officer adds the staged private attachments
  Then the private attachments section lists "site-photo.jpg" and "weather-log.pdf", each with its own description

Examples:
  | staged                       |
  | dropped                      |
  | chosen, through the picker,  |

@REQ-MOD-174
@ui
Scenario: Removing a staged private attachment before it is added leaves the others staged
  Given a Safety Officer is on the admin site and a pending report exists
  And the Safety Officer has dropped the private attachments "keep-me.pdf" and "drop-me.pdf" on that report at once, both finished uploading
  When the Safety Officer removes the staged attachment "drop-me.pdf"
  Then only "keep-me.pdf" remains in the staging list, and nothing erases the upload for "drop-me.pdf"

@REQ-MED-076
@ui
Scenario: Adding after removing a staged private attachment adds only those left
  Given a Safety Officer is on the admin site and a pending report exists
  And the Safety Officer has dropped the private attachments "keep-me.pdf" and "drop-me.pdf" on that report at once, then removed the staged "drop-me.pdf"
  When the Safety Officer adds the staged private attachments
  Then the private attachments section lists "keep-me.pdf" only

@REQ-MOD-175
@ui
Scenario: A too-large private attachment is refused on its own row while the others proceed
  Given a Safety Officer is on the admin site and a pending report exists
  And the Safety Officer has that report open
  When the Safety Officer drops one ordinary private attachment and one larger than the private cap, at once
  Then the too-large attachment's staged row states the private cap and cannot be added
  And the ordinary attachment finishes uploading and offers a description box

@REQ-MED-077
@ui
Scenario: Adding beside a too-large private attachment adds only the ordinary one
  Given a Safety Officer is on the admin site and a pending report exists
  And the Safety Officer has dropped one ordinary private attachment and one larger than the private cap on that report at once, the ordinary one finished uploading
  When the Safety Officer adds the staged private attachments
  Then the private attachments section lists only the ordinary attachment

@REQ-MOD-176
@ui
Scenario: "Add N attachments" is disabled until every staged private attachment has settled
  Given a Safety Officer is on the admin site and a pending report exists
  And storage is slow to accept a private attachment
  And the Safety Officer has that report open
  When the Safety Officer stages the private attachment "slow-upload.zip"
  Then "Add 0 attachments" stays disabled while "slow-upload.zip" uploads

@REQ-MED-078
@ui
Scenario: "Add N attachments" is enabled once every staged private attachment has settled
  Given a Safety Officer is on the admin site and a pending report exists
  And storage is slow to accept a private attachment
  And the Safety Officer has staged the private attachment "slow-upload.zip" on that report, still uploading
  When storage finishes accepting the staged upload
  Then "Add 1 attachment" becomes enabled

@REQ-MOD-177
@ui
Scenario: Closing or reloading the report page with staged, un-added private attachments warns
  Given a Safety Officer is on the admin site and a pending report exists
  And the Safety Officer has staged the private attachment "unfinished.pdf" on that report, finished uploading
  When the Safety Officer tries to close or reload the tab
  Then the browser's own unload prompt appears, with no custom text

@REQ-MED-079
@ui
Scenario: Following a link away from the report page with staged, un-added private attachments asks first
  Given a Safety Officer is on the admin site and a pending report exists
  And the Safety Officer has staged the private attachment "unfinished.pdf" on that report, finished uploading
  When the Safety Officer navigates away from the report through a link
  Then a bilingual dialog asks whether to leave, offering to stay

@REQ-MED-080
@ui
Scenario: Choosing to stay keeps the report page with its staged private attachments
  Given a Safety Officer is on the admin site and a pending report exists
  And the Safety Officer has staged the private attachment "unfinished.pdf" on that report, finished uploading
  And the Safety Officer is asked whether to leave after following a link away from the report
  When they keep the page
  Then the Safety Officer stays on the report page

@REQ-MED-081
@ui
Scenario: Confirming leaving leaves the report page despite staged private attachments
  Given a Safety Officer is on the admin site and a pending report exists
  And the Safety Officer has staged the private attachment "unfinished.pdf" on that report, finished uploading
  And the Safety Officer is asked whether to leave after following a link away from the report
  When they confirm leaving
  Then the Safety Officer leaves the report page

@REQ-MOD-180
@ui
Scenario: A staged private attachment cannot be removed or re-described while it is being added
  Given a Safety Officer is on the admin site and a pending report exists
  And the report is slow to accept a private attachment
  And the Safety Officer has staged the private attachment "held.pdf" on that report, finished uploading
  When the Safety Officer adds the staged private attachments
  Then the staged attachment "held.pdf" can be neither removed nor re-described while it is added

@REQ-MED-082
@ui
Scenario: A staged private attachment is listed once the report has accepted it
  Given a Safety Officer is on the admin site and a pending report exists
  And the report is slow to accept a private attachment
  And the Safety Officer is adding the staged private attachment "held.pdf" to that report
  When the report finishes accepting the private attachment
  Then the private attachments section lists "held.pdf" only

@REQ-MOD-181
@ui
Scenario: Leaving the report page with only refused private attachments staged does not warn
  Given a Safety Officer is on the admin site and a pending report exists
  And the Safety Officer has dropped only a private attachment larger than the private cap on that report, its row refused
  When the Safety Officer reloads the report page
  Then the page reloads without warning, and the refused row is gone
