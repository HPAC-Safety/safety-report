---
title: Traceability
description: Generated matrix of every claim, the scenario that states it, and every constraint that names one.
type: guide
---

# Traceability

> **Generated file — do not edit by hand.**
> Regenerate with `node tools/traceability.mjs`. CI fails on a difference
> ([ADR-0084](decisions/ADR-0084-stable-claim-ids-and-a-generated-traceability-matrix.md)).
> One block per claim and per constraint, in ID order, and no totals, so
> branches merge it without conflicting
> ([ADR-0106](decisions/ADR-0106-every-line-of-the-matrix-derives-from-one-source-item.md)).

Each claim reads: the scenario that states it — *engine, status*. A
`Planned` claim is still `@ignore`.

## Claims: ai-anonymization

### REQ-AI-001

Exactly one model call summarizes and anonymizes a report — *Reqnroll, Covered*

### REQ-AI-002

An exact private value in report content is deterministically marked before the model call — *Reqnroll, Covered*

### REQ-AI-003

A token from a multi-word private value is also marked — *Reqnroll, Covered*

### REQ-AI-004

A common short word is never marked as a false positive — *Reqnroll, Covered*

### REQ-AI-005

Overlapping candidate matches resolve longest match first — *Reqnroll, Covered*

### REQ-AI-006

Matching is case-insensitive and whitespace-normalized — *Reqnroll, Covered*

### REQ-AI-007

private_context is still supplied alongside the marking pass — *Reqnroll, Covered*

### REQ-AI-008

Concurrent workers cannot claim the same summarization outbox item twice — *Reqnroll, Covered*

### REQ-AI-009

Only eligible, labeled fields reach the model — *Reqnroll, Covered*

### REQ-AI-011

The Worker accepts only the exact two-field JSON response — *Reqnroll, Covered*

### REQ-AI-016

Documents never reach the model — *Reqnroll, Covered*

### REQ-AI-017

A valid response is persisted as one pair-level summary row — *Reqnroll, Covered*

### REQ-AI-019

Retries repeat the single-call operation without adding stages — *Reqnroll, Covered*

### REQ-AI-020

Exhausted retries surface a manually authorable failure — *Reqnroll, Covered*

### REQ-AI-021

Sensitive summarization data is never logged — *Reqnroll, Covered*

### REQ-AI-022

The Worker requests the configured model at the configured reasoning level — *Reqnroll, Covered*

### REQ-AI-023

A Worker holding a key refuses to start with an unusable provider configuration — *Reqnroll, Covered*

### REQ-AI-024

The current prompt carries every anonymization and accuracy rule — *Reqnroll, Covered*

### REQ-AI-027

Only a report with publication consent reaches the model — *Reqnroll, Covered*

## Claims: comments

### REQ-COM-001

A signed-in member comments on a published report — *Reqnroll, Covered*

### REQ-COM-002

Commenting requires a member — *Reqnroll, Covered*

### REQ-COM-003

A report the public cannot see cannot be commented on — *Reqnroll, Covered*

### REQ-COM-004

A comment must have text, and at most 2000 characters — *Reqnroll, Covered*

### REQ-COM-005

A comment is stored in the language it was written in, and the Worker supplies the other — *Reqnroll, Covered*

### REQ-COM-006

Posting a comment never waits for, or calls, a translation provider — *Reqnroll, Covered*

### REQ-COM-007

The API tells a reader which comments are theirs and never who wrote the others — *Reqnroll, Covered*

### REQ-COM-008

An author's edit adds a revision and keeps the one before it — *Reqnroll, Covered*

### REQ-COM-009

An author's deleted comment disappears but is not erased — *Reqnroll, Covered*

### REQ-COM-010

Nobody may change another member's comment — *Reqnroll, Covered*

### REQ-COM-011

A reviewer hides a comment, and the hiding is audited — *Reqnroll, Covered*

### REQ-COM-012

A member who is not a reviewer cannot hide a comment — *Reqnroll, Covered*

### REQ-COM-013

Unpublishing a report hides its comments, and publishing it again brings them back — *Reqnroll, Covered*

### REQ-COM-014

The public feed carries each report's comment count — *Reqnroll, Covered*

### REQ-COM-015

The feed shows how many comments each report has — *playwright-bdd, Covered*

### REQ-COM-016

A visitor who is not signed in is invited to sign in to comment — *playwright-bdd, Covered*

### REQ-COM-017

A signed-in member posts, edits, and deletes their own comment — *playwright-bdd, Covered*

### REQ-COM-018

A reader sees every comment in the site's language, a translated one marked by a subtle icon — *playwright-bdd, Covered*

### REQ-COM-019

A comment still awaiting translation shows its original text — *playwright-bdd, Covered*

### REQ-COM-020

A reviewer hides a comment from the report page — *playwright-bdd, Covered*

## Claims: domain-and-lifecycle

### REQ-DOM-001

A report follows the defined lifecycle transitions — *Reqnroll, Covered*

### REQ-DOM-003

A report is publishable only when every invariant holds — *Reqnroll, Covered*

### REQ-DOM-004

A report is not publishable when one invariant fails — *Reqnroll, Covered*

### REQ-DOM-005

Editing a summary text unpublishes the report — *Reqnroll, Covered*

### REQ-DOM-006

A report without publication consent is never summarized — *Reqnroll, Covered*

### REQ-DOM-007

Soft deletion removes a report from every normal path — *Reqnroll, Covered*

### REQ-DOM-008

A question revision can be deleted only when unreferenced — *Reqnroll, Covered*

### REQ-DOM-009

Retiring a question is a soft delete with no way back — *Reqnroll, Covered*

### REQ-DOM-010

Raw reports are retained until explicit deletion — *Reqnroll, Covered*

### REQ-DOM-011

Soft-deleted and private data remain under managed retention — *Reqnroll, Planned*

### REQ-DOM-012

Unreferenced quarantine objects expire without affecting reports — *Reqnroll, Planned*

### REQ-DOM-013

An audited action is recorded in the immutable audit log — *Reqnroll, Planned*

### REQ-DOM-014

A review action outside its states is refused and changes nothing — *Reqnroll, Covered*

### REQ-DOM-015

A report without publication consent is unpublished for good — *Reqnroll, Covered*

## Claims: media

### REQ-MED-001

Only allowlisted content types are accepted — *Reqnroll, Covered*

### REQ-MED-002

Declared content type must agree with detected content type — *Reqnroll, Covered*

### REQ-MED-003

The client filename is kept only as a reviewer's download name — *Reqnroll, Covered*

### REQ-MED-005

Unclaimed uploads expire automatically — *Reqnroll, Planned*

### REQ-MED-006

Every image is re-encoded to strip metadata — *Reqnroll, Covered*

### REQ-MED-007

Every video is remuxed to strip metadata, never transcoded — *Reqnroll, Covered*

### REQ-MED-008

A document is validated but never transformed — *Reqnroll, Covered*

### REQ-MED-009

Each attachment fails and processes independently of the report — *Reqnroll, Covered*

### REQ-MED-010

A reviewer gets a short-lived URL only for successfully processed media — *Reqnroll, Covered*

### REQ-MED-011

A reviewer downloads a validated document as an unredacted original — *Reqnroll, Covered*

### REQ-MED-012

The admin site never inline-renders a private document — *playwright-bdd, Planned*

### REQ-MED-013

A failed attachment is inaccessible to reviewers — *Reqnroll, Covered*

### REQ-MED-015

A video that cannot be stripped is kept rather than refused — *Reqnroll, Covered*

### REQ-MED-016

Removing an upload erases every version of it — *Reqnroll, Covered*

### REQ-MED-017

A cancelled upload leaves nothing in storage — *Reqnroll, Covered*

### REQ-MED-018

A claimed upload is copied into the report's original compartment — *Reqnroll, Covered*

### REQ-MED-019

A reporter's filename is sanitized before it is stored — *Reqnroll, Covered*

### REQ-MED-020

A download's extension always matches the bytes served — *Reqnroll, Covered*

### REQ-MED-021

An attachment awaits the Worker before a reviewer may view it — *Reqnroll, Covered*

### REQ-MED-022

Processing an attachment twice changes nothing — *Reqnroll, Covered*

### REQ-MED-023

The Worker skips an attachment whose report was deleted — *Reqnroll, Covered*

### REQ-MED-024

Processing never holds a whole attachment in memory — *Reqnroll, Covered*

### REQ-MED-025

A published report lists its verified photos and video when media was consented to — *Reqnroll, Covered*

### REQ-MED-026

A file that is neither a verified derivative nor a validated document is never public — *Reqnroll, Covered*

### REQ-MED-027

Media is public only when the reporter consented to sharing it — *Reqnroll, Covered*

### REQ-MED-028

A visitor gets a short-lived inline link to a public file — *Reqnroll, Covered*

### REQ-MED-029

A file stops being public when its report or a reviewer withdraws it — *Reqnroll, Covered*

### REQ-MED-030

A reviewer hides a file and shows it again, and both are audited — *Reqnroll, Covered*

### REQ-MED-031

A member who is not a reviewer cannot hide or show a file — *Reqnroll, Covered*

### REQ-MED-032

The report page embeds its photos and video with a generic label — *playwright-bdd, Covered*

### REQ-MED-033

An expired link is replaced and the video resumes where it was — *playwright-bdd, Covered*

### REQ-MED-034

Media that is no longer public is removed from the page — *playwright-bdd, Covered*

### REQ-MED-035

A reviewer hides a file from the public report page — *playwright-bdd, Covered*

### REQ-MED-036

The admin report page shows whether each file is public — *playwright-bdd, Covered*

### REQ-MED-037

A published report lists its validated documents when media consent names documents — *Reqnroll, Covered*

### REQ-MED-038

A document is public only when its media consent named documents — *Reqnroll, Covered*

### REQ-MED-039

A visitor gets a short-lived forced download of a public document — *Reqnroll, Covered*

### REQ-MED-040

A reviewer hides a document and shows it again, and both are audited — *Reqnroll, Covered*

### REQ-MED-041

The report page offers a public document as a download, never inline — *playwright-bdd, Covered*

### REQ-MED-042

The admin report page shows whether each document is public — *playwright-bdd, Covered*

### REQ-MED-043

A QuickTime video downloads as an MP4 — *Reqnroll, Covered*

### REQ-MED-044

A published QuickTime video is served as an MP4 — *Reqnroll, Covered*

### REQ-MED-045

A sent upload waits, unvalidated, in a private quarantine compartment — *Reqnroll, Planned*

## Claims: moderation-authentication-and-publication

### REQ-MOD-001

In development the login page offers no third-party sign-in option — *playwright-bdd, Covered*

### REQ-MOD-002

Where a third-party provider is configured, the login page offers it — *playwright-bdd, Covered*

### REQ-MOD-003

Signing in with member credentials returns a session that survives a reload — *playwright-bdd, Covered*

### REQ-MOD-004

Bad credentials show one generic failure and no session — *playwright-bdd, Covered*

### REQ-MOD-005

Repeated sign-in attempts for one identity are rate limited — *Reqnroll, Covered*

### REQ-MOD-006

A member's signed-in session persists across a reload and clears on logout — *playwright-bdd, Covered*

### REQ-MOD-007

A signed-in Administrator's Admin menu offers every option — *playwright-bdd, Covered*

### REQ-MOD-008

A signed-in SafetyOfficer's Admin menu offers manage-reports only — *playwright-bdd, Covered*

### REQ-MOD-009

A signed-in User sees no Admin menu — *playwright-bdd, Covered*

### REQ-MOD-010

An open Admin menu keeps every option on a single line — *playwright-bdd, Covered*

### REQ-MOD-011

Activating an Admin menu option navigates to its page — *playwright-bdd, Covered*

### REQ-MOD-012

The Admin menu is absent for a signed-out visitor — *playwright-bdd, Covered*

### REQ-MOD-013

A token signed by an unknown key is rejected — *Reqnroll, Covered*

### REQ-MOD-014

A token whose signature has been altered is rejected — *Reqnroll, Covered*

### REQ-MOD-015

An expired token is rejected — *Reqnroll, Covered*

### REQ-MOD-016

A token for the wrong audience is rejected — *Reqnroll, Covered*

### REQ-MOD-017

A token with no recognized role claim authenticates as User — *Reqnroll, Covered*

### REQ-MOD-018

The API never reads a name, an email, or any other claim — *Reqnroll, Covered*

### REQ-MOD-019

The development token endpoint does not exist outside development — *Reqnroll, Covered*

### REQ-MOD-020

A development login verified against the members site resolves role from the email lists — *Reqnroll, Covered*

### REQ-MOD-021

Bad members-site credentials show the same generic failure as bad fixed-account credentials — *Reqnroll, Covered*

### REQ-MOD-022

A members-site outage during a development login is reported distinctly from bad credentials — *Reqnroll, Covered*

### REQ-MOD-023

An unauthenticated request to an admin endpoint is refused before the handler — *Reqnroll, Covered*

### REQ-MOD-024

Every operation is authorized by the API, not just the UI — *Reqnroll, Covered*

### REQ-MOD-025

User capabilities — *Reqnroll, Planned*

### REQ-MOD-026

SafetyOfficer capabilities — *Reqnroll, Planned*

### REQ-MOD-027

Administrator capabilities include everything SafetyOfficer has — *Reqnroll, Planned*

### REQ-MOD-028

Only an Administrator may author a question revision — *Reqnroll, Covered*

### REQ-MOD-029

Sensitive admin actions are audited without report content — *Reqnroll, Covered*

### REQ-MOD-030

The admin report list shows every live report with its state — *Reqnroll, Covered*

### REQ-MOD-031

A report detail view exposes only what the reviewer needs — *Reqnroll, Covered*

### REQ-MOD-032

Editing a summary clears approval and returns the report to Pending — *Reqnroll, Covered*

### REQ-MOD-033

Publishing approves the current bilingual pair once — *Reqnroll, Covered*

### REQ-MOD-035

Publication requires every guard to pass, with no bypass — *Reqnroll, Covered*

### REQ-MOD-036

The public DTO exposes only the approved summary and its metadata — *Reqnroll, Covered*

### REQ-MOD-037

The public feed lists only publishable reports — *Reqnroll, Covered*

### REQ-MOD-038

An unknown or non-public report id returns 404 — *Reqnroll, Covered*

### REQ-MOD-039

There is no publication channel besides the HPAC public feed — *Reqnroll, Planned*

### REQ-MOD-041

Revoking a member's access is the identity provider's decision — *Reqnroll, Planned*

### REQ-MOD-042

A signed-out visitor who navigates to an admin route is sent to sign in — *playwright-bdd, Covered*

### REQ-MOD-043

A signed-in member without the required role sees a real 403, not a 404 or the page content — *playwright-bdd, Covered*

### REQ-MOD-044

A successful sign-in writes an audit row — *Reqnroll, Covered*

### REQ-MOD-045

A failed sign-in attempt writes an audit row — *Reqnroll, Covered*

### REQ-MOD-046

A reviewer's attachment view writes its own audit row, distinct from a raw-report view — *Reqnroll, Covered*

### REQ-MOD-047

A failed audit write blocks the action it would have recorded — *Reqnroll, Planned*

### REQ-MOD-048

Signing out sends nothing to the API — *playwright-bdd, Covered*

### REQ-MOD-049

The Needs action filter shows pending, failed, and stuck reports — *Reqnroll, Covered*

### REQ-MOD-050

A status filter narrows the admin report list — *Reqnroll, Covered*

### REQ-MOD-051

Opening a report's detail view is audited — *Reqnroll, Covered*

### REQ-MOD-052

The Manage reports page lists reports with a status badge and a Private badge — *playwright-bdd, Covered*

### REQ-MOD-053

Choosing a filter on Manage reports narrows the list — *playwright-bdd, Covered*

### REQ-MOD-054

Opening a report shows its answers with private answers marked, and its summary pair — *playwright-bdd, Covered*

### REQ-MOD-055

Publishing a consented report's pair makes it public — *Reqnroll, Covered*

### REQ-MOD-057

Unpublishing takes a report off the public feed and keeps it for learning — *Reqnroll, Covered*

### REQ-MOD-058

Unpublishing may carry a note that only reviewers see — *Reqnroll, Covered*

### REQ-MOD-059

A reviewer writes the pair by hand when summarization failed — *Reqnroll, Covered*

### REQ-MOD-060

A review action based on a stale view is refused — *Reqnroll, Covered*

### REQ-MOD-061

Every review action writes one content-free audit entry in its own transaction — *Reqnroll, Covered*

### REQ-MOD-062

The report view offers only the actions its state allows — *playwright-bdd, Covered*

### REQ-MOD-063

Editing the summary pair saves both texts and clears approval — *playwright-bdd, Covered*

### REQ-MOD-064

Publishing a consented report shows it Published — *playwright-bdd, Covered*

### REQ-MOD-065

Unpublishing with a note shows the note on the report — *playwright-bdd, Covered*

### REQ-MOD-066

A stale action tells the reviewer to reload — *playwright-bdd, Covered*

### REQ-MOD-067

Deleting a report asks for confirmation first — *playwright-bdd, Covered*

### REQ-MOD-068

Opening an attachment requests its own audited link — *playwright-bdd, Covered*

### REQ-MOD-069

Only a reviewer may request a machine translation — *Reqnroll, Covered*

### REQ-MOD-070

Each summary language records how it was produced — *Reqnroll, Covered*

### REQ-MOD-071

The editor offers a translate button for each language the reviewer changed — *playwright-bdd, Covered*

### REQ-MOD-072

Translating asks before overwriting and shows what would change — *playwright-bdd, Covered*

### REQ-MOD-073

Writing a pair by hand offers the translate buttons too — *playwright-bdd, Covered*

### REQ-MOD-074

The report view shows how each summary language was produced — *playwright-bdd, Covered*

### REQ-MOD-075

A date, time, or yes/no answer reads in the reviewer's language, not in its stored form — *playwright-bdd, Covered*

### REQ-MOD-076

A stored date that is not a real date is shown as stored — *playwright-bdd, Covered*

### REQ-MOD-077

The report detail view gives a second language only for an answer that has one — *Reqnroll, Covered*

### REQ-MOD-078

Opening a report shows a translation only under answers that have one — *playwright-bdd, Covered*

### REQ-MOD-079

Each report in the public feed opens at its own address — *playwright-bdd, Covered*

### REQ-MOD-080

A report's address opens it directly and survives a reload — *playwright-bdd, Covered*

### REQ-MOD-081

An address for a report that is not public shows not found — *playwright-bdd, Covered*

### REQ-MOD-082

The public feed pages forward and the address keeps the page — *playwright-bdd, Covered*

### REQ-MOD-083

A reviewer can open a published report's public page — *playwright-bdd, Covered*

### REQ-MOD-084

A reviewer reads how many reports need action — *Reqnroll, Covered*

### REQ-MOD-085

Only an Administrator's pending counts include answers awaiting translation — *Reqnroll, Covered*

### REQ-MOD-086

A User cannot read the pending counts — *Reqnroll, Covered*

### REQ-MOD-087

An Administrator's Admin menu shows how much work is waiting — *playwright-bdd, Covered*

### REQ-MOD-088

A SafetyOfficer's Admin menu counts only the reports needing action — *playwright-bdd, Covered*

### REQ-MOD-089

With nothing waiting, the Admin menu shows no count — *playwright-bdd, Covered*

### REQ-MOD-090

A report without publication consent never needs action — *Reqnroll, Covered*

### REQ-MOD-091

Sign-out is not an audited event — *Reqnroll, Covered*

## Claims: question-bank-and-form

### REQ-QB-001

Editing an unanswered question creates a new revision instead of mutating one — *Reqnroll, Covered*

### REQ-QB-002

Editing an answered question retires it and creates a new one — *Reqnroll, Covered*

### REQ-QB-003

An answer on a deleted report still forces a fork — *Reqnroll, Covered*

### REQ-QB-004

A retired question can never be brought back — *Reqnroll, Covered*

### REQ-QB-005

Only one question per key is live at a time — *Reqnroll, Covered*

### REQ-QB-006

Publication consent revises in place even when answered — *Reqnroll, Covered*

### REQ-QB-008

Editing a question copies the latest revision into a new one — *Reqnroll, Covered*

### REQ-QB-009

Only the latest active, non-deleted revision is shown on the form — *Reqnroll, Covered*

### REQ-QB-010

Form questions are ordered deterministically — *Reqnroll, Covered*

### REQ-QB-011

The current form is public — *Reqnroll, Covered*

### REQ-QB-012

A group question's response nests its children rather than repeating them — *Reqnroll, Covered*

### REQ-QB-013

The current form's response includes a question's conditional dependency — *Reqnroll, Covered*

### REQ-QB-014

consent_publish can never be optional — *Reqnroll, Covered*

### REQ-QB-015

An Administrator chooses whether an ordinary question must be answered — *Reqnroll, Covered*

### REQ-QB-016

consent_publish must resolve to an explicit yes or no — *Reqnroll, Covered*

### REQ-QB-018

An answer to a picker stores the words the reporter saw — *Reqnroll, Covered*

### REQ-QB-019

Every answer is stored in its written form — *Reqnroll, Covered*

### REQ-QB-025

Only consent is projected onto the report aggregate — *Reqnroll, Covered*

### REQ-QB-026

Privacy is a property of the revision, not the answer — *Reqnroll, Planned*

### REQ-QB-027

Creating a revision preserves the question bank invariants — *Reqnroll, Covered*

### REQ-QB-030

A revision can be soft-deleted only when no answer references it — *Reqnroll, Covered*

### REQ-QB-031

A referenced revision can never be deleted — *Reqnroll, Covered*

### REQ-QB-035

A reporter adds a choice the type-ahead did not offer — *Reqnroll, Covered*

### REQ-QB-036

Two reporters naming the same new site produce one choice — *Reqnroll, Covered*

### REQ-QB-037

A choice an administrator removed is not revived by a reporter — *Reqnroll, Covered*

### REQ-QB-044

A statement or a group collects no answer — *Reqnroll, Covered*

### REQ-QB-045

A statement or a group is excluded from a submission's answer-producing revisions — *Reqnroll, Planned*

### REQ-QB-046

A question may be grouped under a group question — *Reqnroll, Covered*

### REQ-QB-047

A form renders a question together with its group heading and siblings — *playwright-bdd, Covered*

### REQ-QB-048

Only a group question may be a grouping parent — *Reqnroll, Covered*

### REQ-QB-049

A group cannot itself be grouped under another group — *Reqnroll, Covered*

### REQ-QB-050

A question cannot be grouped under itself — *Reqnroll, Covered*

### REQ-QB-051

Grouping is unaffected by conditional dependency and vice versa — *Reqnroll, Covered*

### REQ-QB-052

Regrouping follows a parent that stops being a group — *Reqnroll, Planned*

### REQ-QB-053

A question can be made conditional only on a yes/no or single-select question — *Reqnroll, Covered*

### REQ-QB-054

A single-select parent's dependency records the required option — *Reqnroll, Covered*

### REQ-QB-055

A single-select dependency must name one of the parent's live choices — *Reqnroll, Covered*

### REQ-QB-056

A yes/no dependency does not name an option — *Reqnroll, Covered*

### REQ-QB-057

A question cannot be conditional on itself or form a cycle — *Reqnroll, Covered*

### REQ-QB-058

Publication consent can never be made conditional — *Reqnroll, Covered*

### REQ-QB-059

Rearranging the form writes a new revision for every question that moved — *Reqnroll, Covered*

### REQ-QB-060

A question type either takes options or does not — *Reqnroll, Covered*

### REQ-QB-061

A question key is normalized and cannot be reused — *Reqnroll, Covered*

### REQ-QB-062

Retiring a question keeps it and its history — *Reqnroll, Covered*

### REQ-QB-063

Publication consent can never be deleted or deactivated — *Reqnroll, Covered*

### REQ-QB-066

A translation draft comes from the API and is saved only by a person — *Reqnroll, Covered*

### REQ-QB-067

A server with no translation credential still authors questions — *Reqnroll, Covered*

### REQ-QB-069

An Administrator drafts the French from the English — *playwright-bdd, Covered*

### REQ-QB-070

An Administrator drafts the English from the French — *playwright-bdd, Covered*

### REQ-QB-071

A question cannot be saved in one language — *playwright-bdd, Covered*

### REQ-QB-072

Translation is not offered when the server has no provider — *playwright-bdd, Covered*

### REQ-QB-074

An Administrator sees which choices reporters added — *playwright-bdd, Covered*

### REQ-QB-075

An Administrator corrects a reporter-added choice — *playwright-bdd, Covered*

### REQ-QB-076

An Administrator authors a question from the dashboard — *playwright-bdd, Covered*

### REQ-QB-077

The options editor appears only for a type that takes options — *playwright-bdd, Covered*

### REQ-QB-078

Only yes/no and single-select questions are offered as a condition — *playwright-bdd, Covered*

### REQ-QB-079

Naming a required option appears only for a single-select condition — *playwright-bdd, Covered*

### REQ-QB-080

Questions are reordered from the keyboard — *playwright-bdd, Covered*

### REQ-QB-081

Editing an unanswered question from the dashboard shows its new version — *playwright-bdd, Covered*

### REQ-QB-082

Editing an answered question warns that it will be replaced — *playwright-bdd, Covered*

### REQ-QB-083

An Administrator sees answers awaiting a second language — *playwright-bdd, Covered*

### REQ-QB-084

An Administrator translates an answer from the queue — *playwright-bdd, Covered*

### REQ-QB-085

Deleting a question removes it from the list — *playwright-bdd, Covered*

### REQ-QB-086

A rejected save tells the Administrator why — *playwright-bdd, Covered*

### REQ-QB-087

The editor carries an existing question's settings into the form — *playwright-bdd, Covered*

### REQ-QB-088

Reviewing an imported Typeform draft prefills the editor — *playwright-bdd, Covered*

### REQ-QB-089

An Administrator downloads the question bank as Typeform JSON — *playwright-bdd, Covered*

### REQ-QB-090

An Administrator writes a question's choice by its wording alone — *playwright-bdd, Covered*

### REQ-QB-092

A choice an Administrator writes is recorded under a code derived from its English wording — *Reqnroll, Covered*

### REQ-QB-093

Editing a question opens the editor in that question's place — *playwright-bdd, Covered*

### REQ-QB-094

A reporter answering in French adds a choice recorded in French only — *Reqnroll, Covered*

### REQ-QB-095

Submitting a report records a type-ahead value the question did not offer — *Reqnroll, Covered*

### REQ-QB-096

A new question's key is derived from its English wording and never reused — *Reqnroll, Covered*

### REQ-QB-097

Only a type-ahead grows from reporters' answers — *Reqnroll, Covered*

### REQ-QB-098

Editing an answered question's wording carries every choice to the replacement — *Reqnroll, Covered*

### REQ-QB-099

Editing an answered question's choices keeps the question and its version — *Reqnroll, Covered*

### REQ-QB-100

A removed choice is hidden from the form and kept in history — *Reqnroll, Covered*

### REQ-QB-101

A choice a live question depends on cannot be removed — *Reqnroll, Covered*

### REQ-QB-102

A choice in only one language is offered in the language it has — *Reqnroll, Covered*

### REQ-QB-103

The report form shows a one-language choice in the language it has — *playwright-bdd, Covered*

### REQ-QB-104

A new installation asks for several attachments — *Reqnroll, Covered*

### REQ-QB-105

The seeded single-file wording on an unanswered attachment question is revised — *Reqnroll, Covered*

### REQ-QB-106

The seeded single-file wording on an answered attachment question forks it — *Reqnroll, Covered*

### REQ-QB-107

An attachment question an Administrator already reworded is left alone — *Reqnroll, Covered*

### REQ-QB-108

Only free text can be marked as needing translation — *Reqnroll, Covered*

### REQ-QB-109

Marking a non-text question as needing translation is rejected — *Reqnroll, Covered*

### REQ-QB-110

Whether a question needs translation is a revision field — *Reqnroll, Covered*

### REQ-QB-111

The editor offers Auto-translate answer only for free text — *playwright-bdd, Covered*

### REQ-QB-112

Media consent is a system question that can never be removed or made conditional — *Reqnroll, Covered*

### REQ-QB-113

The form asks for media consent only when there is a file to share — *playwright-bdd, Covered*

### REQ-QB-114

A media consent answer is recorded on the report — *Reqnroll, Covered*

### REQ-QB-115

A media consent answer must be an explicit yes or no — *Reqnroll, Covered*

### REQ-QB-116

A media consent answer covers documents only under the wording the form showed — *Reqnroll, Covered*

### REQ-QB-117

Media consent names documents and says they are published as uploaded — *Reqnroll, Covered*

### REQ-QB-118

An answer not in its written form is rejected — *Reqnroll, Covered*

### REQ-QB-119

A yes or no answer takes its fixed counterpart at submission — *Reqnroll, Covered*

### REQ-QB-120

A yes in either language enables a conditional question — *Reqnroll, Covered*

### REQ-QB-121

A consent answer means the same in either language — *Reqnroll, Covered*

## Claims: report-submission

### REQ-SUB-001

The browser holds report state locally until submission — *playwright-bdd, Covered*

### REQ-SUB-002

A successful submission clears local browser state — *playwright-bdd, Covered*

### REQ-SUB-003

Expired local state is not restored — *playwright-bdd, Covered*

### REQ-SUB-004

One answer entry per shown answer-producing revision — *Reqnroll, Covered*

### REQ-SUB-005

A skipped answer is represented by an empty value, not omission — *Reqnroll, Covered*

### REQ-SUB-006

A submitted select value must be one the revision offered — *Reqnroll, Covered*

### REQ-SUB-007

The submission path never calls a translation provider — *Reqnroll, Covered*

### REQ-SUB-008

The API rejects a malformed submission DTO — *Reqnroll, Covered*

### REQ-SUB-009

A submission may answer a known superseded revision — *Reqnroll, Covered*

### REQ-SUB-010

A revision that was never shown as answer-producing is rejectable — *Reqnroll, Planned*

### REQ-SUB-011

Reporter-visible errors never echo submitted content — *Reqnroll, Covered*

### REQ-SUB-013

A valid submission is persisted atomically — *Reqnroll, Covered*

### REQ-SUB-014

A failed transaction leaves no visible report and no leaked blobs — *Reqnroll, Covered*

### REQ-SUB-015

A successful submission returns an opaque accepted receipt — *Reqnroll, Covered*

### REQ-SUB-016

The UI prevents duplicate submission while a request is in flight — *playwright-bdd, Covered*

### REQ-SUB-017

A rate-limited submission is rejected — *Reqnroll, Covered*

### REQ-SUB-018

An unauthenticated submission is rejected — *Reqnroll, Covered*

### REQ-SUB-019

A member of any role may submit a report — *Reqnroll, Covered*

### REQ-SUB-020

A stored report carries no submitter subject, user id, or link — *Reqnroll, Covered*

### REQ-SUB-021

No audit entry or log line records who submitted a report — *Reqnroll, Planned*

### REQ-SUB-022

A signed-out visitor is asked to sign in before the report page is offered — *playwright-bdd, Covered*

### REQ-SUB-023

The report page tells the reporter that signing in does not attach them to the report — *playwright-bdd, Covered*

### REQ-SUB-024

The not-tracked notice is shown in the reporter's chosen language — *playwright-bdd, Covered*

### REQ-SUB-025

Every answer's value and locale are immutable once submitted — *Reqnroll, Covered*

### REQ-SUB-026

The Worker mechanically translates every answer that needs it — *Reqnroll, Covered*

### REQ-SUB-027

An administrator's correction always wins over the Worker's translation — *Reqnroll, Covered*

### REQ-SUB-028

The leading statement question renders as an introduction — *playwright-bdd, Covered*

### REQ-SUB-029

A reporter pages through questions one at a time — *playwright-bdd, Covered*

### REQ-SUB-030

A group question and its children page together — *playwright-bdd, Covered*

### REQ-SUB-031

A required question blocks Next until answered — *playwright-bdd, Covered*

### REQ-SUB-032

A conditional question is absent from paging until its parent condition is met — *playwright-bdd, Covered*

### REQ-SUB-033

The Next button becomes Submit on the final page — *playwright-bdd, Covered*

### REQ-SUB-034

A multi-select question is a picker dropdown, not a flat list — *playwright-bdd, Covered*

### REQ-SUB-035

A returning reporter is asked whether to continue their saved report — *playwright-bdd, Covered*

### REQ-SUB-036

Continuing a saved report restores it where the reporter left off — *playwright-bdd, Covered*

### REQ-SUB-037

Declining a saved report starts a fresh form — *playwright-bdd, Covered*

### REQ-SUB-038

A reporter with no saved report is not asked — *playwright-bdd, Covered*

### REQ-SUB-041

A submission naming an expired or unknown upload is refused by name — *Reqnroll, Covered*

### REQ-SUB-042

A claimed upload leaves quarantine once the report commits — *Reqnroll, Covered*

### REQ-SUB-043

An unauthenticated upload is rejected — *Reqnroll, Covered*

### REQ-SUB-044

A rate-limited upload is rejected — *Reqnroll, Covered*

### REQ-SUB-045

Attaching a file uploads it at once with an activity indicator — *playwright-bdd, Covered*

### REQ-SUB-046

Next and Submit wait for every upload to finish — *playwright-bdd, Covered*

### REQ-SUB-047

A reporter may cancel an upload in progress — *playwright-bdd, Covered*

### REQ-SUB-048

A reporter may remove an uploaded file — *playwright-bdd, Covered*

### REQ-SUB-049

The form refuses a file past the attachment limit — *playwright-bdd, Covered*

### REQ-SUB-050

A refused upload is explained on that file's row — *playwright-bdd, Covered*

### REQ-SUB-051

An expired upload is marked for re-attachment and nothing else is lost — *playwright-bdd, Covered*

### REQ-SUB-053

Each page of the form has its own address — *playwright-bdd, Covered*

### REQ-SUB-054

The browser's Back and Forward buttons move between pages under the form's rules — *playwright-bdd, Covered*

### REQ-SUB-055

Continuing a saved report puts its page in the address — *playwright-bdd, Covered*

### REQ-SUB-056

A page address never answers the continue question for the reporter — *playwright-bdd, Covered*

### REQ-SUB-057

A page address without a saved report opens the introduction — *playwright-bdd, Covered*

### REQ-SUB-058

The attachment field is a drop zone with a large choose-files control — *playwright-bdd, Covered*

### REQ-SUB-059

The drop zone's control opens the file chooser from a pointer or the keyboard — *playwright-bdd, Covered*

### REQ-SUB-060

Files dropped on the drop zone upload exactly as chosen files do — *playwright-bdd, Covered*

### REQ-SUB-061

Dropped files past the attachment limit are refused — *playwright-bdd, Covered*

### REQ-SUB-062

A file dropped outside the drop zone does nothing — *playwright-bdd, Covered*

### REQ-SUB-063

Continuing a saved report restores its uploaded files — *playwright-bdd, Covered*

### REQ-SUB-064

Starting over erases the saved report's uploads — *playwright-bdd, Covered*

### REQ-SUB-065

A reporter may discard the report in progress — *playwright-bdd, Covered*

### REQ-SUB-066

Discarding a report asks for confirmation first — *playwright-bdd, Covered*

### REQ-SUB-067

An expired saved report's uploads are erased — *playwright-bdd, Covered*

### REQ-SUB-068

The continue dialog shows a saved date or time in the reporter's language — *playwright-bdd, Covered*

### REQ-SUB-069

A picker answer takes its choice's other-language label at submission — *Reqnroll, Covered*

### REQ-SUB-070

A type-ahead answer uses its choice when it names one, and the Worker otherwise — *Reqnroll, Covered*

### REQ-SUB-071

Only free text marked for translation is machine-translated — *Reqnroll, Covered*

### REQ-SUB-072

Minting an upload returns a pre-signed PUT for one quarantine key and nothing else — *Reqnroll, Planned*

### REQ-SUB-073

A declared file the API will not accept gets no upload URL — *Reqnroll, Planned*

### REQ-SUB-074

Storage accepts only the upload the URL was signed for — *Reqnroll, Planned*

### REQ-SUB-075

A submission validates every upload it claims — *Reqnroll, Planned*

### REQ-SUB-076

A file refused at submission is marked on its row and nothing else is lost — *playwright-bdd, Planned*

### REQ-SUB-077

A yes or no is sent in the language the report is submitted in — *playwright-bdd, Covered*

## Claims: typeform-question-import-export

### REQ-TF-001

Import requires both languages — *Reqnroll, Covered*

### REQ-TF-002

A field's ref appears in the English file but not the French one — *Reqnroll, Covered*

### REQ-TF-003

A choice's ref appears in the English file but not the French one — *Reqnroll, Covered*

### REQ-TF-004

A Typeform field type maps to a question type — *Reqnroll, Covered*

### REQ-TF-005

A single-select multiple-choice field imports as single-select — *Reqnroll, Covered*

### REQ-TF-006

A multi-select multiple-choice field imports as multi-select — *Reqnroll, Covered*

### REQ-TF-008

A group field flattens into a heading and its children — *Reqnroll, Covered*

### REQ-TF-009

A contact-info field flattens the same way a group does — *Reqnroll, Covered*

### REQ-TF-010

The generated answer-recap screen is not imported — *Reqnroll, Covered*

### REQ-TF-011

A field type with no equivalent is rejected, not silently dropped — *Reqnroll, Covered*

### REQ-TF-012

A field with only linear flow is not flagged as branching logic — *Reqnroll, Covered*

### REQ-TF-013

Any real branching condition is flagged, not silently dropped or auto-mapped — *Reqnroll, Covered*

### REQ-TF-014

An Administrator resolves a pending logic note — *Reqnroll, Covered*

### REQ-TF-015

Import never saves a question by itself — *Reqnroll, Covered*

### REQ-TF-016

The imported draft's key comes from the Typeform ref — *Reqnroll, Covered*

### REQ-TF-017

Re-importing the same form updates in place — *playwright-bdd, Covered*

### REQ-TF-018

Export produces a zip of two Typeform-shaped files — *Reqnroll, Covered*

### REQ-TF-019

Export preserves data Typeform has no field for — *Reqnroll, Covered*

### REQ-TF-020

Exporting and reimporting reproduces the same drafts — *Reqnroll, Covered*

### REQ-TF-021

Only an Administrator may import or export — *Reqnroll, Covered*

## Claims: web-localization-and-design

### REQ-WLD-001

The admin review queue is a route on the one deployed site — *playwright-bdd, Planned*

### REQ-WLD-002

The homepage header exposes navigation to reporting, submission, and contact, and a distinct member-login action — *playwright-bdd, Covered*

### REQ-WLD-003

The contact page shows HPAC's organization details, mailing address, email, and social links — *playwright-bdd, Covered*

### REQ-WLD-004

On a mobile-width viewport, header navigation is reached through a hamburger toggle — *playwright-bdd, Covered*

### REQ-WLD-005

The initial locale is selected in priority order — *playwright-bdd, Covered*

### REQ-WLD-006

Switching the language toggle updates the document language and persists the choice — *playwright-bdd, Covered*

### REQ-WLD-007

Switching the language toggle rerenders without losing answers — *playwright-bdd, Covered*

### REQ-WLD-008

A visitor can toggle and persist a light/dark theme choice — *playwright-bdd, Covered*

### REQ-WLD-009

The footer sits at the bottom of the viewport on a short page but below the fold on a long one — *playwright-bdd, Covered*

### REQ-WLD-010

Application chrome strings come from committed locale catalogues — *Reqnroll, Covered*

### REQ-WLD-011

A translation missing locally is stubbed with a visible marker, and CI must replace it before merge — *Reqnroll, Covered*

### REQ-WLD-012

A French value edited by hand is recorded rather than overwritten — *Reqnroll, Covered*

### REQ-WLD-013

Editing both languages at once is one correction, not a conflict — *Reqnroll, Covered*

### REQ-WLD-014

Question content comes from the bilingual database revision — *Reqnroll, Covered*

### REQ-WLD-015

Required questions, and only those, are marked required on the form — *playwright-bdd, Covered*

### REQ-WLD-016

The form explains local storage and warns about attachments — *playwright-bdd, Covered*

### REQ-WLD-017

The client shows inline validation before submission — *playwright-bdd, Covered*

### REQ-WLD-018

Client validation never replaces server validation — *Reqnroll, Covered*

### REQ-WLD-019

The interface language alone decides which summary text is shown — *playwright-bdd, Covered*

### REQ-WLD-020

Admin pages distinguish private, ordinary, and output content — *playwright-bdd, Planned*

### REQ-WLD-021

Assets are self-hosted, never loaded from third-party CDNs — *Reqnroll, Covered*

### REQ-WLD-022

Dark mode renders correctly in every state — *playwright-bdd, Planned*

### REQ-WLD-023

The form meets baseline accessibility requirements — *playwright-bdd, Covered*

### REQ-WLD-024

A JavaScript failure never exposes or erases report data — *playwright-bdd, Covered*

### REQ-WLD-025

A network failure preserves local state and explains retry — *playwright-bdd, Covered*

### REQ-WLD-026

French that renders a listed term the forbidden way fails verification — *Reqnroll, Covered*

### REQ-WLD-027

The machine translator is told the required rendering of every listed term — *Reqnroll, Covered*

### REQ-WLD-028

French is machine-translated into the English the configuration names — *Reqnroll, Covered*

### REQ-WLD-029

A translator with no usable English target refuses to start — *Reqnroll, Covered*

## Constraints

A constraint states something the system must be true of; the claims beside
it are the scenarios that prove it. `none` is an honest answer — an
infrastructure or test-suite property is not observable from a scenario —
and it carries its reason.

### CON-DP-001

data-and-persistence.md — verified by `REQ-MOD-036`, `REQ-AI-009`

### CON-DP-002

data-and-persistence.md — verified by `REQ-DOM-007`, `REQ-DOM-011`

### CON-DP-003

data-and-persistence.md — verified by none — managed encryption is an infrastructure property, not something a scenario can observe through the application

### CON-DP-004

data-and-persistence.md — verified by `REQ-QB-019`, `REQ-QB-026`, `REQ-SUB-009`

### CON-DP-005

data-and-persistence.md — verified by `REQ-MOD-018`

### CON-DP-006

data-and-persistence.md — verified by `REQ-SUB-020`, `REQ-SUB-021`

### CON-DP-007

data-and-persistence.md — verified by `REQ-QB-005`, `REQ-QB-030`, `REQ-QB-031`

### CON-DP-008

data-and-persistence.md — verified by `REQ-SUB-013`, `REQ-SUB-014`

### CON-DP-009

data-and-persistence.md — verified by `REQ-AI-008`

### CON-DP-010

data-and-persistence.md — verified by `REQ-DOM-013`, `REQ-MOD-029`

### CON-DP-011

data-and-persistence.md — verified by `REQ-MOD-031`, `REQ-MOD-036`

### CON-DP-012

data-and-persistence.md — verified by none — a startup property no running scenario observes; `MigrationRunner` and its tests are its check

### CON-IF-001

interfaces-and-data-flow.md — verified by `REQ-QB-011`, `REQ-SUB-018`, `REQ-MOD-037`, `REQ-MOD-038`

### CON-IF-002

interfaces-and-data-flow.md — verified by `REQ-SUB-001`, `REQ-MOD-039`

### CON-IF-003

interfaces-and-data-flow.md — verified by `REQ-MOD-019`

### CON-IF-004

interfaces-and-data-flow.md — verified by `REQ-MOD-023`, `REQ-MOD-024`, `REQ-MOD-028`, `REQ-MOD-029`, `REQ-COM-011`, `REQ-COM-012`

### CON-IF-005

interfaces-and-data-flow.md — verified by `REQ-MOD-041`

### CON-IF-006

interfaces-and-data-flow.md — verified by `REQ-MOD-060`

### CON-IF-007

interfaces-and-data-flow.md — verified by none — an internal structural rule with no observable behavior; it is enforced in review and by the conventions skill

### CON-IF-008

interfaces-and-data-flow.md — verified by `REQ-AI-008`, `REQ-MED-009`, `REQ-DOM-007`

### CON-IF-009

interfaces-and-data-flow.md — verified by `REQ-AI-001`, `REQ-AI-009`, `REQ-AI-016`, `REQ-AI-019`, `REQ-MED-010`

### CON-IF-010

interfaces-and-data-flow.md — verified by `REQ-AI-021`, `REQ-SUB-021`, `REQ-MED-003`

### CON-INF-001

infrastructure-and-operations.md — verified by none — an infrastructure property no application scenario can observe; Terraform validation and the `infra` job are its check

### CON-INF-002

infrastructure-and-operations.md — verified by `REQ-MOD-039`

### CON-INF-003

infrastructure-and-operations.md — verified by none — an infrastructure property no application scenario can observe; Terraform validation and the `infra` job are its check

### CON-INF-004

infrastructure-and-operations.md — verified by `REQ-SUB-018`, `REQ-MOD-003`

### CON-INF-005

infrastructure-and-operations.md — verified by none — an infrastructure property no application scenario can observe; Terraform validation and the `infra` job are its check

### CON-INF-006

infrastructure-and-operations.md — verified by `REQ-MOD-019`

### CON-INF-007

infrastructure-and-operations.md — verified by none — an infrastructure property no application scenario can observe; Terraform validation and the `infra` job are its check

### CON-INF-008

infrastructure-and-operations.md — verified by `REQ-AI-021`, `REQ-MED-003`

### CON-INF-009

infrastructure-and-operations.md — verified by `REQ-MOD-039`

### CON-INF-010

infrastructure-and-operations.md — verified by `REQ-DOM-010`, `REQ-DOM-011`, `REQ-DOM-012`, `REQ-MED-005`

### CON-SO-001

system-overview.md — verified by `REQ-QB-001`, `REQ-QB-002`, `REQ-QB-009`

### CON-SO-002

system-overview.md — verified by `REQ-QB-014`, `REQ-QB-016`, `REQ-QB-112`, `REQ-QB-113`

### CON-SO-003

system-overview.md — verified by `REQ-MOD-036`, `REQ-MED-025`, `REQ-MED-026`, `REQ-MED-037`, `REQ-MED-039`

### CON-SO-004

system-overview.md — verified by `REQ-AI-001`, `REQ-AI-011`

### CON-SO-005

system-overview.md — verified by `REQ-AI-007`, `REQ-AI-024`

### CON-SO-006

system-overview.md — verified by `REQ-AI-017`, `REQ-MOD-033`

### CON-SO-007

system-overview.md — verified by `REQ-DOM-003`, `REQ-MOD-035`

### CON-SO-008

system-overview.md — verified by `REQ-DOM-007`

### CON-SO-009

system-overview.md — verified by `REQ-MOD-039`

### CON-TQ-001

testing-and-quality.md — verified by none — a rule about what the suites are for, not about what the system does

### CON-TQ-002

testing-and-quality.md — verified by none — a rule about the tests themselves, enforced by the suites and the CI gates rather than by a scenario

### CON-TQ-003

testing-and-quality.md — verified by none — a delivery rule, enforced by the `feature-coverage` job and review

### CON-TQ-004

testing-and-quality.md — verified by `REQ-QB-001`, `REQ-QB-009`, `REQ-QB-016`, `REQ-SUB-004`, `REQ-SUB-005`, `REQ-SUB-009`, `REQ-SUB-013`, `REQ-SUB-017`, `REQ-SUB-018`

### CON-TQ-005

testing-and-quality.md — verified by `REQ-AI-001`, `REQ-AI-009`, `REQ-AI-011`, `REQ-AI-020`, `REQ-AI-021`, `REQ-AI-024`

### CON-TQ-006

testing-and-quality.md — verified by `REQ-MED-001`, `REQ-MED-002`, `REQ-MED-003`, `REQ-MED-006`, `REQ-MED-007`, `REQ-MED-008`, `REQ-MED-010`, `REQ-MED-011`, `REQ-MED-025`, `REQ-MED-026`, `REQ-MED-037`, `REQ-MED-039`

### CON-TQ-007

testing-and-quality.md — verified by `REQ-MOD-024`, `REQ-MOD-029`, `REQ-MOD-032`, `REQ-MOD-033`, `REQ-MOD-035`, `REQ-MOD-036`, `REQ-DOM-007`

### CON-TQ-008

testing-and-quality.md — verified by none — a rule about the tests themselves, enforced by the suites and the CI gates rather than by a scenario

### CON-TQ-009

testing-and-quality.md — verified by none — a rule about the tests themselves, enforced by the suites and the CI gates rather than by a scenario
