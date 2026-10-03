Feature: Authentication and roles
Members present a signed token and get one of three roles from its claims.
The API authorizes every operation, and audits sign-in and sensitive admin
actions without report content.

@REQ-MOD-001
@ui
Scenario: In development the sign-in page offers no third-party sign-in option
  Given a visitor activates the member sign-in action
  Then the sign-in page shows an email field, a password field, and a sign-in action
  And the email field is an email input, so a phone offers its email keyboard
  And the sign-in page shows no third-party sign-in option

@REQ-MOD-002
@ui
Scenario: Where a third-party provider is configured, the sign-in page offers it
  Given the API reports that a third-party provider is configured
  When a visitor activates the member sign-in action
  Then the sign-in page also shows a third-party sign-in option

@REQ-MOD-003
@ui
Scenario: Signing in with member credentials returns a session that survives a reload
  Given a visitor signs in with valid member credentials
  Then the header shows a sign-out action instead of the member sign-in action
  When the page reloads
  Then the header still shows the sign-out action

@REQ-MOD-004
@ui
Scenario: Bad credentials show one generic failure and no session
  Given a visitor submits credentials that are not valid
  Then the sign-in page shows one generic failure message
  And the failure does not say whether the email or the password was wrong
  And the header still shows the member sign-in action

@REQ-MOD-005
Scenario: Repeated sign-in attempts for one identity are rate limited
  Given repeated sign-in attempts arrive for the same username
  When the sign-in rate limit for that identity is exceeded
  Then the API rejects further attempts with 429 and a safe retry signal
  And the rejection does not reveal whether any attempted username or password was valid

@REQ-MOD-006
@ui
Scenario: A member's signed-in session persists across a reload and clears on sign-out
  Given a visitor signs in from the member sign-in page
  Then the header shows a sign-out action instead of the member sign-in action
  When the page reloads
  Then the header still shows the sign-out action
  When the visitor activates the sign-out action
  Then the header shows the member sign-in action again

@REQ-MOD-007
@ui
Scenario: An Administrator's Admin menu offers every option
  Given a visitor signs in as an Administrator
  Then the header shows an Admin menu and no other header nav change
  When the visitor activates the Admin menu
  Then it opens with manage-reports, review-type-ahead-values, and manage-questions options

@REQ-MOD-092
@ui
Scenario: A Safety Officer's Admin menu offers reports and type-ahead review
  Given a visitor signs in as a Safety Officer
  When the visitor activates the Admin menu
  Then it opens with manage-reports and review-type-ahead-values options
  And it offers no manage-questions option

@REQ-MOD-009
@ui
Scenario: A User sees no Admin menu
  Given a visitor signs in as a User
  Then the header shows a sign-out action
  And the header shows no Admin menu

@REQ-MOD-010
@ui
Scenario: An open Admin menu keeps every option on a single line
  Given a visitor signs in from the member sign-in page
  When the visitor activates the Admin menu
  Then every option is on one line and none is truncated

@REQ-MOD-011
@ui
Scenario Outline: Activating an Admin menu option navigates to its page
  Given a visitor signs in from the member sign-in page
  When the visitor activates the Admin menu
  And the visitor activates the <option> option
  Then the browser navigates to the <destination> page

Examples:
  | option           | destination      |
  | Manage reports   | manage-reports   |
  | Manage questions | manage-questions |

@REQ-MOD-012
@ui
Scenario: The Admin menu is absent for an anonymous visitor
  Given a visitor loads the homepage
  Then the header shows no Admin menu

@REQ-MOD-087
@ui
Scenario: An Administrator's Admin menu shows how much work is waiting
  Given the API counts 3 reports needing action
  And a visitor signs in as an Administrator
  Then the Admin menu shows a count of 3
  When the visitor activates the Admin menu
  Then the manage-reports option shows a count of 3
  And the manage-questions option shows no count

@REQ-MOD-093
@ui
Scenario: A Safety Officer's Admin menu counts reports and type-ahead values waiting
  Given the API counts 4 reports needing action and 3 type-ahead values awaiting review
  And a visitor signs in as a Safety Officer
  Then the Admin menu shows a count of 7
  When the visitor activates the Admin menu
  Then the manage-reports option shows a count of 4
  And the review-type-ahead-values option shows a count of 3

@REQ-MOD-089
@ui
Scenario: With nothing waiting, the Admin menu shows no count
  Given the API counts 0 reports needing action
  And a visitor signs in as an Administrator
  Then the Admin menu shows no count
  When the visitor activates the Admin menu
  Then no option shows a count

@REQ-MOD-013
Scenario: A token signed by an unknown key is rejected
  Given a bearer token signed with a key the API does not trust
  When it is presented to any authenticated endpoint
  Then the API refuses the request
  And it does not disclose why the token was refused

@REQ-MOD-014
Scenario: A token whose signature has been altered is rejected
  Given a validly issued bearer token whose signature segment has been changed
  When it is presented to any authenticated endpoint
  Then the API refuses the request

@REQ-MOD-015
Scenario: An expired token is rejected
  Given a bearer token whose expiry has passed
  When it is presented to any authenticated endpoint
  Then the API refuses the request

@REQ-MOD-016
Scenario: A token for the wrong audience is rejected
  Given a bearer token issued for a different audience
  When it is presented to any authenticated endpoint
  Then the API refuses the request

@REQ-MOD-017
Scenario: A token with no recognized role claim authenticates as User
  Given a validly signed bearer token carrying no recognized role claim
  When it is presented to the API
  Then the request is authenticated
  And the identity has the User role and no administrative capability

@REQ-MOD-018
Scenario: The API never reads a name, an email, or any other claim
  Given a validly signed bearer token carrying a name, an email, and a picture claim
  When the API establishes the member's identity
  Then it reads only the token subject and the role claim
  And no other claim reaches domain code, a log, or the database

@REQ-MOD-019
Scenario: The development token endpoint does not exist outside development
  Given the API is not running in development
  When the development token endpoint is called
  Then the route does not exist

@REQ-MOD-020
Scenario Outline: A development sign-in verified against the members site resolves role from the email lists
  Given the development token endpoint is available
  And "<email>" is <listed>
  When that email signs in with credentials the members site accepts
  Then the API returns a signed development token with the <role> role

Examples:
  | email                       | listed                                 | role          |
  | admin@example.test          | on the development Administrator list  | Administrator |
  | officer@example.test        | on the development safety-officer list | SafetyOfficer |
  | nobody-special@example.test | on neither development list            | User          |

@REQ-MOD-021
Scenario: Bad members-site credentials show the same generic failure as bad fixed-account credentials
  Given the development token endpoint is available
  When a sign-in is attempted with credentials the members site does not accept
  Then the API returns one generic invalid-credentials failure
  And nothing distinguishes it from an unknown fixed development account

@REQ-MOD-022
Scenario: A members-site outage during a development sign-in is reported distinctly from bad credentials
  Given the development token endpoint is available
  When the members site cannot be reached during a sign-in attempt
  Then the API reports the members site as unavailable
  And it does not report invalid credentials

@REQ-MOD-023
Scenario: An unauthenticated request to an admin endpoint is refused before the handler
  Given a request carries no bearer token
  When it reaches an admin endpoint
  Then the API refuses it before the handler runs

@REQ-MOD-024
Scenario: Every operation is authorized by the API, not just the UI
  Given a member without the required role calls an admin operation
  When the API processes the request
  Then the API rejects the operation regardless of what the UI would have shown

@REQ-MOD-156
Scenario: An environment with no identity provider configured still starts and serves its public endpoints, and refuses every bearer token
  Given the API is not running in development and no identity provider is configured
  When the health endpoint is requested
  Then the API answers 200
  When a request carrying a bearer token reaches an authorization-protected endpoint
  Then the API refuses it before the handler runs

@REQ-MOD-157
Scenario: With the temporary interim issuer enabled, a member signs in with their members-site credentials, and the fixed development accounts do not exist
  Given the API is not running in development and the temporary interim issuer is enabled
  When a member signs in with credentials the members site accepts
  Then the API issues a token the API itself accepts
  And an allowlisted Administrator account's token carries the Administrator role
  When a sign-in is attempted with the fixed development Administrator account
  Then the API refuses it

@REQ-MOD-158
Scenario: With the temporary interim issuer disabled, none of its endpoints exist
  Given the API is not running in development and the temporary interim issuer is disabled
  When the interim issuer's discovery document is requested
  Then the API answers 404
  When the interim issuer's JWKS is requested
  Then the API answers 404
  When a token is requested from the token endpoint
  Then the API answers 404

@REQ-MOD-159
Scenario: The temporary interim issuer's JWKS publishes only a public key
  Given the API is not running in development and the temporary interim issuer is enabled
  When the interim issuer's JWKS is requested
  Then the response carries only a public key, never a private key field

@REQ-MOD-025
Scenario Outline: A User may only submit a report
  Given a member has the User role
  When that member attempts to <capability>
  Then the API <outcome> the attempt

Examples:
  | capability                     | outcome |
  | submit an occurrence report    | allows  |
  | list the review queue          | forbids |
  | read a report's private detail | forbids |
  | obtain an attachment link      | forbids |
  | edit a report's summary        | forbids |
  | publish a report               | forbids |
  | unpublish a report             | forbids |
  | delete a report           | forbids |
  | create a question revision     | forbids |
  | edit a question's choices      | forbids |

@REQ-MOD-026
Scenario Outline: A Safety Officer reviews and publishes but does not author questions
  Given a member has the Safety Officer role
  When that member attempts to <capability>
  Then the API <outcome> the attempt

Examples:
  | capability                     | outcome |
  | submit an occurrence report    | allows  |
  | list the review queue          | allows  |
  | read a report's private detail | allows  |
  | obtain an attachment link      | allows  |
  | edit a report's summary        | allows  |
  | publish a report               | allows  |
  | unpublish a report             | allows  |
  | delete a report           | allows  |
  | create a question revision     | forbids |
  | edit a question's choices      | forbids |

@REQ-MOD-027
Scenario Outline: An Administrator has every Safety Officer capability and authors questions
  Given a member has the Administrator role
  When that member attempts to <capability>
  Then the API <outcome> the attempt

Examples:
  | capability                     | outcome |
  | submit an occurrence report    | allows  |
  | list the review queue          | allows  |
  | read a report's private detail | allows  |
  | obtain an attachment link      | allows  |
  | edit a report's summary        | allows  |
  | publish a report               | allows  |
  | unpublish a report             | allows  |
  | delete a report           | allows  |
  | create a question revision     | allows  |
  | edit a question's choices      | allows  |

@REQ-MOD-028
Scenario Outline: Only an Administrator may author a question revision
  Given a member has the <role> role
  When that member attempts to create a question revision
  Then the API <outcome> the attempt

Examples:
  | role           | outcome |
  | User           | rejects |
  | Safety Officer | rejects |
  | Administrator  | accepts |

@REQ-MOD-029
Scenario: Sensitive admin actions are audited without report content
  Given a sensitive read or material mutation occurs in the admin application
  When the action completes
  Then an audit entry records the acting token subject, action, target, and time
  And the token subject is stored as an opaque string that joins to no user record
  And it never records report content

@REQ-MOD-042
@ui
Scenario: An anonymous visitor who navigates to an admin route is sent to sign in
  Given an anonymous visitor
  When the visitor navigates directly to an admin route
  Then the browser is redirected to the member sign-in page
  And no admin page content is shown first

@REQ-MOD-043
@ui
Scenario Outline: A member without the required role sees a real 403, not a 404 or the page content
  Given a visitor signs in as a <role>
  When the visitor navigates directly to <route>, which their role cannot use
  Then the page shows a forbidden (403) view in place of the route's content
  And it is not the not-found page
  And no request for that route's data is made, the Admin menu's pending counts aside

Examples:
  | role           | route                      |
  | User           | /admin/reports             |
  | User           | /admin/questions           |
  | Safety Officer | /admin/questions           |

@REQ-MOD-184
@ui
Scenario: There is no admin page left to edit an answer's translation by hand
  Given a visitor signs in as an Administrator
  When the visitor navigates directly to /admin/answer-translations
  Then the page shows the not-found view

@REQ-MOD-044
Scenario: A successful sign-in writes an audit row
  Given a member signs in with valid credentials
  When the sign-in succeeds
  Then an audit entry records the token subject, a sign-in-succeeded action, and the time
  And it never records the credentials

@REQ-MOD-045
Scenario: A failed sign-in attempt writes an audit row
  Given a sign-in attempt uses credentials that are not valid
  When the attempt is rejected
  Then an audit entry records a sign-in-failed action and the time
  And it never records the attempted credentials
  And the actor is recorded as the attempted identity rather than left blank

@REQ-MOD-046
Scenario: A reviewer's attachment view writes its own audit row, distinct from a raw-report view
  Given a reviewer opens a private attachment
  When the view completes
  Then an audit entry records an attachment-viewed action naming that attachment as the target
  And it is distinguishable from a raw-report-viewed audit entry for the same report

@REQ-MOD-047
Scenario: A failed audit write blocks the action it would have recorded
  Given an Administrator or reviewer performs an action that must be audited
  When the audit row fails to write
  Then the action itself does not commit
  And the member sees the action as failed, not succeeded

@REQ-MOD-048
@ui
Scenario: Signing out sends nothing to the API
  Given a member activates the sign-out action
  When the client discards its token
  Then no request reaches the API for that sign-out

@REQ-MOD-091
Scenario: Sign-out is not an audited event
  Given the API's mapped routes
  Then none of them signs a member out
  And no audit action records a sign-out
