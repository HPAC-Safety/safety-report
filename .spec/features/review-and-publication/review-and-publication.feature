Feature: Review and publication
A reviewer reviews a report and its summary
pair, keeps private notes on it, and publishes it. Only a fully approved,
consented, non-deleted report ever reaches the public feed.

@REQ-MOD-031
Scenario: A report detail exposes only what the reviewer needs
  Given a reviewer opens a report detail
  When the detail query runs
  Then it supplies the reporter language, exact bilingual question labels and each question's type, answers with privacy indicated, processing state, both summary texts with their shared provenance/approval, and each attachment's kind and whether it can be opened
  And it supplies no storage key and no link; an attachment is opened only through its own audited view or download request

@REQ-MOD-051
Scenario: Opening a report detail is audited
  Given a reviewer opens a report detail
  When the detail query runs
  Then an audit entry records the reviewer's token subject, ViewedRawReport, the report, and the time
  And the audit entry records no report content

@REQ-MOD-032
Scenario Outline: Editing a summary of a report that is not live saves a draft
  Given a <from> report whose reporter consented to publication
  When a reviewer edits either summary language
  Then the new revision is not approved
  And the report is Pending and not on the public feed

Examples:
  | from        |
  | Pending     |
  | Unpublished |

@REQ-MOD-194
Scenario: An edit saves a new revision that records its author
  Given a Pending report whose reporter consented to publication
  When a reviewer edits either summary language
  Then the summary has 2 revisions
  And the new revision records the reviewer's token subject and that its English text was written by a human
  And the first revision is unchanged

@REQ-MOD-195
Scenario: An edit to a live report stays published with the new text and the same publish date
  Given a Published report whose reporter consented to publication
  When a reviewer edits either summary language
  Then the report is still Published with the same publish date
  And the new revision is approved by the reviewer who saved it
  And the public feed shows the edited text

@REQ-MOD-196
Scenario: A rollback saves a new revision equal to the old one
  Given a Pending report whose reporter consented to publication
  And a reviewer has edited either summary language
  When a reviewer restores the first revision
  Then the summary has 3 revisions
  And the new revision has the first revision's text and sources and names it as restored from
  And the first and second revisions are unchanged
  And the restoring is audited as RolledBackSummary without any text

@REQ-MOD-197
Scenario: A rollback on a live report is published at once
  Given a Published report whose reporter consented to publication
  And a reviewer has edited either summary language
  When a reviewer restores the first revision
  Then the report is still Published with the same publish date
  And the public feed shows the first revision's text

@REQ-MOD-198
Scenario: A draft on a Pending report needs approval
  Given a Pending report whose reporter consented to publication
  And a reviewer has edited either summary language
  Then the report is not on the public feed
  When a reviewer publishes the pair
  Then the latest revision is approved by that reviewer
  And the public feed shows the edited text

@REQ-MOD-199
Scenario: No visitor ever sees an unapproved revision
  Given a Published report whose reporter consented to publication
  And a newer revision that nobody has approved exists
  When a visitor reads that report from the public feed
  Then the visitor sees the latest approved revision's text and never the draft

@REQ-MOD-200
Scenario: The history lists every revision with its author, time, and source
  Given a Pending report whose reporter consented to publication
  And a reviewer has edited either summary language
  And a reviewer has restored the first revision
  When a reviewer reads the summary history
  Then the history lists 3 revisions, newest first
  And each carries its author, its time, and how each language was written
  And the restored one names the revision it was restored from

@REQ-MOD-201
Scenario Outline: Only a reviewer edits or restores, and only to an earlier revision that exists
  Given a Pending report whose reporter consented to publication
  When <attempt>
  Then the request is refused with <status> and saves nothing

Examples:
  | attempt                                          | status |
  | a User edits the summary pair                    | 403    |
  | a User restores the first revision               | 403    |
  | a reviewer restores the current revision         | 400    |
  | a reviewer restores a revision that does not exist | 404  |

@REQ-MOD-205
Scenario: A save that changes neither language is refused
  Given a Pending report whose reporter consented to publication
  When a reviewer saves the summary pair unchanged
  Then the request is refused with 400 and saves nothing

@REQ-MOD-033
Scenario: Publishing approves the current summary pair once
  Given a reviewer publishes the current English/French summary pair
  When the publication is recorded
  Then it applies to that pair as a whole, not to one language
  And the approving token subject is recorded as an opaque string

@REQ-MOD-035
Scenario: Publication requires every guard to pass, with no bypass
  Given a report is non-deleted, has explicit positive consent, has two nonblank summary texts, and a reviewer publishes it
  When the publication is recorded
  Then the pair is approved and the report is Published in the same action
  And no Administrator, migration, background job, or direct API request can bypass any of these guards

@REQ-MOD-054
@ui
Scenario: Opening a report shows its answers with private answers marked, and its summary pair
  Given a Safety Officer is signed in and reports exist in several states
  When the Safety Officer opens Manage reports
  And the Safety Officer opens a pending report
  Then its answers are shown under their questions, with each private answer marked private
  And both the English and French summary texts are shown with the model and prompt version

@REQ-MOD-075
@ui
Scenario Outline: A date, time, or yes/no answer reads in the reviewer's language, not in its stored form
  Given a Safety Officer is signed in and a report with a <type> answer stored as "<stored>" exists
  And the interface language is <language>
  When the Safety Officer opens that report
  Then the answer reads "<shown>"
  And no translation is shown beside it

Examples:
  | type   | stored     | language | shown              |
  | date   | 2026-09-13 | English  | September 13, 2026 |
  | date   | 2026-09-13 | French   | 13 septembre 2026  |
  | time   | 14:30      | English  | 2:30 p.m.          |
  | time   | 14:30      | French   | 14 h 30            |
  | yes/no | true       | English  | Yes                |
  | yes/no | true       | French   | Oui                |
  | yes/no | false      | English  | No                 |
  | yes/no | false      | French   | Non                |

@REQ-MOD-076
@ui
Scenario: A stored date that is not a real date is shown as stored
  Given a Safety Officer is signed in and a report with a date answer stored as "2026-13-45" exists
  And the interface language is English
  When the Safety Officer opens that report
  Then the answer reads "2026-13-45"

@REQ-MOD-118
@ui
Scenario Outline: A phone answer reads formatted, and one stored before phone numbers were validated reads as stored
  Given a Safety Officer is signed in and a report with a phone answer stored as "<stored>" exists
  And the interface language is <language>
  When the Safety Officer opens that report
  Then the answer reads "<shown>"
  And no translation is shown beside it

Examples:
  | stored        | language | shown            |
  | +16045551234  | English  | +1 604 555 1234  |
  | +16045551234  | French   | +1 604 555 1234  |
  | +442079460018 | English  | +44 20 7946 0018 |
  | 604-555-1234  | English  | 604-555-1234     |

@REQ-MOD-055
Scenario Outline: Publishing a consented report's pair makes it public
  Given a <from> report whose reporter consented to publication
  When a reviewer publishes the pair
  Then the report becomes Published
  And the approval and the publication are recorded in one audited action

Examples:
  | from        |
  | Pending     |
  | Unpublished |

@REQ-MOD-057
Scenario Outline: Unpublishing takes a report off the public feed and keeps it for learning
  Given a <from> report whose reporter consented to publication
  When a reviewer unpublishes it
  Then it is no longer publishable
  And the pair's approval is cleared and the report is Unpublished
  And the report remains available for internal learning
  And the unpublishing is audited

Examples:
  | from      |
  | Pending   |
  | Published |

@REQ-MOD-058
Scenario: Unpublishing may carry a note that only reviewers see
  Given a reviewer unpublishes a report with a note
  When the unpublishing is recorded
  Then the report detail shows the note to reviewers
  And the note never reaches the public API, the audit log, or the application logs
  And unpublishing without a note also succeeds

@REQ-MOD-059
Scenario: A reviewer writes the pair by hand when summarization failed
  Given a report is Summary failed
  When a reviewer saves an English and a French summary text
  Then the report has one summary pair with "manual" as its model and prompt version
  And the report goes to Pending
  And writing the pair is audited

@REQ-MOD-060
Scenario: A review action based on a stale view is refused
  Given two reviewers opened the same report
  And the first reviewer has saved a change to it
  When the second reviewer sends a change based on the view they loaded
  Then the API answers 409 with a problem that asks them to reload
  And nothing the second reviewer sent is saved

@REQ-MOD-061
Scenario Outline: Every review action writes one content-free audit entry in its own transaction
  Given a report on which a reviewer can <action>
  When the reviewer does so
  Then one audit entry records the reviewer's token subject, <audit action>, the report, and the time
  And the entry records no summary text, answer, or unpublishing note

Examples:
  | action                | audit action      |
  | edit the summary pair | EditedSummary     |
  | publish the pair      | PublishedReport   |
  | unpublish the report  | UnpublishedReport |
  | write a manual pair   | EditedSummary     |
  | restore a summary version | RolledBackSummary |

@REQ-MOD-062
@ui
Scenario Outline: The report view offers only the actions its state allows
  Given a Safety Officer is signed in and a <status> report exists
  When the Safety Officer opens that report
  Then the offered actions are <actions>

Examples:
  | status              | actions                                  |
  | pending             | Edit summary, Publish, Unpublish, Delete |
  | published           | Edit summary, Unpublish, Delete          |
  | unpublished         | Edit summary, Publish, Delete            |
  | summary-failed      | Write summary, Delete                    |
  | private-unpublished | Delete                                   |

@REQ-MOD-063
@ui
Scenario: Editing a pending report's summary pair saves a draft
  Given a Safety Officer is signed in and a pending report exists
  When the Safety Officer opens that report
  And the Safety Officer edits the English summary and saves
  Then the report shows the "Pending" badge
  And the saved English text is shown

@REQ-MOD-206
@ui
Scenario: Editing a published report's summary keeps it Published
  Given a Safety Officer is signed in and a published report exists
  When the Safety Officer opens that report
  And the Safety Officer edits the English summary and saves
  Then the report shows the "Published" badge
  And the saved English text is shown

@REQ-MOD-207
@ui
Scenario: Save is offered only once a language has changed
  Given a Safety Officer is signed in and a pending report exists
  When the Safety Officer opens that report
  And the Safety Officer opens the summary editor
  Then Save summary is not offered
  When the Safety Officer changes the English text
  Then Save summary is offered

@REQ-MOD-202
@ui
Scenario: The report view lists the summary's revisions
  Given a Safety Officer is signed in and a pending report with four summary revisions exists
  When the Safety Officer opens that report
  Then the revision history lists four revisions, newest first
  And each shows its author, its time, and how each language was written
  And the restored revision says which revision it was restored from

@REQ-MOD-203
@ui
Scenario: Any revision can be viewed without changing the current summary
  Given a Safety Officer is signed in and a pending report with four summary revisions exists
  When the Safety Officer opens that report
  And the Safety Officer views the first revision
  Then that revision's English and French text is shown
  And the current summary is unchanged

@REQ-MOD-204
@ui
Scenario: Restoring a version asks for confirmation first
  Given a Safety Officer is signed in and a pending report with four summary revisions exists
  When the Safety Officer opens that report
  And the Safety Officer chooses Restore this version on the first revision
  Then a confirmation asks whether to restore that version
  And nothing has been restored yet
  When the Safety Officer confirms the restore
  Then the browser asks the API to restore that revision
  And the restored text is the current summary

@REQ-MOD-064
@ui
Scenario: Publishing a consented report shows it Published
  Given a Safety Officer is signed in and a pending report exists
  When the Safety Officer opens that report
  And the Safety Officer publishes it
  Then the report shows the "Published" badge

@REQ-MOD-065
@ui
Scenario: Unpublishing with a note shows the note on the report
  Given a Safety Officer is signed in and a pending report exists
  When the Safety Officer opens that report
  And the Safety Officer unpublishes it with the note "Duplicate of an earlier report"
  Then the report shows the "Unpublished" badge
  And the note "Duplicate of an earlier report" is shown

@REQ-MOD-066
@ui
Scenario: A stale action tells the reviewer to reload
  Given a Safety Officer is signed in and a pending report exists
  And another reviewer has changed that report since it was opened
  When the Safety Officer opens that report
  And the Safety Officer publishes it
  Then a message says the report changed and offers to reload it

@REQ-MOD-067
@ui
Scenario: Deleting a report asks for confirmation first
  Given a Safety Officer is signed in and a pending report exists
  When the Safety Officer opens that report
  And the Safety Officer chooses Delete
  Then a confirmation asks whether to delete the report
  When the Safety Officer confirms
  Then the browser returns to Manage reports

@REQ-MOD-068
@ui
Scenario: Opening an attachment requests its own audited link
  Given a Safety Officer is signed in and a pending report exists
  When the Safety Officer opens that report
  And the Safety Officer opens its document attachment
  Then the browser requests that attachment's download link

@REQ-MOD-069
Scenario Outline: Only a reviewer may request a machine translation
  Given a member signed in as <role>
  When that member requests a translation
  Then the API answers <outcome>

Examples:
  | role          | outcome       |
  | User          | forbidden     |
  | Safety Officer | a translation |
  | Administrator | a translation |

@REQ-MOD-070
Scenario Outline: Each summary language records how it was produced
  Given <situation>
  When the pair is saved
  Then the English text is recorded as <english> and the French text as <french>

Examples:
  | situation                                                                     | english   | french    |
  | the Worker produced the pair                                                  | generated | generated |
  | a reviewer edited only the English text of a summary pair                   | human     | generated |
  | a reviewer edited the English text and accepted its French translation        | human     | machine   |
  | a reviewer wrote both texts by hand after summarization failed                | human     | human     |
  | a reviewer wrote the French text by hand and accepted its English translation | machine   | human     |

@REQ-MOD-071
@ui
Scenario Outline: The editor offers a translate button for each language the reviewer changed
  Given a Safety Officer is signed in and a pending report exists
  When the Safety Officer opens that report
  And the Safety Officer opens the summary editor
  And the Safety Officer changes <changed>
  Then the translate buttons offered are <buttons>

Examples:
  | changed                     | buttons                                   |
  | nothing                     | none                                      |
  | the English text            | Translate to French                       |
  | the French text             | Translate to English                      |
  | the English and French text | Translate to French, Translate to English |

@REQ-MOD-072
@ui
Scenario: Translating asks before overwriting and shows what would change
  Given a Safety Officer is signed in and a pending report exists
  When the Safety Officer opens that report
  And the Safety Officer opens the summary editor
  And the Safety Officer changes the English text
  And the Safety Officer chooses Translate to French
  Then a confirmation shows the current French text and the proposed translation with their differences marked
  When the Safety Officer keeps the current text
  Then the French text is unchanged
  When the Safety Officer chooses Translate to French and accepts the translation
  Then the French text is the proposed translation
  And the Translate to English button is not offered for it

@REQ-MOD-073
@ui
Scenario: Writing a pair by hand offers the translate buttons too
  Given a Safety Officer is signed in and a summary-failed report exists
  When the Safety Officer opens that report
  And the Safety Officer chooses Write summary
  And the Safety Officer types the English text
  Then the translate buttons offered are Translate to French

@REQ-MOD-077
Scenario: The report detail gives a second language only for an answer that has one
  Given a submitted report answered a first name, an email, a date, a picker, and a narrative marked for translation
  And the Worker has translated the narrative
  When a reviewer opens the report detail
  Then the picker and the narrative each carry their second language
  And the first name, the email, and the date carry none, even if one was stored before this rule

@REQ-MOD-078
@ui
Scenario: Opening a report shows a translation only under answers that have one
  Given a Safety Officer is signed in and reports exist in several states
  When the Safety Officer opens Manage reports
  And the Safety Officer opens a pending report
  Then a translated narrative answer shows its translation beneath it
  And a name or email answer shows no translation line

@REQ-MOD-074
@ui
Scenario: The report view shows how each summary language was produced
  Given a Safety Officer is signed in and a report whose French text was machine-translated exists
  When the Safety Officer opens that report
  Then the English text is labelled as edited by a reviewer
  And the French text is labelled as machine-translated

# Private notes (ADR-0133). Staff-only plain text on a report: never
# summarized, translated, or published.

@REQ-MOD-098
Scenario Outline: Only a reviewer may keep private notes
  Given a report carrying one private note
  When <who> adds, lists, edits, reads the history of, and removes private notes on it
  Then the API answers <outcome> to every one of those requests

Examples:
  | who                  | outcome            |
  | an anonymous visitor | 401                |
  | a User               | 403                |
  | a Safety Officer     | with success       |
  | an Administrator     | with success       |

@REQ-MOD-099
Scenario Outline: Staff add any number of private notes to a report in any status
  Given a <status> report that staff keep private notes on
  When a Safety Officer adds two private notes and an Administrator adds a third
  Then all three private notes are listed, newest first
  And each lists its text, its writer's token subject, and when it was written
  And writing them queued no work for the Worker

Examples:
  | status         |
  | pending        |
  | published      |
  | unpublished    |
  | summary-failed |
  | no-consent     |

@REQ-MOD-100
Scenario: Editing a private note adds a revision and keeps every earlier one
  Given a Safety Officer wrote a private note on a report
  When an Administrator edits that private note twice
  Then the private note lists the latest text, written by the Administrator, marked as edited
  And its history lists all three revisions oldest first, each unchanged with its own text, writer, and time
  And an edit based on an earlier revision is refused with 409 and saves nothing

@REQ-MOD-101
Scenario: Removing a private note deletes it
  Given a Safety Officer wrote a private note on a report and edited it once
  When an Administrator removes that private note
  Then the private note is no longer listed, and editing it or reading its history answers 404
  And the private note and both its revisions are marked deleted at one time, and nothing is erased
  And one audit entry records the Administrator's token subject, RemovedPrivateNote, the note, and the time, without its text

@REQ-MOD-102
Scenario Outline: A private note is plain text of 1 to 4000 characters
  Given a pending report that staff keep private notes on
  When a Safety Officer adds a private note whose text is <text>
  Then the API answers 400 and no private note is stored

Examples:
  | text                 |
  | empty                |
  | only whitespace      |
  | 4001 characters long |

@REQ-MOD-103
Scenario: A deleted report's private notes go with it
  Given a report carrying one private note
  When a Safety Officer deletes that report
  Then the private note and its revision are marked deleted at the report's deletion time
  And adding, listing, or editing private notes on that report answers 404

@REQ-MOD-104
Scenario: No public or member read ever returns a private note, not even a count
  Given a published report whose reporter consented to publication and media carries one private note
  When an anonymous visitor and a User read the public feed, that report's public page, and its comments
  Then no response carries the private note's text or identifier, or any count of private notes
  And no database view other than admin_report_search_document reads a private-note table

@REQ-MOD-105
Scenario: A private note never reaches the model or a translation provider
  Given a consented report carrying one private note is due for summarization
  When the Worker claims the message and builds the model input DTO
  Then the model input carries nothing from the private note
  And no outbox message names the private note or its revision

@REQ-MOD-106
@ui
Scenario: A Safety Officer keeps private notes on the report page
  Given a Safety Officer is signed in and a pending report exists
  And another reviewer left the private note "Called the pilot; follow up Monday."
  When the Safety Officer opens that report
  Then the private notes section lists "Called the pilot; follow up Monday." with its writer and time
  When the Safety Officer adds the private note "Investigator report requested."
  Then "Investigator report requested." is listed first, marked as theirs
  When the Safety Officer edits that private note to "Investigator report received."
  Then that private note reads "Investigator report received." and is marked as edited
  And its history shows both revisions
  When the Safety Officer removes that private note and confirms
  Then "Investigator report received." is no longer listed

@REQ-MOD-208
@ui
Scenario: The admin review page renders a summary and its revision history as Markdown
  Given a Safety Officer and a report whose summary has a "## Description" section in each language
  When they open that report
  Then each language's summary shows a heading "Description" and its text as a paragraph
  And opening a version in the history shows its sections the same way

@REQ-MOD-209
@ui
Scenario: The admin report detail renders a long-text answer and its translation as Markdown
  Given a Safety Officer and a report with a long-text answer written in Markdown and its Worker translation
  When they open that report
  Then the answer shows its bold text as bold and its list as a list
  And its translation shows the same formatting
  And a short-text answer with Markdown characters is shown as written

@REQ-MOD-211
@ui
Scenario: Markdown support is not advertised to a reviewer editing a summary
  Given a Safety Officer and a report whose summary is Markdown
  When they start editing the summary
  Then each language is a plain text area holding the Markdown as written
  And no Markdown toolbar, preview, or hint appears
