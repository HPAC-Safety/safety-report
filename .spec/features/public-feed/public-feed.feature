Feature: Public feed
Visitors read published reports in the public feed and on each report's
own page, and search them in the site's language.

@REQ-MOD-036
Scenario: The public DTO exposes only the approved summary and its metadata
  Given a report is published
  When the public API returns it
  Then the response contains only the opaque report ID, ai_summary_en, ai_summary_fr, the publication timestamp, the number of visible comments, the viewer-scoped attachment count, the language the report was written in, each public file's opaque id, kind, and — for a document only — coarse format, and the staff attachment list, null for this anonymous viewer
  And it never contains question keys, labels, answers, consent values, private flags, raw reports, attachment names, sizes, content types, keys, or URLs, member or reviewer identities, model provenance, or audit records

@REQ-MOD-150
Scenario: The feed's attachment count is the public count for a visitor and the full count for staff
  Given a published report has one public attachment and one attachment only staff may see
  When an anonymous visitor lists the feed
  Then the report's attachment count is 1
  When a signed-in safety officer lists the feed
  Then the report's attachment count is 2

@REQ-MOD-155
Scenario: An ordinary member's token widens nothing; only SafetyOfficer or Administrator does
  Given a published report has one public attachment and one attachment only staff may see
  When a signed-in member with the User role lists the feed
  Then the report's attachment count is 1
  When a signed-in Administrator lists the feed
  Then the report's attachment count is 2

@REQ-MOD-152
Scenario: A signed-in safety officer sees every attachment on the public report page, each marked public or not
  Given a published report has a public image and a hidden image
  When a signed-in safety officer asks the public API for that report
  Then the response carries a staff attachment for each file, with its state and public visibility
  And the hidden file's visibility reads "hidden"
  And the public file's visibility reads "public"

@REQ-MOD-164
@ui
Scenario Outline: A published report page offers a same-tab link to its admin detail page for a reviewer
  Given <visitor> visits a published report's page
  Then the page offers a link to that report's admin detail page
  When the visitor activates that link
  Then the browser opens the report's admin detail page, in the same tab

Examples:
  | visitor                    |
  | a signed-in Administrator  |
  | a signed-in SafetyOfficer  |

@REQ-MOD-165
@ui
Scenario Outline: A published report page offers no admin link to a non-reviewer
  Given <visitor> visits a published report's page
  Then the page offers no link to the admin detail page

Examples:
  | visitor              |
  | a signed-in User      |
  | a signed-out visitor  |

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
Scenario: An unknown or non-public report id returns 404
  Given a report id is unknown, deleted, pending, unpublished, or not consented
  When the public API is asked for that report
  Then the API returns 404
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
  Then the address bar shows /reports/ followed by that report's ID
  And the page shows that report's full summary in the visitor's language

@REQ-MOD-080
@ui
Scenario: A report's address opens it directly and survives a reload
  Given a visitor has the address of a published report
  When the visitor opens that address directly
  Then the page shows that report's full summary
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
Scenario: The translation label follows the header's language toggle without a reload
  Given a report written in French is published
  And a visitor has its page open with the site in English
  Then the page shows the muted label "Translated from French"
  When the visitor switches the site's language
  Then the page shows no translation label
  When the visitor switches the site's language
  Then the page shows the muted label "Translated from French"

@REQ-MOD-193
Scenario Outline: A published report's own page carries the language it was written in, and the feed does not
  Given a report written in <written> has been published
  When the public API returns the report
  Then the response's language is "<code>"
  When the public feed is queried
  Then no feed item carries a language

Examples:
  | written | code  |
  | French  | fr-CA |
  | English | en-CA |

@REQ-MOD-081
@ui
Scenario: An address for a report that is not public shows not found
  Given a report ID the public API answers with 404
  When a visitor opens /reports/ followed by that ID
  Then the page says the report was not found
  And it says nothing about whether such a report exists

@REQ-MOD-082
@ui
Scenario: The public feed loads more reports automatically, and going back restores them
  Given the public feed has more published reports than fit on one page
  When a visitor scrolls to the end of the list
  Then the older reports load without a page change or an address change
  When a visitor opens one of them and goes back
  Then the same reports are still shown, at the same scroll position

@REQ-MOD-178
@ui
Scenario: Opening the public feed afresh starts at its top and loads its first page again
  Given the public feed has more published reports than fit on one page
  And the visitor's window is too short to show the whole feed
  When a visitor scrolls to the end of the list
  And the visitor follows the footer's link to the contact page, then the one back to View safety reports
  Then the public feed asks for its first page again
  And the public feed is shown from its top

@REQ-MOD-126
@ui
Scenario: The public feed's next page offers a keyboard-only fallback and announces itself
  Given the public feed has more published reports than fit on one page
  When a visitor opens the feed
  Then the "Load more" action is not visible
  When a keyboard visitor tabs to the "Load more" action
  Then it becomes visible
  When that visitor activates it
  Then the older reports load
  And a screen reader is told how many more reports loaded

@REQ-MOD-127
@ui
Scenario: The public feed offers a visible Retry action when its next page fails to load
  Given the public feed's next page fails to load
  When a visitor activates the "Load more" action without scrolling
  Then the feed offers a visible "Retry" action instead of failing silently

@REQ-MOD-083
@ui
Scenario: A reviewer can open a published report's public page
  Given a safety officer is signed in and a published report exists
  When the safety officer opens that report
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
  When a visitor searches that French-only word in French
  Then the report is listed among the results

@REQ-MOD-143
Scenario Outline: The public search never widens by caller role
  Given a published report whose summary contains a public word, and whose private answer, private note, and private attachment file name each hold their own word no summary or visible comment contains
  And another report is not publishable, and its summary contains a further private-only word
  When <who> searches for the public word
  Then the report is listed among the results
  When <who> searches for each private-only word
  Then no report is listed among the results, for every one of those searches

Examples:
  | who                  |
  | an anonymous visitor |
  | a User               |
  | a SafetyOfficer      |
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
Scenario: A hidden or a deleted comment never matches
  Given a published report carrying a comment that is later hidden
  And another published report carrying a comment that is later deleted
  When a visitor searches the hidden comment's distinctive word
  Then the report with the hidden comment is not listed among the results
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
  When a visitor opens View safety reports
  And the visitor types a search term into the search box at the top of the page
  Then the address bar carries that search term as ?q=
  And only matching reports are listed
  When the page reloads
  Then the search box still shows that search term, and only matching reports are listed
  When the visitor goes back
  Then the search box is empty and the full feed is shown again

@REQ-MOD-210
@ui
Scenario: The public feed previews the first section's text, without its heading
  Given the public feed has a report whose summary has a "## Description" section and a second section
  When a visitor opens the public feed
  Then the report's preview shows the body of the first section as plain text
  And it shows no heading and no Markdown characters
