Feature: Authentication and roles
Members present a signed token and get one of three roles from its claims.
The server authorizes every operation, and audits sign-in and sensitive admin
actions without report content.

@REQ-MOD-001
@ui
Scenario: In development the sign-in page offers no third-party sign-in option
  Given a visitor activates the member sign-in action
  Then the sign-in page shows an email box, a password box, and a sign-in action
  And the email box is an email input, so a phone offers its email keyboard
  And the sign-in page shows no third-party sign-in option

@REQ-MOD-002
@ui
Scenario: Where a third-party provider is configured, the sign-in page offers it
  Given a third-party sign-in provider is configured
  When a visitor activates the member sign-in action
  Then the sign-in page also shows a third-party sign-in option

@REQ-MOD-003
@ui
Scenario: Signing in with member credentials returns a session
  Given a visitor signs in with valid member credentials
  Then the header shows a sign-out action and no member sign-in action

@REQ-AUTH-001
@ui
Scenario: A session from member credentials survives a reload
  Given a visitor signs in with valid member credentials
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
  Then further attempts are refused as too frequent, with a safe retry signal
  And the refusal does not reveal whether any attempted username or password was valid

@REQ-MOD-006
@ui
Scenario: Signing in from the member sign-in page starts a session
  Given a visitor signs in from the member sign-in page
  Then the header shows a sign-out action and no member sign-in action

@REQ-AUTH-002
@ui
Scenario: A session started from the member sign-in page persists across a reload
  Given a visitor signs in from the member sign-in page
  When the page reloads
  Then the header still shows the sign-out action

@REQ-AUTH-003
@ui
Scenario: Signing out clears the member's session
  Given a visitor signs in from the member sign-in page
  When the visitor activates the sign-out action
  Then the header shows the member sign-in action again

@REQ-MOD-007
@ui
Scenario: An Administrator's header shows an Admin menu
  Given a visitor signs in as an Administrator
  Then the header shows an Admin menu and no other header nav change

@REQ-AUTH-004
@ui
Scenario: An Administrator's Admin menu offers every item
  Given a visitor signs in as an Administrator
  When the visitor activates the Admin menu
  Then it opens with manage-reports, review-type-ahead-values, and manage-questions items

@REQ-MOD-092
@ui
Scenario: A Safety Officer's Admin menu offers reports and type-ahead review
  Given a visitor signs in as a Safety Officer
  When the visitor activates the Admin menu
  Then it opens with manage-reports and review-type-ahead-values items
  And it offers no manage-questions item

@REQ-MOD-009
@ui
Scenario: A User sees no Admin menu
  Given a visitor signs in as a User
  Then the header shows a sign-out action
  And the header shows no Admin menu

@REQ-MOD-010
@ui
Scenario: An open Admin menu keeps every item on a single line
  Given a visitor signs in from the member sign-in page
  When the visitor activates the Admin menu
  Then every item is on one line and none is truncated

@REQ-MOD-011
@ui
Scenario Outline: Activating an Admin menu item navigates to its page
  Given a visitor signs in from the member sign-in page
  And the visitor activates the Admin menu
  When the visitor activates the <item> item
  Then the browser navigates to the <destination> page

Examples:
  | item             | destination      |
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
  Given 3 reports need action
  And a visitor signs in as an Administrator
  Then the Admin menu shows a count of 3

@REQ-AUTH-005
@ui
Scenario: An Administrator's open Admin menu counts the reports waiting on their item
  Given 3 reports need action
  And a visitor signs in as an Administrator
  When the visitor activates the Admin menu
  Then the manage-reports item shows a count of 3
  And the manage-questions item shows no count

@REQ-MOD-093
@ui
Scenario: A Safety Officer's Admin menu counts reports and type-ahead values waiting
  Given 4 reports need action and 3 type-ahead values await review
  And a visitor signs in as a Safety Officer
  Then the Admin menu shows a count of 7

@REQ-AUTH-006
@ui
Scenario: A Safety Officer's open Admin menu counts each kind of waiting work on its item
  Given 4 reports need action and 3 type-ahead values await review
  And a visitor signs in as a Safety Officer
  When the visitor activates the Admin menu
  Then the manage-reports item shows a count of 4
  And the review-type-ahead-values item shows a count of 3

@REQ-MOD-089
@ui
Scenario: With nothing waiting, the Admin menu shows no count
  Given 0 reports need action
  And a visitor signs in as an Administrator
  Then the Admin menu shows no count

@REQ-AUTH-007
@ui
Scenario: With nothing waiting, no item of the open Admin menu shows a count
  Given 0 reports need action
  And a visitor signs in as an Administrator
  When the visitor activates the Admin menu
  Then no item shows a count

@REQ-MOD-013
Scenario: A token signed by an unknown key is refused
  Given a bearer token signed with an untrusted key
  When it is presented with a request that needs a member
  Then the token is refused
  And it does not disclose why the token was refused

@REQ-MOD-014
Scenario: A token whose signature has been altered is refused
  Given a validly issued bearer token whose signature segment has been changed
  When it is presented with a request that needs a member
  Then the token is refused

@REQ-MOD-015
Scenario: An expired token is refused
  Given a bearer token whose expiry has passed
  When it is presented with a request that needs a member
  Then the token is refused

@REQ-MOD-016
Scenario: A token for the wrong audience is refused
  Given a bearer token issued for a different audience
  When it is presented with a request that needs a member
  Then the token is refused

@REQ-MOD-017
Scenario: A token with no recognized role claim authenticates as User
  Given a validly signed bearer token carrying no recognized role claim
  When it is presented with a request
  Then the request is authenticated
  And the identity has the User role and no administrative capability

@REQ-MOD-018
Scenario: A member's name, email, or any other claim is never read
  Given a validly signed bearer token carrying a name, an email, and a picture claim
  When the member's identity is established
  Then it reads only the token subject and the role claim
  And no other claim reaches domain code, a log, or the database

@REQ-MOD-019
Scenario: Development sign-in does not exist outside development
  Given a deployment outside development
  When a development token is requested
  Then the request is not found

@REQ-MOD-020
Scenario Outline: A development sign-in verified against the members site resolves role from the email lists
  Given development sign-in is available
  And "<email>" is <listed>
  When that email signs in with credentials the members site accepts
  Then the member receives a signed development token with the <role> role

Examples:
  | email                       | listed                                  | role           |
  | admin@example.test          | on the development Administrator list   | Administrator  |
  | officer@example.test        | on the development Safety Officer list  | Safety Officer |
  | nobody-special@example.test | on neither development list             | User           |

@REQ-MOD-021
Scenario: Bad members-site credentials show the same generic failure as bad fixed-account credentials
  Given development sign-in is available
  When a sign-in is attempted with credentials the members site does not accept
  Then the sign-in fails with one generic invalid-credentials failure
  And nothing distinguishes it from an unknown fixed development account

@REQ-MOD-022
Scenario: A members-site outage during a development sign-in is reported distinctly from bad credentials
  Given development sign-in is available
  When the members site cannot be reached during a sign-in attempt
  Then the sign-in reports the members site as unavailable
  And it does not report invalid credentials

@REQ-MOD-023
Scenario: An unauthenticated request for admin data is refused before it is handled
  Given a request carries no bearer token
  When it asks for admin data
  Then it is refused as unauthenticated before it is handled

@REQ-MOD-024
Scenario: Every operation is authorized by the server, not just the interface
  Given a member without the required role calls an admin operation
  When the request is processed
  Then the operation is refused as forbidden, whatever the interface would have shown

@REQ-MOD-156
Scenario: An environment with no identity provider configured still starts and answers public requests
  Given a deployment outside development with no identity provider configured
  When the health check is requested
  Then the health check is answered with success

@REQ-AUTH-008
Scenario: An environment with no identity provider configured refuses every bearer token
  Given a deployment outside development with no identity provider configured
  When a request carrying a bearer token asks for something that needs authorization
  Then it is refused as unauthenticated before it is handled

@REQ-MOD-157
Scenario: With the temporary interim issuer enabled, a member signs in with their members-site credentials
  Given a deployment outside development with the temporary interim issuer enabled
  When a member signs in with credentials the members site accepts
  Then the member receives a token that later requests are accepted with
  And an allowlisted Administrator account's token carries the Administrator role

@REQ-AUTH-009
Scenario: With the temporary interim issuer enabled, the fixed development accounts do not exist
  Given a deployment outside development with the temporary interim issuer enabled
  When a sign-in is attempted with the fixed development Administrator account
  Then the sign-in is refused

@REQ-MOD-158
Scenario: With the temporary interim issuer disabled, its discovery document does not exist
  Given a deployment outside development with the temporary interim issuer disabled
  When the interim issuer's discovery document is requested
  Then it is not found

@REQ-AUTH-010
Scenario: With the temporary interim issuer disabled, its JWKS does not exist
  Given a deployment outside development with the temporary interim issuer disabled
  When the interim issuer's JWKS is requested
  Then it is not found

@REQ-AUTH-011
Scenario: With the temporary interim issuer disabled, it issues no token
  Given a deployment outside development with the temporary interim issuer disabled
  When a token is requested from the interim issuer
  Then it is not found

@REQ-MOD-159
Scenario: The temporary interim issuer's JWKS publishes only a public key
  Given a deployment outside development with the temporary interim issuer enabled
  When the interim issuer's JWKS is requested
  Then the response carries only a public key, never a private key

@REQ-MOD-025
Scenario Outline: A User may only submit a report
  Given a member has the User role
  When that member attempts to <capability>
  Then that member's attempt is <outcome>

Examples:
  | capability                     | outcome              |
  | submit an occurrence report    | allowed              |
  | read the report list           | refused as forbidden |
  | read a report's private detail | refused as forbidden |
  | obtain an attachment link      | refused as forbidden |
  | edit a report's summary        | refused as forbidden |
  | publish a report               | refused as forbidden |
  | unpublish a report             | refused as forbidden |
  | delete a report                | refused as forbidden |
  | create a question revision     | refused as forbidden |
  | edit a question's choices      | refused as forbidden |

@REQ-MOD-026
Scenario Outline: A Safety Officer reviews and publishes but does not author questions
  Given a member has the Safety Officer role
  When that member attempts to <capability>
  Then that member's attempt is <outcome>

Examples:
  | capability                     | outcome              |
  | submit an occurrence report    | allowed              |
  | read the report list           | allowed              |
  | read a report's private detail | allowed              |
  | obtain an attachment link      | allowed              |
  | edit a report's summary        | allowed              |
  | publish a report               | allowed              |
  | unpublish a report             | allowed              |
  | delete a report                | allowed              |
  | create a question revision     | refused as forbidden |
  | edit a question's choices      | refused as forbidden |

@REQ-MOD-027
Scenario Outline: An Administrator has every Safety Officer capability and authors questions
  Given a member has the Administrator role
  When that member attempts to <capability>
  Then that member's attempt is <outcome>

Examples:
  | capability                     | outcome              |
  | submit an occurrence report    | allowed              |
  | read the report list           | allowed              |
  | read a report's private detail | allowed              |
  | obtain an attachment link      | allowed              |
  | edit a report's summary        | allowed              |
  | publish a report               | allowed              |
  | unpublish a report             | allowed              |
  | delete a report                | allowed              |
  | create a question revision     | allowed              |
  | edit a question's choices      | allowed              |

@REQ-MOD-028
Scenario Outline: Only an Administrator may author a question revision
  Given a member has the <role> role
  When that member attempts to create a question revision
  Then that member's attempt is <outcome>

Examples:
  | role           | outcome              |
  | User           | refused as forbidden |
  | Safety Officer | refused as forbidden |
  | Administrator  | allowed              |

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
Scenario Outline: A member without the required role sees a forbidden page, not the not-found page or the page's content
  Given a visitor signs in as a <role>
  When the visitor navigates directly to the <page> page, which their role cannot use
  Then the page shows a forbidden view in place of that page's content
  And it is not the not-found page
  And no request for that page's data is made, the Admin menu's pending counts aside

Examples:
  | role           | page             |
  | User           | manage-reports   |
  | User           | manage-questions |
  | Safety Officer | manage-questions |

@REQ-MOD-184
@ui
Scenario: There is no admin page left to edit an answer's translation by hand
  Given a visitor signs in as an Administrator
  When the visitor navigates directly to the old answer-translations page
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
  When the attempt is refused
  Then an audit entry records a sign-in-failed action and the time
  And it never records the attempted credentials
  And the actor is recorded as the attempted identity, not left blank

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
Scenario: Signing out sends nothing to the server
  Given a member activates the sign-out action
  When the client discards its token
  Then no request reaches the server for that sign-out

@REQ-MOD-091
Scenario: Sign-out is not an audited event
  Given every route the server answers
  Then none of them signs a member out
  And no audit action records a sign-out
