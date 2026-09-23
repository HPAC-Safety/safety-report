---
title: A question key shown to the person who cannot choose it
description: The question editor rendered the stable question key as a field — required when authoring, read-only but indistinguishable when editing — though an administrator has no basis to choose one.
type: lesson
date: 2026-09-22
issue: 342
status: accepted
---

# Lesson 0007 — A question key shown to the person who cannot choose it

## Symptom

On `/admin/questions`, the question editor showed a **Key** field above the
wording. Editing a question, the field was marked read-only but styled exactly
like every editable field beside it, so an administrator read it as something
they could change. Authoring a new question, it was required, so they had to
invent one.

## Root cause

A question key is the system's stable handle for a question: answers, exports,
forks (ADR-0071), and `consent_publish` refer to it. The API took it as a
required field on `SaveQuestionRequest`, and the editor rendered that field as
written. `REQ-QB-087` said the key "cannot be changed" while editing, which
proved the field was read-only and never asked whether it should be there at
all. No scenario said who chooses a key, so the storage shape became the
authoring shape by default — the same gap lesson 0006 closed for option codes,
left open one field over.

The duplicate-key check also looked only at live questions. A key typed, or
imported from Typeform, that matched a retired question was accepted, and the
new question joined that question's history. An explicit key is now refused
when any question holds it, retired ones included.

## Spec delta

- `REQ-QB-096`: a new question's key is derived from its English wording, is
  suffixed when another question already holds it, and never takes a retired
  question's key.
- `REQ-QB-087`: the editor shows no question key, rather than a read-only one.
- `REQ-QB-088`: a reviewed Typeform draft prefills its type and wording; its
  key travels with it unseen.
- `REQ-QB-086`: a refused save is shown with its reason, now provoked by two
  choices that read alike, since an administrator can no longer type a key.
- `features/question-bank-and-form/README.md` out of scope: an administrator
  authoring, seeing, or changing a question key.

## Scenario

`REQ-QB-086`, `REQ-QB-087`, `REQ-QB-088`, `REQ-QB-096`.

## Skill

None — the claim is the remedy.
