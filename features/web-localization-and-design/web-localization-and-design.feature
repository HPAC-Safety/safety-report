Feature: Web, localization, and design
  The public and admin sites are separately deployed static sites that render
  bilingual content, preserve local report state, and meet WCAG 2.2 AA.

  Scenario: The public and admin sites are independently deployed
    Given the product ships the public site and the admin site
    Then they have separate origins/distributions and deployment permissions
    And neither requires a SPA framework, client router, Node production server, or bundler

  Scenario Outline: The initial locale is selected in priority order
    Given a visitor has <signal>
    When the page loads
    Then the locale <chosen> is selected

    Examples:
      | signal                                            | chosen                        |
      | an explicit stored language choice of fr-CA        | the stored choice, fr-CA      |
      | no stored choice but a supported browser language of fr-CA | the browser language, fr-CA |
      | no stored choice and no supported browser language | English, as the fallback      |

  Scenario: Switching the language toggle rerenders without losing answers
    Given a reporter has entered answers in one locale
    When the reporter switches the language toggle
    Then labels, help, validation, navigation, and formatting rerender in the new locale
    And the document lang attribute and page title update
    And entered answers are neither cleared nor remapped

  Scenario: Application chrome strings come from committed locale catalogues
    Given the UI renders chrome or a stable validation/error message
    When the string is displayed
    Then it comes from a committed locale catalogue with key parity between en-CA and fr-CA
    And no user-facing literal appears directly in code

  Scenario: Question content comes from the bilingual database revision
    Given a question revision has English and French labels, help, and options authored by an Administrator
    When the form renders that question
    Then both languages come from the database revision
    And no runtime or CI auto-translation service produces question rendering

  Scenario: Only publication consent is marked required on the form
    Given the form renders its questions in database order
    When a reporter views the form
    Then only the consent_publish question displays required treatment
    And every optional question offers a natural blank/skipped state with no coerced answer
    And consent_publish has no selected default and requires an explicit yes or no

  Scenario: The form preserves local state and warns about attachments
    Given a reporter is filling out the form
    Then the browser preserves locale, revision IDs, and answers locally for 15 days
    And a privacy explanation is shown before submission
    And attachment selection appears last with type/count/size guidance and a warning that files are not restored after reload

  Scenario: Submission feedback is bounded and idempotent-looking
    Given a reporter submits the form
    When the request is in flight
    Then the UI shows bounded progress and disables accidental duplicate clicks
    And saved local state is cleared only after a definite 202 response

  Scenario: Client validation never replaces server validation
    Given the client validates a reporter's answer before submission
    When the API independently validates the same submission
    Then the API's validation is authoritative regardless of what the client allowed
    And both use the same stable type/option rules and localized messages

  Scenario: Public pages render only the public DTO
    Given the public site renders a published report
    When the page is built
    Then it renders only fields from the public DTO
    And the HTML/JS never receives private fields to hide client-side

  Scenario: The active locale controls which summary text is primary
    Given a published report has both ai_summary_en and ai_summary_fr
    When a visitor views it in a given locale
    Then that locale's text is shown first
    And the visitor can switch to the counterpart text

  Scenario: Admin pages distinguish private, ordinary, and output content
    Given a reviewer opens a report in the admin site
    Then private context, ordinary report content, summary output, processing failures, approval state, safe image/video derivatives, and unredacted private document downloads are all visibly distinguished
    And dangerous actions require clear confirmation
    And editing either summary text visibly invalidates approval
    And question editing explains that saving always creates a new immutable revision

  Scenario: Assets are self-hosted, never loaded from third-party CDNs
    Given the site renders fonts, styles, or imagery
    Then Aleo, Poppins, and other assets are served as self-hosted WOFF2/ committed files
    And no asset is loaded from a third-party CDN
    And the current logo is a placeholder that may only be replaced with an approved HPAC asset

  Scenario: Dark mode is a token redefinition, not duplicated markup
    Given a visitor's OS or stored preference requests dark mode
    When the page renders
    Then the same component markup is used with redefined CSS custom-property tokens
    And contrast, focus, error, disabled, and success states work in both themes and languages

  Scenario: The form meets baseline accessibility requirements
    Given a reporter uses assistive technology to complete the form
    Then every control has a programmatic label and usable keyboard order
    And groups use fieldset/legend
    And errors are linked to their fields and summarized
    And focus is visible and status updates use appropriate live regions
    And motion respects reduced-motion and touch targets/contrast are sufficient
    And media previews are never required to complete a report

  Scenario: Core content and navigation do not depend on JavaScript
    Given a visitor loads the site
    When JavaScript is unavailable
    Then core content and navigation still render

  Scenario: A JavaScript failure never exposes or erases report data
    Given a script error occurs while a reporter is filling out the form
    When the failure happens
    Then no private data is exposed
    And nothing is silently published
    And saved local answers are not erased

  Scenario: A network failure preserves local state and explains retry
    Given a submission request fails due to a network error
    When the browser detects the failure
    Then the browser keeps the local report state
    And explains to the reporter how to retry
