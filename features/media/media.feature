Feature: Attachments
  A reporter may attach images, videos, and documents to the finalized
  report. Every attachment stays private, is validated by content rather than
  by name, and only images/videos get a safe reviewer-facing derivative.

  Background:
    Given the maximum attachment count is configurable and defaults to five across all attachment kinds
    And each file is limited to 50 MB

  Scenario Outline: Only allowlisted content types are accepted
    Given an attachment part has detected content type <mime>
    When the API validates it
    Then the attachment is accepted as an allowlisted <kind>

    Examples:
      | mime                                                                     | kind     |
      | image/jpeg                                                               | image    |
      | image/png                                                                | image    |
      | image/webp                                                               | image    |
      | image/heic                                                               | image    |
      | video/mp4                                                                | video    |
      | video/quicktime                                                          | video    |
      | application/pdf                                                          | document |
      | application/msword                                                       | document |
      | application/vnd.openxmlformats-officedocument.wordprocessingml.document  | document |
      | application/rtf                                                          | document |
      | text/markdown                                                            | document |
      | text/plain                                                               | document |
      | application/vnd.oasis.opendocument.text                                  | document |

  Scenario: Declared content type must agree with detected content type
    Given an attachment's declared content type differs from its detected, allowlisted type
    When the API validates the attachment
    Then the API rejects the attachment
    And the file extension and client filename are never trusted as the basis for acceptance

  Scenario: The client filename never leaves the HTTP boundary
    Given a reporter uploads a file with a client-supplied filename
    When the API accepts the attachment
    Then the client filename is not persisted, logged, placed in an exception, used in a key, sent to the model, or returned to an admin
    And the object key encodes only an opaque report/file identity and a managed compartment

  Scenario: An accepted attachment starts in a private quarantine compartment
    Given an attachment part passes request and count bounds
    When the API ingests it
    Then the API mints an opaque filename, streams the file through a bounded counter and signature sniffer, and writes accepted bytes to a private quarantine key

  Scenario: Unreferenced quarantine blobs expire automatically
    Given a quarantine blob was written during a submission whose transaction never committed
    When the storage lifecycle rule runs
    Then the unreferenced quarantine blob expires

  Scenario: Every image is re-encoded to strip metadata
    Given an accepted image attachment enters Worker processing
    When the Worker produces its derivative
    Then the image is decoded and re-encoded into a supported safe representation
    And EXIF, GPS, profiles, comments, thumbnails, and other metadata are removed

  Scenario: Every video is remuxed or transcoded to strip metadata
    Given an accepted video attachment enters Worker processing
    When the Worker produces its derivative
    Then the video is decoded/remuxed or transcoded through a controlled toolchain that removes container metadata, location, device, creation, and filename fields
    And a byte-for-byte copy of the original video is never used as the derivative

  Scenario: A document is validated and scanned but never transformed
    Given an accepted document attachment enters Worker processing
    When the Worker processes it
    Then the Worker scans it for known malware and validates its actual format, including internal package shape for DOCX/ODT and bounded text decoding for Markdown/plain text
    And the Worker never extracts its text, and the document is never sent to the model and never published
    And the document remains the reporter-supplied original, available for private download, and the review UI labels it as unredacted private evidence

  Scenario: Each attachment fails and processes independently of the report
    Given a report has multiple attachments, one of which is slow or corrupt
    When the Worker processes the report's outbox items
    Then each file's processing is an independent outbox item
    And the slow or corrupt file neither rolls back the valid report nor forces an additional AI call

  Scenario: A reviewer gets a short-lived URL only for successfully processed
    media
    Given an image or video attachment has finished processing successfully
    When an authorized reviewer requests to view it
    Then the reviewer receives a short-lived read URL to the derivative
    And the response forces download with a server-minted display name and the header X-Content-Type-Options: nosniff

  Scenario: A reviewer downloads a validated document as an unredacted original
    Given a document attachment has passed validation and malware scanning
    When an authorized reviewer requests it
    Then the reviewer receives a short-lived URL to the private original
    And the admin site does not embed or inline-render the document content
    And the reviewer is warned that the document is unredacted before download

  Scenario: A failed attachment is inaccessible to reviewers
    Given signature validation, malware scanning, decoding, metadata removal, re-encoding/remuxing, writing, or verification fails for an attachment
    When the Worker finishes processing it
    Then the file is marked failed
    And the file is inaccessible to any reviewer

  Scenario: Attachments are never exposed publicly, even after publication
    Given a report has been published
    When the public API returns the report
    Then the public DTO contains no file counts, types, keys, or links
