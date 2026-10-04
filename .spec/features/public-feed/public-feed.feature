Feature: Public feed
Visitors read published reports in the public feed and on each report's
own page, and search them in the site's language.

@REQ-MOD-036
Scenario: A published report carries only its approved summary and metadata
  Given a report is published
  When a visitor reads it
  Then the response contains only the opaque report ID, the English and French summary texts, the publication timestamp, the number of visible comments, the viewer-scoped attachment count, the language the report was written in, each public file's opaque id, kind, and — for a document only — coarse format, and the staff attachment list, null for this anonymous viewer
  And it never contains question keys, labels, answers, consent values, private flags, raw reports, attachment names, sizes, content types, keys, or URLs, member or reviewer identities, model provenance, or audit records

@REQ-MOD-150
Scenario: The feed's attachment count is the public count for an anonymous visitor
  Given a published report has one public attachment and one attachment only staff may see
  When an anonymous visitor lists the feed
  Then the report's attachment count is 1

@REQ-PUB-016
Scenario: The feed's attachment count is the full count for a Safety Officer
  Given a published report has one public attachment and one attachment only staff may see
  When a Safety Officer lists the feed
  Then the report's attachment count is 2

@REQ-MOD-155
Scenario: An ordinary member's token widens nothing
  Given a published report has one public attachment and one attachment only staff may see
  When a member with the User role lists the feed
  Then the report's attachment count is 1

@REQ-PUB-017
Scenario: An Administrator's token widens the feed's attachment count to the full count
  Given a published report has one public attachment and one attachment only staff may see
  When an Administrator lists the feed
  Then the report's attachment count is 2

@REQ-MOD-152
Scenario: A Safety Officer sees every attachment on the public report page, each marked public or not
  Given a published report has a public image and a hidden image
  When a Safety Officer reads that report's public page
  Then the response carries a staff attachment for each file, with its state and public visibility
  And the hidden file's visibility reads "hidden"
  And the public file's visibility reads "public"

@REQ-MOD-164
@ui
Scenario Outline: A published report page offers a link to its report detail for a reviewer
  Given <visitor> visits a published report's page
  Then the page offers a link to its report detail

Examples:
  | visitor            |
  | an Administrator   |
  | a Safety Officer   |

@REQ-PUB-018
@ui
Scenario Outline: A reviewer's link from a published report page opens its report detail in the same tab
  Given <visitor> visits a published report's page
  When the visitor follows the page's link to its report detail
  Then the browser opens its report detail, in the same tab

Examples:
  | visitor            |
  | an Administrator   |
  | a Safety Officer   |

@REQ-MOD-165
@ui
Scenario Outline: A published report page offers no admin link to a non-reviewer
  Given <visitor> visits a published report's page
  Then the page offers no link to the report detail

Examples:
  | visitor               |
  | a User                |
  | an anonymous visitor  |

@REQ-MOD-037
Scenario: The public feed lists only publishable reports, newest submitted first
  Given some reports are publishable and others are not
  When the public feed is queried
  Then the response is a deterministic paginated list containing only publishable reports
  And no non-publishable report ever appears
  And the list is newest submitted first, a tie broken by report ID, and each page names the cursor that continues it
  And no feed entry names its submission time
  And no cursor reveals a submission time

@REQ-MOD-038
Scenario: An unknown or non-public report id is not found
  Given a report id is unknown, deleted, pending, unpublished, or not consented
  When a visitor asks for that report
  Then the report is not found
  And non-public ids are indistinguishable from unknown ids

@REQ-MOD-153
@ui
Scenario: The public feed shows each report's attachment icon and count, omitted at zero
  Given the public feed holds a report with attachments and one with none
  When a visitor opens the public feed
  Then the report with attachments shows an attachment icon with its count, accessibly labelled
  And the report with none shows no attachment icon

@REQ-MOD-079
@ui
Scenario: Each report in the public feed opens at its own address
  Given the public feed has published reports
  When a visitor opens View safety reports and selects one
  Then the address bar shows "/reports/" followed by that report's ID
  And the page shows that report's full summary in the visitor's language

@REQ-MOD-080
@ui
Scenario: A report's address opens it directly
  Given a visitor has the address of a published report
  When the visitor opens that address directly
  Then the page shows that report's full summary

@REQ-PUB-019
@ui
Scenario: A report's page survives a reload
  Given a visitor has opened a published report's address directly
  When the page reloads
  Then the page still shows that report's full summary

@REQ-MOD-190
@ui
Scenario Outline: A published report page says its summary was translated from the other language the report was written in
  Given a report written in <written> is published
  When a visitor opens its page with the site in <shown>
  Then the page shows the muted label "<label>"

Examples:
  | written | shown   | label                     |
  | French  | English | Translated from French    |
  | English | French  | Traduit de l'anglais      |

@REQ-MOD-191
@ui
Scenario Outline: A published report page shows no translation label when the site's language is the one the report was written in
  Given a report written in <written> is published
  When a visitor opens its page with the site in <shown>
  Then the page shows no translation label

Examples:
  | written | shown   |
  | English | English |
  | French  | French  |

@REQ-MOD-192
@ui
Scenario: The translation label goes when the visitor switches to the report's own language, without a reload
  Given a report written in French is published
  And a visitor has its page open with the site in English, showing the muted label "Translated from French"
  When the visitor switches the site's language
  Then the page shows no translation label

@REQ-PUB-020
@ui
Scenario: The translation label returns when the visitor switches back to the other language, without a reload
  Given a report written in French is published
  And a visitor has its page open with the site in English, then switched to French
  When the visitor switches the site's language
  Then the page shows the muted label "Translated from French"

@REQ-MOD-193
Scenario Outline: A published report's own page carries the language it was written in
  Given a report written in <written> has been published
  When a visitor reads the report
  Then the response's language is "<code>"

Examples:
  | written | code  |
  | French  | fr-CA |
  | English | en-CA |

@REQ-PUB-021
Scenario Outline: The public feed carries no report's language
  Given a report written in <written> has been published
  When the public feed is queried
  Then no feed item carries a language

Examples:
  | written |
  | French  |
  | English |

@REQ-MOD-081
@ui
Scenario: An address for a report that is not public shows not found
  Given a report ID that is not found
  When a visitor opens the report address for that ID
  Then the page says the report was not found
  And it says nothing about whether such a report exists

@REQ-MOD-082
@ui
Scenario: The public feed loads more reports automatically
  Given the public feed has more published reports than fit on one page
  When a visitor reaches the end of the list
  Then the older reports load without a page change or an address change

@REQ-PUB-022
@ui
Scenario: Going back to the public feed restores the reports already loaded
  Given the public feed has more published reports than fit on one page
  And a visitor has reached the end of the list and the older reports have loaded
  When a visitor opens one of them and goes back
  Then the same reports are still shown, at the same place in the list

@REQ-MOD-178
@ui
Scenario: Opening the public feed afresh starts at its top and loads its first page again
  Given the public feed has more published reports than fit on one page
  And the visitor's window is too short to show the whole feed
  And a visitor has reached the end of the list
  When the visitor follows the footer's link to the contact page, then the one back to View safety reports
  Then the public feed asks for its first page again
  And the public feed is shown from its top

@REQ-MOD-126
@ui
Scenario Outline: The public feed's "Load more" fallback becomes visible once reached
  Given the public feed has more published reports than fit on one page
  And a visitor has the feed open
  When the visitor reaches the "Load more" action with the <key> key
  Then it becomes visible

Examples:
  | key |
  | Tab |

@REQ-PUB-023
@ui
Scenario: The public feed's "Load more" fallback is not visible until it is needed
  Given the public feed has more published reports than fit on one page
  When a visitor opens the feed
  Then the "Load more" action is not visible

@REQ-PUB-024
@ui
Scenario Outline: Activating the public feed's "Load more" fallback loads the older reports and announces them
  Given the public feed has more published reports than fit on one page
  And a visitor has reached the feed's "Load more" action
  When the visitor activates it with the <key> key
  Then the older reports load
  And a screen reader is told how many more reports loaded

Examples:
  | key   |
  | Enter |

@REQ-MOD-127
@ui
Scenario: The public feed offers a visible Retry action when its next page fails to load
  Given the public feed's next page fails to load
  When a visitor activates the "Load more" action before reaching the end of the list
  Then the feed offers a visible "Retry" action and does not fail silently

@REQ-MOD-083
@ui
Scenario: A reviewer can open a published report's public page
  Given a Safety Officer is on the admin site and a published report exists
  When the Safety Officer opens that report
  Then the report view links to the report's public address
  And a report that is not published shows no such link

@REQ-MOD-140
Scenario: Search matches the approved published summary in the visitor's site language
  Given a published report whose English summary says "The pilot landed in a field."
  When a visitor searches "landed" in English
  Then the report is listed among the results

@REQ-MOD-141
Scenario: Search matches a visible member comment as shown in the visitor's site language
  Given a published report carrying a member comment that says "Good reminder to check the fuel gauge."
  When a visitor searches "fuel gauge" in English
  Then the report is listed among the results

@REQ-MOD-142
Scenario: Search is scoped to the visitor's current site language only
  Given a published report whose French summary mentions a word its English summary does not
  When a visitor searches that French-only word in English
  Then the report is not listed among the results

@REQ-PUB-025
Scenario: Search finds a French-only word in French
  Given a published report whose French summary mentions a word its English summary does not
  When a visitor searches that French-only word in French
  Then the report is listed among the results

@REQ-MOD-143
Scenario Outline: The public search never widens by the member's role
  Given a published report whose summary contains a public word, and whose private answer, private note, and private attachment file name each hold their own word no summary or visible comment contains
  And another report is not publishable, and its summary contains a further private-only word
  When <who> searches for each private-only word
  Then no report is listed among the results, for every one of those searches

Examples:
  | who                  |
  | an anonymous visitor |
  | a User               |
  | a Safety Officer     |
  | an Administrator     |

@REQ-PUB-026
Scenario Outline: The public search finds a public word for every role
  Given a published report whose summary contains a public word, and whose private answer, private note, and private attachment file name each hold their own word no summary or visible comment contains
  And another report is not publishable, and its summary contains a further private-only word
  When <who> searches for the public word
  Then the report is listed among the results

Examples:
  | who                  |
  | an anonymous visitor |
  | a User               |
  | a Safety Officer     |
  | an Administrator     |

@REQ-MOD-144
Scenario Outline: A non-publishable report's summary text never matches
  Given a <status> report whose summary would otherwise match
  When a visitor searches its summary's distinctive word
  Then the report is not listed among the results

Examples:
  | status      |
  | pending     |
  | unpublished |
  | no-consent  |
  | deleted     |

@REQ-MOD-145
Scenario: A hidden comment never matches
  Given a published report carrying a comment that is later hidden
  When a visitor searches the hidden comment's distinctive word
  Then the report with the hidden comment is not listed among the results

@REQ-PUB-027
Scenario: A deleted comment never matches
  Given a published report carrying a comment that is later deleted
  When a visitor searches the deleted comment's distinctive word
  Then the report with the deleted comment is not listed among the results

@REQ-MOD-146
Scenario Outline: A typo or a missing accent still finds the best match
  Given a published report whose English summary says "The pilot landed in a field." and whose French summary says "Le pilote s'est posé dans un champ."
  When a visitor searches <query> in <language>
  Then the report is listed among the results

Examples:
  | query      | language |
  | "landde"   | English  |
  | "pose"     | French   |

@REQ-MOD-147
Scenario: Best match ranks first while a query is active
  Given a published report whose summary contains the exact phrase "hydraulic leak"
  And another published report whose summary only misspells "hydraulic leak"
  When a visitor searches "hydraulic leak" in English
  Then the exact match is ranked above the misspelled match

@REQ-MOD-148
Scenario: An empty search box lists newest submitted first, unchanged
  Given some reports are publishable and others are not
  When the public feed is queried with a blank search box
  Then a blank search box's first page is identical to the plain feed's first page

@REQ-MOD-149
@ui
Scenario: The search box sits at the top of the public feed, and its query is bookmarkable
  Given the public feed has published reports
  And a visitor has View safety reports open
  When the visitor searches for a term from the search box at the top of the page
  Then the address bar carries that search term as ?q=
  And only matching reports are listed

@REQ-PUB-028
@ui
Scenario: A search on the public feed survives a reload
  Given the public feed has published reports
  And a visitor has searched the public feed for a term
  When the page reloads
  Then the search box still shows that search term, and only matching reports are listed

@REQ-PUB-029
@ui
Scenario: Going back from a search on the public feed shows the full feed again
  Given the public feed has published reports
  And a visitor has searched the public feed for a term
  When the visitor goes back
  Then the search box is empty and the full feed is shown again

@REQ-MOD-210
@ui
Scenario: The public feed previews the first section's text, without its heading
  Given the public feed has a report whose summary has a "## Description" section and a second section
  When a visitor opens the public feed
  Then the report's preview shows the body of the first section as plain text
  And it shows no heading and no Markdown characters

Rule: A reporter's browser sees its own report before it is published

@REQ-PUB-001
Scenario: Another visitor never sees a report that is not published
  Given John has submitted a report that is not published
  And Cheryl, signed in or not, holds no receipt for it
  When Cheryl lists the public feed and asks for that report's page
  Then the feed does not list it and the page is not found
  And asking with a receipt that is not John's shows nothing either

@REQ-PUB-002
Scenario: Before a summary exists the holder's entry shows its submitted date and no summary
  Given John holds the receipt for a report whose summary the Worker has not produced yet
  When John's browser looks up its receipts
  Then the report is listed with its submitted date and no summary
  And the entry carries only the report ID, its submitted date, whether it is for publication, its summary, and its attachment count, never an answer, a question, or any member data

@REQ-PUB-003
Scenario: The holder reads the latest summary revision, approved or not
  Given John holds the receipt for a Pending report whose latest summary revision a reviewer edited and has not approved
  When John's browser looks up its receipts
  Then the entry carries that latest revision's English and French text
  And the entry is marked not yet published

@REQ-PUB-004
Scenario: A report without publication consent is listed for its holder and never has a summary
  Given John holds the receipt for a report whose reporter did not consent to publication
  When John's browser looks up its receipts
  Then the report is listed as not for publication, with no summary
  And it stays listed until a moderator deletes it

@REQ-PUB-005
Scenario: A report a moderator deleted disappears for its holder
  Given John holds the receipt for a report a moderator has deleted
  When John's browser looks up its receipts
  Then the report is not listed
  And the lookup tells the browser to drop that receipt

@REQ-PUB-006
Scenario: A published report is no longer listed as the holder's own
  Given John holds the receipt for a report that has since been published
  When John's browser looks up its receipts
  Then the report is not listed among his own
  And the lookup tells the browser to drop that receipt
  And the public feed lists the report for everyone

@REQ-PUB-015
Scenario: A report that was ever published never returns to its holder's own reports
  Given John holds the receipt for a report that was published and a reviewer has since unpublished it
  When John's browser looks up its receipts
  Then the report is not listed among his own
  And the lookup tells the browser to drop that receipt

@REQ-PUB-007
Scenario: A receipt that does not match its report shows nothing, like an unknown report
  Given a report that is not published
  When a lookup names it with a receipt that is not its own, a malformed receipt, or an unknown report
  Then each answer is the same: nothing is listed and the receipt is settled
  And no response says whether the report exists

@REQ-PUB-008
Scenario: The holder's attachments follow exactly the public rules
  Given John holds the receipt for a report with a verified image, a hidden image, an unverified image, and a validated document
  When the reporter consented to media and to documents
  Then the entry and page count and list only the verified image and the validated document
  And the image is offered only as its verified derivative and the document only as a forced download

@REQ-PUB-030
Scenario: The holder's attachments are not counted or listed without media consent
  Given John holds the receipt for a report with a verified image, a hidden image, an unverified image, and a validated document
  When the reporter did not consent to media
  Then no attachment is counted or listed

@REQ-PUB-009
Scenario: The holder opens their own report's page and its file links
  Given John holds the receipt for a report that is not published and has a public image
  When John asks for that report's page and for the image's link with his receipt
  Then the page carries its summary, submitted date, language, and image
  And the link is a pre-signed URL that lives at most 15 minutes
  And the same requests without his receipt are not found

@REQ-PUB-010
@ui
Scenario: The holder's own reports sit at the top of the first page of the feed, newest first
  Given the public feed has more published reports than fit on one page
  And a browser holds receipts for two of its own reports that are not published
  When the visitor opens View safety reports
  Then the two own reports are listed first, newest submitted first, each with its pill
  And the public feed follows them

@REQ-PUB-031
@ui
Scenario: The holder's own reports are not repeated among the later pages of the feed
  Given the public feed has more published reports than fit on one page
  And a browser holds receipts for two of its own reports that are not published
  And the visitor has View safety reports open
  When the visitor loads the later pages of the feed
  Then the own reports are still listed once, above the first page, and not among the later pages

@REQ-PUB-011
@ui
Scenario: Each own report says it is not yet published and shows its summary as a draft
  Given a browser holds receipts for one report with a summary, one still without a summary, and one without publication consent
  When the visitor opens View safety reports
  Then the first shows its submitted date, the pill "Not yet published", and its summary labelled as a draft that may change
  And the second shows its submitted date, the pill, and "Summary in preparation"
  And the third shows the pill "Not for publication" and no summary
  And no pill reads "Unpublished"

@REQ-PUB-012
@ui
Scenario: The browser drops a receipt once its report is published or gone
  Given a browser holds receipts for a report that has been published and one a moderator deleted
  When the visitor opens View safety reports
  Then neither is listed as the visitor's own
  And the browser no longer holds either receipt

@REQ-PUB-013
@ui
Scenario: A visitor without a receipt sees only the public feed and asks the server nothing
  Given a browser holds no receipt
  When the visitor opens View safety reports
  Then the feed lists only published reports
  And the browser sends no receipt lookup

@REQ-PUB-014
@ui
Scenario: The holder opens their own report's page
  Given a browser holds the receipt for a report that is not published
  When the visitor opens that report's address
  Then the page shows the report with its pill, its submitted date, and its summary labelled as a draft
  And the page offers no comments
  And the receipt is sent inside the request and never in any address
