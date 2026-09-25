Feature: AI anonymization
The Worker makes exactly one versioned model call per summary attempt. That
call both summarizes and anonymizes the report and returns one
English/French summary pair.

@REQ-AI-001
Scenario: Exactly one model call summarizes and anonymizes a report
  Given a report has been submitted and its summarization outbox item is due
  When the Worker processes the summarization attempt
  Then the Worker makes exactly one call to the model
  And that call produces both the English and French summary texts
  And no second model call, separate PII-audit call, or translation call runs

@REQ-AI-002
Scenario: An exact private value in report content is deterministically marked before the model call
  Given a private answer's value appears verbatim in a report_content field
  When the Worker builds the marked report_content
  Then that occurrence is replaced with a marker naming the private question it came from
  And the replacement happens before the model call, not as a separate call or stage

@REQ-AI-003
Scenario: A token from a multi-word private value is also marked
  Given a private answer's value is multiple words
  And one of its words, at or above the minimum match length and not on the stopword list, appears alone in a report_content field
  When the Worker builds the marked report_content
  Then that occurrence is replaced with a marker naming the private question it came from

@REQ-AI-004
Scenario: A common short word is never marked as a false positive
  Given a report_content field contains a word that is below the minimum match length or on the stopword list
  And that word also appears as a token of a private answer's value
  When the Worker builds the marked report_content
  Then that word is left unmarked

@REQ-AI-005
Scenario: Overlapping candidate matches resolve longest match first
  Given a report_content field contains a private answer's whole multi-word value verbatim
  When the Worker builds the marked report_content
  Then the whole value is replaced with a single marker
  And its individual words are not separately marked inside that same span

@REQ-AI-006
Scenario: Matching is case-insensitive and whitespace-normalized
  Given a private answer's value appears in a report_content field with different casing or extra whitespace
  When the Worker builds the marked report_content
  Then that occurrence is still replaced with a marker

@REQ-AI-007
Scenario: private_context is still supplied alongside the marking pass
  Given the Worker has built the marked report_content for a report
  When the Worker builds the model input DTO
  Then private_context still contains every private answered field, unchanged
  And the model receives both the marked report_content and the unmarked private_context

@REQ-AI-028
Scenario: A private yes/no answer is never a marking candidate
  Given a private yes/no answer is true
  And a report_content field contains the word "true"
  When the Worker builds the marked report_content
  Then that word is left unmarked
  And private_context still carries the yes/no answer as true

@REQ-AI-029
Scenario Outline: A yes/no answer reaches the model as true or false, never as words
  Given a report written in <language> answers an ordinary yes/no question <answer>
  When the Worker claims the message and builds the model input DTO
  Then report_content carries that answer's value as "<answer>"

Examples:
  | language | answer |
  | English  | true   |
  | French   | false  |

@REQ-AI-008
Scenario: Concurrent workers cannot claim the same summarization outbox item twice
  Given a summarization outbox item is pending
  When two Worker instances attempt to claim it concurrently
  Then exactly one Worker claims the item
  And the other Worker finds no work and makes no model call

@REQ-AI-009
Scenario: Only eligible, labeled fields reach the model
  Given a report has non-private answered fields and private answered fields
  When the Worker claims the message and builds the model input DTO
  Then report_content contains only non-private answered fields eligible to contribute facts
  And private_context contains only private answered fields, supplied to help recognize identifying details that recur in report content
  And skipped/null answers, both system consent answers, and file-upload answers are excluded from both arrays
  And the DTO contains no attachment bytes, document text, storage keys, admin data, audit data, deleted content, or client filenames

@REQ-AI-011
Scenario: The Worker accepts only the exact two-field JSON response
  Given the model returns a response for a summarization attempt
  When the Worker validates the response
  Then a response with exactly two nonblank string fields "ai_summary_en" and "ai_summary_fr" is accepted
  And a response with a Markdown fence, commentary, an extra key, a null field, or only one language is rejected

@REQ-AI-016
Scenario: Documents never reach the model
  Given a report has document attachments
  When the Worker builds the summarization input
  Then no document or document-derived text is sent to the model
  And document text is not extracted, summarized, translated, or anonymized

@REQ-AI-017
Scenario: A valid response is persisted as one pair-level summary row
  Given the model returns a valid two-field response
  When the Worker persists it
  Then one summary row is created or replaced with AiSummaryEn, AiSummaryFr, shared model and prompt_version provenance, and creation/update timestamps
  And no separate row is created per locale

@REQ-AI-019
Scenario: Retries repeat the single-call operation without adding stages
  Given a summarization attempt fails with a transient provider error or invalid output
  When the outbox retries the attempt within its bounded budget
  Then the retry repeats the single model call
  And no repair or audit call is added

@REQ-AI-020
Scenario: Exhausted retries surface a manually authorable failure
  Given a report's summarization retry budget is exhausted
  When the Worker gives up on the attempt
  Then the report becomes SummaryFailed with a safe operational error
  And the report appears in the review queue
  And a human can author both summary texts manually and continue review

@REQ-AI-021
Scenario: Sensitive summarization data is never logged
  Given a summarization attempt runs, succeeds, or fails
  When the Worker emits application logs
  Then prompts, model responses, private context, and raw report content are never written to those logs

@REQ-AI-022
Scenario: The Worker requests the configured model at the configured reasoning level
  Given the Worker is configured with a provider, a model, and a reasoning level
  When the Worker makes the summarization call
  Then the call names the configured model and asks for the configured reasoning level
  And the call asks the provider for a JSON object response
  And the call leaves the sampling temperature at the provider's default

@REQ-AI-023
Scenario Outline: A Worker holding a key refuses to start with an unusable provider configuration
  Given the Worker has a model provider key
  And its provider configuration has <problem>
  When the Worker starts
  Then startup fails before any report is claimed

Examples:
  | problem                                        |
  | a provider no strategy is registered for       |
  | a blank model                                  |
  | a reasoning level other than low, medium, high |

@REQ-AI-024
Scenario Outline: The current prompt carries every anonymization and accuracy rule
  Given the prompt version the Worker currently sends
  When the prompt is read
  Then it states the rule that <rule>

Examples:
  | rule                                                                                     |
  | every statement must be supported by report_content, and nothing is invented             |
  | a pilot becomes exactly "the pilot" / "le pilote"                                        |
  | any other person becomes the role the report supports, or "a person" / "une personne"    |
  | a place becomes a generic phrase such as "the launch site" or "the location" / "le lieu" |
  | an exact date becomes its month or season, and the time of day is kept                   |
  | a club, school, or company becomes "the club", "the school", or "the company"            |
  | an aircraft make or model becomes its category                                           |
  | "redacted", "caviardé", placeholders, and invented names are never written               |
  | every private marker is resolved and never appears literally                             |
  | the response is exactly the two-field ai_summary_en / ai_summary_fr JSON object          |

@REQ-AI-027
Scenario Outline: Only a report with publication consent reaches the model
  Given a report whose reporter answered <consent> to publication is due for summarization
  When the Worker processes its summarization attempt
  Then the model is called <calls> time(s)

Examples:
  | consent | calls |
  | yes     | 1     |
  | no      | 0     |
