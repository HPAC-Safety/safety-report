---
title: Traceability
description: Generated matrix of every claim, the scenario that states it, and every constraint that names one.
type: guide
---

# Traceability

> **Generated file — do not edit by hand.**
> Regenerate with `node tools/traceability.mjs`. CI fails on a difference
> ([ADR-0084](decisions/ADR-0084-stable-claim-ids-and-a-generated-traceability-matrix.md)).

270 claims across 8 areas: 179 covered by a step definition today, 91 still `@ignore`. 50 constraints.

## Claims

| Claim | Area | Scenario | Engine | Status |
|---|---|---|---|---|
| `REQ-AI-001` | ai-anonymization | Exactly one model call summarizes and anonymizes a report | Reqnroll | Covered |
| `REQ-AI-002` | ai-anonymization | An exact private value in report content is deterministically marked before the model call | Reqnroll | Covered |
| `REQ-AI-003` | ai-anonymization | A token from a multi-word private value is also marked | Reqnroll | Covered |
| `REQ-AI-004` | ai-anonymization | A common short word is never marked as a false positive | Reqnroll | Covered |
| `REQ-AI-005` | ai-anonymization | Overlapping candidate matches resolve longest match first | Reqnroll | Covered |
| `REQ-AI-006` | ai-anonymization | Matching is case-insensitive and whitespace-normalized | Reqnroll | Covered |
| `REQ-AI-007` | ai-anonymization | private_context is still supplied alongside the marking pass | Reqnroll | Covered |
| `REQ-AI-008` | ai-anonymization | Concurrent workers cannot claim the same summarization outbox item twice | Reqnroll | Covered |
| `REQ-AI-009` | ai-anonymization | Only eligible, labeled fields reach the model | Reqnroll | Covered |
| `REQ-AI-010` | ai-anonymization | A fact appearing only in private context is never summarized | Reqnroll | Planned |
| `REQ-AI-011` | ai-anonymization | The Worker accepts only the exact two-field JSON response | Reqnroll | Planned |
| `REQ-AI-012` | ai-anonymization | A private person's identity is replaced with their role | Reqnroll | Planned |
| `REQ-AI-013` | ai-anonymization | Both summaries preserve safety-relevant content while anonymizing | Reqnroll | Planned |
| `REQ-AI-014` | ai-anonymization | An identifying category is never disclosed in a summary | Reqnroll | Planned |
| `REQ-AI-015` | ai-anonymization | A private-only fact is never added merely for completeness | Reqnroll | Planned |
| `REQ-AI-016` | ai-anonymization | Documents never reach the model | Reqnroll | Planned |
| `REQ-AI-017` | ai-anonymization | A valid response is persisted as one pair-level summary row | Reqnroll | Covered |
| `REQ-AI-018` | ai-anonymization | The reviewer may correct either text before approval | Reqnroll | Planned |
| `REQ-AI-019` | ai-anonymization | Retries repeat the single-call operation without adding stages | Reqnroll | Planned |
| `REQ-AI-020` | ai-anonymization | Exhausted retries surface a manually authorable failure | Reqnroll | Covered |
| `REQ-AI-021` | ai-anonymization | Sensitive summarization data is never logged | Reqnroll | Covered |
| `REQ-DOM-001` | domain-and-lifecycle | A report follows the defined lifecycle transitions | Reqnroll | Planned |
| `REQ-DOM-002` | domain-and-lifecycle | SummaryFailed remains visible to safety officers | Reqnroll | Planned |
| `REQ-DOM-003` | domain-and-lifecycle | A report is publishable only when every invariant holds | Reqnroll | Planned |
| `REQ-DOM-004` | domain-and-lifecycle | A report is not publishable when one invariant fails | Reqnroll | Planned |
| `REQ-DOM-005` | domain-and-lifecycle | Editing a summary text unpublishes the report | Reqnroll | Planned |
| `REQ-DOM-006` | domain-and-lifecycle | Negative consent still allows internal review | Reqnroll | Planned |
| `REQ-DOM-007` | domain-and-lifecycle | Soft deletion removes a report from every normal path | Reqnroll | Planned |
| `REQ-DOM-008` | domain-and-lifecycle | A question revision can be deleted only when unreferenced | Reqnroll | Planned |
| `REQ-DOM-009` | domain-and-lifecycle | Retiring a question is a soft delete with no way back | Reqnroll | Planned |
| `REQ-DOM-010` | domain-and-lifecycle | Raw reports are retained until explicit deletion | Reqnroll | Covered |
| `REQ-DOM-011` | domain-and-lifecycle | Soft-deleted and private data remain under managed retention | Reqnroll | Planned |
| `REQ-DOM-012` | domain-and-lifecycle | Unreferenced quarantine objects expire without affecting reports | Reqnroll | Planned |
| `REQ-DOM-013` | domain-and-lifecycle | An audited action is recorded in the immutable audit log | Reqnroll | Planned |
| `REQ-MED-001` | media | Only allowlisted content types are accepted | Reqnroll | Covered |
| `REQ-MED-002` | media | Declared content type must agree with detected content type | Reqnroll | Planned |
| `REQ-MED-003` | media | The client filename never leaves the HTTP boundary | Reqnroll | Planned |
| `REQ-MED-004` | media | An accepted attachment starts in a private quarantine compartment | Reqnroll | Planned |
| `REQ-MED-005` | media | Unreferenced quarantine blobs expire automatically | Reqnroll | Planned |
| `REQ-MED-006` | media | Every image is re-encoded to strip metadata | Reqnroll | Planned |
| `REQ-MED-007` | media | Every video is remuxed to strip metadata, never transcoded | Reqnroll | Planned |
| `REQ-MED-015` | media | A video that cannot be stripped is kept rather than refused | Reqnroll | Covered |
| `REQ-MED-008` | media | A document is validated but never transformed | Reqnroll | Covered |
| `REQ-MED-009` | media | Each attachment fails and processes independently of the report | Reqnroll | Planned |
| `REQ-MED-010` | media | A reviewer gets a short-lived URL only for successfully processed media | Reqnroll | Planned |
| `REQ-MED-011` | media | A reviewer downloads a validated document as an unredacted original | Reqnroll | Covered |
| `REQ-MED-012` | media | The admin site never inline-renders a private document | playwright-bdd | Planned |
| `REQ-MED-013` | media | A failed attachment is inaccessible to reviewers | playwright-bdd | Covered |
| `REQ-MED-014` | media | Attachments are never exposed publicly, even after publication | Reqnroll | Planned |
| `REQ-MOD-001` | moderation-authentication-and-publication | In development the login page offers no third-party sign-in option | playwright-bdd | Covered |
| `REQ-MOD-002` | moderation-authentication-and-publication | Where a third-party provider is configured, the login page offers it | playwright-bdd | Covered |
| `REQ-MOD-003` | moderation-authentication-and-publication | Signing in with member credentials returns a session that survives a reload | playwright-bdd | Covered |
| `REQ-MOD-004` | moderation-authentication-and-publication | Bad credentials show one generic failure and no session | playwright-bdd | Covered |
| `REQ-MOD-005` | moderation-authentication-and-publication | Repeated sign-in attempts for one identity are rate limited | playwright-bdd | Covered |
| `REQ-MOD-006` | moderation-authentication-and-publication | A member's signed-in session persists across a reload and clears on logout | playwright-bdd | Covered |
| `REQ-MOD-007` | moderation-authentication-and-publication | A signed-in Administrator's Admin menu offers every option | playwright-bdd | Covered |
| `REQ-MOD-008` | moderation-authentication-and-publication | A signed-in SafetyOfficer's Admin menu offers manage-reports only | playwright-bdd | Covered |
| `REQ-MOD-009` | moderation-authentication-and-publication | A signed-in User sees no Admin menu | playwright-bdd | Covered |
| `REQ-MOD-010` | moderation-authentication-and-publication | An open Admin menu keeps every option on a single line | playwright-bdd | Covered |
| `REQ-MOD-011` | moderation-authentication-and-publication | Activating an Admin menu option navigates to its placeholder page | playwright-bdd | Covered |
| `REQ-MOD-012` | moderation-authentication-and-publication | The Admin menu is absent for a signed-out visitor | playwright-bdd | Covered |
| `REQ-MOD-013` | moderation-authentication-and-publication | A token signed by an unknown key is rejected | playwright-bdd | Covered |
| `REQ-MOD-014` | moderation-authentication-and-publication | A token whose signature has been altered is rejected | Reqnroll | Covered |
| `REQ-MOD-015` | moderation-authentication-and-publication | An expired token is rejected | Reqnroll | Covered |
| `REQ-MOD-016` | moderation-authentication-and-publication | A token for the wrong audience is rejected | Reqnroll | Covered |
| `REQ-MOD-017` | moderation-authentication-and-publication | A token with no recognized role claim authenticates as User | Reqnroll | Covered |
| `REQ-MOD-018` | moderation-authentication-and-publication | The API never reads a name, an email, or any other claim | Reqnroll | Covered |
| `REQ-MOD-019` | moderation-authentication-and-publication | The development token endpoint does not exist outside development | Reqnroll | Covered |
| `REQ-MOD-020` | moderation-authentication-and-publication | A development login verified against the members site resolves role from the email lists | Reqnroll | Covered |
| `REQ-MOD-021` | moderation-authentication-and-publication | Bad members-site credentials show the same generic failure as bad fixed-account credentials | Reqnroll | Covered |
| `REQ-MOD-022` | moderation-authentication-and-publication | A members-site outage during a development login is reported distinctly from bad credentials | Reqnroll | Covered |
| `REQ-MOD-023` | moderation-authentication-and-publication | An unauthenticated request to an admin endpoint is refused before the handler | Reqnroll | Covered |
| `REQ-MOD-024` | moderation-authentication-and-publication | Every operation is authorized by the API, not just the UI | Reqnroll | Covered |
| `REQ-MOD-025` | moderation-authentication-and-publication | User capabilities | Reqnroll | Planned |
| `REQ-MOD-026` | moderation-authentication-and-publication | SafetyOfficer capabilities | Reqnroll | Planned |
| `REQ-MOD-027` | moderation-authentication-and-publication | Administrator capabilities include everything SafetyOfficer has | Reqnroll | Planned |
| `REQ-MOD-028` | moderation-authentication-and-publication | Only an Administrator may author a question revision | Reqnroll | Planned |
| `REQ-MOD-029` | moderation-authentication-and-publication | Sensitive admin actions are audited without report content | Reqnroll | Planned |
| `REQ-MOD-030` | moderation-authentication-and-publication | The review queue shows reports needing action | Reqnroll | Planned |
| `REQ-MOD-031` | moderation-authentication-and-publication | A report detail view exposes only what the reviewer needs | Reqnroll | Planned |
| `REQ-MOD-032` | moderation-authentication-and-publication | Editing a summary clears approval and unpublishes | Reqnroll | Planned |
| `REQ-MOD-033` | moderation-authentication-and-publication | Approval applies once to the current bilingual pair | Reqnroll | Planned |
| `REQ-MOD-034` | moderation-authentication-and-publication | Rejection blocks publication but keeps the report for learning | Reqnroll | Planned |
| `REQ-MOD-035` | moderation-authentication-and-publication | Publication requires every guard to pass, with no bypass | Reqnroll | Planned |
| `REQ-MOD-036` | moderation-authentication-and-publication | The public DTO exposes only the approved summary and its metadata | Reqnroll | Planned |
| `REQ-MOD-037` | moderation-authentication-and-publication | The public feed lists only publishable reports | Reqnroll | Planned |
| `REQ-MOD-038` | moderation-authentication-and-publication | An unknown or non-public report id returns 404 | Reqnroll | Planned |
| `REQ-MOD-039` | moderation-authentication-and-publication | There is no publication channel besides the HPAC public feed | Reqnroll | Planned |
| `REQ-MOD-040` | moderation-authentication-and-publication | Soft-deleting a report stops it everywhere immediately | Reqnroll | Planned |
| `REQ-MOD-041` | moderation-authentication-and-publication | Revoking a member's access is the identity provider's decision | Reqnroll | Planned |
| `REQ-MOD-042` | moderation-authentication-and-publication | A signed-out visitor who navigates to an admin route is sent to sign in | playwright-bdd | Covered |
| `REQ-MOD-043` | moderation-authentication-and-publication | A signed-in member without the required role sees a real 403, not a 404 or the page content | playwright-bdd | Covered |
| `REQ-MOD-044` | moderation-authentication-and-publication | A successful sign-in writes an audit row | Reqnroll | Covered |
| `REQ-MOD-045` | moderation-authentication-and-publication | A failed sign-in attempt writes an audit row | Reqnroll | Covered |
| `REQ-MOD-046` | moderation-authentication-and-publication | A reviewer's attachment view writes its own audit row, distinct from a raw-report view | Reqnroll | Covered |
| `REQ-MOD-047` | moderation-authentication-and-publication | A failed audit write blocks the action it would have recorded | Reqnroll | Planned |
| `REQ-MOD-048` | moderation-authentication-and-publication | Sign-out is not an audited event | Reqnroll | Planned |
| `REQ-QB-001` | question-bank-and-form | Editing an unanswered question creates a new revision instead of mutating one | Reqnroll | Planned |
| `REQ-QB-002` | question-bank-and-form | Editing an answered question retires it and creates a new one | Reqnroll | Planned |
| `REQ-QB-003` | question-bank-and-form | An answer on a deleted report still forces a fork | Reqnroll | Planned |
| `REQ-QB-004` | question-bank-and-form | A retired question can never be brought back | Reqnroll | Planned |
| `REQ-QB-005` | question-bank-and-form | Only one question per key is live at a time | Reqnroll | Planned |
| `REQ-QB-006` | question-bank-and-form | Publication consent revises in place even when answered | Reqnroll | Planned |
| `REQ-QB-007` | question-bank-and-form | Only an Administrator may create a revision | Reqnroll | Planned |
| `REQ-QB-008` | question-bank-and-form | Editing a question copies the latest revision into a new one | Reqnroll | Planned |
| `REQ-QB-009` | question-bank-and-form | Only the latest active, non-deleted revision is shown on the form | Reqnroll | Covered |
| `REQ-QB-010` | question-bank-and-form | Form questions are ordered deterministically | Reqnroll | Covered |
| `REQ-QB-011` | question-bank-and-form | The current form is public | Reqnroll | Covered |
| `REQ-QB-012` | question-bank-and-form | A group question's response nests its children rather than repeating them | Reqnroll | Covered |
| `REQ-QB-013` | question-bank-and-form | The current form's response includes a question's conditional dependency | Reqnroll | Covered |
| `REQ-QB-014` | question-bank-and-form | consent_publish is the only question that can never be optional | Reqnroll | Planned |
| `REQ-QB-015` | question-bank-and-form | An Administrator chooses whether an ordinary question must be answered | Reqnroll | Planned |
| `REQ-QB-016` | question-bank-and-form | consent_publish must resolve to an explicit yes or no | Reqnroll | Planned |
| `REQ-QB-017` | question-bank-and-form | Skipping an ordinary question still records that it was shown | Reqnroll | Planned |
| `REQ-QB-018` | question-bank-and-form | An answer to a picker stores the words the reporter saw | Reqnroll | Planned |
| `REQ-QB-019` | question-bank-and-form | Every answer is stored in one invariant written form | Reqnroll | Planned |
| `REQ-QB-020` | question-bank-and-form | A select answer records the reporter's language and waits for the other | Reqnroll | Planned |
| `REQ-QB-021` | question-bank-and-form | A curated list's other language is not copied onto the answer | Reqnroll | Planned |
| `REQ-QB-022` | question-bank-and-form | An Administrator supplies the second language of an answer | Reqnroll | Planned |
| `REQ-QB-023` | question-bank-and-form | Only an Administrator may translate | Reqnroll | Planned |
| `REQ-QB-024` | question-bank-and-form | A skipped file-upload question produces an answer with no attachment | Reqnroll | Planned |
| `REQ-QB-025` | question-bank-and-form | Only consent is projected onto the report aggregate | Reqnroll | Planned |
| `REQ-QB-026` | question-bank-and-form | Privacy is a property of the revision, not the answer | Reqnroll | Planned |
| `REQ-QB-027` | question-bank-and-form | Creating a revision preserves the question bank invariants | Reqnroll | Planned |
| `REQ-QB-028` | question-bank-and-form | A report may answer a known superseded revision | Reqnroll | Planned |
| `REQ-QB-029` | question-bank-and-form | Unknown or deleted revisions are rejected at submission | Reqnroll | Planned |
| `REQ-QB-030` | question-bank-and-form | A revision can be soft-deleted only when no answer references it | Reqnroll | Planned |
| `REQ-QB-031` | question-bank-and-form | A referenced revision can never be deleted | Reqnroll | Covered |
| `REQ-QB-032` | question-bank-and-form | A shared choice list is copied into the revision that uses it | Reqnroll | Covered |
| `REQ-QB-033` | question-bank-and-form | Editing a shared choice list never changes a revision already built from it | Reqnroll | Covered |
| `REQ-QB-034` | question-bank-and-form | Removing an option from a shared list keeps every snapshot of it | Reqnroll | Covered |
| `REQ-QB-035` | question-bank-and-form | A reporter adds a choice the type-ahead did not offer | Reqnroll | Covered |
| `REQ-QB-036` | question-bank-and-form | Two reporters naming the same new site produce one choice | Reqnroll | Covered |
| `REQ-QB-037` | question-bank-and-form | A choice an administrator removed is not revived by a reporter | Reqnroll | Covered |
| `REQ-QB-038` | question-bank-and-form | A type-ahead offers the live list while its revision records what was shown | Reqnroll | Covered |
| `REQ-QB-039` | question-bank-and-form | Only a type-ahead reads the live list | Reqnroll | Covered |
| `REQ-QB-040` | question-bank-and-form | A retired shared list leaves a type-ahead showing what it recorded | Reqnroll | Covered |
| `REQ-QB-041` | question-bank-and-form | A multi-select may allow reporter additions the same way a type-ahead does | Reqnroll | Covered |
| `REQ-QB-042` | question-bank-and-form | A reporter adds a choice a multi-select did not offer | Reqnroll | Planned |
| `REQ-QB-043` | question-bank-and-form | An ordinary multi-select never accepts an unlisted value | Reqnroll | Planned |
| `REQ-QB-044` | question-bank-and-form | A statement or a group collects no answer | Reqnroll | Planned |
| `REQ-QB-045` | question-bank-and-form | A statement or a group is excluded from a submission's answer-producing revisions | Reqnroll | Planned |
| `REQ-QB-046` | question-bank-and-form | A question may be grouped under a group question | Reqnroll | Covered |
| `REQ-QB-047` | question-bank-and-form | A form renders a question together with its group heading and siblings | playwright-bdd | Covered |
| `REQ-QB-048` | question-bank-and-form | Only a group question may be a grouping parent | playwright-bdd | Covered |
| `REQ-QB-049` | question-bank-and-form | A group cannot itself be grouped under another group | Reqnroll | Covered |
| `REQ-QB-050` | question-bank-and-form | A question cannot be grouped under itself | Reqnroll | Covered |
| `REQ-QB-051` | question-bank-and-form | Grouping is unaffected by conditional dependency and vice versa | Reqnroll | Covered |
| `REQ-QB-052` | question-bank-and-form | Regrouping follows a parent that stops being a group | Reqnroll | Planned |
| `REQ-QB-053` | question-bank-and-form | A question can be made conditional only on a yes/no or single-select question | Reqnroll | Planned |
| `REQ-QB-054` | question-bank-and-form | A single-select parent's dependency records the required option | Reqnroll | Covered |
| `REQ-QB-055` | question-bank-and-form | A single-select dependency must name one of the parent's current options | Reqnroll | Covered |
| `REQ-QB-056` | question-bank-and-form | A yes/no dependency does not name an option | Reqnroll | Covered |
| `REQ-QB-057` | question-bank-and-form | A question cannot be conditional on itself or form a cycle | Reqnroll | Covered |
| `REQ-QB-058` | question-bank-and-form | Publication consent can never be made conditional | Reqnroll | Covered |
| `REQ-QB-059` | question-bank-and-form | Rearranging the form writes a new revision for every question that moved | Reqnroll | Covered |
| `REQ-QB-060` | question-bank-and-form | A question type either takes options or does not | Reqnroll | Covered |
| `REQ-QB-061` | question-bank-and-form | A question key is normalized and cannot be reused | Reqnroll | Covered |
| `REQ-QB-062` | question-bank-and-form | Retiring a question keeps it and its history | Reqnroll | Covered |
| `REQ-QB-063` | question-bank-and-form | Publication consent can never be deleted or deactivated | Reqnroll | Covered |
| `REQ-QB-064` | question-bank-and-form | A retired choice list refuses further edits | Reqnroll | Covered |
| `REQ-QB-065` | question-bank-and-form | A choice list is rearranged as a whole or not at all | Reqnroll | Covered |
| `REQ-QB-066` | question-bank-and-form | Translation is offered for question wording and for a select answer's second language | Reqnroll | Covered |
| `REQ-QB-067` | question-bank-and-form | A server with no translation credential still authors questions | Reqnroll | Covered |
| `REQ-QB-068` | question-bank-and-form | A development server translates through a stand-in rather than refusing | Reqnroll | Covered |
| `REQ-QB-069` | question-bank-and-form | An Administrator drafts the French from the English | playwright-bdd | Covered |
| `REQ-QB-070` | question-bank-and-form | An Administrator drafts the English from the French | playwright-bdd | Covered |
| `REQ-QB-071` | question-bank-and-form | A question cannot be saved in one language | playwright-bdd | Covered |
| `REQ-QB-072` | question-bank-and-form | Translation is not offered when the server has no provider | playwright-bdd | Covered |
| `REQ-QB-073` | question-bank-and-form | A development stand-in says what it is | playwright-bdd | Covered |
| `REQ-QB-074` | question-bank-and-form | An Administrator sees which choices reporters added | playwright-bdd | Covered |
| `REQ-QB-075` | question-bank-and-form | An Administrator corrects a reporter-added choice | playwright-bdd | Covered |
| `REQ-QB-076` | question-bank-and-form | An Administrator authors a question from the dashboard | playwright-bdd | Covered |
| `REQ-QB-077` | question-bank-and-form | The options editor appears only for a type that takes options | playwright-bdd | Covered |
| `REQ-QB-078` | question-bank-and-form | Only yes/no and single-select questions are offered as a condition | playwright-bdd | Covered |
| `REQ-QB-079` | question-bank-and-form | Naming a required option appears only for a single-select condition | playwright-bdd | Covered |
| `REQ-QB-080` | question-bank-and-form | Questions are reordered from the keyboard | playwright-bdd | Covered |
| `REQ-QB-081` | question-bank-and-form | Editing an unanswered question from the dashboard shows its new version | playwright-bdd | Covered |
| `REQ-QB-082` | question-bank-and-form | Editing an answered question warns that it will be replaced | playwright-bdd | Covered |
| `REQ-QB-083` | question-bank-and-form | An Administrator sees answers awaiting a second language | playwright-bdd | Covered |
| `REQ-QB-084` | question-bank-and-form | An Administrator translates an answer from the queue | playwright-bdd | Covered |
| `REQ-QB-085` | question-bank-and-form | Deleting a question removes it from the list | playwright-bdd | Covered |
| `REQ-QB-086` | question-bank-and-form | A rejected save tells the Administrator why | playwright-bdd | Covered |
| `REQ-QB-087` | question-bank-and-form | The editor carries an existing question's settings into the form | playwright-bdd | Covered |
| `REQ-QB-088` | question-bank-and-form | Reviewing an imported Typeform draft prefills the editor | playwright-bdd | Covered |
| `REQ-QB-089` | question-bank-and-form | An Administrator downloads the question bank as Typeform JSON | playwright-bdd | Covered |
| `REQ-QB-090` | question-bank-and-form | An Administrator writes a question's choice by its wording alone | playwright-bdd | Covered |
| `REQ-QB-091` | question-bank-and-form | An Administrator writes a shared choice list's choice by its wording alone | playwright-bdd | Covered |
| `REQ-QB-092` | question-bank-and-form | A choice an Administrator writes is recorded under a code derived from its English wording | playwright-bdd | Covered |
| `REQ-QB-094` | question-bank-and-form | A reporter answering in French adds a choice recorded in French | Reqnroll | Covered |
| `REQ-QB-095` | question-bank-and-form | Submitting a report records a type-ahead value the list did not offer | Reqnroll | Covered |
| `REQ-SUB-001` | report-submission | The browser holds report state locally until submission | playwright-bdd | Covered |
| `REQ-SUB-002` | report-submission | A successful submission clears local browser state | playwright-bdd | Covered |
| `REQ-SUB-003` | report-submission | Expired local state is not restored | playwright-bdd | Covered |
| `REQ-SUB-028` | report-submission | The leading statement question renders as an introduction | playwright-bdd | Covered |
| `REQ-SUB-029` | report-submission | A reporter pages through questions one at a time | playwright-bdd | Covered |
| `REQ-SUB-030` | report-submission | A group question and its children page together | playwright-bdd | Covered |
| `REQ-SUB-031` | report-submission | A required question blocks Next until answered | playwright-bdd | Covered |
| `REQ-SUB-032` | report-submission | A conditional question is absent from paging until its parent condition is met | playwright-bdd | Covered |
| `REQ-SUB-033` | report-submission | The Next button becomes Submit on the final page | playwright-bdd | Covered |
| `REQ-SUB-004` | report-submission | One answer entry per shown answer-producing revision | playwright-bdd | Covered |
| `REQ-SUB-005` | report-submission | A skipped answer is represented by an empty value, not omission | Reqnroll | Covered |
| `REQ-SUB-006` | report-submission | A submitted select value must be one the revision offered | Reqnroll | Covered |
| `REQ-SUB-007` | report-submission | The submission path never calls a translation provider | Reqnroll | Covered |
| `REQ-SUB-025` | report-submission | Every answer's value and locale are immutable once submitted | Reqnroll | Covered |
| `REQ-SUB-026` | report-submission | The Worker mechanically translates every answer into its second language | Reqnroll | Covered |
| `REQ-SUB-027` | report-submission | An administrator's correction always wins over the Worker's translation | Reqnroll | Covered |
| `REQ-SUB-008` | report-submission | The API rejects a malformed submission DTO | Reqnroll | Covered |
| `REQ-SUB-009` | report-submission | A submission may answer a known superseded revision | Reqnroll | Covered |
| `REQ-SUB-010` | report-submission | A revision that was never shown as answer-producing is rejectable | Reqnroll | Planned |
| `REQ-SUB-011` | report-submission | Reporter-visible errors never echo submitted content | Reqnroll | Planned |
| `REQ-SUB-012` | report-submission | Accepted attachments are streamed into quarantine under a bound | Reqnroll | Covered |
| `REQ-SUB-013` | report-submission | A valid submission is persisted atomically | Reqnroll | Covered |
| `REQ-SUB-014` | report-submission | A failed transaction leaves no visible report and no leaked blobs | Reqnroll | Covered |
| `REQ-SUB-015` | report-submission | A successful submission returns an opaque accepted receipt | Reqnroll | Covered |
| `REQ-SUB-016` | report-submission | The UI prevents duplicate submission while a request is in flight | playwright-bdd | Covered |
| `REQ-SUB-017` | report-submission | A rate-limited submission is rejected | Reqnroll | Covered |
| `REQ-SUB-018` | report-submission | An unauthenticated submission is rejected | Reqnroll | Covered |
| `REQ-SUB-019` | report-submission | A member of any role may submit a report | Reqnroll | Covered |
| `REQ-SUB-020` | report-submission | A stored report carries no submitter subject, user id, or link | Reqnroll | Covered |
| `REQ-SUB-021` | report-submission | No audit entry or log line records who submitted a report | Reqnroll | Planned |
| `REQ-SUB-022` | report-submission | A signed-out visitor is asked to sign in before the report page is offered | playwright-bdd | Covered |
| `REQ-SUB-023` | report-submission | The report page tells the reporter that signing in does not attach them to the report | playwright-bdd | Covered |
| `REQ-SUB-024` | report-submission | The not-tracked notice is shown in the reporter's chosen language | playwright-bdd | Covered |
| `REQ-TF-001` | typeform-question-import-export | Import requires both languages | Reqnroll | Covered |
| `REQ-TF-002` | typeform-question-import-export | A field's ref appears in the English file but not the French one | Reqnroll | Covered |
| `REQ-TF-003` | typeform-question-import-export | A choice's ref appears in the English file but not the French one | Reqnroll | Covered |
| `REQ-TF-004` | typeform-question-import-export | A Typeform field type maps to a question type | Reqnroll | Covered |
| `REQ-TF-005` | typeform-question-import-export | A single-select multiple-choice field imports as single-select | Reqnroll | Covered |
| `REQ-TF-006` | typeform-question-import-export | A multi-select multiple-choice field imports as multi-select | Reqnroll | Covered |
| `REQ-TF-007` | typeform-question-import-export | A multi-select field with a free-text choice enables reporter additions | Reqnroll | Covered |
| `REQ-TF-008` | typeform-question-import-export | A group field flattens into a heading and its children | Reqnroll | Covered |
| `REQ-TF-009` | typeform-question-import-export | A contact-info field flattens the same way a group does | Reqnroll | Covered |
| `REQ-TF-010` | typeform-question-import-export | The generated answer-recap screen is not imported | Reqnroll | Covered |
| `REQ-TF-011` | typeform-question-import-export | A field type with no equivalent is rejected, not silently dropped | Reqnroll | Covered |
| `REQ-TF-012` | typeform-question-import-export | A field with only linear flow is not flagged as branching logic | Reqnroll | Covered |
| `REQ-TF-013` | typeform-question-import-export | Any real branching condition is flagged, not silently dropped or auto-mapped | Reqnroll | Covered |
| `REQ-TF-014` | typeform-question-import-export | An Administrator resolves a pending logic note | Reqnroll | Covered |
| `REQ-TF-015` | typeform-question-import-export | Import never saves a question by itself | Reqnroll | Covered |
| `REQ-TF-016` | typeform-question-import-export | The imported draft's key comes from the Typeform ref | Reqnroll | Covered |
| `REQ-TF-017` | typeform-question-import-export | Re-importing the same form updates in place | playwright-bdd | Covered |
| `REQ-TF-018` | typeform-question-import-export | Export produces a zip of two Typeform-shaped files | playwright-bdd | Covered |
| `REQ-TF-019` | typeform-question-import-export | Export preserves data Typeform has no field for | Reqnroll | Covered |
| `REQ-TF-020` | typeform-question-import-export | Exporting and reimporting reproduces the same drafts | Reqnroll | Covered |
| `REQ-TF-021` | typeform-question-import-export | Only an Administrator may import or export | Reqnroll | Covered |
| `REQ-WLD-001` | web-localization-and-design | The admin review queue is a route on the one deployed site | playwright-bdd | Planned |
| `REQ-WLD-002` | web-localization-and-design | The homepage header exposes navigation to reporting, submission, and contact, and a distinct member-login action | playwright-bdd | Covered |
| `REQ-WLD-003` | web-localization-and-design | The contact page shows HPAC's organization details, mailing address, email, and social links | playwright-bdd | Covered |
| `REQ-WLD-004` | web-localization-and-design | On a mobile-width viewport, header navigation is reached through a hamburger toggle | playwright-bdd | Covered |
| `REQ-WLD-005` | web-localization-and-design | The initial locale is selected in priority order | playwright-bdd | Covered |
| `REQ-WLD-006` | web-localization-and-design | Switching the language toggle updates the document language and persists the choice | playwright-bdd | Covered |
| `REQ-WLD-007` | web-localization-and-design | Switching the language toggle rerenders without losing answers | playwright-bdd | Covered |
| `REQ-WLD-008` | web-localization-and-design | A visitor can toggle and persist a light/dark theme choice | playwright-bdd | Covered |
| `REQ-WLD-009` | web-localization-and-design | The footer sits at the bottom of the viewport on a short page but below the fold on a long one | playwright-bdd | Covered |
| `REQ-WLD-010` | web-localization-and-design | Application chrome strings come from committed locale catalogues | playwright-bdd | Covered |
| `REQ-WLD-011` | web-localization-and-design | A translation missing locally is stubbed with a visible marker, and CI must replace it before merge | Reqnroll | Covered |
| `REQ-WLD-012` | web-localization-and-design | A French value edited by hand is recorded rather than overwritten | Reqnroll | Covered |
| `REQ-WLD-013` | web-localization-and-design | Editing both languages at once is one correction, not a conflict | Reqnroll | Covered |
| `REQ-WLD-014` | web-localization-and-design | Question content comes from the bilingual database revision | Reqnroll | Planned |
| `REQ-WLD-015` | web-localization-and-design | Only publication consent is marked required on the form | playwright-bdd | Covered |
| `REQ-WLD-016` | web-localization-and-design | The form explains local storage and warns about attachments | playwright-bdd | Covered |
| `REQ-WLD-017` | web-localization-and-design | The client shows inline validation before submission | playwright-bdd | Covered |
| `REQ-WLD-018` | web-localization-and-design | Client validation never replaces server validation | playwright-bdd | Planned |
| `REQ-WLD-019` | web-localization-and-design | The active locale controls which summary text is primary | playwright-bdd | Planned |
| `REQ-WLD-020` | web-localization-and-design | Admin pages distinguish private, ordinary, and output content | playwright-bdd | Planned |
| `REQ-WLD-021` | web-localization-and-design | Assets are self-hosted, never loaded from third-party CDNs | Reqnroll | Planned |
| `REQ-WLD-022` | web-localization-and-design | Dark mode renders correctly in every state | playwright-bdd | Planned |
| `REQ-WLD-023` | web-localization-and-design | The form meets baseline accessibility requirements | playwright-bdd | Covered |
| `REQ-WLD-024` | web-localization-and-design | A JavaScript failure never exposes or erases report data | playwright-bdd | Covered |
| `REQ-WLD-025` | web-localization-and-design | A network failure preserves local state and explains retry | playwright-bdd | Covered |

## Constraints

A constraint states something the system must be true of; the claims beside
it are the scenarios that prove it. `none` is an honest answer — an
infrastructure or test-suite property is not observable from a scenario —
and it carries its reason.

| Constraint | Page | Verified by |
|---|---|---|
| `CON-SO-001` | system-overview.md | `REQ-QB-001`, `REQ-QB-002`, `REQ-QB-009` |
| `CON-SO-002` | system-overview.md | `REQ-QB-014`, `REQ-QB-016`, `REQ-WLD-015` |
| `CON-SO-003` | system-overview.md | `REQ-MOD-036`, `REQ-MED-014` |
| `CON-SO-004` | system-overview.md | `REQ-AI-001`, `REQ-AI-011` |
| `CON-SO-005` | system-overview.md | `REQ-AI-007`, `REQ-AI-010`, `REQ-AI-015` |
| `CON-SO-006` | system-overview.md | `REQ-AI-017`, `REQ-MOD-033` |
| `CON-SO-007` | system-overview.md | `REQ-DOM-003`, `REQ-MOD-035` |
| `CON-SO-008` | system-overview.md | `REQ-DOM-007`, `REQ-MOD-040` |
| `CON-SO-009` | system-overview.md | `REQ-MOD-039` |
| `CON-DP-001` | data-and-persistence.md | `REQ-MOD-036`, `REQ-AI-009` |
| `CON-DP-002` | data-and-persistence.md | `REQ-DOM-007`, `REQ-DOM-011` |
| `CON-DP-003` | data-and-persistence.md | none — managed encryption is an infrastructure property, not something a scenario can observe through the application |
| `CON-DP-004` | data-and-persistence.md | `REQ-QB-019`, `REQ-QB-026`, `REQ-QB-028` |
| `CON-DP-005` | data-and-persistence.md | `REQ-MOD-018` |
| `CON-DP-006` | data-and-persistence.md | `REQ-SUB-020`, `REQ-SUB-021` |
| `CON-DP-007` | data-and-persistence.md | `REQ-QB-005`, `REQ-QB-030`, `REQ-QB-031` |
| `CON-DP-008` | data-and-persistence.md | `REQ-SUB-013`, `REQ-SUB-014` |
| `CON-DP-009` | data-and-persistence.md | `REQ-AI-008` |
| `CON-DP-010` | data-and-persistence.md | `REQ-DOM-013`, `REQ-MOD-029` |
| `CON-DP-011` | data-and-persistence.md | `REQ-MOD-031`, `REQ-MOD-036` |
| `CON-DP-012` | data-and-persistence.md | none — a deployment-time property no running scenario observes |
| `CON-IF-001` | interfaces-and-data-flow.md | `REQ-QB-011`, `REQ-SUB-018`, `REQ-MOD-037`, `REQ-MOD-038` |
| `CON-IF-002` | interfaces-and-data-flow.md | `REQ-SUB-001`, `REQ-MOD-039` |
| `CON-IF-003` | interfaces-and-data-flow.md | `REQ-MOD-017`, `REQ-MOD-019` |
| `CON-IF-004` | interfaces-and-data-flow.md | `REQ-MOD-023`, `REQ-MOD-024`, `REQ-MOD-028`, `REQ-MOD-029` |
| `CON-IF-005` | interfaces-and-data-flow.md | `REQ-MOD-041` |
| `CON-IF-006` | interfaces-and-data-flow.md | `REQ-SUB-011` |
| `CON-IF-007` | interfaces-and-data-flow.md | none — an internal structural rule with no observable behavior; it is enforced in review and by the conventions skill |
| `CON-IF-008` | interfaces-and-data-flow.md | `REQ-AI-008`, `REQ-MED-009`, `REQ-MOD-040` |
| `CON-IF-009` | interfaces-and-data-flow.md | `REQ-AI-001`, `REQ-AI-009`, `REQ-AI-016`, `REQ-AI-019`, `REQ-MED-010` |
| `CON-IF-010` | interfaces-and-data-flow.md | `REQ-AI-021`, `REQ-SUB-021`, `REQ-MED-003` |
| `CON-INF-001` | infrastructure-and-operations.md | none — an infrastructure property no application scenario can observe; Terraform validation and the `infra` job are its check |
| `CON-INF-002` | infrastructure-and-operations.md | `REQ-MOD-039` |
| `CON-INF-003` | infrastructure-and-operations.md | none — an infrastructure property no application scenario can observe; Terraform validation and the `infra` job are its check |
| `CON-INF-004` | infrastructure-and-operations.md | `REQ-SUB-018`, `REQ-MOD-003` |
| `CON-INF-005` | infrastructure-and-operations.md | none — an infrastructure property no application scenario can observe; Terraform validation and the `infra` job are its check |
| `CON-INF-006` | infrastructure-and-operations.md | `REQ-MOD-019` |
| `CON-INF-007` | infrastructure-and-operations.md | none — an infrastructure property no application scenario can observe; Terraform validation and the `infra` job are its check |
| `CON-INF-008` | infrastructure-and-operations.md | `REQ-AI-021`, `REQ-MED-003` |
| `CON-INF-009` | infrastructure-and-operations.md | `REQ-MOD-039` |
| `CON-INF-010` | infrastructure-and-operations.md | `REQ-DOM-010`, `REQ-DOM-011`, `REQ-DOM-012`, `REQ-MED-005` |
| `CON-TQ-001` | testing-and-quality.md | none — a rule about what the suites are for, not about what the system does |
| `CON-TQ-002` | testing-and-quality.md | none — a rule about the tests themselves, enforced by the suites and the CI gates rather than by a scenario |
| `CON-TQ-003` | testing-and-quality.md | none — a delivery rule, enforced by the `feature-coverage` job and review |
| `CON-TQ-004` | testing-and-quality.md | `REQ-QB-001`, `REQ-QB-009`, `REQ-QB-016`, `REQ-SUB-004`, `REQ-SUB-005`, `REQ-SUB-009`, `REQ-SUB-013`, `REQ-SUB-017`, `REQ-SUB-018` |
| `CON-TQ-005` | testing-and-quality.md | `REQ-AI-001`, `REQ-AI-009`, `REQ-AI-010`, `REQ-AI-011`, `REQ-AI-012`, `REQ-AI-013`, `REQ-AI-020`, `REQ-AI-021` |
| `CON-TQ-006` | testing-and-quality.md | `REQ-MED-001`, `REQ-MED-002`, `REQ-MED-003`, `REQ-MED-006`, `REQ-MED-007`, `REQ-MED-008`, `REQ-MED-010`, `REQ-MED-011`, `REQ-MED-014` |
| `CON-TQ-007` | testing-and-quality.md | `REQ-MOD-024`, `REQ-MOD-029`, `REQ-MOD-032`, `REQ-MOD-033`, `REQ-MOD-035`, `REQ-MOD-036`, `REQ-MOD-040`, `REQ-DOM-007` |
| `CON-TQ-008` | testing-and-quality.md | none — a rule about the tests themselves, enforced by the suites and the CI gates rather than by a scenario |
| `CON-TQ-009` | testing-and-quality.md | none — a rule about the tests themselves, enforced by the suites and the CI gates rather than by a scenario |
