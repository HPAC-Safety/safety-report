Feature: Admin report list and search
Staff list, filter, search, and act on reports from the admin report list.

@REQ-MOD-096
Scenario Outline: A report's consent reaches the admin view as true, false, or null
  Given a report whose publication consent is <publication> and whose media consent is <media>
  When a safety officer reads the report list and the report's detail
  Then the list row and the detail give consent as <consent json>
  And the detail gives media consent as <media json>

Examples:
  | publication | media     | consent json | media json |
  | given       | given     | true         | true       |
  | given       | refused   | true         | false      |
  | given       | not asked | true         | null       |
  | refused     | not asked | false        | null       |

@REQ-MOD-030
Scenario: The admin report list shows every live report with its state
  Given reports exist in every workflow state, one without publication consent, and one soft-deleted
  When a reviewer lists reports
  Then every live report appears, newest first, with its workflow status and whether publication consent was refused
  And the soft-deleted report does not appear
  And no answer text or summary text appears in the list, except the reporter's and pilot's names

@REQ-MOD-124
Scenario Outline: The admin report list shows the reporter's and pilot's names by stable role, blank when unanswered
  Given a report whose reporter first name is "<reporter first>", reporter last name is "<reporter last>", pilot first name is "<pilot first>", and pilot last name is "<pilot last>"
  When a reviewer lists reports
  Then the row's reporter name reads "<reporter name>"
  And the row's pilot name reads "<pilot name>"

Examples:
  | reporter first | reporter last | pilot first | pilot last | reporter name | pilot name  |
  | Alex            | Rivera        | Sam         | Chen       | Alex Rivera   | Sam Chen    |
  | Alex            | Rivera        | Alex        | Rivera     | Alex Rivera   | Alex Rivera |
  | Alex            |               |             |            | Alex          |             |
  |                 |               | Sam         | Chen       |               | Sam Chen    |
  |                 |               |             |            |               |             |

@REQ-MOD-119
Scenario: A list row carries the version a review command sends back
  Given reports exist in every workflow state
  When a reviewer lists reports
  Then each row carries the same version the report's detail view gives
  And publishing an unpublished report with its row's version succeeds without opening the report
  And no ViewedRawReport entry is written for that report

@REQ-MOD-129
Scenario: The admin report list pages forward with a keyset cursor, restarting from the top for an unreadable one
  Given reports exist in every workflow state
  When a reviewer lists reports
  And a reviewer lists reports after a cursor naming a report no longer in the queue
  Then that list starts with the same report the first page did

@REQ-MOD-049
Scenario: The Needs action filter shows pending, failed, and stuck reports
  Given reports exist in every workflow state
  And one report has waited in Submitted and one in Summarizing for more than 24 hours
  And one report has waited in Summarizing for less than 24 hours
  When a reviewer lists reports needing action
  Then the list holds the pending, summary-failed, and two stuck reports
  And each stuck report is marked stuck
  And the report summarizing for less than 24 hours is not listed

@REQ-MOD-050
Scenario Outline: A status filter narrows the admin report list
  Given reports exist in every workflow state, one without publication consent, and one soft-deleted
  When a reviewer lists reports with the <filter> filter
  Then the list holds only <reports>

Examples:
  | filter         | reports                                     |
  | published      | published reports                           |
  | private        | live reports whose reporter refused consent |
  | unpublished    | unpublished reports                         |
  | summary-failed | reports whose summarization failed          |

@REQ-MOD-084
Scenario: A reviewer reads how many reports need action
  Given reports exist in every workflow state
  And one report has waited in Submitted and one in Summarizing for more than 24 hours
  When a SafetyOfficer reads the pending counts
  Then the reports count equals the number of reports the Needs action filter lists
  And the counts carry no answers-awaiting-translation count

@REQ-MOD-085
Scenario: Only an Administrator's pending counts include answers awaiting translation
  Given an answer is awaiting machine translation
  When an Administrator reads the pending counts
  Then the translation count equals the number of answers in the translation queue

@REQ-MOD-086
Scenario: A User cannot read the pending counts
  When a User reads the pending counts
  Then the API refuses the pending counts with 403

@REQ-MOD-090
Scenario: A report without publication consent never needs action
  Given a report whose reporter did not consent to publication is Unpublished
  When a SafetyOfficer lists reports needing action and reads the pending counts
  Then that report is not listed
  And the reports count does not include it

@REQ-MOD-151
Scenario: The admin report list carries every non-deleted attachment's count
  Given a report has one hidden attachment and one still-processing attachment
  When a reviewer lists reports
  Then the row's attachment count is 2

@REQ-MOD-052
@ui
Scenario: The Manage reports page lists reports with a status badge and a Private badge
  Given a safety officer is signed in and reports exist in several states
  When the safety officer opens Manage reports
  Then each report shows its submission time and a badge for its workflow status
  And a report whose reporter refused consent also shows a "Private (no consent)" badge
  And a stuck report shows a "Stuck" badge

@REQ-MOD-125
@ui
Scenario: Manage reports shows each row's reporter and pilot names, blank when unanswered
  Given a safety officer is signed in and reports exist in several states
  When the safety officer opens Manage reports
  Then the pending row shows reporter name "Alex Rivera" and pilot name "Sam Chen"
  And the published row shows no reporter or pilot name

@REQ-MOD-154
@ui
Scenario: Manage reports shows each row's attachment icon and count, omitted at zero
  Given a safety officer is signed in and Manage reports holds a report with attachments and one with none
  When the safety officer opens Manage reports
  Then the row with attachments shows an attachment icon with its count, accessibly labelled
  And the row with none shows no attachment icon

@REQ-MOD-053
@ui
Scenario: Choosing a filter on Manage reports narrows the list
  Given a safety officer is signed in and reports exist in several states
  When the safety officer opens Manage reports
  And the safety officer chooses the "Published" filter
  Then only published reports are listed
  And the chosen filter stays in the address bar

@REQ-MOD-120
@ui
Scenario Outline: Each row of Manage reports offers the quick actions its state allows
  Given a safety officer is signed in and reports exist in several states
  When the safety officer opens Manage reports
  Then the <row> row offers <actions>

Examples:
  | row                 | actions           |
  | pending             | Publish, Delete   |
  | published           | Unpublish, Delete |
  | unpublished         | Publish, Delete   |
  | private-unpublished | Delete            |
  | summary-failed      | Delete            |
  | stuck               | Delete            |

@REQ-MOD-121
@ui
Scenario: Publishing and unpublishing from the list updates the row in place
  Given a safety officer is signed in and reports exist in several states
  When the safety officer opens Manage reports
  And the safety officer publishes the pending row
  Then the pending row shows the "Published" badge and offers Unpublish
  When the safety officer unpublishes the published row
  Then the published row shows the "Unpublished" badge and offers Publish
  And each row action sent the version its row was listed with

@REQ-MOD-122
@ui
Scenario: Deleting from the list asks for confirmation first
  Given a safety officer is signed in and reports exist in several states
  When the safety officer opens Manage reports
  And the safety officer chooses Delete on the pending row
  Then a confirmation asks whether to delete it
  When the safety officer keeps the report
  Then the pending row is still listed and nothing was deleted
  When the safety officer chooses Delete on the pending row
  And the safety officer confirms
  Then the pending row is no longer listed and it was deleted

@REQ-MOD-123
@ui
Scenario: A stale row action tells the reviewer to reload the list
  Given a safety officer is signed in and reports exist in several states
  And another reviewer has changed the pending report since the list was loaded
  When the safety officer opens Manage reports
  And the safety officer publishes the pending row
  Then a message says the report changed and offers to reload the list
  And the pending row still shows the "Pending" badge

@REQ-MOD-130
Scenario Outline: Searching Manage reports finds a report matched by any part of it
  Given a report carries a distinct word in its <source>
  When a reviewer searches for that word
  Then the report is found

Examples:
  | source                          |
  | private answer                  |
  | choice label                    |
  | summary pair                    |
  | private note                    |
  | member comment                  |
  | reporter-uploaded attachment name |
  | staff-only attachment name       |

@REQ-MOD-131
Scenario: A misspelled search still finds the report
  Given a report carries a distinct word in its narrative
  When a reviewer searches for a misspelling of that word
  Then the report is found

@REQ-MOD-132
Scenario: A search matches across English and French stemming
  Given a report carries a distinct word in its narrative
  When a reviewer searches for that word
  Then the report is found

@REQ-MOD-133
Scenario: The best match is listed first
  Given two reports share a word, one repeating it and one only carrying a near-miss typo of it
  When a reviewer searches for that word
  Then the report repeating the word is listed before the one with the typo

@REQ-MOD-135
Scenario: A search stays within the chosen filter
  Given a pending report and a published report share a distinct word
  When a reviewer searches for that word within the published filter
  Then only the published report is found

@REQ-MOD-138
Scenario Outline: Only a reviewer may find a match inside private report content
  Given a report carries a distinct word only in its private answer, its private note, and its staff-only attachment name
  When <who> searches for that word
  Then <result>

Examples:
  | who                  | result                                 |
  | an anonymous visitor | the request is refused as unauthorized |
  | a member             | the request is refused as forbidden    |
  | a safety officer     | the report is found                    |
  | an administrator     | the report is found                    |

@REQ-MOD-139
Scenario: The search query text is never logged
  Given a report carries a distinct word in its narrative
  When a reviewer searches for that word
  Then the query text never appears in anything the host logs

@REQ-MOD-134
@ui
Scenario: Clearing the search box returns to newest submitted first
  Given a safety officer is signed in and reports exist in several states
  When the safety officer opens Manage reports
  And the safety officer searches for "Alex"
  And the safety officer clears the search box
  Then every report is listed newest first as before the search

@REQ-MOD-136
@ui
Scenario: The search text lives in the address bar and survives a reload
  Given a safety officer is signed in and reports exist in several states
  When the safety officer opens Manage reports
  And the safety officer searches for "Alex"
  Then the address bar carries "q=Alex"
  When the safety officer reloads the page
  Then the search box still reads "Alex"

@REQ-MOD-137
@ui
Scenario: A search matching nothing shows a message naming the query, not an error
  Given a safety officer is signed in and reports exist in several states
  When the safety officer opens Manage reports
  And the safety officer searches for a word that matches nothing
  Then a message says no reports match that search
  And no error is shown

@REQ-MOD-179
@ui
Scenario: Opening Manage reports afresh loads its first page again, not the list kept from earlier
  Given a safety officer is signed in and more reports exist than fit on one page
  When the safety officer goes to another page and opens Manage reports again from the Admin menu
  Then Manage reports asks for its first page again

@REQ-MOD-128
@ui
Scenario: Manage reports loads more automatically and offers the same hidden fallback and visible retry
  Given a safety officer is signed in and more reports exist than fit on one page
  Then the "Load more" action is not visible
  When a keyboard visitor tabs to the "Load more" action
  Then it becomes visible
  When that visitor activates it
  Then the older reports load without leaving Manage reports
  Given the next report page fails to load
  When the safety officer activates the "Load more" action
  Then the list offers a visible "Retry" action instead of failing silently
