---
title: A group that stopped being one hid its questions
description: Deleting a group, or retyping it to another type, left each child naming a heading that no longer existed, and the public form dropped those questions. No scenario said what happens to a child when its group stops being one.
type: lesson
date: 2026-09-30
issue: 720
status: accepted
kind: product
---

# Lesson 0040 — A group that stopped being one hid its questions

## Symptom

After an administrator deleted a group or retyped it, its children no longer
appeared on the reporter's form. They were still live in the question bank.

## Root cause

- The public form lists top-level questions and the children of live groups
  only, so a child of a deleted or retyped group was neither.
- REQ-QB-046 to REQ-QB-050 said when grouping is allowed, never what becomes of
  a child when its group stops being one, so deleting or retyping a group
  checked nothing about its children.

## Spec delta

- REQ-QB-052: a grouped question is ungrouped in the same save, an answered one
  by a fork and an unanswered one by a revision, taking the group's slot in
  their existing order with later questions shifting down only as far as they must.
- ADR-0076 is amended and the question-bank README states the rule.

## Scenario

REQ-QB-052.

## Skill

None; the claim is the remedy.
