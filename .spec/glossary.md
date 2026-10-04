---
title: Glossary
description: "The specification's vocabulary: one canonical word per concept, the outcome phrases steps use, and the banned synonyms check-glossary refuses in scenarios and area READMEs."
type: spec
area: glossary
---

# Glossary

The words the scenarios and area READMEs use, one per concept, approved by the
owner on [#815](https://github.com/HPAC-Safety/safety-report/issues/815#issuecomment-5973100915).
This is the *specification's* vocabulary. It is not
[`locales/glossary.json`](../locales/glossary.json), which pins whole
interface strings by key, nor [`locales/terms.json`](../locales/terms.json),
which holds the French translator to a word
([ADR-0102](decisions/ADR-0102-a-term-list-holds-the-french-translator-to-a-word.md));
the French forms of these terms stay there.

## How the lint reads this page

[`node tools/spec/check-glossary.ts`](../tools/spec/check-glossary.ts) parses
every table below whose header has a **Banned in scenarios** column, and fails
any banned synonym in a `.feature` file or a `.spec/features/<area>/README.md`
([CONV-003](conventions/CONV-003-scenarios-and-area-readmes-use-the-glossary.md)).

- **What it reads.** In a `.feature` file: step text, names, descriptions,
  and the body rows of every Examples table, because a step reads those cells
  through its `<placeholder>`. Not tags, comments, doc strings, an Examples
  header, or a step's own data table, which is data. In an area README: the
  prose, without its frontmatter, fenced code, or link targets.
- **What it skips.** Anything in `"double quotes"` or `` `code` ``, and every
  `<placeholder>`. Quote an interface string, a page title, or a value
  exactly as the user sees it.
- **Banned in scenarios.** Each item is in backticks. A plain phrase matches
  whole words, ignoring case. An item written `/pattern/flags` is a JavaScript
  regular expression, matching case as written unless its flags say `i`. A
  `\|` inside an item is a literal `|`.
- **Exempt areas.** The areas, by directory name, where that row's bans do
  not apply. A narrower carve-out — one fixed phrase, such as "landing
  field" — sits inside the pattern, and that row's definition says why.

Identifier-shaped words — snake_case and PascalCase storage names, HTTP status
codes, `API`, `DTO`, `outbox`, `JSON` — are outside this lint:
[`node tools/gherkin/lint-scenarios.ts`](../tools/gherkin/lint-scenarios.ts)
refuses them in scenarios
([CONV-004](conventions/CONV-004-scenarios-describe-behavior-not-implementation.md)).
Interface mechanics are part of
[#815](https://github.com/HPAC-Safety/safety-report/issues/815)'s later rules.

## People and roles

| Term | Definition | Banned in scenarios | Exempt areas |
|---|---|---|---|
| **Member** | Anyone signed in with a valid token, whatever their role. A role already implies a valid token, so no role is written "signed-in", nor said to be "signed in" (J2). | `authenticated member`, `/\bsigned[- ]in (?=(?:Administrator\|Safety Officer\|User\|reviewer\|member)s?\b)/i`, `/\b(?:Administrator\|Safety Officer\|User\|[Mm]ember\|[Rr]eviewer)s? (?:is\|are) signed in\b/`, `caller`, `/\busers?\b(?![ -](?:records?\|tables?\|ids?\|aggregate\|facing))/` | — |
| **User** | The lowest role: it files a report and comments, and nothing else. Always capitalised, and only as the role's name. | — | — |
| **Reporter** | The member filing a report. Nothing stored records who they are. | `submitter` | — |
| **Safety Officer** | The role that reviews reports, edits and approves summaries, and publishes. Always this spelling and case; the role claim's own value, `SafetyOfficer`, only in quotes. | `/\b[Ss]afety officers?\b/`, `/\bSafetyOfficers?\b/` | — |
| **Administrator** | The role that does everything a Safety Officer does, and also authors questions. Always capitalised. "Admin" names only a place: the admin site, an admin page, the Admin menu. | `/\badministrators?\b/`, `admins`, `/\b(?:an?\|the) admin(?=\s*(?:[.,;:!?)]\|$))/i` | — |
| **Reviewer** | A Safety Officer or an Administrator: the two roles with review access, `Policies.Reviewer` (J1). | `authorized reviewer`, `authorised reviewer`, `/\bSafety Officers? or (?:an )?Administrators?\b/`, `/\bAdministrators? or (?:a )?Safety Officers?\b/` | — |
| **Visitor** | Anyone using the public site, signed in or not. "The public" is not an actor; "visible to the public" stays (J11). | `/\b[Tt]he public (?=(?:can\|cannot\|can't\|never\|only\|sees?\|saw\|reads?\|requests?\|gets?)\b)/` | — |
| **Anonymous visitor** | A visitor who is not signed in. | `signed-out visitor`, `/\bvisitors? who (?:is\|are) not signed in\b/i`, `/\b(?:is\|are) signed out\b/i` | — |
| **Pilot** | The role word that replaces a pilot's name in a summary: "the pilot" / "le pilote". | — | — |
| **Token subject** | The opaque `sub` of a validated token, recorded as an approver or an audit actor. It joins to nothing. Always the full term. | `/(?<!token )\bsubjects?\b/i` | — |

## Reports and their lifecycle

| Term | Definition | Banned in scenarios | Exempt areas |
|---|---|---|---|
| **Report** | What a reporter files: answers, attachments, and its lifecycle. | `incident`, `incidents` | — |
| **Occurrence** | The aviation event a report describes; not the report itself. | — | — |
| **Submission** | The one final request that files a report, and the moment it is accepted. Not the report. | — | — |
| **Saved report** | The reporter's unfinished answers and uploads, kept only in that browser for 15 days. "Draft" stays for a summary draft. | — | — |
| **Report status** | **Submitted**, **Summarizing**, **Pending**, **Summary failed**, **Published**, **Unpublished**: the `ReportStatus` values, capitalised as names. | `SummaryFailed` | — |
| **Deleted** | A record marked deleted: terminal, out of every normal path, and never physically removed (J7). "Archive" stays for a file format, such as a zip archive. | `/\bsoft[- ]?delet\w*/i`, `/\bstamp(?:s\|ed\|ing)?\b/i`, `/\barchiv(?:ed\|ing)\b/i` | — |
| **Erased** | Physically gone. Only an unclaimed upload, an upload of an abandoned saved report, or a Typeform import note (`pending_import_logic`) an Administrator resolves (ADR-0077) is ever erased. | — | — |
| **Report list** | The reviewers' list of reports on the admin site, with its filters (J9). The type-ahead review page is its own page. | `/\bqueues?\b/i`, `dashboard`, `dashboards` | — |
| **Report detail** | One report's page on the admin site. | `detail view`, `detail page`, `detail views`, `detail pages` | — |
| **Public feed** | The public list of published reports. | — | — |
| **Report page** | One published report's public page; "public report page" is the same page. | — | — |

## Questions and choices

| Term | Definition | Banned in scenarios | Exempt areas |
|---|---|---|---|
| **Question** | One item of the report form, with a type, a label, and immutable revisions; on the form it is also the input a reporter types into or picks from. An input on any other page is a box: the email box, the search box (J4). A landing or bailout field is a place a pilot lands, not an input, so the pattern leaves it alone. | `/(?<!landing \|bailout )\bfields?\b/i` | `typeform-question-import-export` |
| **Question key** | The stable identifier every revision of a question, and its fork, shares. | `stable key`, `stable keys` | — |
| **Question revision** | One immutable wording-and-settings record of a question. An answer names exactly one. | — | — |
| **Fork** | The new question, with the same key, that editing an answered question creates. The old one is deleted. | — | — |
| **Label** | A question's wording, stored without a closing colon. | — | — |
| **Help text** | A question's optional guidance. | — | — |
| **Question type** | **short text**, **paragraph**, **email**, **phone**, **date**, **time**, **number**, **single-select**, **multi-select**, **type-ahead**, **yes/no**, **checkbox**, **file upload**, **statement**, **group** (J12). Typeform names its own types, so its area keeps them. | `/\blong[- ]text\b/i`, `/\bdrop-?downs?\b/i` | `typeform-question-import-export` |
| **Picker** | A single-select or a multi-select question (ADR-0128). | — | — |
| **Choice** | One selectable entry a single-select, multi-select, or type-ahead owns, outside its revisions. A picker's entry is a picker choice (J5). A menu's entries are items. | `/\b(?<!sign-in )(?<!Content-Type-)options?\b/i` | — |
| **Type-ahead value** | A choice of a type-ahead question (J6, ADR-0129). | — | — |
| **Reporter-added value** | A type-ahead value a reporter typed at submission, flagged for review. | `reporter-added choice`, `reporter-added choices` | — |
| **Removed** | A choice that an Administrator, or for a type-ahead value a reviewer, removed (ADR-0129): no longer offered, and still named by every answer that names it. A dependent choice's link is removed the same way. | — | — |
| **Replaced** | A picker choice superseded by a new one. | — | — |
| **Merged** | A type-ahead value folded into another. | — | — |
| **Retired** | The state of the old choice after a replace or a merge, and of a question a fork superseded (ADR-0128): kept for the answers given under it, never offered again. A deleted question stays "deleted". | — | — |
| **Live** | A question or choice that is not deleted, removed, or retired. Only for questions and choices; a report is Published, never live. | `/\b(?:go\|goes\|going\|went\|gone) live\b/i` | — |
| **Conditional question** | A question shown only when a yes/no or single-select **parent question** has a given answer. | — | — |
| **Dependent choice** | A choice offered only under one or more **parent choices** of an earlier question (ADR-0151). | — | — |
| **System question** | Publication consent or media consent: required, never deleted, read by name. | — | — |
| **Publication consent** | The reporter's system answer allowing a summary to be published. | — | — |
| **Media consent** | The reporter's system answer allowing their images and videos to be published, and their documents when its wording names them (J10). | — | — |
| **Private question** | A question whose answers, the **private answers**, never become summary facts. | — | — |

## Answers and the summary

| Term | Definition | Banned in scenarios | Exempt areas |
|---|---|---|---|
| **Answer** | One immutable response to one question revision. | — | — |
| **Second language** | The other-language text the Worker writes once for an answer that needs one. | — | — |
| **Report content** | The labelled public answers the model may draw facts from. | — | — |
| **Private context** | The labelled private answers the model may use only to recognise identifying text. | — | — |
| **Marking pass** | The deterministic replacement of a private value with a **marker**, `[PRIVATE:<key>]`, before the model call. | — | — |
| **Summary pair** | A report's English and French summaries, reviewed and approved as one unit. | `bilingual pair`, `generated pair`, `bilingual pairs`, `generated pairs` | — |
| **Summary revision** | One append-only saved state of a summary pair, with its author and how each language was written. | — | — |
| **Approve** | To mark a summary revision approved. Never the same as Publish. | — | — |
| **Publish** | To make a report Published: consent given, not deleted, its current revision approved. **Unpublish** takes it back out. | — | — |
| **Worker** | The background service that summarises, translates, and processes attachments. Always capitalised. | `/\bworkers?\b/` | — |
| **Worker job** | One piece of work queued for the Worker — a summary, a translation, an attachment to process — kept until it succeeds or is poisoned. Stored as an outbox message ([ADR-0002](decisions/ADR-0002-transactional-outbox.md), CON-DP-008). | — | — |
| **Server** | The part of the system that is not the browser: it validates, stores, and authorizes. A step names it only where the split between the browser and the server is the behavior; otherwise a step has no actor ("the submission is stored"). | — | — |
| **The model** | The one summarisation model call. | — | — |
| **Machine translation** | The translation port, never on the submission path; its provider is the **translation provider**. | — | — |

## Attachments

| Term | Definition | Banned in scenarios | Exempt areas |
|---|---|---|---|
| **Upload** | A file in private quarantine, under an opaque upload ID, before a submission claims it. | — | — |
| **Attachment** | An upload a submission has claimed: an image, a video, or a document. "Evidence" means proof, as in "production evidence", never an attachment. | `/(?<!production )\bevidence\b/i` | — |
| **Image** | One kind of attachment. Not a photo. A file name such as `photo.jpg` is data, so the pattern leaves a word followed by an extension alone. | `/\bphotos?\b(?!\.\w)/i` | — |
| **Video** | One kind of attachment. | — | — |
| **Document** | One kind of attachment: PDF, DOC, DOCX, RTF, Markdown, text, or ODT. | — | — |
| **Media** | A report's images and videos, never its documents (J10). | — | — |
| **Derivative** | The re-encoded, metadata-stripped image or remuxed video that may be published. | — | — |
| **Original** | The file as it arrived. It is never published. | — | — |
| **Quarantine** | Private storage for uploads before a submission claims them. | — | — |
| **Hidden** | Taken off the public page by a reviewer. An image, video, or document can be shown again; a comment cannot (ADR-0114). | — | — |
| **Private attachment** | A file a reviewer adds to a report. Never public, and distinct from a reporter's attachment. | — | — |
| **Private note** | A reviewer's note on a report. Never public. | — | — |
| **Comment** | A member's public remark on a published report. | — | — |

## Outcome phrases

What a step says happened, not how a transport reports it. The status codes
themselves are refused by `lint-scenarios`'s `no-http-status` rule, not by
this lint; the step definition still asserts the code.

| Term | Definition | Banned in scenarios | Exempt areas |
|---|---|---|---|
| **is allowed** | A member's role may do what the request asks, and it succeeded (2xx). | — | — |
| **is answered with success** | The request succeeded (200 or 204). "Answered with <something>" names what came back, such as a translation. | — | — |
| **is accepted** | The request was taken for later work (202). | — | — |
| **is created** | The request created what it names (201). | — | — |
| **is refused as unauthenticated** | No valid token (401). | — | — |
| **is refused as forbidden** | A valid token whose role may not do it (403). | — | — |
| **is not found** | The thing does not exist for this member, which is also how a hidden one reads (404). | — | — |
| **is refused as invalid** | The request is malformed or breaks a rule (400). | — | — |
| **is refused as out of date** | The request was made against a version someone has since changed (409). | — | — |
| **is refused as too frequent** | A rate limit refused it (429). | — | — |
| **refused** | The one verb for any refusal. Only the Worker *rejects*, and only a model response (J8). | `/\breject(?:s\|ed\|ing\|ion\|ions)?\b/i` | `ai-anonymization` |
| **sign in** / **sign out** | What a member does to start and end a session. | `/\blog(?:-\| )?ins?\b/i`, `/\blogs? in\b/i`, `/\blogged in\b/i`, `/\blogging in\b/i`, `/\blog(?:-\| )?outs?\b/i` | — |

## Languages

Steps say **English** and **French**; the locale codes `en-CA` and `fr-CA`
appear only in Examples cells (J16). `lint-scenarios`'s `no-locale-codes`
rule refuses a code in a step or a title.
