Feature: Web, localization, and design
The public form and the admin review queue are routes within one
React/TypeScript single-page application that renders bilingual content,
preserves local report state, and meets WCAG 2.2 AA.

@REQ-WLD-001
@ignore
@ui
Scenario: The admin review queue is a route on the one deployed site
  Given the product ships one website
  Then the public form and the admin review queue are routes within the same React/TypeScript application, built with Vite and served from one containerized deployment
  And loading the site requires JavaScript

@REQ-WLD-002
@ui
Scenario: The homepage header exposes navigation to reporting, submission, and contact, and a distinct member-login action
  Given a visitor loads the homepage
  Then the header shows links to view safety reports, submit a safety report, and contact
  And the header shows a visually distinct member-login action
  When a visitor activates any of those links or the member-login action
  Then the browser navigates to that destination's page

@REQ-WLD-003
@ui
Scenario: The contact page shows HPAC's organization details, mailing address, email, and social links
  Given a visitor loads the contact page
  Then the page shows the organization name and mailing address
  And the page shows an email link addressed to the current locale's contact address
  And the page shows Facebook, YouTube, and WhatsApp links that open in a new tab

@REQ-WLD-004
@ui
Scenario: On a mobile-width viewport, header navigation is reached through a hamburger toggle
  Given a visitor loads the homepage on a mobile-width viewport
  Then the header nav is hidden and a menu toggle is shown instead
  When the visitor activates the menu toggle
  Then a dialog containing the header's navigation links and member-login action opens
  When the visitor activates the menu toggle again
  Then the dialog closes

@REQ-WLD-005
@ui
Scenario Outline: The initial locale is selected in priority order
  Given a visitor has <signal>
  When the page loads
  Then the locale <chosen> is selected

Examples:
  | signal                                                     | chosen                      |
  | an explicit stored language choice of fr-CA                | the stored choice, fr-CA    |
  | no stored choice but a supported browser language of fr-CA | the browser language, fr-CA |
  | no stored choice and no supported browser language         | English, as the fallback    |

@REQ-WLD-006
@ui
Scenario: Switching the language toggle updates the document language and persists the choice
  Given a visitor is on any page
  When the visitor switches the language toggle
  Then the document lang attribute and page title update
  And the language choice persists to local storage across a reload

@REQ-WLD-007
@ui
Scenario: Switching the language toggle rerenders without losing answers
  Given a reporter has entered answers in one locale
  When the reporter switches the language toggle
  Then labels, help, validation, navigation, and formatting rerender in the new locale
  And the document lang attribute and page title update
  And entered answers are neither cleared nor remapped

@REQ-WLD-008
@ui
Scenario: A visitor can toggle and persist a light/dark theme choice
  Given a visitor has no stored theme preference
  Then the page follows the operating system's light/dark preference
  When the visitor toggles the theme control
  Then the data-theme attribute updates immediately
  And the header logo matches the active theme
  And the theme choice persists to local storage across a reload

@REQ-WLD-009
@ui
Scenario: The footer sits at the bottom of the viewport on a short page but below the fold on a long one
  Given a visitor loads a page whose content is shorter than the viewport
  Then the footer sits flush with the bottom of the viewport
  Given a visitor loads a page whose content is taller than the viewport
  Then the footer sits below the content, not pinned to the viewport

@REQ-WLD-010
Scenario: Application chrome strings come from committed locale catalogues
  Given the UI renders chrome or a stable validation/error message
  When the string is displayed
  Then it comes from a committed locale catalogue with key parity between en-CA and fr-CA
  And no user-facing literal appears directly in code

@REQ-WLD-011
Scenario: A translation missing locally is stubbed with a visible marker, and CI must replace it before merge
  Given a key exists in en-CA.json but not in fr-CA.json, or in fr-CA.json but not in en-CA.json
  When the local build runs, or a commit is made that stages a locales/ file
  Then the file missing that key gains it, with the other file's text prefixed with a # marker
  And a key still carrying that # marker fails locale verification, so it can never reach main untranslated

@REQ-WLD-012
Scenario: A French value edited by hand is recorded rather than overwritten
  Given a French value is edited by hand and its English is unchanged
  When the locales are verified
  Then the edit is accepted as a human correction
  And verification says it will be recorded and never machine-translated again

@REQ-WLD-013
Scenario: Editing both languages at once is one correction, not a conflict
  Given a key is edited in both en-CA.json and fr-CA.json
  When the locales are verified
  Then the edit is accepted as a human correction
  And neither language is overwritten

@REQ-WLD-014
@ignore
Scenario: Question content comes from the bilingual database revision
  Given a question revision has English and French labels, help, and options authored by an Administrator
  When the form renders that question
  Then both languages come from the database revision
  And no runtime or CI auto-translation service produces question rendering

@REQ-WLD-015
@ui
Scenario: Only publication consent is marked required on the form
  Given the form renders its questions in database order
  When a reporter views the form
  Then only the consent_publish question displays required treatment
  And every optional question offers a natural blank/skipped state with no coerced answer
  And consent_publish has no selected default and requires an explicit yes or no

@REQ-WLD-016
@ui
Scenario: The form explains local storage and warns about attachments
  Given a reporter is filling out the form
  Then a privacy explanation of the 15-day local storage is shown before submission
  And a notice states that signing in only confirms HPAC membership and that the report is not linked to their account
  And attachment selection appears last with type/count/size guidance and a warning that files are not restored after reload

@REQ-WLD-017
@ui
Scenario: The client shows inline validation before submission
  Given a reporter enters an answer
  When the client validates it before submission
  Then the client shows inline validation using the same stable type/option rules and localized messages the API uses

@REQ-WLD-018
@ignore
Scenario: Client validation never replaces server validation
  Given a submission reaches the API
  When the API independently validates it
  Then the API's validation is authoritative regardless of what the client allowed or displayed

@REQ-WLD-019
@ignore
@ui
Scenario: The active locale controls which summary text is primary
  Given a published report has both ai_summary_en and ai_summary_fr
  When a visitor views it in a given locale
  Then that locale's text is shown first
  And the visitor can switch to the counterpart text

@REQ-WLD-020
@ignore
@ui
Scenario: Admin pages distinguish private, ordinary, and output content
  Given a reviewer opens a report in the admin site
  Then private context, ordinary report content, summary output, processing failures, approval state, safe image/video derivatives, and unredacted private document downloads are all visibly distinguished
  And dangerous actions require clear confirmation
  And editing either summary text visibly invalidates approval
  And question editing explains that saving always creates a new immutable revision

@REQ-WLD-021
@ignore
Scenario: Assets are self-hosted, never loaded from third-party CDNs
  Given the site renders fonts, styles, or imagery
  Then Aleo, Poppins, and other assets are bundled and served from the site's own origin, WOFF2 vendored via a committed npm lockfile
  And no asset is loaded from a third-party CDN
  And the logo is the approved HPAC mark, as light/dark SVG variants

@REQ-WLD-022
@ignore
@ui
Scenario: Dark mode renders correctly in every state
  Given a visitor's OS or stored preference requests dark mode
  When the page renders
  Then contrast, focus, error, disabled, and success states work in both themes and languages

@REQ-WLD-023
@ui
Scenario: The form meets baseline accessibility requirements
  Given a reporter uses assistive technology to complete the form
  Then every control has a programmatic label and usable keyboard order
  And groups use fieldset/legend
  And errors are linked to their fields and summarized
  And focus is visible and status updates use appropriate live regions
  And motion respects reduced-motion and touch targets/contrast are sufficient
  And media previews are never required to complete a report

@REQ-WLD-024
@ui
Scenario: A JavaScript failure never exposes or erases report data
  Given a script error occurs while a reporter is filling out the form
  When the failure happens
  Then no private data is exposed
  And nothing is silently published
  And saved local answers are not erased

@REQ-WLD-025
@ui
Scenario: A network failure preserves local state and explains retry
  Given a submission request fails due to a network error
  When the browser detects the failure
  Then the browser keeps the local report state
  And explains to the reporter how to retry
